namespace Opcilloscope.App.Views;

/// <summary>
/// Braille-character rendering engine for high-resolution terminal graphics.
/// Each terminal cell maps to a 2x4 braille dot matrix (Unicode U+2800-U+28FF),
/// giving 8x the resolution of character-cell rendering.
/// </summary>
public class BrailleCanvas
{
    // Braille dot positions within a 2x4 cell:
    //   (0,0) (1,0)     bit 0  bit 3
    //   (0,1) (1,1)     bit 1  bit 4
    //   (0,2) (1,2)     bit 2  bit 5
    //   (0,3) (1,3)     bit 6  bit 7
    private static readonly int[,] BrailleBitMap =
    {
        { 0, 1, 2, 6 }, // column 0: rows 0-3
        { 3, 4, 5, 7 }  // column 1: rows 0-3
    };

    private readonly bool[,] _pixels;
    private readonly int[,] _layers;
    private readonly int _pixelWidth;
    private readonly int _pixelHeight;

    /// <summary>Width in braille pixels (2x cell columns).</summary>
    public int PixelWidth => _pixelWidth;

    /// <summary>Height in braille pixels (4x cell rows).</summary>
    public int PixelHeight => _pixelHeight;

    /// <summary>Width in terminal cell columns.</summary>
    public int CellWidth { get; }

    /// <summary>Height in terminal cell rows.</summary>
    public int CellHeight { get; }

    /// <summary>
    /// Creates a new BrailleCanvas with the given terminal cell dimensions.
    /// </summary>
    public BrailleCanvas(int cellWidth, int cellHeight)
    {
        CellWidth = Math.Max(1, cellWidth);
        CellHeight = Math.Max(1, cellHeight);
        _pixelWidth = CellWidth * 2;
        _pixelHeight = CellHeight * 4;
        _pixels = new bool[_pixelWidth, _pixelHeight];
        _layers = new int[_pixelWidth, _pixelHeight];
    }

