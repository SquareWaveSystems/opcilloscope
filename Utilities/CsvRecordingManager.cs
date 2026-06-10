using System.Collections.Concurrent;
using System.Globalization;
using Opcilloscope.OpcUa.Models;

namespace Opcilloscope.Utilities;

/// <summary>
/// Manages CSV recording of monitored variable value changes.
/// Writes data to file in real-time as values change using a background queue.
/// Output is culture-invariant: timestamps are ISO 8601 (Gregorian calendar,
/// '.' decimal / ':' time separators regardless of locale) and values are the
/// full-precision raw representation ('.' decimal separator, arrays as
/// semicolon-joined elements) rather than the truncated UI display string.
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
        // InvariantCulture pins the Gregorian calendar (e.g. th-TH defaults to
        // the Buddhist calendar, which would shift the year by 543).
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
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

    /// <summary>
    /// Immutable snapshot of a monitored value captured at enqueue time.
    /// Decouples the background writer from the live <see cref="MonitoredNode"/>,
    /// which the OPC notification thread mutates in place.
    /// </summary>
    private readonly record struct RecordSnapshot(
        DateTime? Timestamp,
        string DisplayName,
        string NodeId,
        string Value,
        string Status);

    private readonly Logger _logger;
    private StreamWriter? _writer;
    private string? _filePath;
    private bool _isRecording;
    private readonly object _lock = new();
    private DateTime _recordingStartTime;
    private long _recordCount;
    private readonly ConcurrentQueue<RecordSnapshot> _recordQueue = new();
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

                // Discard any stale snapshots left over from a previous session
                // so they cannot cross-contaminate the new recording.
                while (_recordQueue.TryDequeue(out _)) { }

                _isRecording = true;
                _recordingStartTime = DateTime.Now;
                _recordCount = 0;

                // Start background writer task
                _cancellationTokenSource = new CancellationTokenSource();
                _writeTask = Task.Run(() => WriteQueuedRecordsAsync(_cancellationTokenSource.Token));

                _logger.Info($"Started recording to {_filePath}");
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

        // Raise the event after releasing the lock to avoid invoking
        // subscriber callbacks while holding it.
        RecordingStateChanged?.Invoke(true);
        return true;
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

            _isRecording = false;
            taskToWait = _writeTask;
            ctsToDispose = _cancellationTokenSource;
        }

        // 1. Signal cancellation BEFORE waiting
        ctsToDispose?.Cancel();

        // 2. Wait for write loop to complete with timeout
        // This must happen BEFORE disposing the writer to prevent ObjectDisposedException
        bool taskCompleted = false;
        if (taskToWait != null)
        {
            try
            {
                // Wait with a reasonable timeout - if the task doesn't complete,
                // we still need to clean up, but the writer access is protected by the lock
                taskCompleted = taskToWait.Wait(TimeSpan.FromSeconds(10));
                if (!taskCompleted)
                {
                    _logger.Warning("Background writer task did not complete within timeout");
                }
            }
            catch (AggregateException ex)
            {
                // Task.Wait wraps exceptions in AggregateException
                foreach (var inner in ex.InnerExceptions)
                {
                    if (inner is not OperationCanceledException)
                    {
                        _logger.Error($"Error in background writer: {inner.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error waiting for background writer: {ex.Message}");
            }
        }

        // 3. Only dispose writer AFTER the write loop has exited (or timed out)
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
            }
        }

        // Raise the event after releasing the lock to avoid invoking
        // subscriber callbacks while holding it.
        RecordingStateChanged?.Invoke(false);
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

        // Capture an immutable snapshot at enqueue time. The OPC notification
        // thread mutates the live MonitoredNode in place, so queuing the
        // reference would let the writer serialize a newer state than was
        // sampled (duplicated/skipped rows under load).
        // Record the full-precision, culture-invariant RawValue rather than the
        // truncated ("F2"), culture-aware display Value. Fall back to Value for
        // nodes that never had a raw representation set.
        var snapshot = new RecordSnapshot(
            item.Timestamp,
            item.DisplayName,
            item.NodeId.ToString(),
            string.IsNullOrEmpty(item.RawValue) ? item.Value : item.RawValue,
            item.StatusString);

        // Queue the snapshot for background writing (non-blocking)
        _recordQueue.Enqueue(snapshot);
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
                try
                {
                    await _queueSemaphore.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation requested - exit the loop cleanly
                    break;
                }

                // Check cancellation again before processing (defensive)
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // Process all queued items
                while (_recordQueue.TryDequeue(out var item))
                {
                    // Check cancellation between items for faster shutdown
                    if (cancellationToken.IsCancellationRequested)
                    {
                        // Re-queue the item so it can be processed in the finally block
                        _recordQueue.Enqueue(item);
                        break;
                    }
                    WriteRecord(item);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error in background writer: {ex.Message}");
        }
        finally
        {
            // Write any remaining queued items before exiting
            // This runs BEFORE StopRecording disposes the writer (due to the Wait)
            while (_recordQueue.TryDequeue(out var item))
            {
                WriteRecord(item);
            }
        }
    }

    /// <summary>
    /// Write a single record to the CSV file.
    /// </summary>
    private void WriteRecord(RecordSnapshot item)
    {
        lock (_lock)
        {
            // Only the writer guard here: _isRecording is cleared before the
            // shutdown drain, so checking it would discard the in-flight tail
            // (flush-on-stop and re-queue-on-cancel records).
            if (_writer == null)
            {
                return;
            }

            try
            {
                // Use ISO 8601 timestamp format with milliseconds for precision.
                // InvariantCulture is required: the ':' custom-format specifier
                // is replaced by the culture's time separator (fi-FI uses '.')
                // and the culture's default calendar applies (th-TH uses the
                // Buddhist calendar), which would break the ISO 8601 contract.
                var timestamp = item.Timestamp?.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)
                    ?? DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);

                // Escape values for CSV (RFC 4180 quoting plus formula
                // injection neutralization for server-supplied fields).
                // The timestamp is generated locally in a fixed format, so it
                // needs no escaping; the header line is a constant.
                var displayName = EscapeCsvField(item.DisplayName);
                var nodeId = EscapeCsvField(item.NodeId);
                var value = EscapeCsvField(item.Value);
                var status = EscapeCsvField(item.Status);

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

        // Neutralize spreadsheet formula injection before applying RFC 4180
        // quoting, so the quoting decision sees the final field content.
        field = NeutralizeFormulaInjection(field);

        // If field contains comma, quote, or newline, wrap in quotes and escape internal quotes
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    /// <summary>
    /// Neutralizes spreadsheet formula injection (CWE-1236). DisplayName,
    /// NodeId, Value and Status originate from the OPC UA server, which is
    /// potentially untrusted on a plant network; a field such as
    /// "=cmd|'/C calc'!A0" executes as a formula when the CSV is opened in
    /// Excel/LibreOffice (RFC 4180 quoting alone does not prevent this).
    /// Fields starting with a formula trigger character ('=', '+', '-', '@',
    /// tab, or CR) are prefixed with a single quote, which spreadsheets
    /// interpret as "treat as text".
    /// Exception: fields that parse as a number under InvariantCulture are
    /// NOT neutralized - recorded values are routinely negative numbers
    /// (e.g. "-12.5"), they are inert in spreadsheets, and prefixing them
    /// would corrupt the data column for downstream tools.
    /// </summary>
    private static string NeutralizeFormulaInjection(string field)
    {
        var first = field[0];
        if (first is not ('=' or '+' or '-' or '@' or '\t' or '\r'))
        {
            return field;
        }

        // Valid invariant-culture numbers (covers leading '+'/'-') are safe
        // and must round-trip unchanged.
        if (double.TryParse(field, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return field;
        }

        return "'" + field;
    }

    public void Dispose()
    {
        StopRecording();
    }
}
