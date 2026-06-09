using Opcilloscope.App;

namespace Opcilloscope.Tests.App;

/// <summary>
/// Pure unit tests for the FocusManager next/prev index ordering logic.
/// These exercise the extracted index helpers directly and do not start the
/// Terminal.Gui application loop.
/// </summary>
public class FocusManagerTests
{
    // ── GetNextIndex ──

    [Theory]
    [InlineData(0, 3, 1)]
    [InlineData(1, 3, 2)]
    [InlineData(2, 3, 0)] // wrap around past the last pane
    public void GetNextIndex_AdvancesAndWrapsAround(int currentIndex, int paneCount, int expected)
    {
        var result = FocusManager.GetNextIndex(currentIndex, paneCount);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetNextIndex_FromNoSelection_ReturnsFirstPane()
    {
        // FocusNext treats "no current pane" as index -1 so the first Next lands on pane 0.
        var result = FocusManager.GetNextIndex(-1, 3);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetNextIndex_SinglePane_StaysOnPaneZero()
    {
        var result = FocusManager.GetNextIndex(0, 1);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetNextIndex_FullCycle_VisitsEveryPaneInOrder()
    {
        const int paneCount = 4;
        var visited = new List<int>();
        var index = -1;

        for (var i = 0; i < paneCount; i++)
        {
            index = FocusManager.GetNextIndex(index, paneCount);
            visited.Add(index);
        }

        Assert.Equal(new[] { 0, 1, 2, 3 }, visited);
        // One more step wraps back to the start.
        Assert.Equal(0, FocusManager.GetNextIndex(index, paneCount));
    }

    // ── GetPreviousIndex ──

    [Theory]
    [InlineData(2, 3, 1)]
    [InlineData(1, 3, 0)]
    [InlineData(0, 3, 2)] // wrap around before the first pane
    public void GetPreviousIndex_RetreatsAndWrapsAround(int currentIndex, int paneCount, int expected)
    {
        var result = FocusManager.GetPreviousIndex(currentIndex, paneCount);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetPreviousIndex_FromNoSelection_ReturnsLastPane()
    {
        // FocusPrevious treats "no current pane" as index 0 so the first Previous wraps to the last pane.
        var result = FocusManager.GetPreviousIndex(0, 3);

        Assert.Equal(2, result);
    }

    [Fact]
    public void GetPreviousIndex_SinglePane_StaysOnPaneZero()
    {
        var result = FocusManager.GetPreviousIndex(0, 1);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetPreviousIndex_FullCycle_VisitsEveryPaneInReverseOrder()
    {
        const int paneCount = 4;
        var visited = new List<int>();
        var index = 0;

        // Starting from "no selection" (index 0) the first step wraps to the last pane.
        for (var i = 0; i < paneCount; i++)
        {
            index = FocusManager.GetPreviousIndex(index, paneCount);
            visited.Add(index);
        }

        Assert.Equal(new[] { 3, 2, 1, 0 }, visited);
    }

    [Fact]
    public void NextThenPrevious_ReturnsToOriginalPane()
    {
        const int paneCount = 5;
        const int start = 2;

        var next = FocusManager.GetNextIndex(start, paneCount);
        var back = FocusManager.GetPreviousIndex(next, paneCount);

        Assert.Equal(start, back);
    }
}
