using Terminal.Gui;
using OpcScope.App.Views;
using OpcScope.App.Themes;
using OpcScope.OpcUa;
using OpcScope.OpcUa.Models;

namespace OpcScope.App.Dialogs;

/// <summary>
/// Industrial-themed dialog for displaying a real-time oscilloscope view.
/// Features a retro-futuristic CRT aesthetic with theme support.
/// </summary>
public class TrendPlotDialog : Dialog
{
    private readonly TrendPlotView _trendPlotView;
    private readonly SubscriptionManager? _subscriptionManager;
    private readonly IReadOnlyCollection<MonitoredNode>? _availableNodes;

    // Theme-aware accessor
    private static RetroTheme Theme => ThemeManager.Current;

    public TrendPlotDialog(SubscriptionManager? subscriptionManager = null, MonitoredNode? initialNode = null)
    {
        _subscriptionManager = subscriptionManager;
        _availableNodes = subscriptionManager?.MonitoredItems;

        Title = $"{Theme.TitleDecoration}[ OSCILLOSCOPE ]{Theme.TitleDecoration}";
        Width = Dim.Percent(85);
        Height = Dim.Percent(85);

        // Apply theme-based styling
        ColorScheme = Theme.DialogColorScheme;

        // Create the trend plot view - takes up most of the dialog
        _trendPlotView = new TrendPlotView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(2)
        };

        // Industrial-style button row
        var buttonFrame = new View
        {
            X = 0,
            Y = Pos.Bottom(_trendPlotView),
            Width = Dim.Fill(),
            Height = 2,
            ColorScheme = ColorScheme
        };

        var selectNodeButton = new Button
        {
            X = 1,
            Y = 0,
            Text = $"{Theme.ButtonPrefix}NODE{Theme.ButtonSuffix}",
            ColorScheme = Theme.ButtonColorScheme
        };
        selectNodeButton.Accepting += OnSelectNode;

        var demoButton = new Button
        {
            X = Pos.Right(selectNodeButton) + 2,
            Y = 0,
            Text = $"{Theme.ButtonPrefix}DEMO{Theme.ButtonSuffix}",
            ColorScheme = Theme.ButtonColorScheme
        };
        demoButton.Accepting += OnDemoMode;

        var clearButton = new Button
        {
            X = Pos.Right(demoButton) + 2,
            Y = 0,
            Text = $"{Theme.ButtonPrefix}CLR{Theme.ButtonSuffix}",
            ColorScheme = Theme.ButtonColorScheme
        };
        clearButton.Accepting += OnClear;

        var closeButton = new Button
        {
            X = Pos.Right(clearButton) + 2,
            Y = 0,
            Text = $"{Theme.ButtonPrefix}EXIT{Theme.ButtonSuffix}",
            ColorScheme = Theme.ButtonColorScheme
        };
        closeButton.Accepting += (s, e) => Application.RequestStop();

        buttonFrame.Add(selectNodeButton, demoButton, clearButton, closeButton);

        Add(_trendPlotView, buttonFrame);

        // Start with initial node, demo mode, or wait for selection
        if (initialNode != null)
        {
            BindToNode(initialNode);
        }
        else if (_availableNodes == null || !_availableNodes.Any())
        {
            StartDemoMode();
        }

        _trendPlotView.SetFocus();
    }

    private void OnSelectNode(object? sender, CommandEventArgs e)
    {
        if (_availableNodes == null || !_availableNodes.Any())
        {
            MessageBox.Query(Theme.NoSignalMessage, "No monitored items available.\nSubscribe to a variable node first.", "OK");
            return;
        }

        var nodeList = _availableNodes.ToList();
        var nodeNames = nodeList.Select(n => n.DisplayName).ToArray();

        var dialog = new Dialog
        {
            Title = $"{Theme.TitleDecoration}[ SELECT SIGNAL ]{Theme.TitleDecoration}",
            Width = 50,
            Height = Math.Min(nodeList.Count + 7, 20),
            ColorScheme = Theme.DialogColorScheme
        };

        var headerLabel = new Label
        {
            X = 1,
            Y = 0,
            Text = "▼ AVAILABLE SIGNALS ▼",
            ColorScheme = Theme.DialogColorScheme
        };

        var listView = new ListView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(4),
            ColorScheme = Theme.DialogColorScheme
        };
        listView.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(nodeNames));

        var hintLabel = new Label
        {
            X = 1,
            Y = Pos.Bottom(listView),
            Text = "[ Numeric values only ]",
            ColorScheme = Theme.DialogColorScheme
        };

        var selectButton = new Button
        {
            X = Pos.Center() - 12,
            Y = Pos.Bottom(hintLabel) + 1,
            Text = $"{Theme.ButtonPrefix}SELECT{Theme.ButtonSuffix}",
            IsDefault = true,
            ColorScheme = Theme.ButtonColorScheme
        };

        var cancelButton = new Button
        {
            X = Pos.Center() + 2,
            Y = Pos.Bottom(hintLabel) + 1,
            Text = $"{Theme.ButtonPrefix}CANCEL{Theme.ButtonSuffix}",
            ColorScheme = Theme.ButtonColorScheme
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

        dialog.Add(headerLabel, listView, hintLabel, selectButton, cancelButton);
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
    }

    private void OnClear(object? sender, CommandEventArgs e)
    {
        _trendPlotView.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trendPlotView.Dispose();
        }
        base.Dispose(disposing);
    }
}
