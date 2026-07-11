using Opcilloscope.App;
using Terminal.Gui;

namespace Opcilloscope.Tests.App;

public class MainWindowStateTests
{
    [Fact]
    public void CanQuit_WhenClean_DoesNotPrompt()
    {
        var prompted = false;

        var result = MainWindow.CanQuit(false, () =>
        {
            prompted = true;
            return false;
        });

        Assert.True(result);
        Assert.False(prompted);
    }

    [Fact]
    public void CanQuit_WhenDirtyAndDiscardRejected_BlocksQuit()
    {
        Assert.False(MainWindow.CanQuit(true, () => false));
    }

    [Fact]
    public void CanQuit_WhenDirtyAndDiscardConfirmed_AllowsQuit()
    {
        Assert.True(MainWindow.CanQuit(true, () => true));
    }

    [Fact]
    public void IsQuitKey_Escape_UsesGuardedQuitPath()
    {
        Assert.True(MainWindow.IsQuitKey(Key.Esc));
        Assert.False(MainWindow.IsQuitKey(Key.Enter));
        Assert.False(MainWindow.IsQuitKey(Key.Q.WithCtrl));
    }

    [Fact]
    public async Task AwaitRecordingStopForQuitAsync_CompletedStop_DoesNotPrompt()
    {
        var prompted = false;

        var result = await MainWindow.AwaitRecordingStopForQuitAsync(
            Task.CompletedTask,
            TimeSpan.Zero,
            () =>
            {
                prompted = true;
                return Task.FromResult(MainWindow.SlowRecordingQuitDecision.Cancel);
            });

        Assert.True(result);
        Assert.False(prompted);
    }

    [Fact]
    public async Task AwaitRecordingStopForQuitAsync_SlowStopAndCancel_BlocksQuit()
    {
        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var result = await MainWindow.AwaitRecordingStopForQuitAsync(
            stop.Task,
            TimeSpan.Zero,
            () => Task.FromResult(MainWindow.SlowRecordingQuitDecision.Cancel));

        Assert.False(result);
        stop.TrySetResult();
    }

    [Fact]
    public async Task AwaitRecordingStopForQuitAsync_KeepWaiting_ObservesCompletion()
    {
        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var promptCount = 0;

        var result = await MainWindow.AwaitRecordingStopForQuitAsync(
            stop.Task,
            TimeSpan.Zero,
            () =>
            {
                promptCount++;
                stop.TrySetResult();
                return Task.FromResult(MainWindow.SlowRecordingQuitDecision.KeepWaiting);
            });

        Assert.True(result);
        Assert.Equal(1, promptCount);
    }

    [Fact]
    public async Task AwaitRecordingStopForQuitAsync_QuitAnyway_DoesNotWaitForWriter()
    {
        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var result = await MainWindow.AwaitRecordingStopForQuitAsync(
            stop.Task,
            TimeSpan.Zero,
            () => Task.FromResult(MainWindow.SlowRecordingQuitDecision.QuitAnyway));

        Assert.True(result);
        Assert.False(stop.Task.IsCompleted);
        stop.TrySetResult();
    }
}
