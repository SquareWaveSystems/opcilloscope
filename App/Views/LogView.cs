using Terminal.Gui;
using OpcScope.Utilities;

namespace OpcScope.App.Views;

/// <summary>
/// Scrolling event log panel with color-coded severity.
/// </summary>
public class LogView : FrameView
{
    private readonly ListView _listView;
    private readonly List<LogEntry> _displayedEntries = new();
    private Logger? _logger;

    public LogView()
    {
        Title = "Log";

        _listView = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Source = new LogListDataSource(_displayedEntries)
        };

        Add(_listView);
    }

    public void Initialize(Logger logger)
    {
        _logger = logger;
        _logger.LogAdded += OnLogAdded;

        // Load existing entries
        foreach (var entry in logger.GetEntries())
        {
            _displayedEntries.Add(entry);
        }
        RefreshList();
    }

    private void OnLogAdded(LogEntry entry)
    {
        UiThread.Run(() =>
        {
            _displayedEntries.Add(entry);

            // Keep max 500 entries displayed
            while (_displayedEntries.Count > 500)
            {
                _displayedEntries.RemoveAt(0);
            }

            RefreshList();

            // Auto-scroll to bottom
            if (_displayedEntries.Count > 0)
            {
                _listView.SelectedItem = _displayedEntries.Count - 1;
                _listView.TopItem = Math.Max(0, _displayedEntries.Count - _listView.Frame.Height);
            }
        });
    }

    private void RefreshList()
    {
        _listView.SetSource(_displayedEntries);
        _listView.SetNeedsDisplay();
    }

    public void Clear()
    {
        _displayedEntries.Clear();
        _logger?.Clear();
        RefreshList();
    }
}

/// <summary>
/// Custom data source for log entries with color coding.
/// </summary>
public class LogListDataSource : IListDataSource
{
    private readonly List<LogEntry> _entries;

    public LogListDataSource(List<LogEntry> entries)
    {
        _entries = entries;
    }

    public int Count => _entries.Count;
    public int Length => _entries.Count;

    public bool IsMarked(int item) => false;
    public void SetMark(int item, bool value) { }

    public void Render(ListView container, ConsoleDriver driver, bool selected, int item, int col, int line, int width, int start = 0)
    {
        if (item < 0 || item >= _entries.Count)
            return;

        var entry = _entries[item];
        var text = entry.ToString();

        // Truncate if too long
        if (text.Length > width)
            text = text[..width];
        else if (text.Length < width)
            text = text.PadRight(width);

        // Set color based on log level
        var attr = entry.Level switch
        {
            LogLevel.Error => new Attribute(Color.Red, Color.Black),
            LogLevel.Warning => new Attribute(Color.Yellow, Color.Black),
            _ => new Attribute(Color.White, Color.Black)
        };

        if (selected)
        {
            attr = new Attribute(Color.Black, entry.Level switch
            {
                LogLevel.Error => Color.Red,
                LogLevel.Warning => Color.Yellow,
                _ => Color.White
            });
        }

        driver.SetAttribute(attr);
        driver.AddStr(text);
    }

    public string ToList()
    {
        return string.Join("\n", _entries.Select(e => e.ToString()));
    }

    public IList ToList(int start, int count)
    {
        return _entries.Skip(start).Take(count).ToList();
    }
}
