using Opcilloscope.App.Views;

namespace Opcilloscope.Tests.App.Views;

public class BrailleCanvasTests
{
    [Fact]
    public void Constructor_SetsCorrectDimensions()
    {
        var canvas = new BrailleCanvas(10, 5);

        Assert.Equal(10, canvas.CellWidth);
        Assert.Equal(5, canvas.CellHeight);
        Assert.Equal(20, canvas.PixelWidth);
        Assert.Equal(20, canvas.PixelHeight);
    }

    [Fact]
    public void Constructor_ClampsMinimumToOne()
    {
        var canvas = new BrailleCanvas(0, -1);

        Assert.Equal(1, canvas.CellWidth);
        Assert.Equal(1, canvas.CellHeight);
        Assert.Equal(2, canvas.PixelWidth);
        Assert.Equal(4, canvas.PixelHeight);
    }

    [Fact]
    public void EmptyCanvas_ReturnsBlankBrailleChars()
    {
        var canvas = new BrailleCanvas(3, 3);

        for (int cx = 0; cx < 3; cx++)
        for (int cy = 0; cy < 3; cy++)
            Assert.Equal('\u2800', canvas.GetCell(cx, cy));
    }

    [Fact]
    public void SetPixel_TopLeftDot_SetsCorrectBrailleBit()
    {
        // Pixel (0,0) in cell (0,0) => bit 0 => U+2801
        var canvas = new BrailleCanvas(2, 2);
        canvas.SetPixel(0, 0);

        Assert.Equal('\u2801', canvas.GetCell(0, 0));
    }

    [Fact]
    public void SetPixel_TopRightDot_SetsCorrectBrailleBit()
    {
        // Pixel (1,0) in cell (0,0) => bit 3 => U+2808
        var canvas = new BrailleCanvas(2, 2);
        canvas.SetPixel(1, 0);

        Assert.Equal('\u2808', canvas.GetCell(0, 0));
    }

    [Fact]
    public void SetPixel_BottomLeftDot_SetsCorrectBrailleBit()
    {
        // Pixel (0,3) in cell (0,0) => bit 6 => U+2840
        var canvas = new BrailleCanvas(2, 2);
        canvas.SetPixel(0, 3);

        Assert.Equal('\u2840', canvas.GetCell(0, 0));
    }

    [Fact]
    public void SetPixel_BottomRightDot_SetsCorrectBrailleBit()
    {
        // Pixel (1,3) in cell (0,0) => bit 7 => U+2880
        var canvas = new BrailleCanvas(2, 2);
        canvas.SetPixel(1, 3);

        Assert.Equal('\u2880', canvas.GetCell(0, 0));
    }

    [Fact]
    public void SetPixel_AllDotsInCell_ReturnsFullBlock()
    {
        // All 8 bits set => U+28FF
        var canvas = new BrailleCanvas(2, 2);
        for (int dx = 0; dx < 2; dx++)
        for (int dy = 0; dy < 4; dy++)
            canvas.SetPixel(dx, dy);

        Assert.Equal('\u28FF', canvas.GetCell(0, 0));
    }

    [Fact]
    public void SetPixel_OutOfBounds_SilentlyIgnored()
    {
        var canvas = new BrailleCanvas(2, 2);

        // Should not throw
        canvas.SetPixel(-1, 0);
        canvas.SetPixel(0, -1);
        canvas.SetPixel(100, 0);
        canvas.SetPixel(0, 100);

        // Canvas should remain empty
        Assert.Equal('\u2800', canvas.GetCell(0, 0));
    }

    [Fact]
    public void IsPixelSet_ReturnsCorrectState()
    {
        var canvas = new BrailleCanvas(2, 2);
        canvas.SetPixel(1, 2);

        Assert.True(canvas.IsPixelSet(1, 2));
        Assert.False(canvas.IsPixelSet(0, 0));
    }

