using Moq;
using Opcilloscope.Utilities;

namespace Opcilloscope.Tests.Utilities;

/// <summary>
/// Tests for the <see cref="TerminalUi"/> helper that centralizes access to the
/// instance-based Terminal.Gui <see cref="IApplication"/>. The design contract is:
/// fire-and-forget members (Invoke, timers, clipboard, top-level queries) degrade
/// to no-ops when no application is running (as in headless tests), while the
/// interactive members (modal dialogs, message boxes) throw rather than silently
/// skip, since a hidden no-op there would mask a real bug.
///
/// <see cref="TerminalUi.App"/> is process-global mutable state, so these tests
/// share the non-parallel "Tui" collection with the other Terminal.Gui tests and
/// reset <see cref="TerminalUi.App"/> after each test.
/// </summary>
[Collection("Tui")]
public class TerminalUiTests : IDisposable
{
    public TerminalUiTests()
    {
        // Start from a known headless state (no application running).
        TerminalUi.App = null;
    }

    public void Dispose()
    {
        // Never leak a mock instance into sibling tests in the collection.
        TerminalUi.App = null;
    }

    // ── Fire-and-forget members: no-op when no application is running ──

    [Fact]
    public void Invoke_WithNoApp_DoesNotThrow()
    {
        var ran = false;
        // The action is queued onto the (non-existent) main loop, so it does not run,
        // but the call itself must be a safe no-op.
        var ex = Record.Exception(() => TerminalUi.Invoke(() => ran = true));

        Assert.Null(ex);
        Assert.False(ran);
    }

    [Fact]
    public void AddTimeout_WithNoApp_ReturnsNullAndDoesNotThrow()
    {
        object? token = null;
        var ex = Record.Exception(() =>
            token = TerminalUi.AddTimeout(TimeSpan.FromMilliseconds(100), () => false));

        Assert.Null(ex);
        Assert.Null(token);
    }

    [Fact]
    public void RemoveTimeout_WithNoApp_DoesNotThrow()
    {
        var ex = Record.Exception(() => TerminalUi.RemoveTimeout(new object()));

        Assert.Null(ex);
    }

    [Fact]
    public void TrySetClipboardData_WithNoApp_ReturnsFalse()
    {
        Assert.False(TerminalUi.TrySetClipboardData("some text"));
    }

    [Fact]
    public void TopRunnableView_WithNoApp_IsNull()
    {
        Assert.Null(TerminalUi.TopRunnableView);
    }

    [Fact]
    public void IsTopRunnable_WithNoApp_IsFalse()
    {
        var runnable = new Mock<IRunnable>().Object;

        Assert.False(TerminalUi.IsTopRunnable(runnable));
    }

    [Fact]
    public void Driver_WithNoApp_IsNull()
    {
        Assert.Null(TerminalUi.Driver);
    }

    [Fact]
    public void KeyDownHandlers_WithNoApp_DoNotThrow()
    {
        EventHandler<Key> handler = (_, _) => { };

        var ex = Record.Exception(() =>
        {
            TerminalUi.AddKeyDownHandler(handler);
            TerminalUi.RemoveKeyDownHandler(handler);
        });

        Assert.Null(ex);
    }

    // ── Interactive members: throw when no application is running ──

    [Fact]
    public void RunModal_WithNoApp_Throws()
    {
        var dialog = new Mock<IRunnable>().Object;

        Assert.Throws<InvalidOperationException>(() => TerminalUi.RunModal(dialog));
    }

    [Fact]
    public void RequestStop_WithNoApp_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => TerminalUi.RequestStop());
    }

    [Fact]
    public void Query_WithNoApp_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => TerminalUi.Query("Title", "Message", "OK"));
    }

    [Fact]
    public void ErrorQuery_WithNoApp_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => TerminalUi.ErrorQuery("Title", "Message", "OK"));
    }

    // ── Delegation to the running application instance ──

    [Fact]
    public void Invoke_WithApp_DelegatesToApplication()
    {
        var app = new Mock<IApplication>();
        TerminalUi.App = app.Object;
        Action action = () => { };

        TerminalUi.Invoke(action);

        app.Verify(a => a.Invoke(action), Times.Once);
    }

    [Fact]
    public void AddTimeout_WithApp_ReturnsApplicationToken()
    {
        var token = new object();
        var app = new Mock<IApplication>();
        app.Setup(a => a.AddTimeout(It.IsAny<TimeSpan>(), It.IsAny<Func<bool>>()))
           .Returns(token);
        TerminalUi.App = app.Object;

        var result = TerminalUi.AddTimeout(TimeSpan.FromSeconds(1), () => false);

        Assert.Same(token, result);
    }

    [Fact]
    public void RemoveTimeout_WithApp_DelegatesToApplication()
    {
        var token = new object();
        var app = new Mock<IApplication>();
        TerminalUi.App = app.Object;

        TerminalUi.RemoveTimeout(token);

        app.Verify(a => a.RemoveTimeout(token), Times.Once);
    }

    [Fact]
    public void TrySetClipboardData_WithApp_DelegatesToClipboard()
    {
        var clipboard = new Mock<IClipboard>();
        clipboard.Setup(c => c.TrySetClipboardData("payload")).Returns(true);
        var app = new Mock<IApplication>();
        app.Setup(a => a.Clipboard).Returns(clipboard.Object);
        TerminalUi.App = app.Object;

        Assert.True(TerminalUi.TrySetClipboardData("payload"));
        clipboard.Verify(c => c.TrySetClipboardData("payload"), Times.Once);
    }

    [Fact]
    public void RequestStop_WithApp_DelegatesToApplication()
    {
        var app = new Mock<IApplication>();
        TerminalUi.App = app.Object;

        TerminalUi.RequestStop();

        app.Verify(a => a.RequestStop(), Times.Once);
    }

    [Fact]
    public void IsTopRunnable_WithApp_ComparesAgainstTopRunnable()
    {
        var runnable = new Mock<IRunnable>().Object;
        var other = new Mock<IRunnable>().Object;
        var app = new Mock<IApplication>();
        app.Setup(a => a.TopRunnable).Returns(runnable);
        TerminalUi.App = app.Object;

        Assert.True(TerminalUi.IsTopRunnable(runnable));
        Assert.False(TerminalUi.IsTopRunnable(other));
    }
}
