using System.Globalization;
using System.Threading.Channels;
using Opcilloscope.OpcUa.Models;

namespace Opcilloscope.Utilities;

public readonly record struct RecordingStopResult(
    bool Completed,
    long RecordCount,
    long DroppedRecordCount,
    long FailedRecordCount,
    string? ErrorMessage)
{
    public bool HasDataLoss => DroppedRecordCount > 0
        || FailedRecordCount > 0
        || !string.IsNullOrEmpty(ErrorMessage);
}

/// <summary>
/// Manages CSV recording of monitored variable value changes.
/// Writes data to file in real-time as values change using a background queue.
/// Output is culture-invariant: timestamps are ISO 8601 UTC with a 'Z'
/// designator (Gregorian calendar, '.' decimal / ':' time separators
/// regardless of locale) and values are the full-precision raw representation
/// ('.' decimal separator, arrays as semicolon-joined elements) rather than
/// the truncated UI display string.
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
            baseName = ConnectionIdentifier.SanitizeUrlForFilename(connectionUrl);
        }
        else
        {
            baseName = "recording";
        }

        return $"{baseName}_{variableCount}vars_{timestamp}{RecordingFileExtension}";
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

    private sealed class RecordingSession
    {
        public required string FilePath { get; init; }
        public required TextWriter Writer { get; init; }
        public required Channel<RecordSnapshot> Queue { get; init; }
        public required DateTime StartTime { get; init; }
        public Task WriteTask { get; set; } = Task.CompletedTask;
        public long RecordCount;
        public long DroppedRecordCount;
        public long FailedRecordCount;
        public string? ErrorMessage;
    }

    private readonly Logger _logger;
    private readonly object _lock = new();
    private readonly int _queueCapacity;
    private readonly Func<string, TextWriter> _writerFactory;
    private RecordingSession? _session;
    private RecordingSession? _stoppingSession;
    private RecordingSession? _lastSession;

    private const int DefaultQueueCapacity = 10_000;

    public event Action<bool>? RecordingStateChanged;

    public bool IsRecording
    {
        get
        {
            lock (_lock)
            {
                return _session is not null;
            }
        }
    }

    public bool IsStopping
    {
        get
        {
            lock (_lock)
            {
                return _stoppingSession is not null;
            }
        }
    }

    public string? FilePath
    {
        get
        {
            lock (_lock)
            {
                return (_session ?? _lastSession)?.FilePath;
            }
        }
    }

    public long RecordCount
    {
        get
        {
            RecordingSession? session;
            lock (_lock)
            {
                session = _session ?? _lastSession;
            }
            return session is null ? 0 : Interlocked.Read(ref session.RecordCount);
        }
    }

    public long DroppedRecordCount
    {
        get
        {
            RecordingSession? session;
            lock (_lock)
            {
                session = _session ?? _lastSession;
            }
            return session is null ? 0 : Interlocked.Read(ref session.DroppedRecordCount);
        }
    }

    public long FailedRecordCount
    {
        get
        {
            RecordingSession? session;
            lock (_lock)
            {
                session = _session ?? _stoppingSession ?? _lastSession;
            }
            return session is null ? 0 : Interlocked.Read(ref session.FailedRecordCount);
        }
    }

    public string? LastError
    {
        get
        {
            RecordingSession? session;
            lock (_lock)
            {
                session = _session ?? _stoppingSession ?? _lastSession;
            }
            return session is null ? null : Volatile.Read(ref session.ErrorMessage);
        }
    }

    public TimeSpan RecordingDuration
    {
        get
        {
            lock (_lock)
            {
                return _session is null ? TimeSpan.Zero : DateTime.Now - _session.StartTime;
            }
        }
    }

    public CsvRecordingManager(Logger logger)
        : this(logger, DefaultQueueCapacity, path => new StreamWriter(path, append: false))
    {
    }

    internal CsvRecordingManager(
        Logger logger,
        int queueCapacity,
        Func<string, TextWriter> writerFactory)
    {
        if (queueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(queueCapacity));
        }

        _logger = logger;
        _queueCapacity = queueCapacity;
        _writerFactory = writerFactory;
    }

    /// <summary>
    /// Start recording to the specified file path.
    /// </summary>
    public bool StartRecording(string filePath)
    {
        lock (_lock)
        {
            if (_session is not null || _stoppingSession is not null)
            {
                _logger.Warning("Recording is already in progress");
                return false;
            }

            TextWriter? writer = null;
            try
            {
                writer = _writerFactory(filePath);

                // Write CSV header
                writer.WriteLine("Timestamp,DisplayName,NodeId,Value,Status");
                writer.Flush();

                var queue = Channel.CreateBounded<RecordSnapshot>(new BoundedChannelOptions(_queueCapacity)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait
                });
                var session = new RecordingSession
                {
                    FilePath = filePath,
                    Writer = writer,
                    Queue = queue,
                    StartTime = DateTime.Now
                };

                _session = session;
                session.WriteTask = Task.Run(() => WriteQueuedRecordsAsync(session));

                _logger.Info($"Started recording to {filePath}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to start recording: {ex.Message}");
                try
                {
                    writer?.Dispose();
                }
                catch (Exception disposeException)
                {
                    _logger.Error($"Failed to close recording file after start error: {disposeException.Message}");
                }
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
        => StopRecordingAsync(System.Threading.Timeout.InfiniteTimeSpan).GetAwaiter().GetResult();

    /// <summary>
    /// Stops accepting records and asynchronously drains every accepted record.
    /// A timeout is reported explicitly; the session remains tracked and blocks a
    /// new recording until its writer actually closes.
    /// </summary>
    public async Task<RecordingStopResult> StopRecordingAsync(TimeSpan? timeout = null)
    {
        var wait = timeout ?? TimeSpan.FromSeconds(10);
        if (wait < TimeSpan.Zero && wait != System.Threading.Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        RecordingSession? session;
        var beganStopping = false;

        lock (_lock)
        {
            session = _session ?? _stoppingSession;
            if (session is null)
            {
                return CreateStopResult(_lastSession, completed: true);
            }

            if (_session is not null)
            {
                _session = null;
                _stoppingSession = session;
                _lastSession = session;
                beganStopping = true;
            }
        }

        if (beganStopping)
        {
            // Completing this session's private channel prevents a late callback
            // from crossing into the next recording and lets the reader drain all
            // accepted records before it closes its writer.
            session.Queue.Writer.TryComplete();
            RecordingStateChanged?.Invoke(false);
        }

        bool completed;
        if (wait == System.Threading.Timeout.InfiniteTimeSpan)
        {
            await session.WriteTask.ConfigureAwait(false);
            completed = true;
        }
        else
        {
            completed = ReferenceEquals(
                await Task.WhenAny(session.WriteTask, Task.Delay(wait)).ConfigureAwait(false),
                session.WriteTask);
            if (completed)
            {
                await session.WriteTask.ConfigureAwait(false);
            }
        }

        var result = CreateStopResult(session, completed);
        if (!completed)
        {
            _logger.Error(
                $"Recording writer did not finish within {wait}. The file is still open; " +
                "new recordings remain disabled until it closes.");
            return result;
        }

        var duration = DateTime.Now - session.StartTime;
        var lossSuffix = result.HasDataLoss
            ? $", {result.DroppedRecordCount} queue drops, {result.FailedRecordCount} failed writes"
            : string.Empty;
        _logger.Info(
            $"Stopped recording. {result.RecordCount} records written to {session.FilePath}" +
            $" (duration: {duration:hh\\:mm\\:ss}{lossSuffix})");
        return result;
    }

    private static RecordingStopResult CreateStopResult(RecordingSession? session, bool completed) =>
        session is null
            ? new RecordingStopResult(completed, 0, 0, 0, null)
            : new RecordingStopResult(
                completed,
                Interlocked.Read(ref session.RecordCount),
                Interlocked.Read(ref session.DroppedRecordCount),
                Interlocked.Read(ref session.FailedRecordCount),
                Volatile.Read(ref session.ErrorMessage));

    /// <summary>
    /// Record a value change. Called from the subscription's ValueChanged event.
    /// Queues the value for asynchronous writing to avoid blocking the notification thread.
    /// </summary>
    public void RecordValue(MonitoredNode item)
    {
        RecordingSession? session;
        lock (_lock)
        {
            session = _session;
        }

        if (session is null) return;

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

        long dropped = 0;
        lock (_lock)
        {
            // The snapshot was built outside the lock. If Stop/Start happened
            // meanwhile, discard it instead of contaminating the new file.
            if (!ReferenceEquals(_session, session))
            {
                return;
            }

            if (!session.Queue.Writer.TryWrite(snapshot))
            {
                dropped = Interlocked.Increment(ref session.DroppedRecordCount);
            }
        }

        if (dropped == 1)
        {
            _logger.Warning(
                $"CSV recording queue reached its {_queueCapacity:N0}-record capacity; new records will be dropped until storage catches up");
        }
    }

    /// <summary>
    /// Background task that processes the queue and writes records to the file.
    /// </summary>
    private async Task WriteQueuedRecordsAsync(RecordingSession session)
    {
        try
        {
            await foreach (var item in session.Queue.Reader.ReadAllAsync())
            {
                WriteRecord(session, item);
            }
        }
        catch (Exception ex)
        {
            RegisterSessionError(session, "Background writer failed", ex);
        }
        finally
        {
            try
            {
                session.Writer.Flush();
            }
            catch (Exception ex)
            {
                RegisterSessionError(session, "Final recording flush failed", ex);
            }

            try
            {
                session.Writer.Dispose();
            }
            catch (Exception ex)
            {
                RegisterSessionError(session, "Recording file close failed", ex);
            }

            lock (_lock)
            {
                if (ReferenceEquals(_stoppingSession, session))
                {
                    _stoppingSession = null;
                }
            }
        }
    }

    /// <summary>
    /// Write a single record to the CSV file.
    /// </summary>
    private void WriteRecord(RecordingSession session, RecordSnapshot item)
    {
        try
        {
            // Use ISO 8601 timestamp format with milliseconds for precision.
            // InvariantCulture is required: the ':' custom-format specifier
            // is replaced by the culture's time separator (fi-FI uses '.')
            // and the culture's default calendar applies (th-TH uses the
            // Buddhist calendar), which would break the ISO 8601 contract.
            // All timestamps are normalized to UTC with an explicit 'Z'
            // designator: OPC UA source timestamps are UTC while the
            // no-timestamp fallback used to be local time, so a single file
            // could silently mix timezones with no way to tell them apart.
            var ts = item.Timestamp ?? DateTime.UtcNow;
            if (ts.Kind == DateTimeKind.Local)
            {
                ts = ts.ToUniversalTime();
            }
            // Kind=Unspecified is treated as UTC (the OPC UA convention)
            // rather than local, so the recorded instant never shifts.
            var timestamp = ts.ToString("yyyy-MM-ddTHH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

            // Escape values for CSV (RFC 4180 quoting plus formula
            // injection neutralization for server-supplied fields).
            // The timestamp is generated locally in a fixed format, so it
            // needs no escaping; the header line is a constant.
            var displayName = EscapeCsvField(item.DisplayName);
            var nodeId = EscapeCsvField(item.NodeId);
            var value = EscapeCsvField(item.Value);
            var status = EscapeCsvField(item.Status);

            session.Writer.WriteLine($"{timestamp},{displayName},{nodeId},{value},{status}");

            // Flush periodically (every 10 records) for durability without too much I/O
            var count = Interlocked.Increment(ref session.RecordCount);
            if (count % 10 == 0)
            {
                session.Writer.Flush();
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref session.FailedRecordCount);
            RegisterSessionError(session, "Writing a recording row failed", ex);
        }
    }

    private void RegisterSessionError(RecordingSession session, string context, Exception exception)
    {
        var message = $"{context}: {exception.Message}";
        if (Interlocked.CompareExchange(ref session.ErrorMessage, message, null) is null)
        {
            _logger.Error(message);
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
        var result = StopRecordingAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        if (!result.Completed)
        {
            _logger.Error("Recording shutdown is incomplete; the output file may be truncated.");
        }
        else if (result.HasDataLoss)
        {
            _logger.Error(
                "Recording closed with data loss: " +
                $"{result.DroppedRecordCount} dropped, {result.FailedRecordCount} failed" +
                (string.IsNullOrEmpty(result.ErrorMessage)
                    ? "."
                    : $". {result.ErrorMessage}"));
        }
    }
}
