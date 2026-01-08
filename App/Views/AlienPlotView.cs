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
public class AlienPlotView : View
{
    private const char BrailleBase = '\u2800';
    private static readonly int[] BrailleDots = { 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80 };

    private List<PointF> _dataPoints = new();
    private float _minX = 0, _maxX = 100, _minY = 0, _maxY = 100;
    private int _sweepPosition = 0;

    public bool ShowIsometricPerspective { get; set; } = true;
    public bool UseBrailleRendering { get; set; } = true;
    public int GridSpacing { get; set; } = 8;
    public float IsometricSkew { get; set; } = 0.3f;
    public Terminal.Gui.Color PrimaryColor { get; set; } = Terminal.Gui.Color.BrightGreen;
    public Terminal.Gui.Color SecondaryColor { get; set; } = Terminal.Gui.Color.Green;
    public Terminal.Gui.Color DimColor { get; set; } = Terminal.Gui.Color.DarkGray;
    public Terminal.Gui.Color BackgroundColor { get; set; } = Terminal.Gui.Color.Black;

    public void SetData(IEnumerable<PointF> points)
    {
        _dataPoints = new List<PointF>(points);
        RecalculateBounds();
        SetNeedsLayout();
    }

    public void SetData(IEnumerable<float> yValues)
    {
        _dataPoints.Clear();
        int x = 0;
        foreach (var y in yValues)
        {
            _dataPoints.Add(new PointF(x++, y));
        }
        RecalculateBounds();
        SetNeedsLayout();
    }

    public void AnimateTick()
    {
        _sweepPosition = (_sweepPosition + 1) % (Viewport.Width > 0 ? Viewport.Width : 80);
        SetNeedsLayout();
    }

    private void RecalculateBounds()
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

        if (rangeX == 0) rangeX = 1;
        if (rangeY == 0) rangeY = 1;

        _minX -= rangeX * 0.05f;
        _maxX += rangeX * 0.05f;
        _minY -= rangeY * 0.05f;
        _maxY += rangeY * 0.05f;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        base.OnDrawingContent(context);

        var driver = Application.Driver;
        if (driver == null) return true;

        int width = Viewport.Width;
        int height = Viewport.Height;
        if (width <= 0 || height <= 0) return true;

        driver.SetAttribute(new Attribute(PrimaryColor, BackgroundColor));

        if (ShowIsometricPerspective)
        {
            DrawIsometricGrid(driver, width, height);
        }

        if (UseBrailleRendering)
        {
            DrawBraillePlot(driver, width, height);
        }
        else
        {
            DrawBlockPlot(driver, width, height);
        }

        DrawFrame(driver, width, height);
        DrawSweepLine(driver, width, height);

