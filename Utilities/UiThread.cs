namespace Opcilloscope.Utilities;

/// <summary>
/// Helper for marshalling calls to the UI thread.
/// </summary>
public static class UiThread
{
    /// <summary>
    /// Executes an action on the UI thread. No-op when no Terminal.Gui
    /// application is running (e.g. in headless tests).
    /// </summary>
    public static void Run(Action action)
    {
        TerminalUi.Invoke(action);
    }

    /// <summary>
    /// Executes a function on the UI thread and asynchronously returns its result.
    /// Use from async continuations (which may resume on a background thread) when
    /// the result is needed before proceeding — e.g. running a modal dialog.
    /// Safe to call from the UI thread itself: the work is queued for a later
    /// iteration of the main loop and awaited rather than blocked on.
    /// </summary>
    public static Task<T> RunAsync<T>(Func<T> func)
    {
        if (!TerminalUi.TryGetDispatchContext(out var app, out var shutdownToken))
        {
            throw new InvalidOperationException("No active Terminal.Gui main loop is available for UI dispatch.");
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationRegistration = shutdownToken.Register(() =>
            tcs.TrySetException(new InvalidOperationException(
                "The Terminal.Gui main loop stopped before the UI callback could run.")));
        _ = tcs.Task.ContinueWith(
            _ => cancellationRegistration.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        try
        {
            app!.Invoke(() =>
            {
                if (shutdownToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    tcs.TrySetResult(func());
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
        }
        catch
        {
            cancellationRegistration.Dispose();
            throw;
        }

        return tcs.Task;
    }

    /// <summary>
    /// Executes an action on the UI thread and completes once it has run.
    /// Unlike <see cref="Run"/>, this throws when no application is running so
    /// callers cannot await a callback that will never be scheduled.
    /// </summary>
    public static Task RunAsync(Action action) => RunAsync(() =>
    {
        action();
        return true;
    });
}
