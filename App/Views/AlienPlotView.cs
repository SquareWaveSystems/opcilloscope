using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace Opcilloscope.App.Views;

/// <summary>
/// Sci-fi styled plot view with isometric perspective, braille rendering, and sweep animation.
/// Used as an alternative "Aliens Mode" visualization in the scope dialog.
/// </summary>
/// <remarks>
/// Unlike the standard GraphView which displays multiple series with distinct colors,
/// this view combines all data points from all series into a single unified plot.
/// This is intentional for the sci-fi aesthetic effect.
/// </remarks>
public class AlienPlotView : View
{
    // === Rendering Constants ===
    private const char BrailleBase = '\u2800';
    private const int DefaultViewportWidth = 80;

    // Braille dot pattern mapping: each braille character is a 2x4 dot matrix.
    // Unicode Braille patterns use the following dot numbering scheme:
    //   Left column:  dots 1,2,3,7 (top to bottom)
    //   Right column: dots 4,5,6,8 (top to bottom)
    // BrailleDots array maps these to Unicode bit positions (0-7).
    private static readonly int[] BrailleDots = { 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80 };

    // Bounds padding percentage (5% on each side)
    private const float BoundsPadding = 0.05f;

    // Frame tick mark intervals
    private const int HorizontalTickInterval = 10;
    private const int VerticalTickInterval = 5;

    // Isometric grid depth thresholds
    private const float DepthThresholdSolid = 0.3f;
    private const float DepthThresholdDashed = 0.6f;
    private const float MinRangeEpsilon = 1e-6f;

    // === Thread-Safe Data ===
    private readonly object _dataLock = new();
    private List<PointF> _dataPoints = new();
    private float _minX = 0, _maxX = 100, _minY = 0, _maxY = 100;
    private int _sweepPosition = 0;

    // Reusable braille buffer to reduce allocations (lazily resized)
    private int[,]? _brailleBuffer;
    private int _bufferWidth;
    private int _bufferHeight;

    public bool ShowIsometricPerspective { get; set; } = true;
    public bool UseBrailleRendering { get; set; } = true;
    public int GridSpacing { get; set; } = 8;
    public float IsometricSkew { get; set; } = 0.3f;
    public Terminal.Gui.Color PrimaryColor { get; set; } = Terminal.Gui.Color.BrightGreen;
    public Terminal.Gui.Color SecondaryColor { get; set; } = Terminal.Gui.Color.Green;
    public Terminal.Gui.Color DimColor { get; set; } = Terminal.Gui.Color.DarkGray;
    public Terminal.Gui.Color BackgroundColor { get; set; } = Terminal.Gui.Color.Black;

    /// <summary>
    /// Sets the data points to display in the plot.
    /// </summary>
    /// <param name="points">The collection of points with X and Y coordinates.</param>
    public void SetData(IEnumerable<PointF> points)
    {
        lock (_dataLock)
        {
            _dataPoints = new List<PointF>(points);
            RecalculateBoundsLocked();
        }
        SetNeedsLayout();
    }

    /// <summary>
    /// Sets the data points using Y values only, with X auto-generated as sequential indices.
    /// </summary>
    /// <param name="yValues">The Y values to plot.</param>
    public void SetData(IEnumerable<float> yValues)
    {
        lock (_dataLock)
        {
            _dataPoints.Clear();
            int x = 0;
            foreach (var y in yValues)
            {
                _dataPoints.Add(new PointF(x++, y));
            }
            RecalculateBoundsLocked();
        }
        SetNeedsLayout();
    }

    /// <summary>
    /// Advances the sweep line animation by one tick.
    /// Call this periodically (e.g., every 100ms) for the animated effect.
    /// </summary>
    public void AnimateTick()
    {
        int width = Viewport.Width;
        _sweepPosition = (_sweepPosition + 1) % (width > 0 ? width : DefaultViewportWidth);
        SetNeedsLayout();
    }

    /// <summary>
    /// Recalculates data bounds. Must be called within _dataLock.
    /// </summary>
    private void RecalculateBoundsLocked()
    {
        if (_dataPoints.Count == 0) return;

        _minX = _maxX = _dataPoints[0].X;
        _minY = _maxY = _dataPoints[0].Y;

        foreach (var p in _dataPoints)
        {
            if (p.X < _minX) _minX = p.X;
            if (p.X > _maxX) _maxX = p.X;
            if (p.Y < _minY) _minY = p.Y;
            if (p.Y > _maxY) _maxY = p.Y;
        }

        float rangeX = _maxX - _minX;
        float rangeY = _maxY - _minY;

        if (Math.Abs(rangeX) < MinRangeEpsilon) rangeX = 1f;
        if (Math.Abs(rangeY) < MinRangeEpsilon) rangeY = 1f;

        // Add padding around data bounds
        _minX -= rangeX * BoundsPadding;
        _maxX += rangeX * BoundsPadding;
        _minY -= rangeY * BoundsPadding;
        _maxY += rangeY * BoundsPadding;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        base.OnDrawingContent(context);

        var driver = Application.Driver;
        if (driver == null) return true;

        int width = Viewport.Width;
        int height = Viewport.Height;
        if (width <= 0 || height <= 0) return true;

        // Take a snapshot of data under lock to avoid race conditions
        List<PointF> dataSnapshot;
        float minX, maxX, minY, maxY;
        lock (_dataLock)
        {
            dataSnapshot = _dataPoints.ToList();
            minX = _minX;
            maxX = _maxX;
            minY = _minY;
            maxY = _maxY;
        }

        driver.SetAttribute(new Attribute(PrimaryColor, BackgroundColor));

        if (ShowIsometricPerspective)
        {
            DrawIsometricGrid(driver, width, height);
        }

        if (UseBrailleRendering)
        {
            DrawBraillePlot(driver, width, height, dataSnapshot, minX, maxX, minY, maxY);
        }
        else
        {
            DrawBlockPlot(driver, width, height, dataSnapshot, minX, maxX, minY, maxY);
        }

        DrawFrame(driver, width, height, dataSnapshot.Count, minY, maxY);
        DrawSweepLine(driver, width, height);

        return true;
    }

