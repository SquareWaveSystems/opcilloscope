using Terminal.Gui;

namespace OpcScope.Utilities;

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
    /// Executes an action on the UI thread after a delay.
    /// </summary>
    public static void RunDelayed(Action action, TimeSpan delay)
    {
        Application.AddTimeout(delay, () =>
        {
            action();
            return false; // Don't repeat
        });
    }
}
