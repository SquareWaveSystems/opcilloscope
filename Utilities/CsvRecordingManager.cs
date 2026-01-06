using System.Collections.Concurrent;
using OpcScope.OpcUa.Models;

namespace OpcScope.Utilities;

/// <summary>
/// Manages CSV recording of monitored item value changes.
/// Writes data to file in real-time as values change using a background queue.
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
