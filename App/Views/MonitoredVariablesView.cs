using Terminal.Gui;
using Opcilloscope.OpcUa.Models;
using Opcilloscope.App.Themes;
using System.Collections.Concurrent;
using System.Data;
using Attribute = Terminal.Gui.Attribute;
using ThemeManager = Opcilloscope.App.Themes.ThemeManager;

namespace Opcilloscope.App.Views;

/// <summary>
/// TableView for displaying subscribed/monitored variables with real-time updates.
/// Features theme-aware styling, row coloring based on status, and CSV recording controls.
/// Uses batched updates to avoid excessive redraws with high-frequency value changes.
/// </summary>
public class MonitoredVariablesView : FrameView
{
    // Checkbox display constants
    private const string CheckedBox = "[●]";
    private const string UncheckedBox = "[ ]";
    private const int MaxScopeSelections = 5;

    // Update batching for performance - collect updates and apply at intervals.
    // 50ms provides a good balance: fast enough for responsive UI (~20 FPS equivalent),
    // slow enough to batch multiple updates when connected to high-frequency remote servers.
    // This reduces table redraws from potentially 100+/sec to max 20/sec.
    private const int UpdateBatchIntervalMs = 50;
    private readonly ConcurrentDictionary<uint, MonitoredNode> _pendingUpdates = new();
    private object? _updateTimer;
    private bool _updateTimerRunning;
    private readonly object _timerLock = new();

    private readonly TableView _tableView;
    private readonly DataTable _dataTable;
    private readonly Dictionary<uint, DataRow> _rowsByHandle = new();


    // Scope selection
    private readonly Label _selectionFeedback;
    private int _cachedScopeSelectionCount;

    // Recording indicator (right-aligned in title bar)
    private readonly Label _recordingIndicatorLabel;

    // Recording toggle button
    private readonly Button _recordButton;
    private bool _isRecording;

    // Empty state
    private readonly Label _emptyStateLabel;

    public event Action<MonitoredNode>? UnsubscribeRequested;
    public event Action? RecordToggleRequested;
    public event Action<MonitoredNode>? WriteRequested;
    public event Action? ScopeRequested;
    public event Action<int>? ScopeSelectionChanged;  // Fires with current selection count

    public MonitoredNode? SelectedVariable
    {
        get
        {
            if (_tableView.SelectedRow >= 0 && _tableView.SelectedRow < _dataTable.Rows.Count)
            {
                var row = _dataTable.Rows[_tableView.SelectedRow];
                return row["_VariableRef"] as MonitoredNode;
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
                if (row["_VariableRef"] is MonitoredNode node && node.IsSelectedForScope)
                {
                    selected.Add(node);
                }
            }
            return selected;
        }
    }

    /// <summary>
    /// Gets the count of variables selected for Scope.
    /// </summary>
    public int ScopeSelectionCount => _cachedScopeSelectionCount;

    public MonitoredVariablesView()
    {
        Title = " Monitored Variables ";
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
        _dataTable.Columns.Add("Sel", typeof(string));  // Selection for Scope/Recording (◉ = selected)
        _dataTable.Columns.Add("Name", typeof(string));
        _dataTable.Columns.Add("NodeId", typeof(string));
        _dataTable.Columns.Add("Access", typeof(string));
        _dataTable.Columns.Add("Time", typeof(string));
        _dataTable.Columns.Add("Status", typeof(string));
        _dataTable.Columns.Add("Value", typeof(string));  // Far right - width varies
        _dataTable.Columns.Add("_VariableRef", typeof(MonitoredNode)); // Hidden reference

        _tableView = new TableView
        {
            X = 0,
            Y = 1,
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

        // Hide the internal _VariableRef column from display
        var variableRefColumnStyle = _tableView.Style.GetOrCreateColumnStyle(_dataTable.Columns.IndexOf("_VariableRef"));
        variableRefColumnStyle.Visible = false;

        // Note: Status icons (●/▲/✕) in text provide visual status indication
        // Terminal.Gui v2 TableView doesn't support per-row coloring

        _tableView.KeyDown += HandleKeyDown;
        _tableView.MouseClick += HandleMouseClick;

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

        // Recording indicator (left of record button in title bar area)
        _recordingIndicatorLabel = new Label
        {
            X = Pos.AnchorEnd(19),
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

        // Recording toggle button (right-aligned, matching LogView Copy button)
        _recordButton = new Button
        {
            Text = "● REC",
            X = Pos.AnchorEnd(10),
            Y = 0,
            Height = 1,
            ShadowStyle = ShadowStyle.None,
            ColorScheme = theme.ButtonColorScheme
        };
        _recordButton.Accepting += OnRecordButtonClicked;

        // Subscribe to theme changes
        ThemeManager.ThemeChanged += OnThemeChanged;

        Add(_selectionFeedback);
        Add(_tableView);
        Add(_emptyStateLabel);
        Add(_recordingIndicatorLabel);
        Add(_recordButton);

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

            // Update record button colors
            _recordButton.ColorScheme = theme.ButtonColorScheme;

            SetNeedsLayout();
        });
    }

