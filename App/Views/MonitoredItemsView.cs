using Terminal.Gui;
using OpcScope.OpcUa.Models;
using OpcScope.App.Themes;
using System.Data;
using Attribute = Terminal.Gui.Attribute;
using ThemeManager = OpcScope.App.Themes.ThemeManager;

namespace OpcScope.App.Views;

/// <summary>
/// TableView for displaying subscribed/monitored items with real-time updates.
/// Features theme-aware styling, row coloring based on status, and CSV recording controls.
/// </summary>
public class MonitoredItemsView : FrameView
{
    // Checkbox display constants
    private const string CheckedBox = "[●]";
    private const string UncheckedBox = "[ ]";
    private const int MaxScopeSelections = 5;

    private readonly TableView _tableView;
    private readonly DataTable _dataTable;
    private readonly Dictionary<uint, DataRow> _rowsByHandle = new();


    // Scope selection
    private readonly Label _selectionFeedback;
    private int _cachedScopeSelectionCount;

    // Recording indicator (right-aligned in title bar)
    private readonly Label _recordingIndicatorLabel;

    // Empty state
    private readonly Label _emptyStateLabel;

    public event Action<MonitoredNode>? UnsubscribeRequested;
    public event Action<MonitoredNode>? WriteRequested;
    public event Action<int>? ScopeSelectionChanged;  // Fires with current selection count

    public MonitoredNode? SelectedItem
    {
        get
        {
            if (_tableView.SelectedRow >= 0 && _tableView.SelectedRow < _dataTable.Rows.Count)
            {
                var row = _dataTable.Rows[_tableView.SelectedRow];
                return row["_Item"] as MonitoredNode;
            }
            return null;
        }
    }

    /// <summary>
    /// Gets all monitored nodes currently selected for Scope display.
    /// </summary>
    public IReadOnlyList<MonitoredNode> ScopeSelectedNodes
    {
        get
        {
            var selected = new List<MonitoredNode>();
            foreach (DataRow row in _dataTable.Rows)
            {
                if (row["_Item"] is MonitoredNode node && node.IsSelectedForScope)
                {
                    selected.Add(node);
                }
            }
            return selected;
        }
    }

    /// <summary>
    /// Gets the count of items selected for Scope.
    /// </summary>
    public int ScopeSelectionCount => _cachedScopeSelectionCount;

    public MonitoredItemsView()
    {
        Title = " Monitored Items ";
        CanFocus = true;

        // Apply theme styling
        var theme = ThemeManager.Current;
        BorderStyle = theme.FrameLineStyle;

        // Selection feedback label (shows when max is reached)
        _selectionFeedback = new Label
        {
            Text = "",
            X = Pos.Center(),
            Y = 0,
            Visible = false
        };

        _dataTable = new DataTable();
        _dataTable.Columns.Add("Scope", typeof(string));  // Checkbox column for scope selection
        _dataTable.Columns.Add("Name", typeof(string));
        _dataTable.Columns.Add("Access", typeof(string));
        _dataTable.Columns.Add("Value", typeof(string));
        _dataTable.Columns.Add("Time", typeof(string));
        _dataTable.Columns.Add("Status", typeof(string));
        _dataTable.Columns.Add("_Item", typeof(MonitoredNode)); // Hidden reference

        _tableView = new TableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Table = new DataTableSource(_dataTable),
            FullRowSelect = true,
            ColorScheme = new ColorScheme
            {
                Normal = new Attribute(theme.Foreground, theme.Background),
                Focus = new Attribute(theme.ForegroundBright, theme.Background),
                HotNormal = new Attribute(theme.Accent, theme.Background),
                HotFocus = new Attribute(theme.AccentBright, theme.Background),
                Disabled = new Attribute(theme.MutedText, theme.Background)
            }
        };

        // Configure table style for cleaner look
        _tableView.Style.ShowHorizontalHeaderOverline = false;
        _tableView.Style.ShowHorizontalHeaderUnderline = true;
        _tableView.Style.ShowHorizontalBottomline = false;
        _tableView.Style.AlwaysShowHeaders = true;
        _tableView.Style.ShowVerticalCellLines = false;
        _tableView.Style.ShowVerticalHeaderLines = false;
        _tableView.Style.ExpandLastColumn = true;

