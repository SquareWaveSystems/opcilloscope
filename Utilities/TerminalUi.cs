using Terminal.Gui;

namespace Opcilloscope.Utilities;

/// <summary>
/// Central access point for Terminal.Gui application services: main-loop timers,
/// modal dialogs, message boxes, clipboard, and top-level view queries.
/// All call sites route through here (and <see cref="UiThread"/> for thread
/// marshalling) so access to the Terminal.Gui application object is confined to
/// a handful of files rather than spread across the UI code.
/// </summary>
public static class TerminalUi
{
    /// <summary>
    /// Adds a recurring timeout on the UI main loop. The callback runs on the UI
    /// thread; returning true keeps the timer running, false stops it.
    /// Returns a token for <see cref="RemoveTimeout"/>.
    /// </summary>
    public static object? AddTimeout(TimeSpan interval, Func<bool> callback)
    {
        return Application.AddTimeout(interval, callback);
    }

    /// <summary>
    /// Removes a timeout previously added with <see cref="AddTimeout"/>.
    /// </summary>
    public static void RemoveTimeout(object token)
    {
        Application.RemoveTimeout(token);
    }

    /// <summary>
    /// Runs a view (dialog) modally, blocking until it requests stop.
    /// </summary>
    public static void RunModal(IRunnable view)
    {
        Application.Run(view);
    }

    /// <summary>
    /// Requests that the currently running (top) view stop, closing a modal dialog.
    /// </summary>
    public static void RequestStop()
    {
        Application.RequestStop();
    }

    /// <summary>
    /// Shows an informational/confirmation message box. Returns the index of the
    /// button pressed, or null if the message box was dismissed without a choice.
    /// </summary>
    public static int? Query(string title, string message, params string[] buttons)
    {
        return MessageBox.Query(Application.Instance, title, message, buttons);
    }

    /// <summary>
    /// Shows an error message box. Returns the index of the button pressed,
    /// or null if the message box was dismissed without a choice.
    /// </summary>
    public static int? ErrorQuery(string title, string message, params string[] buttons)
    {
        return MessageBox.ErrorQuery(Application.Instance, title, message, buttons);
    }

    /// <summary>
    /// Copies text to the OS clipboard. Returns true on success.
    /// </summary>
    public static bool TrySetClipboardData(string text)
    {
        return Clipboard.TrySetClipboardData(text);
    }

    /// <summary>
    /// Gets the view of the currently running (top) runnable, or null when none is running.
    /// </summary>
    public static View? TopRunnableView => Application.TopRunnableView;

    /// <summary>
    /// Returns true when the given runnable is the currently running (top) one,
    /// i.e. no dialog is running above it.
    /// </summary>
    public static bool IsTopRunnable(IRunnable runnable)
    {
        return Application.TopRunnable == runnable;
    }
}
