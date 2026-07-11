using Moq;
using Opcilloscope.Utilities;

namespace Opcilloscope.Tests.Utilities;

/// <summary>
/// Tests for the <see cref="UiThread"/> marshalling helper, which forwards to
/// <see cref="TerminalUi.Invoke"/>. Like <see cref="TerminalUiTests"/> these touch
/// the process-global <see cref="TerminalUi.App"/>, so they share the non-parallel
/// "Tui" collection and reset it after each test.
/// </summary>
[Collection("Tui")]
public class UiThreadTests : IDisposable
{
    public UiThreadTests()
    {
        TerminalUi.App = null;
    }

    public void Dispose()
    {
        TerminalUi.App = null;
    }

    [Fact]
    public void Run_WithNoApp_DoesNotThrow()
    {
        var ran = false;
        var ex = Record.Exception(() => UiThread.Run(() => ran = true));

        Assert.Null(ex);
        Assert.False(ran);
    }

    [Fact]
    public void Run_WithApp_MarshalsThroughApplicationInvoke()
    {
        var app = new Mock<IApplication>();
        app.Setup(a => a.Invoke(It.IsAny<Action>()))
            .Callback<Action>(callback => callback());
        TerminalUi.App = app.Object;
        var ran = false;
        Action action = () => ran = true;

        UiThread.Run(action);

        Assert.True(ran);
        app.Verify(a => a.Invoke(It.IsAny<Action>()), Times.Once);
    }

    [Fact]
    public void RunAsync_WithNoApp_ThrowsInsteadOfReturningANeverCompletingTask()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = UiThread.RunAsync(() => 42);
        });
    }

    [Fact]
    public async Task RunAsync_WithApp_CompletesWithCallbackResult()
    {
        var app = new Mock<IApplication>();
        app.Setup(a => a.Invoke(It.IsAny<Action>()))
            .Callback<Action>(action => action());
        TerminalUi.App = app.Object;

        var result = await UiThread.RunAsync(() => 42);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task RunAsync_WhenCallbackThrows_PropagatesException()
    {
        var app = new Mock<IApplication>();
        app.Setup(a => a.Invoke(It.IsAny<Action>()))
            .Callback<Action>(action => action());
        TerminalUi.App = app.Object;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => UiThread.RunAsync<int>(() => throw new InvalidOperationException("boom")));

        Assert.Equal("boom", error.Message);
    }

    [Fact]
    public async Task RunAsync_WhenMainLoopStopsBeforeDeferredInvoke_FailsInsteadOfHanging()
    {
        Action? queued = null;
        var ran = false;
        var app = new Mock<IApplication>();
        app.Setup(a => a.Invoke(It.IsAny<Action>()))
            .Callback<Action>(action => queued = action);
        TerminalUi.App = app.Object;

        var task = UiThread.RunAsync(() => ran = true);
        Assert.NotNull(queued);

        TerminalUi.BeginShutdown();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        queued!();
        Assert.Contains("main loop stopped", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(ran);
    }
}
