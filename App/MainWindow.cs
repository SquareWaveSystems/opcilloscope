using System.Reflection;
using Terminal.Gui;
using Opcilloscope.App.Views;
using Opcilloscope.App.Dialogs;
using Opcilloscope.App.Themes;
using Opcilloscope.Configuration;
using Opcilloscope.Configuration.Models;
using Opcilloscope.OpcUa;
using Opcilloscope.OpcUa.Models;
using Opcilloscope.Utilities;
using ThemeManager = Opcilloscope.App.Themes.ThemeManager;

namespace Opcilloscope.App;

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
    private readonly MonitoredVariablesView _monitoredVariablesView;
    private readonly NodeDetailsView _nodeDetailsView;
    private readonly LogView _logView;
    private readonly StatusBar _statusBar;
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

    // Focus tracking for context-aware UI
    private View? _focusedPanel;
    private FocusManager? _focusManager;

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
        _connectionManager.AutoReconnectTriggered += OnAutoReconnectTriggered;
        _connectionManager.VariableAdded += variable =>
        {
            UiThread.Run(() => _monitoredVariablesView?.AddVariable(variable));
            _configService.MarkDirty();
            UiThread.Run(UpdateWindowTitle);
        };
        _connectionManager.VariableRemoved += handle =>
        {
            UiThread.Run(() => _monitoredVariablesView?.RemoveVariable(handle));
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
        Title = "opcilloscope";

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

        _monitoredVariablesView = new MonitoredVariablesView
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

        // Minimal status bar: only essential shortcuts
        _statusBar.Add(new Shortcut(Key.F1, "Help", ShowHelp));
        _statusBar.Add(new Shortcut(Key.F5, "Refresh", RefreshTree));
        _statusBar.Add(new Shortcut(Key.F10, "Menu", () => _menuBar.OpenMenu()));

        // Connection status indicator (colored) - FAR RIGHT, overlaid on status bar row
        // We position it dynamically based on text width
        _connectionStatusLabel = new Label
        {
            Y = Pos.AnchorEnd(1),  // Bottom row (status bar)
            Text = $" {theme.DisconnectedIndicator} "
        };
        UpdateConnectionStatusLabelPosition();
        UpdateConnectionStatusLabelStyle(isConnected: false);

        // Create activity spinner for async operations
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


        // Wire up view events
        _addressSpaceView.NodeSelected += OnNodeSelected;
        _addressSpaceView.NodeSubscribeRequested += OnSubscribeRequested;
        _monitoredVariablesView.UnsubscribeRequested += OnUnsubscribeRequested;
        _monitoredVariablesView.WriteRequested += OnWriteRequested;
        _monitoredVariablesView.TrendPlotRequested += OnTrendPlotRequested;
        _monitoredVariablesView.ScopeRequested += LaunchScope;
        _monitoredVariablesView.RecordToggleRequested += ToggleRecording;

        // Focus tracking using polling-based FocusManager (workaround for Terminal.Gui v2 Enter event instability)
        // Only track the two interactive panes (AddressSpace and MonitoredVariables)
        _focusManager = new FocusManager(_addressSpaceView, _monitoredVariablesView);
        _focusManager.FocusChanged += OnPanelFocusChanged;

        // Initialize views
        _logView.Initialize(_logger);
        _nodeDetailsView.Initialize(_connectionManager.NodeBrowser);

        // Add all views
        Add(_menuBar);
        Add(_addressSpaceView);
        Add(_monitoredVariablesView);
        Add(_nodeDetailsView);
        Add(_logView);
        Add(_statusBar);
        Add(_connectionStatusLabel);

        // Apply initial theme (after all controls are created)
        ApplyTheme();

        // Handle window resize to update connection status label position
        Application.SizeChanging += (s, e) => UiThread.Run(UpdateConnectionStatusLabelPosition);

        // Run status bar startup sequence
        RunStatusBarStartup();

        // Start focus tracking after UI is initialized
        _focusManager.StartTracking();
    }

    /// <summary>
    /// Updates the connection status label position to be right-aligned.
    /// </summary>
    private void UpdateConnectionStatusLabelPosition()
    {
        var textLength = _connectionStatusLabel.Text?.Length ?? 0;
        _connectionStatusLabel.X = Pos.AnchorEnd(textLength);
    }

    /// <summary>
    /// Runs a brief startup sequence in the status bar.
    /// </summary>
    private void RunStatusBarStartup()
    {
        var theme = ThemeManager.Current;
        int step = 0;

        // Show first message immediately
        _connectionStatusLabel.Text = " Square Wave Systems 2026 ";
        _connectionStatusLabel.ColorScheme = new ColorScheme
        {
            Normal = new Terminal.Gui.Attribute(theme.Accent, theme.Background)
        };
        UpdateConnectionStatusLabelPosition();

        Application.AddTimeout(TimeSpan.FromSeconds(1), () =>
        {
            step++;
            if (step == 1)
            {
                // Second message
                _connectionStatusLabel.Text = " All systems nominal ";
                _connectionStatusLabel.ColorScheme = new ColorScheme
                {
                    Normal = new Terminal.Gui.Attribute(theme.StatusGood, theme.Background)
                };
                UpdateConnectionStatusLabelPosition();
                return true; // Continue
            }
            else
            {
                // Final state - show disconnected
                UpdateConnectionStatus(isConnected: false);
                return false; // Stop
            }
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
                    new MenuItem("_Scope", "S", LaunchScope),
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
        // MonitoredVariables gets double-line (emphasized)
        _monitoredVariablesView.BorderStyle = theme.EmphasizedBorderStyle;
        ThemeStyler.ApplyToFrame(_monitoredVariablesView, theme);

        // Other panels get single-line (secondary)
        _addressSpaceView.BorderStyle = theme.SecondaryBorderStyle;
        _nodeDetailsView.BorderStyle = theme.SecondaryBorderStyle;
        _logView.BorderStyle = theme.SecondaryBorderStyle;
        ThemeStyler.ApplyToFrame(_addressSpaceView, theme);
        ThemeStyler.ApplyToFrame(_nodeDetailsView, theme);
        ThemeStyler.ApplyToFrame(_logView, theme);

        // Preserve focus highlight on the currently focused panel
        if (_focusedPanel != null)
        {
            UpdatePanelBorder(_focusedPanel, isFocused: true);
        }
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
            UpdateConnectionStatusLabelPosition();
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
        var dialog = new ConnectDialog();
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
                _lastEndpoint = endpoint;
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
        _monitoredVariablesView.Clear();
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
                _logger.Info("Reconnected successfully - subscriptions restored");
            }
            else
            {
                _logger.Error("Reconnection failed");
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
        var variable = _monitoredVariablesView.SelectedVariable;
        if (variable != null)
        {
            OnUnsubscribeRequested(variable);
        }
    }

    private void WriteSelected()
    {
        var variable = _monitoredVariablesView.SelectedVariable;
        if (variable != null)
        {
            OnWriteRequested(variable);
        }
    }

    private void OnNodeSelected(BrowsedNode node)
    {
        _nodeDetailsView.ShowNodeAsync(node).FireAndForget(_logger);
    }

    #region Focus Tracking and Context-Aware UI

    /// <summary>
    /// Handles focus changes to update border highlighting and status bar shortcuts.
    /// Called by FocusManager when the focused pane changes.
    /// </summary>
    private void OnPanelFocusChanged(View? panel)
    {
        // Update border styling for previous panel (remove highlight)
        if (_focusedPanel != null && _focusedPanel != panel)
        {
            UpdatePanelBorder(_focusedPanel, isFocused: false);
        }

        // Update border styling for new panel (add highlight)
        _focusedPanel = panel;
        if (panel != null)
        {
            UpdatePanelBorder(panel, isFocused: true);
        }

        // Update status bar shortcuts based on focused panel
        UpdateStatusBarShortcuts();
    }

    /// <summary>
    /// Updates the border color scheme of a panel based on focus state.
    /// </summary>
    private void UpdatePanelBorder(View panel, bool isFocused)
    {
        var theme = ThemeManager.Current;

        if (panel is FrameView frameView && frameView.Border != null)
        {
            frameView.Border.ColorScheme = isFocused
                ? theme.FocusedBorderColorScheme
                : theme.BorderColorScheme;
            frameView.SetNeedsLayout();
        }
    }

    /// <summary>
    /// Updates the status bar shortcuts based on which panel has focus.
    /// </summary>
    private void UpdateStatusBarShortcuts()
    {
        // Remove existing shortcuts (preserve activity spinner and labels)
        var itemsToRemove = _statusBar.Subviews
            .OfType<Shortcut>()
            .ToList();

        foreach (var item in itemsToRemove)
        {
            _statusBar.Remove(item);
        }

        // Always add global shortcuts first
        _statusBar.Add(new Shortcut(Key.F1, "Help", ShowHelp));
        _statusBar.Add(new Shortcut(Key.Tab, "Switch", () => _focusManager?.FocusNext()));
        _statusBar.Add(new Shortcut(Key.F10, "Menu", () => _menuBar.OpenMenu()));

        // Add context-specific shortcuts based on focused panel
        if (_focusedPanel == _addressSpaceView)
        {
            _statusBar.Add(new Shortcut(Key.Enter, "Subscribe", SubscribeSelected));
            _statusBar.Add(new Shortcut(Key.F5, "Refresh", RefreshTree));
        }
        else if (_focusedPanel == _monitoredVariablesView)
        {
            _statusBar.Add(new Shortcut(Key.Delete, "Unsub", UnsubscribeSelected));
            _statusBar.Add(new Shortcut(Key.Space, "Sel", () => { })); // Handled by view
            _statusBar.Add(new Shortcut(Key.W, "Write", WriteSelected));
            _statusBar.Add(new Shortcut(Key.T, "Trend", ShowTrendForSelected));
            _statusBar.Add(new Shortcut(Key.S, "Scope", LaunchScope));
        }
        else
        {
            // Default: show refresh
            _statusBar.Add(new Shortcut(Key.F5, "Refresh", RefreshTree));
        }

        _statusBar.SetNeedsLayout();
    }

    /// <summary>
    /// Shows trend plot for the selected variable in the monitored view.
    /// </summary>
    private void ShowTrendForSelected()
    {
        var variable = _monitoredVariablesView.SelectedVariable;
        if (variable != null)
        {
            OnTrendPlotRequested(variable);
        }
    }

    #endregion

    #region Global Keyboard Shortcuts

    /// <summary>
    /// Handles global keyboard shortcuts for pane navigation.
    /// </summary>
    protected override bool OnKeyDown(Key key)
    {
        // Tab: Cycle between panes
        if (key == Key.Tab)
        {
            _focusManager?.FocusNext();
            return true;
        }

        return base.OnKeyDown(key);
    }

    #endregion

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

    private void OnValueChanged(MonitoredNode variable)
    {
        // Record to CSV if recording is active AND variable is selected for scope/recording
        if (variable.IsSelectedForScope)
        {
            _csvRecordingManager.RecordValue(variable);
        }

        UiThread.Run(() => _monitoredVariablesView.UpdateVariable(variable));
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

    private void OnAutoReconnectTriggered()
    {
        UiThread.Run(() =>
        {
            _logger.Warning("Connection lost - attempting automatic reconnection...");
            ShowActivity("Reconnecting...");
            StartConnectingAnimation();

            // Start reconnection asynchronously
            ReconnectAsync().FireAndForget(_logger);
        });
    }

    private void UpdateConnectionStatus(bool isConnected)
    {
        _isConnected = isConnected;
        var theme = ThemeManager.Current;

        // Update title (plain text)
        Title = "opcilloscope";

        // Update colored status label in status bar
        _connectionStatusLabel.Text = isConnected
            ? $" {theme.ConnectedIndicator} "
            : $" {theme.DisconnectedIndicator} ";
        UpdateConnectionStatusLabelPosition();
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
            UpdateConnectionStatusLabelPosition();
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

        var selectedNodes = _monitoredVariablesView.ScopeSelectedNodes;

        if (selectedNodes.Count == 0)
        {
            MessageBox.Query("Scope", "Select up to 5 nodes to display in Scope.\nUse Space to toggle selection on monitored variables.", "OK");
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
        if (subscriptionManager == null || !subscriptionManager.MonitoredVariables.Any())
        {
            MessageBox.Query("Record", "No variables to record. Subscribe to variables first.", "OK");
            return;
        }

        // Check that at least one variable is selected for recording
        var selectedCount = _monitoredVariablesView.ScopeSelectionCount;
        if (selectedCount == 0)
        {
            MessageBox.Query("Record",
                "No variables selected for recording.\n\n" +
                "Use Space to select variables in the Sel column (◉).\n" +
                "Selected variables will be recorded and shown in Scope.", "OK");
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
                _monitoredVariablesView.UpdateRecordingStatus($"◉ REC ({selectedCount})", true);
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
        _monitoredVariablesView.UpdateRecordingStatus("", false);
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
                _monitoredVariablesView.UpdateRecordingStatus($"◉ {duration:mm\\:ss}", true);
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
        var dialog = new HelpDialog();
        Application.Run(dialog);
    }

    private void ShowAbout()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        var titleLine = $"opcilloscope v{version}";
        var titlePadded = titleLine.PadLeft((38 + titleLine.Length) / 2).PadRight(38);

        var about = $@"╔══════════════════════════════════════╗
║{titlePadded}║
║      by Square Wave Systems          ║
╚══════════════════════════════════════╝

A lightweight terminal-based OPC UA client
for browsing, monitoring, and visualizing
industrial automation data in real-time.

Features:
  - Multi-signal Scope view (up to 5 signals)
  - Time-based plotting with auto-scale
  - CSV recording of monitored values

Built with:
  - .NET 10
  - Terminal.Gui v2
  - OPC Foundation UA-.NETStandard

© 2026 Square Wave Systems
License: MIT
";
        MessageBox.Query("About opcilloscope", about, "OK");
    }

    #region Configuration File Handling

    /// <summary>
    /// Opens a configuration file, prompting to save changes if necessary.
    /// </summary>
    private void OpenConfig()
    {
        if (_configService.HasUnsavedChanges && !ConfirmDiscardChanges())
            return;

        // Start in the default config directory
        var defaultDir = ConfigurationService.GetDefaultConfigDirectory();

        using var dialog = new OpenDialog
        {
            Title = "Open Configuration",
            AllowedTypes = new List<IAllowedType>
            {
                new AllowedType("opcilloscope Config", ConfigurationService.ConfigFileExtension),
                new AllowedType("Legacy opcilloscope Config", ".opcilloscope"),
                new AllowedType("JSON Files", ".json")
            },
            Path = defaultDir
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
    /// Uses cross-platform default directory and generates filename from connection URL.
    /// The filename is preserved when navigating to different directories.
    /// </summary>
    private void SaveConfigAs()
    {
        // Get the default directory and generate a default filename
        var defaultDir = ConfigurationService.GetDefaultConfigDirectory();
        var defaultFilename = ConfigurationService.GenerateDefaultFilename(_connectionManager.CurrentEndpoint);

        using var dialog = new Dialogs.SaveConfigDialog(defaultDir, defaultFilename);

        Application.Run(dialog);

        if (dialog.Confirmed)
        {
            SaveConfigurationAsync(dialog.FilePath).FireAndForget(_logger);
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
                    _lastEndpoint = config.Server.EndpointUrl;

                    // Only after successful connection, disconnect old and clear views
                    _addressSpaceView.Clear();
                    _monitoredVariablesView.Clear();
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
                _monitoredVariablesView.Clear();
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

            var monitoredVariables = _connectionManager.SubscriptionManager?.MonitoredVariables
                ?? Enumerable.Empty<MonitoredNode>();

            var config = _configService.CaptureCurrentState(
                _connectionManager.CurrentEndpoint,
                _connectionManager.SubscriptionManager?.PublishingInterval ?? 1000,
                monitoredVariables,
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
            ? "opcilloscope"
            : $"opcilloscope - {configName}{unsavedMarker}";

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
            _monitoredVariablesView.RecordToggleRequested -= ToggleRecording;
            _monitoredVariablesView.ScopeRequested -= LaunchScope;

            // Stop focus tracking
            if (_focusManager != null)
            {
                _focusManager.StopTracking();
                _focusManager.FocusChanged -= OnPanelFocusChanged;
            }

            _connectionManager.Dispose();
        }
        base.Dispose(disposing);
    }
}