        // Note: Status icons (●/▲/✕) in text provide visual status indication
        // Terminal.Gui v2 TableView doesn't support per-row coloring

        _tableView.KeyDown += HandleKeyDown;

        // Create empty state label
        _emptyStateLabel = new Label
        {
            X = Pos.Center(),
            Y = Pos.Center(),
            Text = "Select nodes to monitor",
            ColorScheme = new ColorScheme
            {
                Normal = new Attribute(theme.MutedText, theme.Background),
                Focus = new Attribute(theme.MutedText, theme.Background),
                HotNormal = new Attribute(theme.MutedText, theme.Background),
                HotFocus = new Attribute(theme.MutedText, theme.Background),
                Disabled = new Attribute(theme.MutedText, theme.Background)
            }
        };

        // Recording indicator (right-aligned in title bar area)
        _recordingIndicatorLabel = new Label
        {
            X = Pos.AnchorEnd(12),
            Y = 0,
            Text = "",
            Visible = false,
            ColorScheme = new ColorScheme
            {
                Normal = new Attribute(theme.MutedText, theme.Background),
                Focus = new Attribute(theme.MutedText, theme.Background),
                HotNormal = new Attribute(theme.MutedText, theme.Background),
                HotFocus = new Attribute(theme.MutedText, theme.Background),
                Disabled = new Attribute(theme.MutedText, theme.Background)
            }
        };

        // Subscribe to theme changes
        ThemeManager.ThemeChanged += OnThemeChanged;

        Add(_selectionFeedback);
        Add(_tableView);
        Add(_emptyStateLabel);
        Add(_recordingIndicatorLabel);

