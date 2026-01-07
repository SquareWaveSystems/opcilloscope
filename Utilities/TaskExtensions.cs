namespace Opcilloscope.Utilities;

/// <summary>
/// Extension methods for Task handling, providing safe fire-and-forget patterns.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Safely executes a task without awaiting, logging any exceptions.
    /// This is the correct pattern for "fire and forget" scenarios.
    /// </summary>
    /// <param name="task">The task to execute.</param>
    /// <param name="logger">Optional logger for exception reporting.</param>
    /// <param name="context">Optional context description for error messages.</param>
    public static async void FireAndForget(this Task task, Logger? logger = null, string? context = null)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected, don't log
        }
        catch (Exception ex)
        {
            var message = string.IsNullOrEmpty(context)
                ? $"Unhandled exception in background task: {ex.Message}"
                : $"Error in {context}: {ex.Message}";

            logger?.Error(message);
            System.Diagnostics.Debug.WriteLine($"{message}\n{ex}");
        }
    }

    /// <summary>
    /// Safely executes a task without awaiting, using a custom error handler.
    /// </summary>
    /// <param name="task">The task to execute.</param>
    /// <param name="onError">Action to execute when an error occurs.</param>
    public static async void FireAndForget(this Task task, Action<Exception> onError)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected
        }
        catch (Exception ex)
        {
            onError(ex);
        }
    }
}
