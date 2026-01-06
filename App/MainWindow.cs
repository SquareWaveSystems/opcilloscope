using Terminal.Gui;
using OpcScope.App.Views;
using OpcScope.App.Dialogs;
using OpcScope.App.Themes;
using OpcScope.OpcUa;
using OpcScope.OpcUa.Models;
using OpcScope.Utilities;
using ThemeManager = OpcScope.App.Themes.ThemeManager;

namespace OpcScope.App;

/// <summary>
/// Main application window with layout orchestration.
/// Supports retro-futuristic themes inspired by cassette futurism.
/// </summary>
public class MainWindow : Toplevel
{
    private readonly Logger _logger;
    private readonly OpcUaClientWrapper _client;
    private readonly NodeBrowser _nodeBrowser;
    private SubscriptionManager? _subscriptionManager;

    private readonly MenuBar _menuBar;
    private readonly AddressSpaceView _addressSpaceView;
    private readonly MonitoredItemsView _monitoredItemsView;
    private readonly NodeDetailsView _nodeDetailsView;
    private readonly LogView _logView;
    private readonly StatusBar _statusBar;
    private readonly SpinnerView _activitySpinner;
    private readonly Label _activityLabel;

    private string? _lastEndpoint;

    public MainWindow()
    {
        _logger = new Logger();
        _client = new OpcUaClientWrapper(_logger);
        _nodeBrowser = new NodeBrowser(_client, _logger);

        // Wire up client events
        _client.Connected += OnClientConnected;
        _client.Disconnected += OnClientDisconnected;
        _client.ConnectionError += OnConnectionError;

        // Subscribe to theme changes
        ThemeManager.ThemeChanged += OnThemeChanged;

        // Create menu bar
        _menuBar = CreateMenuBar();

        // Create main views
        _addressSpaceView = new AddressSpaceView
        {
            X = 0,
            Y = 1,
            Width = Dim.Percent(35),
            Height = Dim.Percent(60)
        };

        _monitoredItemsView = new MonitoredItemsView
        {
            X = Pos.Right(_addressSpaceView),
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Percent(60)
        };

        _nodeDetailsView = new NodeDetailsView
        {
            X = 0,
            Y = Pos.Bottom(_addressSpaceView),
            Width = Dim.Fill(),
            Height = 5
        };

        _logView = new LogView
        {
            X = 0,
            Y = Pos.Bottom(_nodeDetailsView),
            Width = Dim.Fill(),
            Height = Dim.Fill(1)
        };

        // Create status bar with shortcuts
        _statusBar = new StatusBar
        {
            Visible = true
        };
        _statusBar.Add(new Shortcut(Key.F1, "Help", ShowHelp));
        _statusBar.Add(new Shortcut(Key.F5, "Refresh", RefreshTree));
        _statusBar.Add(new Shortcut(Key.Enter, "Subscribe", SubscribeSelected));
        _statusBar.Add(new Shortcut(Key.Delete, "Unsubscribe", UnsubscribeSelected));
        _statusBar.Add(new Shortcut(Key.F10, "Menu", () => _menuBar.OpenMenu()));

        // Create activity spinner for async operations
        _activitySpinner = new SpinnerView
        {
            X = Pos.AnchorEnd(20),
            Y = 0,
            Visible = false,
            AutoSpin = true
        };

        _activityLabel = new Label
        {
            X = Pos.Right(_activitySpinner) + 1,
            Y = 0,
            Text = "",
            Visible = false
        };

        _statusBar.Add(_activitySpinner);
        _statusBar.Add(_activityLabel);

        // Wire up view events
        _addressSpaceView.NodeSelected += OnNodeSelected;
        _addressSpaceView.NodeSubscribeRequested += OnSubscribeRequested;
        _monitoredItemsView.UnsubscribeRequested += OnUnsubscribeRequested;
        _monitoredItemsView.TrendPlotRequested += OnTrendPlotRequested;

        // Initialize views
        _logView.Initialize(_logger);
        _nodeDetailsView.Initialize(_nodeBrowser);

        // Add all views
        Add(_menuBar);
        Add(_addressSpaceView);
        Add(_monitoredItemsView);
        Add(_nodeDetailsView);
        Add(_logView);
        Add(_statusBar);

        // Apply initial theme (after all controls are created)
        ApplyTheme();

        // Log startup
        _logger.Info("OpcScope started");
        _logger.Info("Press F10 for menu, or use Connection -> Connect");
    }

