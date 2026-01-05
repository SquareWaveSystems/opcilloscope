using Terminal.Gui;
using OpcScope.OpcUa.Models;
using System.Data;

namespace OpcScope.App.Views;

/// <summary>
/// TableView for displaying subscribed/monitored items with real-time updates.
/// </summary>
public class MonitoredItemsView : FrameView
{
    private readonly TableView _tableView;
    private readonly DataTable _dataTable;
    private readonly Dictionary<uint, DataRow> _rowsByHandle = new();

    public event Action<MonitoredNode>? UnsubscribeRequested;
    public event Action<MonitoredNode>? TrendPlotRequested;

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
        Title = "Monitored Items";
        CanFocus = true;

        _dataTable = new DataTable();
        _dataTable.Columns.Add("Name", typeof(string));
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
            FullRowSelect = true
        };

        // Configure columns
        _tableView.Style.ShowHorizontalHeaderOverline = false;
        _tableView.Style.ShowHorizontalHeaderUnderline = true;
        _tableView.Style.AlwaysShowHeaders = true;

        _tableView.KeyDown += HandleKeyDown;

        Add(_tableView);
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

    private void HandleKeyDown(object? sender, Key e)
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
}
