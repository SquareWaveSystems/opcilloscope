using Terminal.Gui;
using OpcScope.OpcUa.Models;
using OpcScope.App.Themes;
using System.Data;
using Attribute = Terminal.Gui.Attribute;

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
    private readonly Button _recordButton;
    private readonly Button _stopButton;
    private readonly Label _recordingStatus;
    private bool _isRecording;

    public event Action<MonitoredNode>? UnsubscribeRequested;
    public event Action<MonitoredNode>? TrendPlotRequested;
    public event Action? RecordRequested;
    public event Action? StopRecordingRequested;

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

    public MonitoredItemsView()
    {
        Title = " Monitored Items ";
        CanFocus = true;

        // Apply theme styling
        var theme = ThemeManager.Current;
        BorderStyle = theme.FrameLineStyle;

        // Create recording control bar
        _recordButton = new Button
        {
            Text = "Record",
            X = 0,
            Y = 0
        };
        _recordButton.Accepting += (_, _) => RecordRequested?.Invoke();

        _stopButton = new Button
        {
            Text = "Stop",
            X = Pos.Right(_recordButton) + 1,
            Y = 0,
            Enabled = false
        };
        _stopButton.Accepting += (_, _) => StopRecordingRequested?.Invoke();

        _recordingStatus = new Label
        {
            Text = "",
            X = Pos.Right(_stopButton) + 2,
            Y = 0,
            Width = Dim.Fill()
        };

        _dataTable = new DataTable();
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
        ThemeManager.ThemeChanged += OnThemeChanged;

        Add(_recordButton);
        Add(_stopButton);
        Add(_recordingStatus);
        Add(_tableView);
    }

    /// <summary>
    /// Set the recording state and update button enabled states.
    /// </summary>
    public void SetRecordingState(bool isRecording, string statusText = "")
    {
        _isRecording = isRecording;
        _recordButton.Enabled = !isRecording;
        _stopButton.Enabled = isRecording;
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

        row["Value"] = item.Value;
        row["Time"] = item.TimestampString;
        row["Status"] = item.StatusString;

        _tableView.Update();
    }

    public void RemoveItem(uint clientHandle)
    {
        if (!_rowsByHandle.TryGetValue(clientHandle, out var row))
            return;

        _dataTable.Rows.Remove(row);
        _rowsByHandle.Remove(clientHandle);

        _tableView.Update();
    }

    public void Clear()
    {
        _dataTable.Rows.Clear();
        _rowsByHandle.Clear();
        _tableView.Update();
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
            var selected = SelectedItem;
            if (selected != null)
            {
                TrendPlotRequested?.Invoke(selected);
                e.Handled = true;
            }
        }
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