    [Fact]
    public void IsPixelSet_OutOfBounds_ReturnsFalse()
    {
        var canvas = new BrailleCanvas(2, 2);

        Assert.False(canvas.IsPixelSet(-1, 0));
        Assert.False(canvas.IsPixelSet(100, 100));
    }

    [Fact]
    public void Clear_ResetsAllPixels()
    {
        var canvas = new BrailleCanvas(3, 3);
        canvas.SetPixel(0, 0);
        canvas.SetPixel(3, 5);

        canvas.Clear();

        Assert.False(canvas.IsPixelSet(0, 0));
        Assert.False(canvas.IsPixelSet(3, 5));
        Assert.Equal('\u2800', canvas.GetCell(0, 0));
    }

    [Fact]
    public void DrawLine_HorizontalLine_SetsAllPixels()
    {
        var canvas = new BrailleCanvas(5, 2);
        canvas.DrawLine(0, 3, 9, 3);

        for (int x = 0; x <= 9; x++)
            Assert.True(canvas.IsPixelSet(x, 3), $"Pixel ({x}, 3) should be set");
    }

    [Fact]
    public void DrawLine_VerticalLine_SetsAllPixels()
    {
        var canvas = new BrailleCanvas(2, 3);
        canvas.DrawLine(1, 0, 1, 11);

        for (int y = 0; y <= 11; y++)
            Assert.True(canvas.IsPixelSet(1, y), $"Pixel (1, {y}) should be set");
    }

    [Fact]
    public void DrawLine_DiagonalLine_SetsExpectedPixels()
    {
        var canvas = new BrailleCanvas(5, 5);
        canvas.DrawLine(0, 0, 4, 4);

        // Diagonal should have pixels set along the path
        Assert.True(canvas.IsPixelSet(0, 0));
        Assert.True(canvas.IsPixelSet(4, 4));
        Assert.True(canvas.IsPixelSet(2, 2));
    }

    [Fact]
    public void DrawLine_SinglePoint_SetsOnePixel()
    {
        var canvas = new BrailleCanvas(3, 3);
        canvas.DrawLine(2, 2, 2, 2);

        Assert.True(canvas.IsPixelSet(2, 2));
    }

    [Fact]
    public void DrawDottedHorizontalLine_SetsPixelsAtSpacing()
    {
        var canvas = new BrailleCanvas(10, 2);
        canvas.DrawDottedHorizontalLine(2, 3);

        Assert.True(canvas.IsPixelSet(0, 2));
        Assert.False(canvas.IsPixelSet(1, 2));
        Assert.False(canvas.IsPixelSet(2, 2));
        Assert.True(canvas.IsPixelSet(3, 2));
        Assert.False(canvas.IsPixelSet(4, 2));
        Assert.False(canvas.IsPixelSet(5, 2));
        Assert.True(canvas.IsPixelSet(6, 2));
    }

    [Fact]
    public void DrawDottedVerticalLine_SetsPixelsAtSpacing()
    {
        var canvas = new BrailleCanvas(2, 5);
        canvas.DrawDottedVerticalLine(1, 4);

        Assert.True(canvas.IsPixelSet(1, 0));
        Assert.False(canvas.IsPixelSet(1, 1));
        Assert.False(canvas.IsPixelSet(1, 2));
        Assert.False(canvas.IsPixelSet(1, 3));
        Assert.True(canvas.IsPixelSet(1, 4));
    }

    [Fact]
    public void DrawDottedHorizontalLine_OutOfBounds_SilentlyIgnored()
    {
        var canvas = new BrailleCanvas(3, 3);

        // Should not throw
        canvas.DrawDottedHorizontalLine(-1, 2);
        canvas.DrawDottedHorizontalLine(100, 2);
    }

    [Fact]
    public void DrawDottedVerticalLine_OutOfBounds_SilentlyIgnored()
    {
        var canvas = new BrailleCanvas(3, 3);

        // Should not throw
        canvas.DrawDottedVerticalLine(-1, 2);
        canvas.DrawDottedVerticalLine(100, 2);
    }

