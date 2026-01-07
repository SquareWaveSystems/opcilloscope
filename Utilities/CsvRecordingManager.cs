using System.Collections.Concurrent;
using Opcilloscope.OpcUa.Models;

namespace Opcilloscope.Utilities;

/// <summary>
/// Manages CSV recording of monitored variable value changes.
/// Writes data to file in real-time as values change using a background queue.
/// </summary>
public class CsvRecordingManager : IDisposable
{
    /// <summary>
    /// The file extension used for recording files.
    /// </summary>
    public const string RecordingFileExtension = ".csv";

    /// <summary>
    /// Gets the default directory for recording files.
    /// Uses cross-platform appropriate locations:
    /// - Windows: %USERPROFILE%/Documents/opcilloscope/recordings/
    /// - macOS: ~/Documents/opcilloscope/recordings/
    /// - Linux: ~/Documents/opcilloscope/recordings/ (or $XDG_DOCUMENTS_DIR/opcilloscope/recordings/)
    /// </summary>
    /// <returns>Path to the default recordings directory.</returns>
    public static string GetDefaultRecordingsDirectory()
    {
        string documentsDir;

        if (OperatingSystem.IsLinux())
        {
            // Linux: Use XDG_DOCUMENTS_DIR or fall back to ~/Documents
            var xdgDocuments = Environment.GetEnvironmentVariable("XDG_DOCUMENTS_DIR");
            if (!string.IsNullOrEmpty(xdgDocuments))
            {
                documentsDir = xdgDocuments;
            }
            else
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                documentsDir = Path.Combine(home, "Documents");
            }
        }
        else
        {
            // Windows and macOS: use system Documents folder
            documentsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Fallback if Documents folder is not available
            if (string.IsNullOrEmpty(documentsDir))
            {
                documentsDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
        }

        // Fallback to temp if all else fails
        if (string.IsNullOrEmpty(documentsDir))
        {
            documentsDir = Path.GetTempPath();
        }

        // Use lowercase folder name on all platforms
        var appFolder = "opcilloscope";

        var dir = Path.Combine(documentsDir, appFolder, "recordings");
        return dir;
    }

    /// <summary>
    /// Ensures the default recordings directory exists.
    /// </summary>
    /// <returns>Path to the recordings directory.</returns>
    public static string EnsureRecordingsDirectory()
    {
        var dir = GetDefaultRecordingsDirectory();
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Generates a default filename for saving a recording.
    /// Format: {sanitized_connection_url}_{variable_count}vars_{timestamp}.csv
    /// </summary>
    /// <param name="connectionUrl">The OPC UA connection URL, or null if not connected.</param>
    /// <param name="variableCount">The number of variables being recorded.</param>
    /// <returns>A sanitized filename with .csv extension.</returns>
    public static string GenerateDefaultRecordingFilename(string? connectionUrl, int variableCount)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string baseName;

        if (!string.IsNullOrEmpty(connectionUrl))
        {
            baseName = SanitizeUrlForFilename(connectionUrl);
        }
        else
        {
            baseName = "recording";
        }

        return $"{baseName}_{variableCount}vars_{timestamp}{RecordingFileExtension}";
    }