        // Initial state
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        var isEmpty = _dataTable.Rows.Count == 0;
        _emptyStateLabel.Visible = isEmpty;
        _tableView.Visible = !isEmpty;
    }

    private void OnThemeChanged(AppTheme theme)
    {
        Application.Invoke(() =>
        {
            BorderStyle = theme.EmphasizedBorderStyle;

            // Update empty state label color
            _emptyStateLabel.ColorScheme = new ColorScheme
            {
                Normal = new Attribute(theme.MutedText, theme.Background),
                Focus = new Attribute(theme.MutedText, theme.Background),
                HotNormal = new Attribute(theme.MutedText, theme.Background),
                HotFocus = new Attribute(theme.MutedText, theme.Background),
                Disabled = new Attribute(theme.MutedText, theme.Background)
            };

            // Update table view colors
            _tableView.ColorScheme = new ColorScheme
            {
                Normal = new Attribute(theme.Foreground, theme.Background),
                Focus = new Attribute(theme.ForegroundBright, theme.Background),
                HotNormal = new Attribute(theme.Accent, theme.Background),
                HotFocus = new Attribute(theme.AccentBright, theme.Background),
                Disabled = new Attribute(theme.MutedText, theme.Background)
            };

            SetNeedsLayout();
        });
    }

    public void AddItem(MonitoredNode item)
    {
        if (_rowsByHandle.ContainsKey(item.ClientHandle))
            return;

        var row = _dataTable.NewRow();
        row["Scope"] = item.IsSelectedForScope ? CheckedBox : UncheckedBox;
        row["Name"] = item.DisplayName;
        row["Access"] = item.AccessString;
        row["Value"] = item.Value;
        row["Time"] = item.TimestampString;
        row["Status"] = FormatStatusWithIcon(item);
        row["_Item"] = item;

        _dataTable.Rows.Add(row);
        _rowsByHandle[item.ClientHandle] = row;

        // Update cache if item is already selected
        if (item.IsSelectedForScope)
        {
            _cachedScopeSelectionCount++;
        }

        _tableView.Update();
        UpdateEmptyState();
    }

    public void UpdateItem(MonitoredNode item)
    {
        if (!_rowsByHandle.TryGetValue(item.ClientHandle, out var row))
            return;

        row["Access"] = item.AccessString;
        row["Scope"] = item.IsSelectedForScope ? CheckedBox : UncheckedBox;
        row["Value"] = item.Value;
        row["Time"] = item.TimestampString;
        row["Status"] = FormatStatusWithIcon(item);

        _tableView.Update();
    }

    public void RemoveItem(uint clientHandle)
    {
        if (!_rowsByHandle.TryGetValue(clientHandle, out var row))
            return;

        // Clear scope selection before removing
        if (row["_Item"] is MonitoredNode node && node.IsSelectedForScope)
        {
            node.IsSelectedForScope = false;
            _cachedScopeSelectionCount--;
            ScopeSelectionChanged?.Invoke(ScopeSelectionCount);
        }

        _dataTable.Rows.Remove(row);
        _rowsByHandle.Remove(clientHandle);

        _tableView.Update();
        UpdateEmptyState();
    }

    public void Clear()
    {
        // Clear all scope selections before clearing table
        foreach (DataRow row in _dataTable.Rows)
        {
            if (row["_Item"] is MonitoredNode node)
            {
                node.IsSelectedForScope = false;
            }
        }

        _cachedScopeSelectionCount = 0;
        _dataTable.Rows.Clear();
        _rowsByHandle.Clear();
        _tableView.Update();
        UpdateEmptyState();
    }

    private string FormatStatusWithIcon(MonitoredNode item)
    {
        var theme = ThemeManager.Current;

        if (item.IsGood)
            return $"{theme.StatusGoodIcon} Good";
        else if (item.IsUncertain)
            return $"{theme.StatusUncertainIcon} Uncertain";
        else if (item.IsBad)
            return $"{theme.StatusBadIcon} Bad";

        return item.StatusString;
    }

    private void HandleKeyDown(object? _, Key e)
    {
        if (e == Key.Delete || e == Key.Backspace)
        {
            var selected = SelectedItem;
            if (selected != null)
            {
                UnsubscribeRequested?.Invoke(selected);
                e.Handled = true;
            }
        }
        else if (e == Key.Space)
        {
            // Toggle scope selection for the highlighted item
            ToggleScopeSelection();
            e.Handled = true;
        }
        else if (e == Key.W)
        {
            var selected = SelectedItem;
            if (selected != null)
            {
                WriteRequested?.Invoke(selected);
                e.Handled = true;
            }
        }
    }

    /// <summary>
    /// Toggle scope selection for the currently highlighted item.
    /// </summary>
    private void ToggleScopeSelection()
    {
        var selected = SelectedItem;
        if (selected == null) return;

        if (selected.IsSelectedForScope)
        {
            // Deselect
            selected.IsSelectedForScope = false;
            _cachedScopeSelectionCount--;
            _selectionFeedback.Visible = false;
        }
        else
        {
            // Check if at max selections
            if (_cachedScopeSelectionCount >= MaxScopeSelections)
            {
                _selectionFeedback.Text = $"Max {MaxScopeSelections} items for Scope";
                _selectionFeedback.Visible = true;
                return;
            }

            // Select
            selected.IsSelectedForScope = true;
            _cachedScopeSelectionCount++;
            _selectionFeedback.Visible = false;
        }

        // Update the row display
        if (_rowsByHandle.TryGetValue(selected.ClientHandle, out var row))
        {
            row["Scope"] = selected.IsSelectedForScope ? CheckedBox : UncheckedBox;
            _tableView.Update();
        }

        ScopeSelectionChanged?.Invoke(ScopeSelectionCount);
    }

    /// <summary>
    /// Updates the recording status indicator in the title bar area.
    /// </summary>
    /// <param name="text">The text to display (e.g., "◉ REC", "◉ 01:23", or empty string).</param>
    /// <param name="isRecording">Whether recording is active (affects color).</param>
    public void UpdateRecordingStatus(string text, bool isRecording)
    {
        var theme = ThemeManager.Current;
        var color = isRecording ? theme.Accent : theme.MutedText;

        _recordingIndicatorLabel.Text = text;
        _recordingIndicatorLabel.Visible = !string.IsNullOrEmpty(text);
        _recordingIndicatorLabel.ColorScheme = new ColorScheme
        {
            Normal = new Attribute(color, theme.Background),
            Focus = new Attribute(color, theme.Background),
            HotNormal = new Attribute(color, theme.Background),
            HotFocus = new Attribute(color, theme.Background),
            Disabled = new Attribute(color, theme.Background)
        };

        SetNeedsLayout();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }
}