    [Fact]
    public void GetCellDominantLayer_EmptyCell_ReturnsNegativeOne()
    {
        var canvas = new BrailleCanvas(3, 3);

        Assert.Equal(-1, canvas.GetCellDominantLayer(0, 0));
    }

    [Fact]
    public void GetCellDominantLayer_SingleSignalLayer_ReturnsThatLayer()
    {
        var canvas = new BrailleCanvas(3, 3);
        canvas.SetPixel(0, 0, layer: 2);

        Assert.Equal(2, canvas.GetCellDominantLayer(0, 0));
    }

    [Fact]
    public void GetCellDominantLayer_GridOnlyCell_ReturnsNegativeOne()
    {
        var canvas = new BrailleCanvas(3, 3);
        canvas.SetPixel(0, 0, layer: -1);
        canvas.SetPixel(1, 0, layer: -1);

        Assert.Equal(-1, canvas.GetCellDominantLayer(0, 0));
    }

    [Fact]
    public void GetCellDominantLayer_SignalBeatsGrid()
    {
        var canvas = new BrailleCanvas(3, 3);
        // 3 grid dots
        canvas.SetPixel(0, 0, layer: -1);
        canvas.SetPixel(1, 0, layer: -1);
        canvas.SetPixel(0, 1, layer: -1);
        // 1 signal dot
        canvas.SetPixel(1, 1, layer: 0);

        // Signal layer 0 should win even with fewer dots
        Assert.Equal(0, canvas.GetCellDominantLayer(0, 0));
    }

    [Fact]
    public void GetCellDominantLayer_MostDotsWinsAmongSignals()
    {
        var canvas = new BrailleCanvas(3, 3);
        // 1 dot on layer 0
        canvas.SetPixel(0, 0, layer: 0);
        // 3 dots on layer 1
        canvas.SetPixel(1, 0, layer: 1);
        canvas.SetPixel(0, 1, layer: 1);
        canvas.SetPixel(1, 1, layer: 1);

        Assert.Equal(1, canvas.GetCellDominantLayer(0, 0));
    }

    [Fact]
    public void GetCellDominantLayer_OutOfBounds_ReturnsNegativeOne()
    {
        var canvas = new BrailleCanvas(3, 3);

        Assert.Equal(-1, canvas.GetCellDominantLayer(-1, 0));
        Assert.Equal(-1, canvas.GetCellDominantLayer(0, -1));
        Assert.Equal(-1, canvas.GetCellDominantLayer(100, 0));
    }

    [Fact]
    public void GetCell_OutOfBounds_ReturnsBlankBraille()
    {
        var canvas = new BrailleCanvas(3, 3);

        Assert.Equal('\u2800', canvas.GetCell(-1, 0));
        Assert.Equal('\u2800', canvas.GetCell(0, -1));
        Assert.Equal('\u2800', canvas.GetCell(100, 100));
    }

    [Fact]
    public void SetPixel_SecondCell_CorrectlyMapped()
    {
        // Pixel (2,0) should be in cell (1,0), local position (0,0) => bit 0 => U+2801
        var canvas = new BrailleCanvas(3, 3);
        canvas.SetPixel(2, 0);

        Assert.Equal('\u2800', canvas.GetCell(0, 0)); // First cell unaffected
        Assert.Equal('\u2801', canvas.GetCell(1, 0)); // Second cell has bit 0
    }

    [Fact]
    public void SetPixel_SecondRowCell_CorrectlyMapped()
    {
        // Pixel (0,4) should be in cell (0,1), local position (0,0) => bit 0 => U+2801
        var canvas = new BrailleCanvas(3, 3);
        canvas.SetPixel(0, 4);

        Assert.Equal('\u2800', canvas.GetCell(0, 0)); // First row unaffected
        Assert.Equal('\u2801', canvas.GetCell(0, 1)); // Second row has bit 0
    }

