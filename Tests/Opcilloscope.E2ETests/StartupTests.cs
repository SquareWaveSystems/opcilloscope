namespace Opcilloscope.E2ETests;

/// <summary>
/// Black-box end-to-end tests: launch the real published binary over a pseudo-terminal,
/// reconstruct the rendered screen, and assert on it. These exercise the full stack — the
/// published single-file binary, the Terminal.Gui driver, and actual rendering — that the
/// in-process component tests cannot reach.
/// </summary>
[Collection("E2E")]
public class StartupTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    private readonly PublishedBinaryFixture _fx;

    public StartupTests(PublishedBinaryFixture fx) => _fx = fx;

    [Fact]
    public void Startup_RendersAllPanesAndStatusBar()
    {
        using var app = new OpcilloscopeSession(_fx.BinaryPath);

        // Panes paint over a few frames; wait for each rather than racing the first paint.
        foreach (var pane in new[] { "Address Space", "Monitored Variables", "Node Details", "Log" })
            Assert.True(app.WaitForText(pane, Timeout), $"Missing pane '{pane}'.\n{Snapshot(app)}");
    }

    [Fact]
    public void Startup_RendersStatusBarKeybindingHints()
    {
        using var app = new OpcilloscopeSession(_fx.BinaryPath);

        // The status bar renders the keybinding hint row (Tab=Switch, Enter=Subscribe, F5=Refresh)
        // shortly after the panes; wait for it rather than racing the first paint.
        Assert.True(app.WaitForText("Switch", Timeout), Snapshot(app));
        Assert.Contains("Subscribe", app.Snapshot());
    }

    [Fact]
    public void Startup_RendersMenuBar()
    {
        using var app = new OpcilloscopeSession(_fx.BinaryPath);
        Assert.True(app.WaitForText("Address Space", Timeout), Snapshot(app));

        var screen = app.Snapshot();
        Assert.Contains("File", screen);
        Assert.Contains("Connection", screen);
        Assert.Contains("View", screen);
        Assert.Contains("Help", screen);
    }

    [Fact]
    public void QuestionMark_OpensHelpDialog()
    {
        using var app = new OpcilloscopeSession(_fx.BinaryPath);
        Assert.True(app.WaitForText("Address Space", Timeout), Snapshot(app));

        app.Send("?");

        // The help dialog's border title only appears once the dialog is open.
        Assert.True(app.WaitForText("opcilloscope - Help", Timeout), Snapshot(app));
    }

    private static string Snapshot(OpcilloscopeSession app) =>
        "Rendered screen was:\n" + app.Snapshot();
}
