using Terminal.Gui;

namespace Opcilloscope.Utilities;

/// <summary>
/// Helper for marshalling calls to the UI thread.
/// </summary>
public static class UiThread
{
    /// <summary>
    /// Executes an action on the UI thread.
    /// </summary>
    public static void Run(Action action)
    {
        Application.Invoke(action);
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
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Application.Invoke(() =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }
}
