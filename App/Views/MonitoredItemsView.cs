using Terminal.Gui;
using OpcScope.OpcUa.Models;
using OpcScope.App.Themes;
using System.Data;
using Attribute = Terminal.Gui.Attribute;
using AppThemeManager = OpcScope.App.Themes.ThemeManager;

namespace OpcScope.App.Views;

/// <summary>
/// TableView for displaying subscribed/monitored items with real-time updates.
/// Features theme-aware styling, row coloring based on status, and CSV recording controls.
/// </summary>
public class MonitoredItemsView : FrameView
{
    private readonly TableView _tableView;
    private readonly DataTable _dataTable;
    private readonly Dictionary<uint, DataRow> _rowsByHandle = new();

    // Recording controls
    private readonly Button _toggleRecordButton;
    private readonly Label _recordingStatus;
    private bool _isRecording;

    // Scope selection
    private const int MaxScopeSelections = 5;
    private readonly Label _selectionFeedback;

    // Unicode symbols for record/stop and checkbox
    private const string RecordSymbol = "●";  // Red circle for record
    private const string StopSymbol = "■";    // Square for stop
    private const string CheckedBox = "[x]";
    private const string UncheckedBox = "[ ]";

    public event Action<MonitoredNode>? UnsubscribeRequested;
    public event Action? RecordRequested;
    public event Action? StopRecordingRequested;
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
    public int ScopeSelectionCount => ScopeSelectedNodes.Count;

    public MonitoredItemsView()
    {
        Title = " Monitored Items ";
        CanFocus = true;

        // Apply theme styling
        var theme = AppThemeManager.Current;
        BorderStyle = theme.FrameLineStyle;

        // Create recording toggle button (right-aligned)
        _toggleRecordButton = new Button
        {
            Text = $" {RecordSymbol} ",
            X = Pos.AnchorEnd(5),  // Right-aligned
            Y = 0
        };
        _toggleRecordButton.Accepting += (_, _) =>
        {
            if (_isRecording)
                StopRecordingRequested?.Invoke();
            else
                RecordRequested?.Invoke();
        };

        _recordingStatus = new Label
        {
            Text = "",
            X = 0,
            Y = 0,
            Width = Dim.Fill()! - 6  // Leave room for the button
        };

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
        _dataTable.Columns.Add("Value", typeof(string));
        _dataTable.Columns.Add("Time", typeof(string));
        _dataTable.Columns.Add("Status", typeof(string));
        _dataTable.Columns.Add("_Item", typeof(MonitoredNode)); // Hidden reference

        _tableView = new TableView
        {
            X = 0,
            Y = 1, // Below the button bar
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Table = new DataTableSource(_dataTable),
            FullRowSelect = true
        };

        // Configure table style for cleaner look
        _tableView.Style.ShowHorizontalHeaderOverline = false;
        _tableView.Style.ShowHorizontalHeaderUnderline = true;
        _tableView.Style.ShowHorizontalBottomline = false;
        _tableView.Style.AlwaysShowHeaders = true;
        _tableView.Style.ShowVerticalCellLines = false;
        _tableView.Style.ShowVerticalHeaderLines = false;
        _tableView.Style.ExpandLastColumn = true;

        _tableView.KeyDown += HandleKeyDown;

        // Subscribe to theme changes
        AppThemeManager.ThemeChanged += OnThemeChanged;

        Add(_recordingStatus);
        Add(_selectionFeedback);
        Add(_toggleRecordButton);
        Add(_tableView);
    }

    /// <summary>
    /// Set the recording state and update toggle button appearance.
    /// </summary>
    public void SetRecordingState(bool isRecording, string statusText = "")
    {
        _isRecording = isRecording;
        _toggleRecordButton.Text = isRecording ? $" {StopSymbol} " : $" {RecordSymbol} ";
        _recordingStatus.Text = statusText;
    }

    /// <summary>
    /// Update the recording status text (e.g., record count, duration).
    /// </summary>
    public void UpdateRecordingStatus(string statusText)
    {
        _recordingStatus.Text = statusText;
    }

    private void OnThemeChanged(RetroTheme theme)
    {
        Application.Invoke(() =>
        {
            BorderStyle = theme.FrameLineStyle;
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
        row["Value"] = item.Value;
        row["Time"] = item.TimestampString;
        row["Status"] = item.StatusString;
        row["_Item"] = item;

        _dataTable.Rows.Add(row);
        _rowsByHandle[item.ClientHandle] = row;

        _tableView.Update();
    }

    public void UpdateItem(MonitoredNode item)
    {
        if (!_rowsByHandle.TryGetValue(item.ClientHandle, out var row))
            return;

        row["Scope"] = item.IsSelectedForScope ? CheckedBox : UncheckedBox;
        row["Value"] = item.Value;
        row["Time"] = item.TimestampString;
        row["Status"] = item.StatusString;

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
            ScopeSelectionChanged?.Invoke(ScopeSelectionCount);
        }

        _dataTable.Rows.Remove(row);
        _rowsByHandle.Remove(clientHandle);

        _tableView.Update();
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

        _dataTable.Rows.Clear();
        _rowsByHandle.Clear();
        _tableView.Update();
        ScopeSelectionChanged?.Invoke(0);
    }

    /// <summary>
    /// Toggles the scope selection for the currently highlighted item.
    /// </summary>
    public void ToggleScopeSelection()
    {
        var selected = SelectedItem;
        if (selected == null)
            return;

        if (selected.IsSelectedForScope)
        {
            // Deselect
            selected.IsSelectedForScope = false;
            UpdateScopeCheckbox(selected);
            HideSelectionFeedback();
            ScopeSelectionChanged?.Invoke(ScopeSelectionCount);
        }
        else
        {
            // Check if we've reached the limit
            if (ScopeSelectionCount >= MaxScopeSelections)
            {
                ShowSelectionFeedback($"Max {MaxScopeSelections} items for Scope");
                return;
            }

            // Select
            selected.IsSelectedForScope = true;
            UpdateScopeCheckbox(selected);
            HideSelectionFeedback();
            ScopeSelectionChanged?.Invoke(ScopeSelectionCount);
        }
    }

    private void UpdateScopeCheckbox(MonitoredNode item)
    {
        if (_rowsByHandle.TryGetValue(item.ClientHandle, out var row))
        {
            row["Scope"] = item.IsSelectedForScope ? CheckedBox : UncheckedBox;
            _tableView.Update();
        }
    }

    private void ShowSelectionFeedback(string message)
    {
        _selectionFeedback.Text = message;
        _selectionFeedback.Visible = true;
        SetNeedsLayout();

        // Auto-hide after 2 seconds
        Application.AddTimeout(TimeSpan.FromSeconds(2), () =>
        {
            Application.Invoke(HideSelectionFeedback);
            return false;  // Don't repeat
        });
    }

    private void HideSelectionFeedback()
    {
        _selectionFeedback.Visible = false;
        _selectionFeedback.Text = "";
        SetNeedsLayout();
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
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AppThemeManager.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }
}
