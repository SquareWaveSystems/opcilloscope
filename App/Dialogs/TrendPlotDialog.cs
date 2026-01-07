using Terminal.Gui;
using Opcilloscope.App.Views;
using Opcilloscope.App.Themes;
using Opcilloscope.OpcUa;
using Opcilloscope.OpcUa.Models;
using ThemeManager = Opcilloscope.App.Themes.ThemeManager;

namespace Opcilloscope.App.Dialogs;

/// <summary>
/// Dialog for displaying a real-time oscilloscope view with theme support.
/// </summary>
public class TrendPlotDialog : Dialog
{
    private readonly TrendPlotView _trendPlotView;
    private readonly SubscriptionManager? _subscriptionManager;
    private readonly IReadOnlyCollection<MonitoredNode>? _availableNodes;
    private readonly Button _selectNodeButton;
    private readonly Button _demoButton;
    private readonly Button _clearButton;
    private readonly Button _closeButton;

    // Theme-aware accessor
    private AppTheme Theme => ThemeManager.Current;

    public TrendPlotDialog(SubscriptionManager? subscriptionManager = null, MonitoredNode? initialNode = null)
    {
        _subscriptionManager = subscriptionManager;
        _availableNodes = subscriptionManager?.MonitoredVariables;

        Title = $"{Theme.TitleDecoration}[ OSCILLOSCOPE ]{Theme.TitleDecoration}";
        Width = Dim.Percent(85);
        Height = Dim.Percent(85);

        // Apply theme-based styling - double-line border for emphasis with grey border color
        ColorScheme = Theme.DialogColorScheme;
        BorderStyle = LineStyle.Double;
        if (Border != null)
        {
            Border.ColorScheme = Theme.BorderColorScheme;
        }

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

        _selectNodeButton = new Button
        {
            X = 1,
            Y = 0,
            Text = $"{Theme.ButtonPrefix}NODE{Theme.ButtonSuffix}",
            ColorScheme = Theme.ButtonColorScheme
        };
        _selectNodeButton.Accepting += OnSelectNode;

        _demoButton = new Button
        {
            X = Pos.Right(_selectNodeButton) + 2,
            Y = 0,
            Text = $"{Theme.ButtonPrefix}DEMO{Theme.ButtonSuffix}",
            ColorScheme = Theme.ButtonColorScheme
        };
        _demoButton.Accepting += OnDemoMode;

        _clearButton = new Button
        {
            X = Pos.Right(_demoButton) + 2,
            Y = 0,
            Text = $"{Theme.ButtonPrefix}CLR{Theme.ButtonSuffix}",
            ColorScheme = Theme.ButtonColorScheme
        };
        _clearButton.Accepting += OnClear;

        _closeButton = new Button
        {
            X = Pos.Right(_clearButton) + 2,
            Y = 0,
            Text = $"{Theme.ButtonPrefix}EXIT{Theme.ButtonSuffix}",
            ColorScheme = Theme.ButtonColorScheme
        };
        _closeButton.Accepting += (_, _) => Application.RequestStop();

        buttonFrame.Add(_selectNodeButton, _demoButton, _clearButton, _closeButton);

        Add(_trendPlotView, buttonFrame);

        // Subscribe to theme changes
        ThemeManager.ThemeChanged += OnThemeChanged;

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

    private void OnSelectNode(object? _, CommandEventArgs _1)
    {
        if (_availableNodes == null || !_availableNodes.Any())
        {
            MessageBox.Query(Theme.NoSignalMessage, "No monitored variables available.\nSubscribe to a variable node first.", "OK");
            return;
        }

        var nodeList = _availableNodes.ToList();
        var nodeNames = nodeList.Select(n => n.DisplayName).ToArray();

        var dialog = new Dialog
        {
            Title = $"{Theme.TitleDecoration}[ SELECT SIGNAL ]{Theme.TitleDecoration}",
            Width = 50,
            Height = Math.Min(nodeList.Count + 7, 20),
            ColorScheme = Theme.DialogColorScheme,
            BorderStyle = Theme.BorderLineStyle
        };
        if (dialog.Border != null)
        {
            dialog.Border.ColorScheme = Theme.BorderColorScheme;
        }

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
        selectButton.Accepting += (_, _) =>
        {
            if (listView.SelectedItem >= 0 && listView.SelectedItem < nodeList.Count)
            {
                selectedNode = nodeList[listView.SelectedItem];
                Application.RequestStop();
            }
        };

        cancelButton.Accepting += (_, _) => Application.RequestStop();

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

    private void OnDemoMode(object? _, CommandEventArgs _1)
    {
        StartDemoMode();
    }

    private void StartDemoMode()
    {
        _trendPlotView.Clear();
        _trendPlotView.StartDemoMode();
    }

    private void OnClear(object? _, CommandEventArgs _1)
    {
        _trendPlotView.Clear();
    }

    private void OnThemeChanged(AppTheme theme)
    {
        Application.Invoke(() =>
        {
            Title = $"{theme.TitleDecoration}[ OSCILLOSCOPE ]{theme.TitleDecoration}";
            ColorScheme = theme.DialogColorScheme;
            BorderStyle = theme.BorderLineStyle;

            _selectNodeButton.Text = $"{theme.ButtonPrefix}NODE{theme.ButtonSuffix}";
            _selectNodeButton.ColorScheme = theme.ButtonColorScheme;

            _demoButton.Text = $"{theme.ButtonPrefix}DEMO{theme.ButtonSuffix}";
            _demoButton.ColorScheme = theme.ButtonColorScheme;

            _clearButton.Text = $"{theme.ButtonPrefix}CLR{theme.ButtonSuffix}";
            _clearButton.ColorScheme = theme.ButtonColorScheme;

            _closeButton.Text = $"{theme.ButtonPrefix}EXIT{theme.ButtonSuffix}";
            _closeButton.ColorScheme = theme.ButtonColorScheme;

            _trendPlotView.SetNeedsLayout();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            _trendPlotView.Dispose();
        }
        base.Dispose(disposing);
    }
}