    /// <summary>
    /// Clears all pixels and layer data.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_pixels);
        Array.Clear(_layers);
    }

    /// <summary>
    /// Sets a pixel at the given braille coordinates, tagged with a layer index.
    /// Out-of-bounds coordinates are silently ignored.
    /// </summary>
    public void SetPixel(int x, int y, int layer = 0)
    {
        if (x < 0 || x >= _pixelWidth || y < 0 || y >= _pixelHeight)
            return;

        _pixels[x, y] = true;
        _layers[x, y] = layer;
    }

    /// <summary>
    /// Returns whether a pixel is set at the given coordinates.
    /// Returns false for out-of-bounds coordinates.
    /// </summary>
    public bool IsPixelSet(int x, int y)
    {
        if (x < 0 || x >= _pixelWidth || y < 0 || y >= _pixelHeight)
            return false;

        return _pixels[x, y];
    }

    /// <summary>
    /// Draws a line between two points using Bresenham's algorithm.
    /// </summary>
    public void DrawLine(int x1, int y1, int x2, int y2, int layer = 0)
    {
        int dx = Math.Abs(x2 - x1);
        int dy = Math.Abs(y2 - y1);
        int sx = x1 < x2 ? 1 : -1;
        int sy = y1 < y2 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            SetPixel(x1, y1, layer);

            if (x1 == x2 && y1 == y2) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x1 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y1 += sy;
            }
        }
    }

    /// <summary>
    /// Draws a dotted horizontal line at the given Y pixel coordinate.
    /// </summary>
    public void DrawDottedHorizontalLine(int y, int spacing, int layer = -1)
    {
        if (y < 0 || y >= _pixelHeight) return;
        for (int x = 0; x < _pixelWidth; x += spacing)
        {
            SetPixel(x, y, layer);
        }
    }

    /// <summary>
    /// Draws a dotted vertical line at the given X pixel coordinate.
    /// </summary>
    public void DrawDottedVerticalLine(int x, int spacing, int layer = -1)
    {
        if (x < 0 || x >= _pixelWidth) return;
        for (int y = 0; y < _pixelHeight; y += spacing)
        {
            SetPixel(x, y, layer);
        }
    }

    /// <summary>
    /// Gets the braille Unicode character for the given cell position.
    /// Returns the blank braille character (U+2800) if no dots are set.
    /// </summary>
    public char GetCell(int cellX, int cellY)
    {
        if (cellX < 0 || cellX >= CellWidth || cellY < 0 || cellY >= CellHeight)
            return '\u2800';

        int baseX = cellX * 2;
        int baseY = cellY * 4;
        int codePoint = 0x2800;

        for (int dx = 0; dx < 2; dx++)
        {
            for (int dy = 0; dy < 4; dy++)
            {
                int px = baseX + dx;
                int py = baseY + dy;
                if (px < _pixelWidth && py < _pixelHeight && _pixels[px, py])
                {
                    codePoint |= 1 << BrailleBitMap[dx, dy];
                }
            }
        }

        return (char)codePoint;
    }

    /// <summary>
    /// Gets the braille character for a cell, suppressing grid dots (layer -1)
    /// when the cell also contains signal dots. This prevents grid dots from
    /// rendering in the signal's color.
    /// </summary>
    public char GetCellFiltered(int cellX, int cellY)
    {
        if (cellX < 0 || cellX >= CellWidth || cellY < 0 || cellY >= CellHeight)
            return '\u2800';

        int baseX = cellX * 2;
        int baseY = cellY * 4;

        // First pass: check if any signal (non-grid) dot exists in the cell
        bool hasSignal = false;
        for (int dx = 0; dx < 2 && !hasSignal; dx++)
        {
            for (int dy = 0; dy < 4 && !hasSignal; dy++)
            {
                int px = baseX + dx;
                int py = baseY + dy;
                if (px < _pixelWidth && py < _pixelHeight && _pixels[px, py] && _layers[px, py] >= 0)
                {
                    hasSignal = true;
                }
            }
        }

        // Second pass: build braille char, excluding grid dots if signals are present
        int codePoint = 0x2800;
        for (int dx = 0; dx < 2; dx++)
        {
            for (int dy = 0; dy < 4; dy++)
            {
                int px = baseX + dx;
                int py = baseY + dy;
                if (px < _pixelWidth && py < _pixelHeight && _pixels[px, py])
                {
                    // Skip grid dots when the cell also has signal dots
                    if (hasSignal && _layers[px, py] < 0)
                        continue;

                    codePoint |= 1 << BrailleBitMap[dx, dy];
                }
            }
        }

        return (char)codePoint;
    }

    /// <summary>
    /// Gets the dominant layer for a cell, determined by which layer has the most
    /// set pixels in the 2x4 block. Grid layer (-1) loses to any signal layer.
    /// Returns -1 if the cell is empty or only contains grid dots.
    /// </summary>
    public int GetCellDominantLayer(int cellX, int cellY)
    {
        if (cellX < 0 || cellX >= CellWidth || cellY < 0 || cellY >= CellHeight)
            return -1;

        int baseX = cellX * 2;
        int baseY = cellY * 4;

        // Count dots per layer
        Dictionary<int, int>? counts = null;

        for (int dx = 0; dx < 2; dx++)
        {
            for (int dy = 0; dy < 4; dy++)
            {
                int px = baseX + dx;
                int py = baseY + dy;
                if (px < _pixelWidth && py < _pixelHeight && _pixels[px, py])
                {
                    counts ??= new Dictionary<int, int>();
                    int layer = _layers[px, py];
                    counts.TryGetValue(layer, out int count);
                    counts[layer] = count + 1;
                }
            }
        }

        if (counts == null) return -1;

        // Find the dominant non-grid layer (grid = -1 loses to any signal)
        int bestLayer = -1;
        int bestCount = 0;

        foreach (var (layer, count) in counts)
        {
            if (layer >= 0 && count > bestCount)
            {
                bestLayer = layer;
                bestCount = count;
            }
        }

        // If only grid dots exist, return -1
        if (bestLayer == -1 && counts.ContainsKey(-1))
            return -1;

        return bestLayer;
    }
}