    private void DrawIsometricGrid(IConsoleDriver driver, int width, int height)
    {
        driver.SetAttribute(new Attribute(DimColor, BackgroundColor));

        // Draw horizontal grid lines with depth-based style
        for (int y = 0; y < height; y += GridSpacing)
        {
            float depth = (float)y / height;
            char lineChar = depth < DepthThresholdSolid ? '─' : depth < DepthThresholdDashed ? '╌' : '┄';
            int indent = (int)(y * IsometricSkew);

            for (int x = indent; x < width - indent; x++)
            {
                AddRune(x, y, (Rune)lineChar);
            }
        }

        // Draw vertical grid lines converging toward center
        int centerX = width / 2;
        for (int i = -5; i <= 5; i++)
        {
            for (int y = 0; y < height; y++)
            {
                float depth = (float)y / height;
                int offset = (int)((float)i * GridSpacing * (1.0f - depth * IsometricSkew));
                int x = centerX + offset;

                if (x >= 0 && x < width)
                {
                    char lineChar = Math.Abs(i) == 0 ? '│' : (y % 2 == 0 ? '┊' : ' ');
                    AddRune(x, y, (Rune)lineChar);
                }
            }
        }
    }

    private void DrawBraillePlot(IConsoleDriver driver, int width, int height,
        List<PointF> dataPoints, float minX, float maxX, float minY, float maxY)
    {
        if (dataPoints.Count == 0) return;

        // Braille characters are 2 dots wide x 4 dots tall per character cell
        int subWidth = width * 2;
        int subHeight = height * 4;

        // Reuse buffer if size matches, otherwise allocate new one
        if (_brailleBuffer == null || _bufferWidth != width || _bufferHeight != height)
        {
            _brailleBuffer = new int[width, height];
            _bufferWidth = width;
            _bufferHeight = height;
        }
        else
        {
            // Clear the existing buffer
            Array.Clear(_brailleBuffer, 0, _brailleBuffer.Length);
        }

        driver.SetAttribute(new Attribute(PrimaryColor, BackgroundColor));

        float rangeX = maxX - minX;
        float rangeY = maxY - minY;

        foreach (var point in dataPoints)
        {
            float normX = rangeX > 0 ? (point.X - minX) / rangeX : 0;
            float normY = rangeY > 0 ? (point.Y - minY) / rangeY : 0;

            int subX = (int)(normX * (subWidth - 1));
            int subY = (int)((1 - normY) * (subHeight - 1));

            int cellX = subX / 2;
            int cellY = subY / 4;
            int dotX = subX % 2;
            int dotY = subY % 4;

            if (cellX >= 0 && cellX < width && cellY >= 0 && cellY < height)
            {
                // Map (dotX, dotY) in our 2x4 sub-cell grid to a Braille dot index (0-7),
                // then into the BrailleDots array, which holds the Unicode bit masks.
                //
                // Unicode Braille patterns use the following dot numbering in a single cell:
                //
                //   column 0   column 1
                //   --------   --------
                //      1          4
                //      2          5
                //      3          6
                //      7          8
                //
                // Our normalized plot coordinates give us (dotX, dotY) in a simple grid:
                //   dotX: 0 = left column, 1 = right column
                //   dotY: 0..3 = from top to bottom
                //
                // The BrailleDots array is ordered to match the Unicode bit layout for dots 1..8.
                int dotIndex;
                if (dotX == 0)
                {
                    // Left column
                    dotIndex = dotY switch
                    {
                        0 => 0, // dot 1
                        1 => 1, // dot 2
                        2 => 2, // dot 3
                        3 => 6, // dot 7
                        _ => 0
                    };
                }
                else
                {
                    // Right column
                    dotIndex = dotY switch
                    {
                        0 => 3, // dot 4
                        1 => 4, // dot 5
                        2 => 5, // dot 6
                        3 => 7, // dot 8
                        _ => 3
                    };
                }

                _brailleBuffer[cellX, cellY] |= BrailleDots[dotIndex];
            }
        }

        for (int cy = 0; cy < height; cy++)
        {
            for (int cx = 0; cx < width; cx++)
            {
                if (_brailleBuffer[cx, cy] != 0)
                {
                    AddRune(cx, cy, (Rune)(char)(BrailleBase + _brailleBuffer[cx, cy]));
                }
            }
        }

        DrawConnectingLines(driver, width, height, dataPoints, minX, maxX, minY, maxY);
    }