    private MenuBar CreateMenuBar()
    {
        // Build theme menu items dynamically
        var themeMenuItems = ThemeManager.AvailableThemes
            .Select((theme, index) => new MenuItem(
                $"_{theme.Name}",
                theme.Description,
                () => ThemeManager.SetThemeByIndex(index)))
            .ToArray();

        return new MenuBar
        {
            Menus = new MenuBarItem[]
            {
                new MenuBarItem("_File", new MenuItem[]
                {
                    new MenuItem("_Export to CSV...", "", ExportToCsv),
                    new MenuItem("E_xit", "", () => RequestStop())
                }),
                new MenuBarItem("_Connection", new MenuItem[]
                {
                    new MenuItem("_Connect...", "", ShowConnectDialog),
                    new MenuItem("_Disconnect", "", Disconnect),
                    new MenuItem("_Reconnect", "", () => _ = ReconnectAsync())
                }),
                new MenuBarItem("_View", new MenuItem[]
                {
                    new MenuItem("_Trend Plot...", "", ShowTrendPlot),
                    new MenuItem("_Refresh Tree", "", RefreshTree),
                    new MenuItem("_Clear Log", "", () => _logView.Clear()),
                    new MenuItem("_Settings...", "", ShowSettings)
                }),
                new MenuBarItem("_Theme", themeMenuItems),
                new MenuBarItem("_Help", new MenuItem[]
                {
                    new MenuItem("_Help", "", ShowHelp),
                    new MenuItem("_About", "", ShowAbout)
                })
            }
        };
    }

    private void ApplyTheme()
    {
        var theme = ThemeManager.Current;

        // Apply main window styling
        ColorScheme = theme.MainColorScheme;
        BorderStyle = theme.BorderLineStyle;

        // Apply styling to menu and status bar
        ThemeStyler.ApplyToMenuBar(_menuBar, theme);
        ThemeStyler.ApplyToStatusBar(_statusBar, theme);

        // Apply to all child views
        ThemeStyler.ApplyToFrame(_addressSpaceView, theme);
        ThemeStyler.ApplyToFrame(_monitoredItemsView, theme);
        ThemeStyler.ApplyToFrame(_nodeDetailsView, theme);
        ThemeStyler.ApplyToFrame(_logView, theme);
    }

    private void OnThemeChanged(RetroTheme theme)
    {
        Application.Invoke(() =>
        {
            ApplyTheme();
            _logger.Info($"Theme changed to: {theme.Name}");
            SetNeedsLayout();
        });
    }

    private void ShowConnectDialog()
    {
        var dialog = new ConnectDialog(_lastEndpoint);
        Application.Run(dialog);

        if (dialog.Confirmed)
        {
            _lastEndpoint = dialog.EndpointUrl;
            _ = ConnectAsync(dialog.EndpointUrl);
        }
    }

    private async Task ConnectAsync(string endpoint)
    {
        // Disconnect if already connected
        Disconnect();

        UpdateConnectionStatus("Connecting...");
        ShowActivity("Connecting...");

        try
        {
            var success = await _client.ConnectAsync(endpoint);

            if (success)
            {
                InitializeAfterConnect();
            }
        }
        finally
        {
            HideActivity();
        }
    }

    private void InitializeAfterConnect()
    {
        // Initialize subscription manager
        _subscriptionManager = new SubscriptionManager(_client, _logger);
        _subscriptionManager.Initialize();

        // Wire up subscription events
        _subscriptionManager.ValueChanged += OnValueChanged;
        _subscriptionManager.ItemAdded += item =>
        {
            UiThread.Run(() => _monitoredItemsView.AddItem(item));
        };
        _subscriptionManager.ItemRemoved += handle =>
        {
            UiThread.Run(() => _monitoredItemsView.RemoveItem(handle));
        };

        // Initialize address space view
        _addressSpaceView.Initialize(_nodeBrowser);

        UpdateConnectionStatus($"Connected to {_client.CurrentEndpoint}");
    }

    private void Disconnect()
    {
        _subscriptionManager?.Dispose();
        _subscriptionManager = null;

        _client.Disconnect();

        _addressSpaceView.Clear();
        _monitoredItemsView.Clear();
        _nodeDetailsView.Clear();

        UpdateConnectionStatus("Disconnected");
    }

