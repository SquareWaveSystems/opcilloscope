using Terminal.Gui;
using OpcScope.Utilities;
using OpcScope.App.Themes;
using System.Collections.ObjectModel;
using Attribute = Terminal.Gui.Attribute;
using ThemeManager = OpcScope.App.Themes.ThemeManager;

namespace OpcScope.App.Views;

/// <summary>
/// Scrolling event log panel with color-coded severity.
/// Uses Terminal.Gui v2 RowRender event for per-row coloring.
/// </summary>
public class LogView : FrameView
{
    private readonly ListView _listView;
    private readonly Button _copyButton;
    private readonly ObservableCollection<string> _displayedEntries = new();
    private readonly List<LogEntry> _entries = new();
    private Logger? _logger;

    public LogView()
    {
        Title = " Log ";

        // Apply theme styling
        var theme = ThemeManager.Current;
        BorderStyle = theme.FrameLineStyle;

        // Copy button in top-right corner of the frame
        _copyButton = new Button
        {
            Text = "Copy",
            X = Pos.AnchorEnd(8),
            Y = 0,
            Height = 1,
            ShadowStyle = ShadowStyle.None,
            ColorScheme = theme.ButtonColorScheme
        };
        _copyButton.Accepting += OnCopyClicked;

        _listView = new ListView
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        _listView.SetSource(_displayedEntries);

        // Add row coloring based on log level
        _listView.RowRender += OnRowRender;

        // Subscribe to theme changes to update colors
        ThemeManager.ThemeChanged += OnThemeChanged;

        Add(_copyButton);
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

    private void OnRowRender(object? sender, ListViewRowEventArgs e)
    {
        if (e.Row < 0 || e.Row >= _entries.Count)
            return;

        var entry = _entries[e.Row];
        var theme = ThemeManager.Current;

        e.RowAttribute = entry.Level switch
        {
            LogLevel.Error => theme.ErrorAttr,
            LogLevel.Warning => theme.WarningAttr,
            _ => theme.NormalAttr
        };
    }

    private void OnThemeChanged(AppTheme theme)
    {
        Application.Invoke(() =>
        {
            BorderStyle = theme.FrameLineStyle;
            _copyButton.ColorScheme = theme.ButtonColorScheme;
            SetNeedsLayout();
        });
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

    private void OnCopyClicked(object? sender, CommandEventArgs e)
    {
        if (_displayedEntries.Count == 0)
            return;

        var logText = string.Join(Environment.NewLine, _displayedEntries);
        Clipboard.TrySetClipboardData(logText);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _copyButton.Accepting -= OnCopyClicked;
            _listView.RowRender -= OnRowRender;
            ThemeManager.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }
}
