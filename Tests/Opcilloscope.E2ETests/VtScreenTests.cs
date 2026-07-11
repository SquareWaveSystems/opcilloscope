namespace Opcilloscope.E2ETests;

public class VtScreenTests
{
    [Fact]
    public void Feed_FragmentedEraseDisplaySequence_ClearsTheScreen()
    {
        var screen = new VtScreen(rows: 2, cols: 10);
        screen.Feed("stale text");

        screen.Feed("\x1b[");
        screen.Feed("2J");

        Assert.Equal("\n", screen.Text());
    }

    [Fact]
    public void Feed_FragmentedCursorSequence_PositionsSubsequentText()
    {
        var screen = new VtScreen(rows: 3, cols: 8);

        screen.Feed("\x1b[2;");
        screen.Feed("3Hplaced");

        Assert.Equal("\n  placed\n", screen.Text());
    }

    [Fact]
    public void Feed_ZeroCursorParameters_UseTheAnsiDefaultOfOne()
    {
        var screen = new VtScreen(rows: 2, cols: 4);
        screen.Feed("xxxx");

        screen.Feed("\x1b[0;0HZ");

        Assert.Equal("Zxxx\n", screen.Text());
    }

    [Theory]
    [InlineData("\x1b]10;rgb:ffff/ffff/ffff\x07")]
    [InlineData("\x1b]11;rgb:0000/0000/0000\x1b\\")]
    public void Feed_FragmentedOscSequence_IgnoresPayload(string sequence)
    {
        var screen = new VtScreen(rows: 1, cols: 8);
        var split = sequence.Length - 1;

        screen.Feed(sequence[..split]);
        screen.Feed(sequence[split..] + "ok");

        Assert.Equal("ok", screen.Text());
    }

    [Fact]
    public void Feed_FragmentedDeviceControlString_IgnoresPayload()
    {
        var screen = new VtScreen(rows: 1, cols: 8);

        screen.Feed("\x1bPignored\x1b");
        screen.Feed("\\ok");

        Assert.Equal("ok", screen.Text());
    }
}
