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
    private readonly Label _companyLabel;
    private readonly Label _connectionStatusLabel;
    private readonly SpinnerView _activitySpinner;
    private readonly Label _activityLabel;
    private readonly CsvRecordingManager _csvRecordingManager;
    private object? _recordingStatusTimer;

    // Connecting animation state
    private object? _connectingAnimationTimer;
    private int _connectingDotCount = 1;
    private bool _isConnecting;
    private bool _isConnected;

    private string? _lastEndpoint;

    public MainWindow()
    {
        _logger = new Logger();
        _client = new OpcUaClientWrapper(_logger);
        _nodeBrowser = new NodeBrowser(_client, _logger);
        _csvRecordingManager = new CsvRecordingManager(_logger);

        // Override global "Menu" ColorScheme BEFORE creating any views
        // This prevents StatusBar's blue background flash on first render
        var theme = ThemeManager.Current;
        Colors.ColorSchemes["Menu"] = new ColorScheme
        {
            Normal = new Terminal.Gui.Attribute(theme.Foreground, theme.Background),
            Focus = new Terminal.Gui.Attribute(theme.ForegroundBright, theme.Background),
            HotNormal = new Terminal.Gui.Attribute(theme.Accent, theme.Background),
            HotFocus = new Terminal.Gui.Attribute(theme.AccentBright, theme.Background),
            Disabled = new Terminal.Gui.Attribute(theme.MutedText, theme.Background)
        };

        // Wire up client events
        _client.Connected += OnClientConnected;
        _client.Disconnected += OnClientDisconnected;
        _client.ConnectionError += OnConnectionError;

        // Subscribe to theme changes
        ThemeManager.ThemeChanged += OnThemeChanged;

        // Set initial window title (status shown in status bar)
        Title = "OPC Scope";

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

        // Also set ColorScheme directly on the StatusBar instance
        _statusBar.ColorScheme = new ColorScheme
        {
            Normal = new Terminal.Gui.Attribute(theme.Foreground, theme.Background),
            Focus = new Terminal.Gui.Attribute(theme.ForegroundBright, theme.Background),
            HotNormal = new Terminal.Gui.Attribute(theme.Accent, theme.Background),
            HotFocus = new Terminal.Gui.Attribute(theme.AccentBright, theme.Background),
            Disabled = new Terminal.Gui.Attribute(theme.MutedText, theme.Background)
        };

        _statusBar.Add(new Shortcut(Key.F1, "Help", ShowHelp));
        _statusBar.Add(new Shortcut(Key.F5, "Refresh", RefreshTree));
        _statusBar.Add(new Shortcut(Key.Enter, "Subscribe", SubscribeSelected));
        _statusBar.Add(new Shortcut(Key.Delete, "Unsubscribe", UnsubscribeSelected));
        _statusBar.Add(new Shortcut(Key.F10, "Menu", () => _menuBar.OpenMenu()));

        // Connection status indicator (colored)
        _connectionStatusLabel = new Label
        {
            X = Pos.AnchorEnd(40),
            Y = 0,
            Text = $" {theme.DisconnectedIndicator} "
        };
        UpdateConnectionStatusLabelStyle(isConnected: false);
        _statusBar.Add(_connectionStatusLabel);

        // Company branding label (bottom right, separate from status bar shortcuts)
        // ColorScheme is set in ApplyTheme() to use theme colors
        _companyLabel = new Label
        {
            X = Pos.AnchorEnd(26),
            Y = Pos.AnchorEnd(1),
            Text = "Square Wave Systems 2026"
        };

        // Create activity spinner for async operations
        // ColorScheme is set in ApplyTheme() to use theme colors
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
        _monitoredItemsView.RecordRequested += OnRecordRequested;
        _monitoredItemsView.StopRecordingRequested += OnStopRecordingRequested;

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
        Add(_companyLabel);

        // Apply initial theme (after all controls are created)
        ApplyTheme();

        // Run startup sequence
        _ = RunStartupSequenceAsync();
    }

    private async Task RunStartupSequenceAsync()
    {
        // Show initializing message
        UiThread.Run(() =>
        {
            Title = "OPC Scope";
            _connectionStatusLabel.Text = " INITIALIZING.. ";
            SetNeedsLayout();
        });

        await Task.Delay(800);

        // Show nominal message
        UiThread.Run(() =>
        {
            _connectionStatusLabel.Text = " All systems nominal. ";
            SetNeedsLayout();
        });

        await Task.Delay(600);

        // Show normal disconnected state and log startup
        UiThread.Run(() =>
        {
            UpdateConnectionStatus(isConnected: false);
            _logger.Info("OPC Scope started - Square Wave Systems");
            _logger.Info("Press F10 for menu, or use Connection -> Connect");
        });
    }

    private MenuBar CreateMenuBar()
    {
        // Build theme menu items dynamically
        var themeMenuItems = ThemeManager.AvailableThemes
            .Select((theme, index) => new MenuItem(
                $"_{theme.Name}",
                "",
                () => ThemeManager.SetThemeByIndex(index)))
            .ToArray();

        return new MenuBar
        {
            X = 0,
            Y = 0,
            Menus = new MenuBarItem[]
            {
                new MenuBarItem("_File", new MenuItem[]
                {
                    new MenuItem("_Export to CSV...", "", ExportToCsv),
                    null!, // Separator
                    new MenuItem("Start _Recording...", "", () => OnRecordRequested()),
                    new MenuItem("Sto_p Recording", "", () => OnStopRecordingRequested()),
                    null!, // Separator
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

        // Update global "Menu" ColorScheme (used by StatusBar)
        Colors.ColorSchemes["Menu"] = new ColorScheme
        {
            Normal = new Terminal.Gui.Attribute(theme.Foreground, theme.Background),
            Focus = new Terminal.Gui.Attribute(theme.ForegroundBright, theme.Background),
            HotNormal = new Terminal.Gui.Attribute(theme.Accent, theme.Background),
            HotFocus = new Terminal.Gui.Attribute(theme.AccentBright, theme.Background),
            Disabled = new Terminal.Gui.Attribute(theme.MutedText, theme.Background)
        };

        // Apply main window styling - double-line for emphasis
        ColorScheme = theme.MainColorScheme;
        BorderStyle = theme.EmphasizedBorderStyle;

        // Apply grey border color to main window border (consistent across terminals)
        if (Border != null)
        {
            Border.ColorScheme = theme.BorderColorScheme;
        }

        // Apply styling to company label (subtle in status bar)
        _companyLabel.ColorScheme = new ColorScheme
        {
            Normal = new Terminal.Gui.Attribute(theme.MutedText, theme.Background)
        };

        // Apply styling to menu bar
        ThemeStyler.ApplyToMenuBar(_menuBar, theme);

        // Apply clean status bar styling (no blue background)
        // Must set ColorScheme AND call SetNeedsDisplay to override Terminal.Gui defaults
        var cleanStatusBarScheme = new ColorScheme
        {
            Normal = new Terminal.Gui.Attribute(theme.Foreground, theme.Background),
            Focus = new Terminal.Gui.Attribute(theme.ForegroundBright, theme.Background),
            HotNormal = new Terminal.Gui.Attribute(theme.Accent, theme.Background),
            HotFocus = new Terminal.Gui.Attribute(theme.AccentBright, theme.Background),
            Disabled = new Terminal.Gui.Attribute(theme.MutedText, theme.Background)
        };
        _statusBar.ColorScheme = cleanStatusBarScheme;
        _statusBar.SetNeedsLayout();

        // Also apply theme to connection status label
        UpdateConnectionStatusLabelStyle(_isConnected);

        // Apply theme to activity spinner and label (for async operations)
        _activitySpinner.ColorScheme = cleanStatusBarScheme;
        _activityLabel.ColorScheme = cleanStatusBarScheme;

        // Apply to child views with border differentiation
        // MonitoredItems gets double-line (emphasized)
        _monitoredItemsView.BorderStyle = theme.EmphasizedBorderStyle;
        ThemeStyler.ApplyToFrame(_monitoredItemsView, theme);

        // Other panels get single-line (secondary)
        _addressSpaceView.BorderStyle = theme.SecondaryBorderStyle;
        _nodeDetailsView.BorderStyle = theme.SecondaryBorderStyle;
        _logView.BorderStyle = theme.SecondaryBorderStyle;
        ThemeStyler.ApplyToFrame(_addressSpaceView, theme);
        ThemeStyler.ApplyToFrame(_nodeDetailsView, theme);
        ThemeStyler.ApplyToFrame(_logView, theme);
    }

    private void OnThemeChanged(RetroTheme theme)
    {
        Application.Invoke(() =>
        {
            ApplyTheme();

            // Update connection status label with new theme colors
            _connectionStatusLabel.Text = _isConnected
                ? $" {theme.ConnectedIndicator} "
                : $" {theme.DisconnectedIndicator} ";
            UpdateConnectionStatusLabelStyle(_isConnected);

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

        StartConnectingAnimation();
        ShowActivity("Connecting...");

        try
        {
            var success = await _client.ConnectAsync(endpoint);

            if (success)
            {
                await InitializeAfterConnectAsync();
            }
        }
        finally
        {
            StopConnectingAnimation();
            UiThread.Run(HideActivity);
        }
    }

    private async Task InitializeAfterConnectAsync()
    {
        // Initialize subscription manager
        _subscriptionManager = new SubscriptionManager(_client, _logger);
        await _subscriptionManager.InitializeAsync();

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

        // Initialize address space view (handles its own async loading)
        _addressSpaceView.Initialize(_nodeBrowser);

        // Update status on UI thread
        UiThread.Run(() => UpdateConnectionStatus(isConnected: true));
    }

    private void Disconnect()
    {
        // Stop recording if active
        if (_csvRecordingManager.IsRecording)
        {
            OnStopRecordingRequested();
        }

        _subscriptionManager?.Dispose();
        _subscriptionManager = null;

        _client.Disconnect();

        _addressSpaceView.Clear();
        _monitoredItemsView.Clear();
        _nodeDetailsView.Clear();

        UpdateConnectionStatus(isConnected: false);
    }

    private async Task ReconnectAsync()
    {
        if (string.IsNullOrEmpty(_lastEndpoint))
        {
            _logger.Warning("No previous connection to reconnect");
            return;
        }

        StartConnectingAnimation();
        ShowActivity("Reconnecting...");

        try
        {
            var success = await _client.ReconnectAsync();

            if (success)
            {
                await InitializeAfterConnectAsync();
            }
        }
        finally
        {
            StopConnectingAnimation();
            UiThread.Run(HideActivity);
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
        _ = _nodeDetailsView.ShowNodeAsync(node);
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

        _ = _subscriptionManager.AddNodeAsync(node.NodeId, node.DisplayName);
    }

    private void OnUnsubscribeRequested(MonitoredNode item)
    {
        _ = _subscriptionManager?.RemoveNodeAsync(item.ClientHandle);
    }

    private void OnValueChanged(MonitoredNode item)
    {
        // Record to CSV if recording is active
        _csvRecordingManager.RecordValue(item);

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
            UpdateConnectionStatus(isConnected: false);
        });
    }

    private void OnConnectionError(string message)
    {
        UiThread.Run(() =>
        {
            MessageBox.ErrorQuery("Connection Error", message, "OK");
        });
    }

    private void UpdateConnectionStatus(bool isConnected)
    {
        _isConnected = isConnected;
        var theme = ThemeManager.Current;

        // Update title (plain text)
        Title = "OPC Scope";

        // Update colored status label in status bar
        _connectionStatusLabel.Text = isConnected
            ? $" {theme.ConnectedIndicator} "
            : $" {theme.DisconnectedIndicator} ";
        UpdateConnectionStatusLabelStyle(isConnected);

        SetNeedsLayout();
    }

    private void UpdateConnectionStatusLabelStyle(bool isConnected)
    {
        var theme = ThemeManager.Current;
        _connectionStatusLabel.ColorScheme = new ColorScheme
        {
            Normal = new Terminal.Gui.Attribute(
                isConnected ? theme.StatusGood : theme.Accent,
                theme.Background),
            Focus = new Terminal.Gui.Attribute(
                isConnected ? theme.StatusGood : theme.Accent,
                theme.Background),
            HotNormal = new Terminal.Gui.Attribute(
                isConnected ? theme.StatusGood : theme.Accent,
                theme.Background),
            HotFocus = new Terminal.Gui.Attribute(
                isConnected ? theme.StatusGood : theme.Accent,
                theme.Background)
        };
    }

    private void StartConnectingAnimation()
    {
        _isConnecting = true;
        _connectingDotCount = 1;
        _connectingAnimationTimer = Application.AddTimeout(TimeSpan.FromMilliseconds(400), () =>
        {
            if (!_isConnecting)
                return false; // Stop animation

            _connectingDotCount = (_connectingDotCount % 3) + 1;
            var dots = new string('.', _connectingDotCount);
            _connectionStatusLabel.Text = $" Connecting{dots} ";
            SetNeedsLayout();
            return true; // Continue animation
        });
    }

    private void StopConnectingAnimation()
    {
        _isConnecting = false;
        if (_connectingAnimationTimer != null)
        {
            Application.RemoveTimeout(_connectingAnimationTimer);
            _connectingAnimationTimer = null;
        }
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

    private void OnRecordRequested()
    {
        if (_csvRecordingManager.IsRecording)
        {
            _logger.Warning("Recording is already in progress");
            return;
        }

        if (_subscriptionManager == null || !_subscriptionManager.MonitoredItems.Any())
        {
            MessageBox.Query("Record", "No items to record. Subscribe to items first.", "OK");
            return;
        }

        using var dialog = new SaveDialog
        {
            Title = "Save Recording As",
            AllowedTypes = new List<IAllowedType> { new AllowedType("CSV Files", ".csv") }
        };

        Application.Run(dialog);

        if (!dialog.Canceled && dialog.Path != null)
        {
            var path = dialog.Path.ToString();
            if (!path!.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                path += ".csv";
            }

            if (_csvRecordingManager.StartRecording(path))
            {
                _monitoredItemsView.SetRecordingState(true, "REC");
                StartRecordingStatusUpdates();
            }
            else
            {
                MessageBox.ErrorQuery("Recording Error", "Failed to start recording", "OK");
            }
        }
    }

    private void OnStopRecordingRequested()
    {
        if (!_csvRecordingManager.IsRecording)
        {
            return;
        }

        StopRecordingStatusUpdates();
        _csvRecordingManager.StopRecording();
        _monitoredItemsView.SetRecordingState(false, "");
        MessageBox.Query("Recording", $"Recording saved.\n{_csvRecordingManager.RecordCount} records written.", "OK");
    }

    private void StartRecordingStatusUpdates()
    {
        // Use Terminal.Gui's Application.AddTimeout for periodic updates
        _recordingStatusTimer = Application.AddTimeout(TimeSpan.FromSeconds(1), () =>
        {
            if (_csvRecordingManager.IsRecording)
            {
                var duration = _csvRecordingManager.RecordingDuration;
                var status = $"REC {duration:mm\\:ss} ({_csvRecordingManager.RecordCount} records)";
                _monitoredItemsView.UpdateRecordingStatus(status);
                return true; // Continue timer
            }
            return false; // Stop timer
        });
    }

    private void StopRecordingStatusUpdates()
    {
        if (_recordingStatusTimer != null)
        {
            Application.RemoveTimeout(_recordingStatusTimer);
            _recordingStatusTimer = null;
        }
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
            
            // Wait for the export task to complete before disposing
            exportTask.Wait();

            progressDialog.Dispose();
        }
    }

    private void ShowHelp()
    {
        var help = @"OPC Scope - Terminal OPC UA Client
by Square Wave Systems

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

CSV Recording:
  - Use Record/Stop buttons in Monitored Items panel
  - Or use File > Start Recording / Stop Recording
  - Records all value changes to CSV in real-time
  - CSV format: Timestamp, DisplayName, NodeId, Value, Status

Tips:
  - Only Variable nodes can be subscribed
  - Double-click a node to subscribe
  - Values update in real-time via subscription
  - Use View > Trend Plot to visualize values
";
        MessageBox.Query("OPC Scope Help", help, "OK");
    }

    private void ShowAbout()
    {
        var theme = ThemeManager.Current;
        var about = $@"╔══════════════════════════════════════╗
║           OPC Scope v1.0.0           ║
║      by Square Wave Systems          ║
╚══════════════════════════════════════╝

A lightweight terminal-based OPC UA client
for browsing, monitoring, and subscribing
to industrial automation data.

Current Theme: {theme.Name}
  {theme.Description}

Built with:
  - .NET 10
  - Terminal.Gui v2
  - OPC Foundation UA-.NETStandard

Themes inspired by:
  - Cassette Futurism (Alien, Blade Runner)
  - github.com/Imetomi/retro-futuristic-ui-design

© 2026 Square Wave Systems
License: MIT
";
        MessageBox.Query("About OPC Scope", about, "OK");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopRecordingStatusUpdates();
            StopConnectingAnimation();
            _csvRecordingManager.Dispose();
            ThemeManager.ThemeChanged -= OnThemeChanged;
            _subscriptionManager?.Dispose();
            _client.Dispose();
        }
        base.Dispose(disposing);
    }
}