        return true;
    }

    private void DrawIsometricGrid(IConsoleDriver driver, int width, int height)
    {
        driver.SetAttribute(new Attribute(DimColor, BackgroundColor));

        for (int y = 0; y < height; y += GridSpacing)
        {
            float depth = (float)y / height;
            char lineChar = depth < 0.3f ? '─' : depth < 0.6f ? '╌' : '┄';
            int indent = (int)(y * IsometricSkew);

            for (int x = indent; x < width - indent; x++)
            {
                AddRune(x, y, (Rune)lineChar);
            }
        }

        int centerX = width / 2;
        for (int i = -5; i <= 5; i++)
        {
            for (int y = 0; y < height; y++)
            {
                float depth = (float)y / height;
                int offset = (int)(i * GridSpacing * (1.0f - depth * IsometricSkew));
                int x = centerX + offset;

                if (x >= 0 && x < width)
                {
                    char lineChar = Math.Abs(i) == 0 ? '│' : (y % 2 == 0 ? '┊' : ' ');
                    AddRune(x, y, (Rune)lineChar);
                }
            }
        }
    }

    private void DrawBraillePlot(IConsoleDriver driver, int width, int height)
    {
        if (_dataPoints.Count == 0) return;

        int subWidth = width * 2;
        int subHeight = height * 4;
        var brailleBuffer = new int[width, height];

        driver.SetAttribute(new Attribute(PrimaryColor, BackgroundColor));

        foreach (var point in _dataPoints)
        {
            float normX = (_maxX - _minX) > 0 ? (point.X - _minX) / (_maxX - _minX) : 0;
            float normY = (_maxY - _minY) > 0 ? (point.Y - _minY) / (_maxY - _minY) : 0;

            int subX = (int)(normX * (subWidth - 1));
            int subY = (int)((1 - normY) * (subHeight - 1));

            int cellX = subX / 2;
            int cellY = subY / 4;
            int dotX = subX % 2;
            int dotY = subY % 4;

            if (cellX >= 0 && cellX < width && cellY >= 0 && cellY < height)
            {
                int dotIndex = dotY + (dotX * 4);
                if (dotIndex >= 4 && dotIndex < 6) dotIndex = dotY + 3;
                brailleBuffer[cellX, cellY] |= BrailleDots[Math.Min(dotIndex, 7)];
            }
        }

        for (int cy = 0; cy < height; cy++)
        {
            for (int cx = 0; cx < width; cx++)
            {
                if (brailleBuffer[cx, cy] != 0)
                {
                    AddRune(cx, cy, (Rune)(char)(BrailleBase + brailleBuffer[cx, cy]));
                }
            }
        }

        DrawConnectingLines(driver, width, height);
    }

    private void DrawConnectingLines(IConsoleDriver driver, int width, int height)
    {
        if (_dataPoints.Count < 2) return;

        driver.SetAttribute(new Attribute(PrimaryColor, BackgroundColor));

        for (int i = 0; i < _dataPoints.Count - 1; i++)
        {
            var p1 = _dataPoints[i];
            var p2 = _dataPoints[i + 1];

            float x1 = ((_maxX - _minX) > 0 ? (p1.X - _minX) / (_maxX - _minX) : 0) * (width - 1);
            float y1 = (1 - ((_maxY - _minY) > 0 ? (p1.Y - _minY) / (_maxY - _minY) : 0)) * (height - 1);
            float x2 = ((_maxX - _minX) > 0 ? (p2.X - _minX) / (_maxX - _minX) : 0) * (width - 1);
            float y2 = (1 - ((_maxY - _minY) > 0 ? (p2.Y - _minY) / (_maxY - _minY) : 0)) * (height - 1);

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

    private void DrawBlockPlot(IConsoleDriver driver, int width, int height)
    {
        if (_dataPoints.Count == 0) return;

        driver.SetAttribute(new Attribute(PrimaryColor, BackgroundColor));

        foreach (var point in _dataPoints)
        {
            float normX = (_maxX - _minX) > 0 ? (point.X - _minX) / (_maxX - _minX) : 0;
            float normY = (_maxY - _minY) > 0 ? (point.Y - _minY) / (_maxY - _minY) : 0;

            int x = (int)(normX * (width - 1));
            int y = (int)((1 - normY) * (height - 1));

            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                AddRune(x, y, (Rune)'█');
            }
        }
    }

    private void DrawFrame(IConsoleDriver driver, int width, int height)
    {
        driver.SetAttribute(new Attribute(SecondaryColor, BackgroundColor));

        AddRune(0, 0, (Rune)'╔');
        AddRune(width - 1, 0, (Rune)'╗');
        AddRune(0, height - 1, (Rune)'╚');
        AddRune(width - 1, height - 1, (Rune)'╝');

        for (int x = 1; x < width - 1; x++)
        {
            AddRune(x, 0, (Rune)(x % 10 == 0 ? '╦' : '═'));
            AddRune(x, height - 1, (Rune)(x % 10 == 0 ? '╩' : '═'));
        }

        for (int y = 1; y < height - 1; y++)
        {
            AddRune(0, y, (Rune)(y % 5 == 0 ? '╠' : '║'));
            AddRune(width - 1, y, (Rune)(y % 5 == 0 ? '╣' : '║'));
        }

        if (width > 10 && height > 4)
        {
            driver.SetAttribute(new Attribute(PrimaryColor, BackgroundColor));

            string label = "[SCOPE]";
            for (int i = 0; i < label.Length; i++)
            {
                AddRune(2 + i, 1, (Rune)label[i]);
            }

            string status = $"RNG:{_minY:F0}-{_maxY:F0} PTS:{_dataPoints.Count}";
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
