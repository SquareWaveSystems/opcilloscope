using Opcilloscope.App;

namespace Opcilloscope.Tests.App;

/// <summary>
/// Pure unit tests for the FocusManager next index ordering logic.
/// These exercise the extracted index helper directly and do not start the
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
}