    [Fact]
    public void BrailleEncoding_MiddleRows_CorrectBits()
    {
        var canvas = new BrailleCanvas(2, 2);

        // Row 1, col 0 => bit 1 => U+2802
        canvas.SetPixel(0, 1);
        Assert.Equal('\u2802', canvas.GetCell(0, 0));

        canvas.Clear();

        // Row 2, col 0 => bit 2 => U+2804
        canvas.SetPixel(0, 2);
        Assert.Equal('\u2804', canvas.GetCell(0, 0));

        canvas.Clear();

        // Row 1, col 1 => bit 4 => U+2810
        canvas.SetPixel(1, 1);
        Assert.Equal('\u2810', canvas.GetCell(0, 0));

        canvas.Clear();

        // Row 2, col 1 => bit 5 => U+2820
        canvas.SetPixel(1, 2);
        Assert.Equal('\u2820', canvas.GetCell(0, 0));
    }

    [Fact]
    public void DrawLine_WithLayer_TagsCorrectly()
    {
        var canvas = new BrailleCanvas(5, 2);
        canvas.DrawLine(0, 0, 4, 0, layer: 3);

        Assert.Equal(3, canvas.GetCellDominantLayer(0, 0));
        Assert.Equal(3, canvas.GetCellDominantLayer(1, 0));
        Assert.Equal(3, canvas.GetCellDominantLayer(2, 0));
    }

    [Fact]
    public void DrawDottedLines_UseGridLayer()
    {
        var canvas = new BrailleCanvas(5, 5);
        canvas.DrawDottedHorizontalLine(4, 2); // default layer -1
        canvas.DrawDottedVerticalLine(2, 2);   // default layer -1

        // All cells with only grid dots should return -1
        Assert.Equal(-1, canvas.GetCellDominantLayer(0, 1));
    }

    [Fact]
    public void GetCellFiltered_GridOnlyCell_IncludesGridDots()
    {
        var canvas = new BrailleCanvas(3, 3);
        // Grid dot at (0,0) => bit 0 => U+2801
        canvas.SetPixel(0, 0, layer: -1);

        Assert.Equal('\u2801', canvas.GetCellFiltered(0, 0));
    }

    [Fact]
    public void GetCellFiltered_MixedCell_ExcludesGridDots()
    {
        var canvas = new BrailleCanvas(3, 3);
        // Grid dot at (0,0) => bit 0
        canvas.SetPixel(0, 0, layer: -1);
        // Signal dot at (1,0) => bit 3
        canvas.SetPixel(1, 0, layer: 0);

        // GetCell includes both: U+2800 | bit0 | bit3 = U+2809
        Assert.Equal('\u2809', canvas.GetCell(0, 0));

        // GetCellFiltered should exclude grid dot: only bit3 => U+2808
        Assert.Equal('\u2808', canvas.GetCellFiltered(0, 0));
    }

    [Fact]
    public void GetCellFiltered_SignalOnlyCell_IncludesAllDots()
    {
        var canvas = new BrailleCanvas(3, 3);
        canvas.SetPixel(0, 0, layer: 0);
        canvas.SetPixel(1, 0, layer: 1);

        // Both signal dots should be included: bit0 | bit3 = U+2809
        Assert.Equal('\u2809', canvas.GetCellFiltered(0, 0));
    }

    [Fact]
    public void GetCellFiltered_EmptyCell_ReturnsBlank()
    {
        var canvas = new BrailleCanvas(3, 3);
        Assert.Equal('\u2800', canvas.GetCellFiltered(0, 0));
    }

    [Fact]
    public void GetCellFiltered_OutOfBounds_ReturnsBlank()
    {
        var canvas = new BrailleCanvas(3, 3);
        Assert.Equal('\u2800', canvas.GetCellFiltered(-1, 0));
        Assert.Equal('\u2800', canvas.GetCellFiltered(100, 100));
    }
}