    private async Task ReconnectAsync()
    {
        if (string.IsNullOrEmpty(_lastEndpoint))
        {
            _logger.Warning("No previous connection to reconnect");
            return;
        }

        UpdateConnectionStatus("Reconnecting...");
        ShowActivity("Reconnecting...");

        try
        {
            var success = await _client.ReconnectAsync();

            if (success)
            {
                InitializeAfterConnect();
            }
        }
        finally
        {
            HideActivity();
        }
    }

    private void RefreshTree()
    {
        if (_client.IsConnected)
        {
            _addressSpaceView.Refresh();
            _logger.Info("Address space refreshed");
        }
    }

    private void SubscribeSelected()
    {
        var node = _addressSpaceView.SelectedNode;
        if (node != null)
        {
            OnSubscribeRequested(node);
        }
    }

    private void UnsubscribeSelected()
    {
        var item = _monitoredItemsView.SelectedItem;
        if (item != null)
        {
            OnUnsubscribeRequested(item);
        }
    }

    private void OnNodeSelected(BrowsedNode node)
    {
        _nodeDetailsView.ShowNode(node);
    }

    private void OnSubscribeRequested(BrowsedNode node)
    {
        if (_subscriptionManager == null)
        {
            _logger.Warning("Not connected");
            return;
        }

        if (node.NodeClass != Opc.Ua.NodeClass.Variable)
        {
            _logger.Warning($"Cannot subscribe to {node.NodeClass} nodes, only Variables");
            return;
        }

        _subscriptionManager.AddNode(node.NodeId, node.DisplayName);
    }

    private void OnUnsubscribeRequested(MonitoredNode item)
    {
        _subscriptionManager?.RemoveNode(item.ClientHandle);
    }

    private void OnValueChanged(MonitoredNode item)
    {
        UiThread.Run(() =>
        {
            _monitoredItemsView.UpdateItem(item);
        });
    }

    private void OnClientConnected()
    {
        UiThread.Run(() =>
        {
            _logger.Info("Connected successfully");
        });
    }

    private void OnClientDisconnected()
    {
        UiThread.Run(() =>
        {
            UpdateConnectionStatus("Disconnected");
        });
    }

    private void OnConnectionError(string message)
    {
        UiThread.Run(() =>
        {
            MessageBox.ErrorQuery("Connection Error", message, "OK");
        });
    }

    private void UpdateConnectionStatus(string status)
    {
        Title = $"OpcScope - {status}";
        SetNeedsLayout();
    }

    /// <summary>
    /// Shows the activity spinner and message in the status bar during async operations.
    /// </summary>
    /// <param name="message">The message to display next to the spinner.</param>
    private void ShowActivity(string message)
    {
        _activityLabel.Text = message;
        _activityLabel.Visible = true;
        _activitySpinner.Visible = true;
        SetNeedsLayout();
    }

    /// <summary>
    /// Hides the activity spinner and clears the activity message in the status bar.
    /// </summary>
    private void HideActivity()
    {
        _activitySpinner.Visible = false;
        _activityLabel.Visible = false;
        _activityLabel.Text = "";
        SetNeedsLayout();
    }

    private void ShowSettings()
    {
        var currentInterval = _subscriptionManager?.PublishingInterval ?? 1000;
        var dialog = new SettingsDialog(currentInterval);
        Application.Run(dialog);

        if (dialog.Confirmed && _subscriptionManager != null)
        {
            _subscriptionManager.PublishingInterval = dialog.PublishingInterval;
            _logger.Info($"Publishing interval changed to {dialog.PublishingInterval}ms");
        }
    }

    private void ShowTrendPlot()
    {
        var dialog = new TrendPlotDialog(_subscriptionManager);
        Application.Run(dialog);
    }

    private void OnTrendPlotRequested(MonitoredNode node)
    {
        var dialog = new TrendPlotDialog(_subscriptionManager, node);
        Application.Run(dialog);
    }

