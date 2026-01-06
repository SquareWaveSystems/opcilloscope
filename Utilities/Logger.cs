namespace OpcScope.Utilities;

/// <summary>
/// Simple in-app logger with severity levels.
/// </summary>
public enum LogLevel
{
    Info,
    Warning,
    Error
}

public class LogEntry
{
    public DateTime Timestamp { get; init; }
    public LogLevel Level { get; init; }
    public string Message { get; init; } = string.Empty;

    public override string ToString()
    {
        var levelStr = Level switch
        {
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            _ => "INFO"
        };
        return $"[{Timestamp:HH:mm:ss}] [{levelStr}] {Message}";
    }
}

public class Logger
{
    private readonly List<LogEntry> _entries = new();
    private readonly object _lock = new();
    private const int MaxEntries = 1000;

    public event Action<LogEntry>? LogAdded;

    public void Info(string message) => Log(LogLevel.Info, message);
    public void Warning(string message) => Log(LogLevel.Warning, message);
    public void Error(string message) => Log(LogLevel.Error, message);

    public void Log(LogLevel level, string message)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message
        };

        lock (_lock)
        {
            _entries.Add(entry);
            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveAt(0);
            }
        }

        LogAdded?.Invoke(entry);
    }

    public IReadOnlyList<LogEntry> GetEntries()
    {
        lock (_lock)
        {
            return _entries.ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }
}