    private void DrawConnectingLines(IConsoleDriver driver, int width, int height,
        List<PointF> dataPoints, float minX, float maxX, float minY, float maxY)
    {
        if (dataPoints.Count < 2) return;

        driver.SetAttribute(new Attribute(PrimaryColor, BackgroundColor));

        float rangeX = maxX - minX;
        float rangeY = maxY - minY;

        for (int i = 0; i < dataPoints.Count - 1; i++)
        {
            var p1 = dataPoints[i];
            var p2 = dataPoints[i + 1];

            float x1 = (rangeX > 0 ? (p1.X - minX) / rangeX : 0) * (width - 1);
            float y1 = (1 - (rangeY > 0 ? (p1.Y - minY) / rangeY : 0)) * (height - 1);
            float x2 = (rangeX > 0 ? (p2.X - minX) / rangeX : 0) * (width - 1);
            float y2 = (1 - (rangeY > 0 ? (p2.Y - minY) / rangeY : 0)) * (height - 1);

            DrawLine((int)x1, (int)y1, (int)x2, (int)y2, width, height);
        }
    }

    private void DrawLine(int x0, int y0, int x1, int y1, int width, int height)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        char lineChar = dx == 0 ? '│' : dy == 0 ? '─' : (x1 - x0) * (y1 - y0) > 0 ? '╲' : '╱';

        while (true)
        {
            if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height)
            {
                AddRune(x0, y0, (Rune)lineChar);
            }

            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private void DrawBlockPlot(IConsoleDriver driver, int width, int height,
        List<PointF> dataPoints, float minX, float maxX, float minY, float maxY)
    {
        if (dataPoints.Count == 0) return;

        driver.SetAttribute(new Attribute(PrimaryColor, BackgroundColor));

        float rangeX = maxX - minX;
        float rangeY = maxY - minY;

        foreach (var point in dataPoints)
        {
            float normX = rangeX > 0 ? (point.X - minX) / rangeX : 0;
            float normY = rangeY > 0 ? (point.Y - minY) / rangeY : 0;

            int x = (int)(normX * (width - 1));
            int y = (int)((1 - normY) * (height - 1));

            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                AddRune(x, y, (Rune)'█');
            }
        }
    }

    private void DrawFrame(IConsoleDriver driver, int width, int height, int pointCount, float minY, float maxY)
    {
        driver.SetAttribute(new Attribute(SecondaryColor, BackgroundColor));

        // Draw corners
        AddRune(0, 0, (Rune)'╔');
        AddRune(width - 1, 0, (Rune)'╗');
        AddRune(0, height - 1, (Rune)'╚');
        AddRune(width - 1, height - 1, (Rune)'╝');

        // Draw horizontal edges with tick marks
        for (int x = 1; x < width - 1; x++)
        {
            AddRune(x, 0, (Rune)(x % HorizontalTickInterval == 0 ? '╦' : '═'));
            AddRune(x, height - 1, (Rune)(x % HorizontalTickInterval == 0 ? '╩' : '═'));
        }

        // Draw vertical edges with tick marks
        for (int y = 1; y < height - 1; y++)
        {
            AddRune(0, y, (Rune)(y % VerticalTickInterval == 0 ? '╠' : '║'));
            AddRune(width - 1, y, (Rune)(y % VerticalTickInterval == 0 ? '╣' : '║'));
        }

        // Draw info labels if there's enough space
        if (width > 10 && height > 4)
        {
            driver.SetAttribute(new Attribute(PrimaryColor, BackgroundColor));

            string label = "[SCOPE]";
            for (int i = 0; i < label.Length; i++)
            {
                AddRune(2 + i, 1, (Rune)label[i]);
            }

            string status = $"RNG:{minY:F0}-{maxY:F0} PTS:{pointCount}";
            for (int i = 0; i < status.Length && i < width - 4; i++)
            {
                AddRune(2 + i, height - 2, (Rune)status[i]);
            }
        }
    }

    private void DrawSweepLine(IConsoleDriver driver, int width, int height)
    {
        if (_sweepPosition >= 0 && _sweepPosition < width)
        {
            for (int y = 1; y < height - 1; y++)
            {
                driver.SetAttribute(new Attribute(PrimaryColor, BackgroundColor));
                AddRune(_sweepPosition, y, (Rune)'┃');

                if (_sweepPosition > 1)
                {
                    driver.SetAttribute(new Attribute(SecondaryColor, BackgroundColor));
                    AddRune(_sweepPosition - 1, y, (Rune)'┆');
                }

                if (_sweepPosition > 2)
                {
                    driver.SetAttribute(new Attribute(DimColor, BackgroundColor));
                    AddRune(_sweepPosition - 2, y, (Rune)'┊');
                }
            }
        }
    }
}
