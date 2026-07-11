namespace Opcilloscope.E2ETests;

/// <summary>
/// Black-box tests for the published single-file binary and real Terminal.Gui driver.
/// </summary>
[Collection("E2E")]
public sealed class StartupTests
{
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(15);
    private readonly PublishedBinaryFixture _fixture;

    public StartupTests(PublishedBinaryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Startup_RendersAllPrimaryPanes()
    {
        using var application = new OpcilloscopeSession(_fixture.BinaryPath);

        foreach (var pane in new[] { "Address Space", "Monitored Variables", "Node Details", "Log" })
        {
            Assert.True(
                application.WaitForText(pane, RenderTimeout),
                $"Missing pane '{pane}'.\n{RenderedScreen(application)}");
        }
    }

    [Fact]
    public void Startup_RendersMenuAndStatusHints()
    {
        using var application = new OpcilloscopeSession(_fixture.BinaryPath);
        Assert.True(application.WaitForText("Address Space", RenderTimeout), RenderedScreen(application));
        Assert.True(application.WaitForText("Switch", RenderTimeout), RenderedScreen(application));

        var screen = application.Snapshot();
        Assert.Contains("File", screen);
        Assert.Contains("Connection", screen);
        Assert.Contains("View", screen);
        Assert.Contains("Help", screen);
        Assert.Contains("Subscribe", screen);
    }

    [Fact]
    public void QuestionMark_OpensHelpDialog()
    {
        using var application = new OpcilloscopeSession(_fixture.BinaryPath);
        Assert.True(application.WaitForText("Address Space", RenderTimeout), RenderedScreen(application));

        application.Send("?");

        Assert.True(
            application.WaitForText("opcilloscope - Help", RenderTimeout),
            RenderedScreen(application));
    }

    [Fact]
    public void ControlQ_ExitsCleanlyWithSuccess()
    {
        using var application = new OpcilloscopeSession(_fixture.BinaryPath);
        Assert.True(application.WaitForText("Address Space", RenderTimeout), RenderedScreen(application));

        application.SendByte(0x11);

        Assert.True(
            application.WaitForExit(TimeSpan.FromSeconds(5)),
            $"Application did not exit after Ctrl+Q.\n{RenderedScreen(application)}");
        Assert.Equal(0, application.ExitCode);
    }

    private static string RenderedScreen(OpcilloscopeSession application) =>
        "Rendered screen was:\n" + application.Snapshot();
}
