using Terminal.Gui;

namespace Opcilloscope.Utilities;

/// <summary>
/// Central access point for Terminal.Gui application services: main-loop timers,
/// modal dialogs, message boxes, clipboard, and top-level view queries.
/// All call sites route through here (and <see cref="UiThread"/> for thread
/// marshalling) so access to the Terminal.Gui application object is confined to
/// a handful of files rather than spread across the UI code.
/// </summary>
/// <remarks>
/// Backed by the instance-based <see cref="IApplication"/> model
/// (<c>Application.Create()</c>); <see cref="App"/> is assigned once at startup
/// by <c>Program.Main</c>. When no application is running (unit tests construct
/// views headlessly), the fire-and-forget members (<see cref="Invoke"/>, timers,
/// clipboard) degrade to no-ops, while the interactive members (modal dialogs,
/// message boxes) throw, since silently skipping them would hide real bugs.
/// </remarks>
public static class TerminalUi
{
    private static readonly object AppLock = new();
    private static IApplication? _app;
    private static CancellationTokenSource _shutdown = CreateShutdownSource(isShutdown: true);

    /// <summary>
    /// The running Terminal.Gui application instance. Set once by Program.Main
    /// right after <c>Application.Create()</c>; null in headless unit tests.
    /// </summary>
    public static IApplication? App
    {
        get
        {
            lock (AppLock)
            {
                return _app;
            }
        }
        set
        {
            CancellationTokenSource previousShutdown;
            lock (AppLock)
            {
                if (ReferenceEquals(_app, value))
                {
                    return;
                }

                previousShutdown = _shutdown;
                _app = value;
                _shutdown = CreateShutdownSource(isShutdown: value is null);
            }

            // Cancellation callbacks may themselves touch TerminalUi. Never
            // invoke them while holding AppLock.
            previousShutdown.Cancel();
            previousShutdown.Dispose();
        }
    }

    /// <summary>
    /// Signals that the application's final main-loop session has ended. Any
    /// queued/awaited UI dispatches are failed instead of being left pending.
    /// </summary>
    public static void BeginShutdown()
    {
        CancellationTokenSource shutdown;
        lock (AppLock)
        {
            shutdown = _shutdown;
        }

        try
        {
            shutdown.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // App was replaced concurrently; its previous token was already
            // cancelled by the property setter.
        }
    }

    internal static bool TryGetDispatchContext(
        out IApplication? app,
        out CancellationToken shutdownToken)
    {
        lock (AppLock)
        {
            app = _app;
            shutdownToken = _shutdown.Token;
            return app is not null && !shutdownToken.IsCancellationRequested;
        }
    }

    private static CancellationTokenSource CreateShutdownSource(bool isShutdown)
    {
        var source = new CancellationTokenSource();
        if (isShutdown)
        {
            source.Cancel();
        }

        return source;
    }

    private static IApplication RequireApp() =>
        App ?? throw new InvalidOperationException("No Terminal.Gui application is running (TerminalUi.App is not set).");

    /// <summary>
    /// Executes an action on the UI thread via the application main loop.
    /// No-op when no application is running.
    /// </summary>
    public static void Invoke(Action action)
    {
        if (!TryGetDispatchContext(out var app, out var shutdownToken))
        {
            return;
        }

        app!.Invoke(() =>
        {
            if (!shutdownToken.IsCancellationRequested)
            {
                action();
            }
        });
    }

    /// <summary>
    /// Adds a recurring timeout on the UI main loop. The callback runs on the UI
    /// thread; returning true keeps the timer running, false stops it.
    /// Returns a token for <see cref="RemoveTimeout"/>, or null when no
    /// application is running.
    /// </summary>
    public static object? AddTimeout(TimeSpan interval, Func<bool> callback)
    {
        return App?.AddTimeout(interval, callback);
    }

    /// <summary>
    /// Removes a timeout previously added with <see cref="AddTimeout"/>.
    /// </summary>
    public static void RemoveTimeout(object token)
    {
        App?.RemoveTimeout(token);
    }

    /// <summary>
    /// Runs a view (dialog) modally, blocking until it requests stop.
    /// </summary>
    public static void RunModal(IRunnable view)
    {
        RequireApp().Run(view);
    }

    /// <summary>
    /// Requests that the currently running (top) view stop, closing a modal dialog.
    /// </summary>
    public static void RequestStop()
    {
        RequireApp().RequestStop();
    }

    /// <summary>
    /// Shows an informational/confirmation message box. Returns the index of the
    /// button pressed, or null if the message box was dismissed without a choice.
    /// </summary>
    public static int? Query(string title, string message, params string[] buttons)
    {
        return MessageBox.Query(RequireApp(), title, message, buttons);
    }

    /// <summary>
    /// Shows an error message box. Returns the index of the button pressed,
    /// or null if the message box was dismissed without a choice.
    /// </summary>
    public static int? ErrorQuery(string title, string message, params string[] buttons)
    {
        return MessageBox.ErrorQuery(RequireApp(), title, message, buttons);
    }

    /// <summary>
    /// Copies text to the OS clipboard. Returns true on success.
    /// </summary>
    public static bool TrySetClipboardData(string text)
    {
        return App?.Clipboard?.TrySetClipboardData(text) ?? false;
    }

    /// <summary>
    /// Gets the view of the currently running (top) runnable, or null when none is running.
    /// </summary>
    public static View? TopRunnableView => App?.TopRunnableView;

    /// <summary>
    /// Returns true when the given runnable is the currently running (top) one,
    /// i.e. no dialog is running above it.
    /// </summary>
    public static bool IsTopRunnable(IRunnable runnable)
    {
        return App?.TopRunnable == runnable;
    }

    /// <summary>
    /// The driver of the running application, or null when none is running
    /// (e.g. in headless tests).
    /// </summary>
    public static IDriver? Driver => App?.Driver;

    /// <summary>
    /// Subscribes to application-level key-down events, which fire before any
    /// view processes the key. No-op when no application is running.
    /// </summary>
    public static void AddKeyDownHandler(EventHandler<Key> handler)
    {
        if (App?.Keyboard is { } keyboard)
        {
            keyboard.KeyDown += handler;
        }
    }

    /// <summary>
    /// Unsubscribes a handler added with <see cref="AddKeyDownHandler"/>.
    /// </summary>
    public static void RemoveKeyDownHandler(EventHandler<Key> handler)
    {
        if (App?.Keyboard is { } keyboard)
        {
            keyboard.KeyDown -= handler;
        }
    }
}
