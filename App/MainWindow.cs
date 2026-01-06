using Terminal.Gui;
using OpcScope.App.Views;
using OpcScope.App.Dialogs;
using OpcScope.App.Themes;
using OpcScope.Configuration;
using OpcScope.Configuration.Models;
using OpcScope.OpcUa;
using OpcScope.OpcUa.Models;
using OpcScope.Utilities;
using ThemeManager = OpcScope.App.Themes.ThemeManager;

namespace OpcScope.App;

/// <summary>
/// Main application window with layout orchestration.
/// </summary>
public class MainWindow : Toplevel
{
    private readonly Logger _logger;
    private readonly ConnectionManager _connectionManager;
    private readonly ConfigurationService _configService;
    private readonly RecentFilesManager _recentFiles;
    private ConfigMetadata? _currentMetadata;

    private MenuBar _menuBar;
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
    private readonly MenuItem _themeToggleItem;

    // Connecting animation state
    private object? _connectingAnimationTimer;
    private int _connectingDotCount = 1;
    private bool _isConnecting;
    private bool _isConnected;

    private string? _lastEndpoint;

    public MainWindow()
    {
        _logger = new Logger();
        _connectionManager = new ConnectionManager(_logger);
        _csvRecordingManager = new CsvRecordingManager(_logger);
        _configService = new ConfigurationService();
        _recentFiles = new RecentFilesManager();

        // Wire up connection manager events
        _connectionManager.StateChanged += OnConnectionStateChanged;
        _connectionManager.ConnectionError += OnConnectionError;
        _connectionManager.ValueChanged += OnValueChanged;
        _connectionManager.ItemAdded += item =>
        {
            UiThread.Run(() => _monitoredItemsView.AddItem(item));
            _configService.MarkDirty();
            UiThread.Run(UpdateWindowTitle);
        };
        _connectionManager.ItemRemoved += handle =>
        {
            UiThread.Run(() => _monitoredItemsView.RemoveItem(handle));
            _configService.MarkDirty();
            UiThread.Run(UpdateWindowTitle);
        };

        // Wire up configuration service events
        _configService.UnsavedChangesStateChanged += _ => UiThread.Run(UpdateWindowTitle);
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

        // Create theme toggle menu item
        _themeToggleItem = new MenuItem(GetThemeToggleTitle(), "", ToggleTheme);

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
        _statusBar.Add(new Shortcut(Key.W, "Write Val", WriteSelected));
        _statusBar.Add(new Shortcut(Key.Space, "Rec Select", null));  // Visual hint only - handled in MonitoredItemsView
        _statusBar.Add(new Shortcut(Key.G.WithCtrl, "Scope", LaunchScope));
        _statusBar.Add(new Shortcut(Key.R.WithCtrl, "Rec", ToggleRecording));
        _statusBar.Add(new Shortcut(Key.F10, "Menu", () => _menuBar.OpenMenu()));

        // Connection status indicator (colored) - FAR RIGHT, overlaid on status bar row
        _connectionStatusLabel = new Label
        {
            X = Pos.AnchorEnd(26),  // " ■ All systems nominal. " (longest text)
            Y = Pos.AnchorEnd(1),  // Bottom row (status bar)
            Text = $" {theme.DisconnectedIndicator} "
        };
        UpdateConnectionStatusLabelStyle(isConnected: false);

        // Company branding label (width-aware, overlaid on status bar row)
        // ColorScheme is set in ApplyTheme() to use theme colors
        _companyLabel = new Label
        {
            X = Pos.Left(_connectionStatusLabel) - 28,
            Y = Pos.AnchorEnd(1),  // Bottom row (status bar)
            Text = "Square Wave Systems 2026 |"
        };

        // Create activity spinner for async operations (left of company label)
        // ColorScheme is set in ApplyTheme() to use theme colors
        _activitySpinner = new SpinnerView
        {
            X = Pos.AnchorEnd(62),
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

        // Initial width-aware update (will also be called from ApplyTheme)
        Initialized += (_, _) => UpdateCompanyLabelForWidth();

        // Wire up view events
        _addressSpaceView.NodeSelected += OnNodeSelected;
        _addressSpaceView.NodeSubscribeRequested += OnSubscribeRequested;
        _monitoredItemsView.UnsubscribeRequested += OnUnsubscribeRequested;
        _monitoredItemsView.WriteRequested += OnWriteRequested;
        _monitoredItemsView.TrendPlotRequested += OnTrendPlotRequested;

        // Initialize views
        _logView.Initialize(_logger);
        _nodeDetailsView.Initialize(_connectionManager.NodeBrowser);

        // Add all views
        Add(_menuBar);
        Add(_addressSpaceView);
        Add(_monitoredItemsView);
        Add(_nodeDetailsView);
        Add(_logView);
        Add(_statusBar);
        Add(_companyLabel);
        Add(_connectionStatusLabel);

        // Apply initial theme (after all controls are created)
        ApplyTheme();

        // Run startup sequence
        _ = RunStartupSequenceAsync();
    }

    private async Task RunStartupSequenceAsync()
    {
        var theme = ThemeManager.Current;

        // Show initializing message in accent color
        UiThread.Run(() =>
        {
            Title = "OPC Scope";
            _connectionStatusLabel.Text = " ■ INITIALIZING... ";
            _connectionStatusLabel.ColorScheme = new ColorScheme
            {
                Normal = new Terminal.Gui.Attribute(theme.Accent, theme.Background)
            };
            SetNeedsLayout();
        });

        await Task.Delay(2000);

        // Show nominal message in bright/success style
        UiThread.Run(() =>
        {
            _connectionStatusLabel.Text = " ■ All systems nominal. ";
            _connectionStatusLabel.ColorScheme = new ColorScheme
            {
                Normal = new Terminal.Gui.Attribute(theme.StatusGood, theme.Background)
            };
            SetNeedsLayout();
        });

        await Task.Delay(3400);

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
        return new MenuBar
        {
            X = 0,
            Y = 0,
            Menus = new MenuBarItem[]
            {
                new MenuBarItem("_File", new MenuItem[]
                {
                    new MenuItem("_Open Config...", "", OpenConfig, shortcutKey: Key.O.WithCtrl),
                    new MenuItem("_Save Config", "", SaveConfig, shortcutKey: Key.S.WithCtrl),
                    new MenuItem("Save Config _As...", "", SaveConfigAs, shortcutKey: Key.S.WithCtrl.WithShift),
                    null!, // Separator
                    new MenuItem("Start _Recording...", "", () => OnRecordRequested(), shortcutKey: Key.R.WithCtrl),
                    new MenuItem("Sto_p Recording", "", () => OnStopRecordingRequested()),
                    null!, // Separator
                    new MenuItem("E_xit", "", () => RequestStop(), shortcutKey: Key.Q.WithCtrl)
                }),
                new MenuBarItem("_Connection", new MenuItem[]
                {
                    new MenuItem("_Connect...", "", ShowConnectDialog),
                    new MenuItem("_Disconnect", "", Disconnect),
                    new MenuItem("_Reconnect", "", () => ReconnectAsync().FireAndForget(_logger))
                }),
                new MenuBarItem("_View", new MenuItem[]
                {
                    new MenuItem("_Scope", "Ctrl+G", LaunchScope),
                    new MenuItem("_Refresh Tree", "F5", RefreshTree),
                    new MenuItem("_Clear Log", "", () => _logView.Clear()),
                    _themeToggleItem,
                    null!, // Separator
                    new MenuItem("Se_ttings...", "", ShowSettings)
                }),
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

        // Update width-aware company label
        UpdateCompanyLabelForWidth();

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

    private void OnThemeChanged(AppTheme theme)
    {
        UiThread.Run(() =>
        {
            ApplyTheme();

            // Update theme toggle menu item title
            _themeToggleItem.Title = GetThemeToggleTitle();

            // Update connection status label with new theme colors
            _connectionStatusLabel.Text = _isConnected
                ? $" {theme.ConnectedIndicator} "
                : $" {theme.DisconnectedIndicator} ";
            UpdateConnectionStatusLabelStyle(_isConnected);

            _logger.Info($"Theme changed to: {theme.Name}");
            SetNeedsLayout();
        });
    }

    private string GetThemeToggleTitle()
    {
        // Show what clicking will do: "Switch to Light" when in Dark, "Switch to Dark" when in Light
        var currentIndex = ThemeManager.GetCurrentThemeIndex();
        return currentIndex == 0 ? "Switch to _Light" : "Switch to _Dark";
    }

    private void ToggleTheme()
    {
        var currentIndex = ThemeManager.GetCurrentThemeIndex();
        var newIndex = (currentIndex + 1) % 2;
        ThemeManager.SetThemeByIndex(newIndex);
    }

    private void ShowConnectDialog()
    {
        var dialog = new ConnectDialog(_connectionManager.LastEndpoint);
        Application.Run(dialog);

        if (dialog.Confirmed)
        {
            ConnectAsync(dialog.EndpointUrl).FireAndForget(_logger);
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
            var success = await _connectionManager.ConnectAsync(endpoint);

            if (success)
            {
                _addressSpaceView.Initialize(_connectionManager.NodeBrowser);
            }
        }
        finally
        {
            StopConnectingAnimation();
            UiThread.Run(HideActivity);
        }
    }

    private void Disconnect()
    {
        // Stop recording if active
        if (_csvRecordingManager.IsRecording)
        {
            OnStopRecordingRequested();
        }

        _connectionManager.Disconnect();

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
            var success = await _connectionManager.ReconnectAsync();

            if (success)
            {
                _addressSpaceView.Initialize(_connectionManager.NodeBrowser);
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
        if (_connectionManager.IsConnected)
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

    private void WriteSelected()
    {
        var item = _monitoredItemsView.SelectedItem;
        if (item != null)
        {
            OnWriteRequested(item);
        }
    }

    private void OnNodeSelected(BrowsedNode node)
    {
        _nodeDetailsView.ShowNodeAsync(node).FireAndForget(_logger);
    }

    private void OnSubscribeRequested(BrowsedNode node)
    {
        if (!_connectionManager.IsConnected)
        {
            _logger.Warning("Not connected");
            return;
        }

        if (node.NodeClass != Opc.Ua.NodeClass.Variable)
        {
            _logger.Warning($"Cannot subscribe to {node.NodeClass} nodes, only Variables");
            return;
        }

        _connectionManager.SubscribeAsync(node.NodeId, node.DisplayName).FireAndForget(_logger);
    }

    private void OnUnsubscribeRequested(MonitoredNode item)
    {
        _connectionManager.UnsubscribeAsync(item.ClientHandle).FireAndForget(_logger);
    }

    private void OnTrendPlotRequested(MonitoredNode item)
    {
        var dialog = new TrendPlotDialog(_connectionManager.SubscriptionManager!, item);
        Application.Run(dialog);
    }

    private void OnWriteRequested(MonitoredNode item)
    {
        if (!_connectionManager.IsConnected)
        {
            _logger.Warning("Cannot write: not connected");
            return;
        }

        // Check if node is writable
        if (!item.IsWritable)
        {
            _logger.Warning($"Cannot write to {item.DisplayName}: node is read-only");
            MessageBox.Query("Write", $"Node '{item.DisplayName}' is read-only", "OK");
            return;
        }

        // Check if data type is supported for writing
        if (!Utilities.OpcValueConverter.IsWriteSupported(item.DataType))
        {
            _logger.Warning($"Write not supported for data type: {item.DataTypeName}");
            MessageBox.Query("Write", $"Write not supported for data type: {item.DataTypeName}", "OK");
            return;
        }

        // Show write dialog
        var dialog = new WriteValueDialog(
            item.NodeId,
            item.DisplayName,
            item.DataType,
            item.DataTypeName,
            item.Value);

        Application.Run(dialog);

        if (dialog.Confirmed && dialog.ParsedValue != null)
        {
            _ = WriteValueAsync(item, dialog.ParsedValue);
        }
    }

    private async Task WriteValueAsync(MonitoredNode item, object value)
    {
        try
        {
            ShowActivity("Writing...");

            var statusCode = await _connectionManager.Client!.WriteValueAsync(item.NodeId, value);

            UiThread.Run(() =>
            {
                HideActivity();

                if (Opc.Ua.StatusCode.IsGood(statusCode))
                {
                    _logger.Info($"Wrote {FormatValueForLog(value)} to {item.NodeId}");
                }
                else
                {
                    var statusName = $"0x{statusCode.Code:X8}";
                    _logger.Error($"Write failed ({statusName}): {item.NodeId}");
                    MessageBox.ErrorQuery("Write Failed", $"Write failed: {statusName}", "OK");
                }
            });
        }
        catch (Exception ex)
        {
            UiThread.Run(() =>
            {
                HideActivity();
                _logger.Error($"Write error: {ex.Message}");
                MessageBox.ErrorQuery("Write Error", ex.Message, "OK");
            });
        }
    }

    private static string FormatValueForLog(object value)
    {
        if (value is string s)
            return $"\"{s}\"";
        return value.ToString() ?? "null";
    }

    private void OnValueChanged(MonitoredNode item)
    {
        // Record to CSV if recording is active AND item is selected for scope/recording
        if (item.IsSelectedForScope)
        {
            _csvRecordingManager.RecordValue(item);
        }

        UiThread.Run(() => _monitoredItemsView.UpdateItem(item));
    }

    private void OnConnectionStateChanged(ConnectionState state)
    {
        UiThread.Run(() =>
        {
            var isConnected = state == ConnectionState.Connected;
            UpdateConnectionStatus(isConnected);

            if (isConnected)
            {
                _logger.Info($"Connected to {_connectionManager.CurrentEndpoint}");
            }
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

    private void ToggleRecording()
    {
        if (_csvRecordingManager.IsRecording)
        {
            OnStopRecordingRequested();
        }
        else
        {
            OnRecordRequested();
        }
    }

    private void UpdateCompanyLabelForWidth()
    {
        var width = _statusBar.Frame.Width;

        // Width-aware company branding with separator
        if (width >= 120)
            _companyLabel.Text = "Square Wave Systems 2026 |";
        else if (width >= 90)
            _companyLabel.Text = "SWS 2026 |";
        else
            _companyLabel.Text = "SWS |";
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
        var currentInterval = _connectionManager.SubscriptionManager?.PublishingInterval ?? 1000;
        var dialog = new SettingsDialog(currentInterval);
        Application.Run(dialog);

        if (dialog.Confirmed && _connectionManager.SubscriptionManager != null)
        {
            _connectionManager.SubscriptionManager.PublishingInterval = dialog.PublishingInterval;
            _logger.Info($"Publishing interval changed to {dialog.PublishingInterval}ms");

            // Mark configuration as having unsaved changes
            _configService.MarkDirty();
            UpdateWindowTitle();
        }
    }

    private void LaunchScope()
    {
        if (_connectionManager.SubscriptionManager == null)
        {
            MessageBox.Query("Scope", "Connect to a server first.", "OK");
            return;
        }

        var selectedNodes = _monitoredItemsView.ScopeSelectedNodes;

        if (selectedNodes.Count == 0)
        {
            MessageBox.Query("Scope", "Select up to 5 nodes to display in Scope.\nUse Space to toggle selection on monitored items.", "OK");
            return;
        }

        var dialog = new ScopeDialog(selectedNodes, _connectionManager.SubscriptionManager);
        Application.Run(dialog);
    }

    private void OnRecordRequested()
    {
        if (_csvRecordingManager.IsRecording)
        {
            _logger.Warning("Recording is already in progress");
            return;
        }

        var subscriptionManager = _connectionManager.SubscriptionManager;
        if (subscriptionManager == null || !subscriptionManager.MonitoredItems.Any())
        {
            MessageBox.Query("Record", "No items to record. Subscribe to items first.", "OK");
            return;
        }

        // Check that at least one item is selected for recording
        var selectedCount = _monitoredItemsView.ScopeSelectionCount;
        if (selectedCount == 0)
        {
            MessageBox.Query("Record",
                "No items selected for recording.\n\n" +
                "Use Space to select items in the Rec column (◉).\n" +
                "Selected items will be recorded and shown in Scope.", "OK");
            return;
        }

        using var dialog = new SaveDialog
        {
            Title = "Save Recording As",
            AllowedTypes = new List<IAllowedType> { new AllowedType("CSV Files", ".csv") },
            Path = $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
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
                _monitoredItemsView.UpdateRecordingStatus($"◉ REC ({selectedCount})", true);
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
        _monitoredItemsView.UpdateRecordingStatus("", false);
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
                _monitoredItemsView.UpdateRecordingStatus($"◉ {duration:mm\\:ss}", true);
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

    private void ShowHelp()
    {
        var help = @"OPC Scope - Terminal OPC UA Client
by Square Wave Systems

Keyboard Shortcuts:
  F1        - Show this help
  F5        - Refresh address space tree
  F10       - Open menu
  Enter     - Subscribe to selected node
  Space     - Toggle recording selection (◉ = record & show in Scope)
  Delete    - Unsubscribe from selected item
  W         - Write value to selected item
  Ctrl+G    - Open Scope with selected items
  Ctrl+R    - Toggle recording (start/stop)
  Ctrl+O    - Connect to server
  Ctrl+Q    - Quit

Navigation:
  Tab       - Move between panels
  Arrow Keys - Navigate within panel
  Space     - Expand/collapse tree node (in tree view)

Scope View:
  - Select up to 5 items using Space in Monitored Items
  - Press Ctrl+G to launch Scope with selected items
  - X-axis shows elapsed time
  - Each signal displayed with distinct color

Scope Controls (in dialog):
  Space     - Pause/resume plotting
  +/-       - Adjust vertical scale
  R         - Reset to auto-scale

CSV Recording & Scope:
  - Press Space on items to select for recording (◉ in Rec column)
  - Same items are shown in Scope view and recorded to CSV
  - Press Ctrl+R to toggle recording on/off
  - Recording indicator shows in status bar with elapsed time
  - CSV format: Timestamp, DisplayName, NodeId, Value, Status

Tips:
  - Only Variable nodes can be subscribed
  - Double-click a node to subscribe
  - Values update in real-time via subscription
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
for browsing, monitoring, and visualizing
industrial automation data in real-time.

Features:
  - Multi-signal Scope view (up to 5 signals)
  - Time-based plotting with auto-scale
  - CSV recording of monitored values

Current Theme: {theme.Name}
  {theme.Description}

Built with:
  - .NET 10
  - Terminal.Gui v2
  - OPC Foundation UA-.NETStandard

© 2026 Square Wave Systems
License: MIT
";
        MessageBox.Query("About OPC Scope", about, "OK");
    }

    #region Configuration File Handling

    /// <summary>
    /// Opens a configuration file, prompting to save changes if necessary.
    /// </summary>
    private void OpenConfig()
    {
        if (_configService.HasUnsavedChanges && !ConfirmDiscardChanges())
            return;

        using var dialog = new OpenDialog
        {
            Title = "Open Configuration",
            AllowedTypes = new List<IAllowedType>
            {
                new AllowedType("OpcScope Config", ".opcscope"),
                new AllowedType("JSON Files", ".json")
            }
        };

        Application.Run(dialog);

        if (!dialog.Canceled && dialog.Path != null)
        {
            LoadConfigurationAsync(dialog.Path.ToString()!).FireAndForget(_logger);
        }
    }

    /// <summary>
    /// Saves the current configuration to the current file path.
    /// </summary>
    private void SaveConfig()
    {
        if (string.IsNullOrEmpty(_configService.CurrentFilePath))
        {
            SaveConfigAs();
            return;
        }

        SaveConfigurationAsync(_configService.CurrentFilePath).FireAndForget(_logger);
    }

    /// <summary>
    /// Saves the current configuration to a new file path.
    /// </summary>
    private void SaveConfigAs()
    {
        using var dialog = new SaveDialog
        {
            Title = "Save Configuration",
            AllowedTypes = new List<IAllowedType>
            {
                new AllowedType("OpcScope Config", ".opcscope")
            }
        };

        Application.Run(dialog);

        if (!dialog.Canceled && dialog.Path != null)
        {
            var filePath = dialog.Path.ToString()!;
            if (!filePath.EndsWith(".opcscope", StringComparison.OrdinalIgnoreCase))
                filePath += ".opcscope";

            SaveConfigurationAsync(filePath).FireAndForget(_logger);
        }
    }

    /// <summary>
    /// Loads a configuration from the specified file path.
    /// </summary>
    private async Task LoadConfigurationAsync(string filePath)
    {
        try
        {
            ShowActivity("Loading configuration...");
            _logger.Info($"Loading configuration from {filePath}...");

            var config = await _configService.LoadAsync(filePath);

            // Store metadata
            _currentMetadata = config.Metadata;

            // Connect to server and subscribe to nodes
            if (!string.IsNullOrEmpty(config.Server.EndpointUrl))
            {
                var connected = await _connectionManager.ConnectAsync(config.Server.EndpointUrl);

                if (connected)
                {
                    // Only after successful connection, disconnect old and clear views
                    _addressSpaceView.Clear();
                    _monitoredItemsView.Clear();
                    _nodeDetailsView.Clear();
                    
                    _addressSpaceView.Initialize(_connectionManager.NodeBrowser);

                    // Apply publishing interval to the newly created subscription manager
                    if (_connectionManager.SubscriptionManager != null)
                    {
                        _connectionManager.SubscriptionManager.PublishingInterval = config.Settings.PublishingIntervalMs;
                    }

                    // Subscribe to saved nodes
                    foreach (var node in config.MonitoredNodes.Where(n => n.Enabled))
                    {
                        try
                        {
                            var nodeId = Opc.Ua.NodeId.Parse(node.NodeId);
                            await _connectionManager.SubscribeAsync(nodeId, node.DisplayName);
                        }
                        catch (Exception ex)
                        {
                            _logger.Warning($"Failed to subscribe to {node.DisplayName}: {ex.Message}");
                        }
                    }

                    _recentFiles.Add(filePath);
                    UpdateWindowTitle();

                    var nodeCount = config.MonitoredNodes.Count(n => n.Enabled);
                    _logger.Info($"Configuration loaded: {nodeCount} nodes");
                }
                else
                {
                    _logger.Error($"Failed to connect to {config.Server.EndpointUrl}");
                    MessageBox.ErrorQuery("Connection Failed", 
                        $"Could not connect to server:\n{config.Server.EndpointUrl}\n\nThe previous connection and data have been preserved.", 
                        "OK");
                }
            }
            else
            {
                // No endpoint URL, just clear views and apply settings
                if (_connectionManager.IsConnected)
                {
                    _connectionManager.Disconnect();
                }
                
                _addressSpaceView.Clear();
                _monitoredItemsView.Clear();
                _nodeDetailsView.Clear();
                
                _recentFiles.Add(filePath);
                UpdateWindowTitle();
                _logger.Info("Configuration loaded (no server connection)");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to load configuration: {ex.Message}");
            MessageBox.ErrorQuery("Error", $"Failed to load configuration:\n{ex.Message}", "OK");
        }
        finally
        {
            UiThread.Run(HideActivity);
        }
    }

    /// <summary>
    /// Saves the current configuration to the specified file path.
    /// </summary>
    private async Task SaveConfigurationAsync(string filePath)
    {
        try
        {
            ShowActivity("Saving configuration...");

            var monitoredItems = _connectionManager.SubscriptionManager?.MonitoredItems
                ?? Enumerable.Empty<MonitoredNode>();

            var config = _configService.CaptureCurrentState(
                _connectionManager.CurrentEndpoint,
                _connectionManager.SubscriptionManager?.PublishingInterval ?? 1000,
                monitoredItems,
                _currentMetadata
            );

            // Update metadata name from filename if not set
            if (string.IsNullOrEmpty(config.Metadata.Name))
            {
                config.Metadata.Name = Path.GetFileNameWithoutExtension(filePath);
            }

            await _configService.SaveAsync(config, filePath);

            _currentMetadata = config.Metadata;
            _recentFiles.Add(filePath);
            UpdateWindowTitle();

            _logger.Info($"Configuration saved to {filePath}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to save configuration: {ex.Message}");
            MessageBox.ErrorQuery("Error", $"Failed to save:\n{ex.Message}", "OK");
        }
        finally
        {
            UiThread.Run(HideActivity);
        }
    }

    /// <summary>
    /// Updates the window title to reflect the current configuration state.
    /// </summary>
    private void UpdateWindowTitle()
    {
        var configName = _configService.GetDisplayName();
        var unsavedMarker = _configService.HasUnsavedChanges ? "*" : "";

        Title = string.IsNullOrEmpty(_configService.CurrentFilePath) && !_configService.HasUnsavedChanges
            ? "OPC Scope"
            : $"OPC Scope - {configName}{unsavedMarker}";

        SetNeedsLayout();
    }

    /// <summary>
    /// Prompts the user to confirm discarding unsaved changes.
    /// </summary>
    /// <returns>True if the user confirms, false to cancel the operation.</returns>
    private bool ConfirmDiscardChanges()
    {
        var result = MessageBox.Query(
            "Unsaved Changes",
            "You have unsaved changes. Do you want to discard them?",
            "Discard",
            "Cancel");

        return result == 0; // Discard
    }

    /// <summary>
    /// Loads a configuration file from the command line argument.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    public void LoadConfigFromCommandLine(string configPath)
    {
        Application.AddTimeout(TimeSpan.FromMilliseconds(100), () =>
        {
            LoadConfigurationAsync(configPath).FireAndForget(_logger);
            return false;
        });
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopRecordingStatusUpdates();
            StopConnectingAnimation();
            _csvRecordingManager.Dispose();
            ThemeManager.ThemeChanged -= OnThemeChanged;
            _connectionManager.Dispose();
        }
        base.Dispose(disposing);
    }
}
