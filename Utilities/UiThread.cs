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
}
