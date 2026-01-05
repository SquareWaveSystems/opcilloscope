using OpcScope.OpcUa.Models;

namespace OpcScope.Utilities;

/// <summary>
/// Manages CSV recording of monitored item value changes.
/// Writes data to file in real-time as values change.
/// </summary>
public class CsvRecordingManager : IDisposable
{
    private readonly Logger _logger;
    private StreamWriter? _writer;
    private string? _filePath;
    private bool _isRecording;
    private readonly object _lock = new();
    private DateTime _recordingStartTime;
    private long _recordCount;

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
    public TimeSpan RecordingDuration => _isRecording ? DateTime.Now - _recordingStartTime : TimeSpan.Zero;

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
        lock (_lock)
        {
            if (!_isRecording)
            {
                return;
            }

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
                _logger.Error($"Error stopping recording: {ex.Message}");
            }
            finally
            {
                _isRecording = false;
                RecordingStateChanged?.Invoke(false);
            }
        }
    }

    /// <summary>
    /// Record a value change. Called from the subscription's ValueChanged event.
    /// </summary>
    public void RecordValue(MonitoredNode item)
    {
        lock (_lock)
        {
            if (!_isRecording || _writer == null)
            {
                return;
            }

            try
            {
                // Use ISO 8601 timestamp format with milliseconds for precision
                var timestamp = item.Timestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                // Escape values for CSV (handle quotes and commas)
                var displayName = EscapeCsvField(item.DisplayName);
                var nodeId = EscapeCsvField(item.NodeId.ToString());
                var value = EscapeCsvField(item.Value);
                var status = item.StatusString;

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
