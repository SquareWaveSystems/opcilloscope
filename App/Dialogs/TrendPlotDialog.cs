using Terminal.Gui;
using OpcScope.App.Views;
using OpcScope.OpcUa;
using OpcScope.OpcUa.Models;

namespace OpcScope.App.Dialogs;

/// <summary>
/// Dialog for displaying a real-time trend plot of monitored values.
/// </summary>
public class TrendPlotDialog : Dialog
{
    private readonly TrendPlotView _trendPlotView;
    private readonly Label _statusLabel;
    private readonly SubscriptionManager? _subscriptionManager;
    private readonly IReadOnlyCollection<MonitoredNode>? _availableNodes;

    public TrendPlotDialog(SubscriptionManager? subscriptionManager = null, MonitoredNode? initialNode = null)
    {
        _subscriptionManager = subscriptionManager;
        _availableNodes = subscriptionManager?.MonitoredItems;

        Title = "Trend Plot";
        Width = Dim.Percent(80);
        Height = Dim.Percent(80);

        // Create the trend plot view
        _trendPlotView = new TrendPlotView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3)
        };

        // Status label
        _statusLabel = new Label
        {
            X = 0,
            Y = Pos.Bottom(_trendPlotView),
            Width = Dim.Fill(),
            Height = 1,
            Text = ""
        };

        // Button row
        var buttonFrame = new View
        {
            X = 0,
            Y = Pos.Bottom(_statusLabel),
            Width = Dim.Fill(),
            Height = 2
        };

        var selectNodeButton = new Button
        {
            X = 1,
            Y = 0,
            Text = "Select Node..."
        };
        selectNodeButton.Accepting += OnSelectNode;

        var demoButton = new Button
        {
            X = Pos.Right(selectNodeButton) + 2,
            Y = 0,
            Text = "Demo Mode"
        };
        demoButton.Accepting += OnDemoMode;

        var clearButton = new Button
        {
            X = Pos.Right(demoButton) + 2,
            Y = 0,
            Text = "Clear"
        };
        clearButton.Accepting += OnClear;

        var closeButton = new Button
        {
            X = Pos.Right(clearButton) + 2,
            Y = 0,
            Text = "Close"
        };
        closeButton.Accepting += (s, e) => Application.RequestStop();

        buttonFrame.Add(selectNodeButton, demoButton, clearButton, closeButton);

        // Wire up events
        _trendPlotView.PauseStateChanged += OnPauseStateChanged;

        Add(_trendPlotView, _statusLabel, buttonFrame);

        // Start in demo mode if no monitored items available
        if (_availableNodes == null || !_availableNodes.Any())
        {
            StartDemoMode();
        }
        else
        {
            UpdateStatus("Press 'Select Node...' to choose a monitored item to plot");
        }

        _trendPlotView.SetFocus();
    }

    private void OnSelectNode(object? sender, CommandEventArgs e)
    {
        if (_availableNodes == null || !_availableNodes.Any())
        {
            MessageBox.Query("No Nodes", "No monitored items available.\nSubscribe to a variable node first.", "OK");
            return;
        }

        // Create a simple selection dialog
        var nodeList = _availableNodes.ToList();
        var nodeNames = nodeList.Select(n => n.DisplayName).ToArray();

        var dialog = new Dialog
        {
            Title = "Select Node to Plot",
            Width = 50,
            Height = Math.Min(nodeList.Count + 6, 20)
        };

        var listView = new ListView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(3)
        };
        listView.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(nodeNames));

        var hintLabel = new Label
        {
            X = 1,
            Y = Pos.Bottom(listView),
            Text = "Only numeric values can be plotted"
        };

        var selectButton = new Button
        {
            X = Pos.Center() - 10,
            Y = Pos.Bottom(hintLabel) + 1,
            Text = "Select",
            IsDefault = true
        };

        var cancelButton = new Button
        {
            X = Pos.Center() + 2,
            Y = Pos.Bottom(hintLabel) + 1,
            Text = "Cancel"
        };

        MonitoredNode? selectedNode = null;
        selectButton.Accepting += (s, ev) =>
        {
            if (listView.SelectedItem >= 0 && listView.SelectedItem < nodeList.Count)
            {
                selectedNode = nodeList[listView.SelectedItem];
                Application.RequestStop();
            }
        };

        cancelButton.Accepting += (s, ev) => Application.RequestStop();

        dialog.Add(listView, hintLabel, selectButton, cancelButton);
        listView.SetFocus();

        Application.Run(dialog);
        dialog.Dispose();

        if (selectedNode != null)
        {
            BindToNode(selectedNode);
        }
    }

    private void BindToNode(MonitoredNode node)
    {
        _trendPlotView.StopDemoMode();
        _trendPlotView.Clear();

        if (_subscriptionManager != null)
        {
            _trendPlotView.BindToMonitoredNode(node, _subscriptionManager);
            UpdateStatus($"Plotting: {node.DisplayName}");
        }
    }

    private void OnDemoMode(object? sender, CommandEventArgs e)
    {
        StartDemoMode();
    }

    private void StartDemoMode()
    {
        _trendPlotView.Clear();
        _trendPlotView.StartDemoMode();
        UpdateStatus("Demo Mode: Sine wave generator");
    }

    private void OnClear(object? sender, CommandEventArgs e)
    {
        _trendPlotView.Clear();
    }

    private void OnPauseStateChanged(bool isPaused)
    {
        if (isPaused)
        {
            UpdateStatus(_statusLabel.Text + " [PAUSED]");
        }
        else
        {
            var text = _statusLabel.Text?.ToString() ?? "";
            UpdateStatus(text.Replace(" [PAUSED]", ""));
        }
    }

    private void UpdateStatus(string message)
    {
        _statusLabel.Text = message;
    }

    private object? _updateToken;

    private void StartUpdateTimer()
    {
        if (_updateToken != null) return;

        // ~10 FPS refresh rate
        _updateToken = Application.AddTimeout(TimeSpan.FromMilliseconds(100), () =>
        {
            _trendPlotView.SetNeedsLayout();
            return true; // Keep timer running
        });
    }

    private void StopUpdateTimer()
    {
        if (_updateToken != null)
        {
            Application.RemoveTimeout(_updateToken);
            _updateToken = null;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopUpdateTimer();
            _trendPlotView.Dispose();
        }
        base.Dispose(disposing);
    }
}