    private void ExportToCsv()
    {
        if (_subscriptionManager == null || !_subscriptionManager.MonitoredItems.Any())
        {
            MessageBox.Query("Export", "No items to export", "OK");
            return;
        }

        using var dialog = new SaveDialog
        {
            Title = "Export to CSV"
        };

        Application.Run(dialog);

        if (!dialog.Canceled && dialog.Path != null)
        {
            var items = _subscriptionManager.MonitoredItems.ToList();
            var path = dialog.Path.ToString();

            // Create progress dialog
            var progressDialog = new Dialog
            {
                Title = " Exporting ",
                Width = 50,
                Height = 7
            };

            var progressLabel = new Label
            {
                X = 1,
                Y = 1,
                Text = "Exporting data..."
            };

            var progressBar = new ProgressBar
            {
                X = 1,
                Y = 2,
                Width = Dim.Fill(1),
                Fraction = 0f,
                ProgressBarStyle = ProgressBarStyle.Continuous
            };

            var statusLabel = new Label
            {
                X = 1,
                Y = 3,
                Text = $"0 / {items.Count} items"
            };

            progressDialog.Add(progressLabel, progressBar, statusLabel);

            // Export in background with proper exception handling
            var exportTask = Task.Run(async () =>
            {
                try
                {
                    using var writer = new StreamWriter(path!);
                    await writer.WriteLineAsync("DisplayName,NodeId,Value,Timestamp,Status");

                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        await writer.WriteLineAsync($"\"{item.DisplayName}\",\"{item.NodeId}\",\"{item.Value}\",\"{item.TimestampString}\",\"{item.StatusString}\"");

                        // Update progress on UI thread
                        var progress = (float)(i + 1) / items.Count;
                        var current = i + 1;
                        Application.Invoke(() =>
                        {
                            progressBar.Fraction = progress;
                            statusLabel.Text = $"{current} / {items.Count} items";
                        });

                        // Small delay for visual feedback on very small datasets
                        if (items.Count < 10)
                            await Task.Delay(10);
                    }

                    Application.Invoke(() =>
                    {
                        progressDialog.RequestStop();
                        _logger.Info($"Exported {items.Count} items to {path}");
                        MessageBox.Query("Export", $"Exported {items.Count} items to {path}", "OK");
                    });
                }
                catch (Exception ex)
                {
                    Application.Invoke(() =>
                    {
                        progressDialog.RequestStop();
                        _logger.Error($"Export failed: {ex.Message}");
                        MessageBox.ErrorQuery("Export Error", ex.Message, "OK");
                    });
                }
            });

            Application.Run(progressDialog);
            
            // Ensure we wait for the export task to complete and propagate any exceptions
            try
            {
                exportTask.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.Error($"Export task exception: {ex.Message}");
            }

            progressDialog.Dispose();
        }
    }

    private void ShowHelp()
    {
        var help = @"OpcScope - Terminal OPC UA Client

Keyboard Shortcuts:
  F1        - Show this help
  F5        - Refresh address space tree
  F10       - Open menu
  Enter     - Subscribe to selected node
  Space     - Show trend plot for selected item
  Delete    - Unsubscribe from selected item
  Ctrl+O    - Connect to server
  Ctrl+Q    - Quit

Navigation:
  Tab       - Move between panels
  Arrow Keys - Navigate within panel
  Space     - Expand/collapse tree node (in tree view)

Trend Plot (in dialog):
  Space     - Pause/resume plotting
  +/-       - Adjust vertical scale
  R         - Reset to auto-scale

Tips:
  - Only Variable nodes can be subscribed
  - Double-click a node to subscribe
  - Values update in real-time via subscription
  - Use View > Trend Plot to visualize values
";
        MessageBox.Query("Help", help, "OK");
    }

    private void ShowAbout()
    {
        var theme = ThemeManager.Current;
        var about = $@"OpcScope v1.0.0

A lightweight terminal-based OPC UA client
for browsing, monitoring, and subscribing
to industrial automation data.

Current Theme: {theme.Name}
  {theme.Description}

Built with:
  - .NET 8
  - Terminal.Gui v2
  - OPC Foundation UA-.NETStandard

Themes inspired by:
  - Cassette Futurism (Alien, Blade Runner)
  - github.com/Imetomi/retro-futuristic-ui-design
  - squarewavesystems.github.io

License: MIT
";
        MessageBox.Query("About OpcScope", about, "OK");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            _subscriptionManager?.Dispose();
            _client.Dispose();
        }
        base.Dispose(disposing);
    }
}
