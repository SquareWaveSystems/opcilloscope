using Terminal.Gui;
using OpcScope.Utilities;
using OpcScope.App.Themes;
using System.Collections.ObjectModel;
using Attribute = Terminal.Gui.Attribute;
using AppThemeManager = OpcScope.App.Themes.ThemeManager;

namespace OpcScope.App.Views;

/// <summary>
/// Scrolling event log panel with color-coded severity.
/// Uses Terminal.Gui v2 ColorGetter for per-row coloring.
/// </summary>
public class LogView : FrameView
{
    private readonly ListView _listView;
    private readonly ObservableCollection<string> _displayedEntries = new();
    private readonly List<LogEntry> _entries = new();
    private Logger? _logger;

    public LogView()
    {
        Title = " Log ";

        // Apply theme styling
        var theme = AppThemeManager.Current;
        BorderStyle = theme.FrameLineStyle;

        _listView = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        _listView.SetSource(_displayedEntries);

        Add(_listView);
    }

    public void Initialize(Logger logger)
    {
        _logger = logger;
        _logger.LogAdded += OnLogAdded;

        // Load existing entries
        foreach (var entry in logger.GetEntries())
        {
            _entries.Add(entry);
            _displayedEntries.Add(FormatEntry(entry));
        }
    }

    private void OnLogAdded(LogEntry entry)
    {
        UiThread.Run(() =>
        {
            _entries.Add(entry);
            _displayedEntries.Add(FormatEntry(entry));

            // Keep max 500 entries displayed
            while (_entries.Count > 500)
            {
                _entries.RemoveAt(0);
                _displayedEntries.RemoveAt(0);
            }

            // Auto-scroll to bottom
            if (_displayedEntries.Count > 0)
            {
                _listView.SelectedItem = _displayedEntries.Count - 1;
                _listView.TopItem = Math.Max(0, _displayedEntries.Count - _listView.Frame.Height);
            }
        });
    }

    private static string FormatEntry(LogEntry entry)
    {
        var levelStr = entry.Level switch
        {
            LogLevel.Warning => "WARN ",
            LogLevel.Error => "ERROR",
            _ => "INFO "
        };
        return $"[{entry.Timestamp:HH:mm:ss}] {levelStr} {entry.Message}";
    }

    public void Clear()
    {
        _entries.Clear();
        _displayedEntries.Clear();
        _logger?.Clear();
    }
}
