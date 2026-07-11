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
        TerminalUi.App = app.Object;
        Action action = () => { };

        UiThread.Run(action);

        app.Verify(a => a.Invoke(action), Times.Once);
    }
}