    /// <summary>
    /// Sanitizes a URL to be used as part of a filename.
    /// Replaces invalid filename characters with underscores.
    /// </summary>
    /// <param name="url">The URL to sanitize.</param>
    /// <returns>A filename-safe string derived from the URL.</returns>
    public static string SanitizeUrlForFilename(string url)
    {
        if (string.IsNullOrEmpty(url))
            return "unknown";

        // Remove protocol prefix
        var sanitized = url;
        if (sanitized.StartsWith("opc.tcp://", StringComparison.OrdinalIgnoreCase))
            sanitized = sanitized.Substring(10);
        else if (sanitized.StartsWith("opc.https://", StringComparison.OrdinalIgnoreCase))
            sanitized = sanitized.Substring(12);
        else if (sanitized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            sanitized = sanitized.Substring(8);
        else if (sanitized.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            sanitized = sanitized.Substring(7);

        // Replace invalid filename characters with underscores
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }

        // Also replace common URL special characters
        sanitized = sanitized
            .Replace(':', '_')
            .Replace('/', '_')
            .Replace('\\', '_')
            .Replace('?', '_')
            .Replace('&', '_')
            .Replace('=', '_');

        // Remove consecutive underscores
        while (sanitized.Contains("__"))
        {
            sanitized = sanitized.Replace("__", "_");
        }

        // Trim underscores from start and end
        sanitized = sanitized.Trim('_');

        // Limit length to avoid overly long filenames
        if (sanitized.Length > 50)
        {
            sanitized = sanitized.Substring(0, 50).TrimEnd('_');
        }

        return string.IsNullOrEmpty(sanitized) ? "unknown" : sanitized;
    }

    /// <summary>
    /// Ensures a filename has the correct .csv extension.
    /// </summary>
    /// <param name="filename">The filename to check.</param>
    /// <returns>The filename with .csv extension.</returns>
    public static string EnsureRecordingExtension(string filename)
    {
        if (string.IsNullOrEmpty(filename))
            return filename;

        if (!filename.EndsWith(RecordingFileExtension, StringComparison.OrdinalIgnoreCase))
        {
            return filename + RecordingFileExtension;
        }

        return filename;
    }

    private readonly Logger _logger;
    private StreamWriter? _writer;
    private string? _filePath;
    private bool _isRecording;
    private readonly object _lock = new();
    private DateTime _recordingStartTime;
    private long _recordCount;
    private readonly ConcurrentQueue<MonitoredNode> _recordQueue = new();
    private readonly SemaphoreSlim _queueSemaphore = new(0);
    private Task? _writeTask;
    private CancellationTokenSource? _cancellationTokenSource;

    public event Action<bool>? RecordingStateChanged;

    public bool IsRecording
    {
        get
        {
            lock (_lock)
            {
                return _isRecording;
            }
        }
    }

    public string? FilePath => _filePath;
    public long RecordCount => Interlocked.Read(ref _recordCount);
    public TimeSpan RecordingDuration
    {
        get
        {
            lock (_lock)
            {
                return _isRecording ? DateTime.Now - _recordingStartTime : TimeSpan.Zero;
            }
        }
    }

    public CsvRecordingManager(Logger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Start recording to the specified file path.
    /// </summary>
    public bool StartRecording(string filePath)
    {
        lock (_lock)
        {
            if (_isRecording)
            {
                _logger.Warning("Recording is already in progress");
                return false;
            }

            try
            {
                _filePath = filePath;
                _writer = new StreamWriter(_filePath, append: false);

                // Write CSV header
                _writer.WriteLine("Timestamp,DisplayName,NodeId,Value,Status");
                _writer.Flush();

                _isRecording = true;
                _recordingStartTime = DateTime.Now;
                _recordCount = 0;

                // Start background writer task
                _cancellationTokenSource = new CancellationTokenSource();
                _writeTask = Task.Run(() => WriteQueuedRecordsAsync(_cancellationTokenSource.Token));

                _logger.Info($"Started recording to {_filePath}");
                RecordingStateChanged?.Invoke(true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to start recording: {ex.Message}");
                _writer?.Dispose();
                _writer = null;
                _filePath = null;
                return false;
            }
        }
    }

    /// <summary>
    /// Stop recording and close the file.
    /// </summary>
    public void StopRecording()
    {
        Task? taskToWait = null;
        CancellationTokenSource? ctsToDispose = null;

        lock (_lock)
        {
            if (!_isRecording)
            {
                return;
            }

            // Signal background task to stop
            _cancellationTokenSource?.Cancel();
            taskToWait = _writeTask;
            ctsToDispose = _cancellationTokenSource;
            _isRecording = false;
        }

        // Wait for background task outside of lock to avoid deadlock
        try
        {
            taskToWait?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.Error($"Error waiting for background writer: {ex.Message}");
        }

        lock (_lock)
        {
            try
            {
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;

                var duration = DateTime.Now - _recordingStartTime;
                _logger.Info($"Stopped recording. {_recordCount} records written to {_filePath} (duration: {duration:hh\\:mm\\:ss})");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error closing recording file: {ex.Message}");
            }
            finally
            {
                ctsToDispose?.Dispose();
                _cancellationTokenSource = null;
                _writeTask = null;
                RecordingStateChanged?.Invoke(false);
            }
        }
    }

    /// <summary>
    /// Record a value change. Called from the subscription's ValueChanged event.
    /// Queues the value for asynchronous writing to avoid blocking the notification thread.
    /// </summary>
    public void RecordValue(MonitoredNode item)
    {
        if (!IsRecording)
        {
            return;
        }

        // Queue the item for background writing (non-blocking)
        _recordQueue.Enqueue(item);
        _queueSemaphore.Release();
    }

    /// <summary>
    /// Background task that processes the queue and writes records to the file.
    /// </summary>
    private async Task WriteQueuedRecordsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Wait for items in the queue or cancellation
                await _queueSemaphore.WaitAsync(cancellationToken);

                // Process all queued items
                while (_recordQueue.TryDequeue(out var item))
                {
                    WriteRecord(item);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }
        catch (Exception ex)
        {
            _logger.Error($"Error in background writer: {ex.Message}");
        }
        finally
        {
            // Write any remaining queued items
            while (_recordQueue.TryDequeue(out var item))
            {
                WriteRecord(item);
            }
        }
    }

    /// <summary>
    /// Write a single record to the CSV file.
    /// </summary>
    private void WriteRecord(MonitoredNode item)
    {
        lock (_lock)
        {
            if (_writer == null)
            {
                return;
            }

            try
            {
                // Use ISO 8601 timestamp format with milliseconds for precision
                var timestamp = item.Timestamp?.ToString("yyyy-MM-ddTHH:mm:ss.fff")
                    ?? DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff");

                // Escape values for CSV (handle quotes and commas)
                var displayName = EscapeCsvField(item.DisplayName);
                var nodeId = EscapeCsvField(item.NodeId.ToString());
                var value = EscapeCsvField(item.Value);
                var status = EscapeCsvField(item.StatusString);

                _writer.WriteLine($"{timestamp},{displayName},{nodeId},{value},{status}");

                // Flush periodically (every 10 records) for durability without too much I/O
                Interlocked.Increment(ref _recordCount);
                if (_recordCount % 10 == 0)
                {
                    _writer.Flush();
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"Error writing record: {ex.Message}");
            }
        }
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return field;
        }

        // If field contains comma, quote, or newline, wrap in quotes and escape internal quotes
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    public void Dispose()
    {
        StopRecording();
    }
}