    public void AddVariable(MonitoredNode variable)
    {
        if (_rowsByHandle.ContainsKey(variable.ClientHandle))
            return;

        var row = _dataTable.NewRow();
        row["Sel"] = variable.IsSelectedForScope ? CheckedBox : UncheckedBox;
        row["Name"] = variable.DisplayName;
        row["NodeId"] = variable.NodeId.ToString();
        row["Access"] = variable.AccessString;
        row["Time"] = variable.TimestampString;
        row["Status"] = FormatStatusWithIcon(variable);
        row["Value"] = variable.Value;
        row["_VariableRef"] = variable;

        _dataTable.Rows.Add(row);
        _rowsByHandle[variable.ClientHandle] = row;

        // Update cache if variable is already selected
        if (variable.IsSelectedForScope)
        {
            _cachedScopeSelectionCount++;
        }

        _tableView.Update();
        UpdateEmptyState();
    }

    public void UpdateVariable(MonitoredNode variable)
    {
        // Queue the update for batched processing
        _pendingUpdates[variable.ClientHandle] = variable;

        // Start the update timer if not already running
        EnsureUpdateTimerRunning();
    }

    /// <summary>
    /// Ensures the batched update timer is running.
    /// </summary>
    private void EnsureUpdateTimerRunning()
    {
        lock (_timerLock)
        {
            if (_updateTimerRunning)
                return;

            _updateTimerRunning = true;
            _updateTimer = Application.AddTimeout(TimeSpan.FromMilliseconds(UpdateBatchIntervalMs), ProcessPendingUpdates);
        }
    }

    /// <summary>
    /// Processes all pending variable updates in a single batch.
    /// </summary>
    private bool ProcessPendingUpdates()
    {
        // Defensive check - handle case where disposal happens concurrently
        if (_pendingUpdates.IsEmpty)
        {
            lock (_timerLock)
            {
                _updateTimerRunning = false;
            }
            return false;
        }

        // Snapshot and clear pending updates
        var keys = _pendingUpdates.Keys.ToList();
        var updates = new List<(uint ClientHandle, MonitoredNode Variable)>();

        foreach (var key in keys)
        {
            if (_pendingUpdates.TryRemove(key, out var variable))
            {
                updates.Add((key, variable));
            }
        }

        if (updates.Count == 0)
        {
            // No more updates - stop the timer
            lock (_timerLock)
            {
                _updateTimerRunning = false;
            }
            return false;
        }

        // Apply all updates to DataTable rows
        foreach (var (clientHandle, variable) in updates)
        {
            if (_rowsByHandle.TryGetValue(clientHandle, out var row))
            {
                row["Access"] = variable.AccessString;
                row["Sel"] = variable.IsSelectedForScope ? CheckedBox : UncheckedBox;
                row["Value"] = variable.Value;
                row["Time"] = variable.TimestampString;
                row["Status"] = FormatStatusWithIcon(variable);
            }
        }

        // Check if more updates arrived while we were processing (before redraw)
        // This avoids a race condition where updates arriving between Update() and
        // IsEmpty check would be orphaned until the next UpdateVariable() call
        bool hasMoreUpdates = !_pendingUpdates.IsEmpty;

        // Single table redraw for all updates
        _tableView.Update();

        if (!hasMoreUpdates)
        {
            lock (_timerLock)
            {
                _updateTimerRunning = false;
            }
            return false; // Stop timer
        }

        return true; // Continue timer for remaining updates
    }

    public void RemoveVariable(uint clientHandle)
    {
        if (!_rowsByHandle.TryGetValue(clientHandle, out var row))
            return;

        // Clear scope selection before removing
        if (row["_VariableRef"] is MonitoredNode node && node.IsSelectedForScope)
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
            if (row["_VariableRef"] is MonitoredNode node)
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

    private void ToggleScopeSelection()
    {
        var variable = SelectedVariable;
        if (variable == null) return;

        ToggleScopeSelectionForVariable(variable);
    }

    private void OnRecordButtonClicked(object? sender, CommandEventArgs e)
    {
        RecordToggleRequested?.Invoke();
    }

    private void HandleKeyDown(object? _, Key e)
    {
        // Use KeyCode for comparisons - Terminal.Gui v2 pattern
        var keyCode = e.KeyCode;

        if (keyCode == KeyCode.Delete || keyCode == KeyCode.Backspace)
        {
            var selected = SelectedVariable;
            if (selected != null)
            {
                UnsubscribeRequested?.Invoke(selected);
                e.Handled = true;
            }
        }
        else if (keyCode == KeyCode.Space)
        {
            // Toggle scope selection for the highlighted variable
            ToggleScopeSelection();
            e.Handled = true;
        }
        else if (keyCode == (KeyCode)'w' || keyCode == (KeyCode)'W')
        {
            var selected = SelectedVariable;
            if (selected != null)
            {
                WriteRequested?.Invoke(selected);
                e.Handled = true;
            }
        }
        else if (keyCode == (KeyCode)'s' || keyCode == (KeyCode)'S')
        {
            ScopeRequested?.Invoke();
            e.Handled = true;
        }
    }

    private void HandleMouseClick(object? sender, MouseEventArgs e)
    {
        // e.Position is relative to the TableView viewport, but ScreenToCell expects screen coordinates
        var screenPoint = _tableView.ViewportToScreen(e.Position);
        var cellPoint = _tableView.ScreenToCell(screenPoint.X, screenPoint.Y, out int? columnIndex, out int? rowIndex);

        // Toggle selection when clicking anywhere on a valid row
        if (rowIndex.HasValue && rowIndex.Value >= 0 && rowIndex.Value < _dataTable.Rows.Count)
        {
            var row = _dataTable.Rows[rowIndex.Value];
            var variable = row["_VariableRef"] as MonitoredNode;
            if (variable == null) return;

            ToggleScopeSelectionForVariable(variable);
            e.Handled = true;
        }
    }

    private void ToggleScopeSelectionForVariable(MonitoredNode variable)
    {
        if (variable.IsSelectedForScope)
        {
            // Deselect
            variable.IsSelectedForScope = false;
            _cachedScopeSelectionCount--;
        }
        else
        {
            // Check if we've reached the max
            if (_cachedScopeSelectionCount >= MaxScopeSelections)
            {
                // Show feedback that max is reached
                var theme = ThemeManager.Current;
                _selectionFeedback.Text = $"Max {MaxScopeSelections} variables for Scope/Recording";
                _selectionFeedback.ColorScheme = new ColorScheme
                {
                    Normal = new Attribute(theme.Warning, theme.Background),
                    Focus = new Attribute(theme.Warning, theme.Background),
                    HotNormal = new Attribute(theme.Warning, theme.Background),
                    HotFocus = new Attribute(theme.Warning, theme.Background),
                    Disabled = new Attribute(theme.Warning, theme.Background)
                };
                _selectionFeedback.Visible = true;

                // Hide after a delay
                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    Application.Invoke(() => _selectionFeedback.Visible = false);
                });
                return;
            }

            // Select
            variable.IsSelectedForScope = true;
            _cachedScopeSelectionCount++;
        }

        // Update the row display
        if (_rowsByHandle.TryGetValue(variable.ClientHandle, out var row))
        {
            row["Sel"] = variable.IsSelectedForScope ? CheckedBox : UncheckedBox;
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

        // Update button text based on recording state
        _isRecording = isRecording;
        _recordButton.Text = isRecording ? "■ STOP" : "● REC";

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
            // Stop the update timer
            lock (_timerLock)
            {
                if (_updateTimer != null)
                {
                    Application.RemoveTimeout(_updateTimer);
                    _updateTimer = null;
                }
                _updateTimerRunning = false;
            }
            _pendingUpdates.Clear();

            ThemeManager.ThemeChanged -= OnThemeChanged;
            _recordButton.Accepting -= OnRecordButtonClicked;
            _tableView.MouseClick -= HandleMouseClick;
        }
        base.Dispose(disposing);
    }
}
