using System.Reflection;
using Terminal.Gui;
using Opcilloscope.App.Keybindings;
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
/// Implements lazygit-inspired keybinding system.
/// </summary>
public class MainWindow : Window, DefaultKeybindings.IKeybindingActions
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
    private object? _startupStatusTimer;
    private readonly MenuItem _themeToggleItem;

    // Connecting animation state
    private object? _connectingAnimationTimer;
    private int _connectingDotCount = 1;
    private bool _isConnecting;
    private bool _isConnected;
    private volatile bool _isHydratingConfiguration;
    private int _operationInProgress;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly object _recordingStopLock = new();
    private Task? _recordingStopTask;
    private int _quitInProgress;

    private string? _lastEndpoint;

    // Focus tracking for context-aware UI
    private View? _focusedPanel;
    private FocusManager? _focusManager;

    // Lazygit-inspired keybinding system
    private readonly KeybindingManager _keybindingManager;

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
            if (!_connectionManager.IsConnectionGenerationActive(variable.ConnectionGeneration))
                return;

            UiThread.Run(() =>
            {
                if (_connectionManager.IsConnectionGenerationActive(variable.ConnectionGeneration))
                    _monitoredVariablesView?.AddVariable(variable);
            });
            MarkConfigurationDirty();
        };
        _connectionManager.VariableRemoved += (handle, generation) =>
        {
            if (_connectionManager.ConnectionGeneration != generation)
                return;

            UiThread.Run(() =>
            {
                if (_connectionManager.ConnectionGeneration == generation)
                    _monitoredVariablesView?.RemoveVariable(handle);
            });
            MarkConfigurationDirty();
        };

        // Wire up configuration service events
        _configService.UnsavedChangesStateChanged += _ => UiThread.Run(UpdateWindowTitle);
        // Override global "Menu" ColorScheme BEFORE creating any views
        // This prevents StatusBar's blue background flash on first render
        var theme = ThemeManager.Current;
        SchemeManager.AddScheme("Menu", ThemeStyler.CreateFlatBarScheme(theme));

        // Create theme toggle menu item
        _themeToggleItem = new MenuItem(GetThemeToggleTitle(), "", ToggleTheme);

        // Subscribe to theme changes
        ThemeManager.ThemeChanged += OnThemeChanged;

        // Set initial window title (status shown in status bar)
        Title = " opcilloscope ";

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
        _statusBar.SetScheme(ThemeStyler.CreateFlatBarScheme(theme));

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
        _monitoredVariablesView.RecordToggleRequested += ToggleRecording;
        _monitoredVariablesView.SelectedVariableChanged += OnMonitoredVariableSelected;

        // Initialize lazygit-inspired keybinding system
        _keybindingManager = new KeybindingManager();
        DefaultKeybindings.Configure(_keybindingManager, this);

        // Intercept letter/symbol keys at application level before views consume them
        TerminalUi.AddKeyDownHandler(OnApplicationKeyDown);

        // Focus tracking using polling-based FocusManager (workaround for Terminal.Gui v2 Enter event instability)
        // Only track the two interactive panes (AddressSpace and MonitoredVariables)
        _focusManager = new FocusManager(_addressSpaceView, _monitoredVariablesView);
        _focusManager.FocusChanged += OnPanelFocusChanged;

        // Initialize views
        _logView.Initialize(_logger);
        _nodeDetailsView.Initialize(_connectionManager.NodeBrowser, _logger);

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

        // Handle window resize to update connection status label position.
        // Terminal.Gui 2.4 removed Application.SizeChanging; the window's own
        // SubViewLayout fires whenever the terminal (and thus this window) is re-laid out.
        SubViewLayout += (s, e) => UiThread.Run(UpdateConnectionStatusLabelPosition);

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
        _connectionStatusLabel.SetScheme(new Scheme
        {
            Normal = new Attribute(theme.Accent, theme.Background)
        });
        UpdateConnectionStatusLabelPosition();

        _startupStatusTimer = TerminalUi.AddTimeout(TimeSpan.FromSeconds(1), () =>
        {
            step++;
            if (step == 1)
            {
                // Second message
                _connectionStatusLabel.Text = " All systems nominal ";
                _connectionStatusLabel.SetScheme(new Scheme
                {
                    Normal = new Attribute(theme.StatusGood, theme.Background)
                });
                UpdateConnectionStatusLabelPosition();
                return true; // Continue
            }
            else
            {
                // Final state reflects the live connection. A fast CLI-config
                // connection may complete before the startup banner does.
                UpdateConnectionStatus(_connectionManager.IsConnected);
                _startupStatusTimer = null; // Timer self-removes after returning false
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
                    new MenuItem("_Open Config...", "", OpenConfig, Key.O.WithCtrl),
                    new MenuItem("_Save Config", "", SaveConfig, Key.S.WithCtrl),
                    new MenuItem("Save Config _As...", "", SaveConfigAs, Key.S.WithCtrl.WithShift),
                    null!, // Separator
                    new MenuItem("Toggle Recording", "", ToggleRecording, Key.R.WithCtrl),
                    null!, // Separator
                    new MenuItem("E_xit", "", RequestQuit, Key.Q.WithCtrl)
                }),
                new MenuBarItem("_Connection", new MenuItem[]
                {
                    new MenuItem("_Connect...", "", ShowConnectDialog),
                    new MenuItem("_Disconnect", "", () => DisconnectAsync().FireAndForget(_logger)),
                    new MenuItem("_Reconnect", "", () => ReconnectAsync().FireAndForget(_logger))
                }),
                new MenuBarItem("_View", new MenuItem[]
                {
                    new MenuItem("_Scope", "s", LaunchScope),
                    new MenuItem("_Refresh Tree", "r", RefreshTree),
                    new MenuItem("_Clear Log", "", () => _logView.Clear()),
                    _themeToggleItem
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
        SchemeManager.AddScheme("Menu", ThemeStyler.CreateFlatBarScheme(theme));

        // Apply main window styling - double-line for emphasis
        SetScheme(theme.MainColorScheme);
        BorderStyle = theme.EmphasizedBorderStyle;

        // NOTE: Terminal.Gui 2.4 removed per-adornment schemes, so the main window border
        // can no longer be given the distinct HighlightTitleBorderColorScheme; it inherits
        // the window scheme. Title-highlight colouring to be revisited via Scheme VisualRoles.

        // Apply styling to menu bar
        ThemeStyler.ApplyToMenuBar(_menuBar, theme);

        // Apply clean status bar styling (no blue background)
        // Must set ColorScheme AND call SetNeedsDisplay to override Terminal.Gui defaults
        var cleanStatusBarScheme = ThemeStyler.CreateFlatBarScheme(theme);
        _statusBar.SetScheme(cleanStatusBarScheme);
        _statusBar.SetNeedsLayout();

        // Also apply theme to connection status label
        UpdateConnectionStatusLabelStyle(_isConnected);

        // Apply theme to activity spinner and label (for async operations)
        _activitySpinner.SetScheme(cleanStatusBarScheme);
        _activityLabel.SetScheme(cleanStatusBarScheme);

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
        // Show what clicking will do: cycle Dark -> Light -> Terminal -> Dark
        var themes = ThemeManager.AvailableThemes;
        var nextIndex = (ThemeManager.GetCurrentThemeIndex() + 1) % themes.Count;
        return $"Switch to _{themes[nextIndex].Name}";
    }

    private void ToggleTheme()
    {
        var newIndex = (ThemeManager.GetCurrentThemeIndex() + 1) % ThemeManager.AvailableThemes.Count;
        ThemeManager.SetThemeByIndex(newIndex);
    }

    private void ShowConnectDialog()
    {
        if (RejectInteractiveMutationWhileBusy("connect")) return;

        var currentInterval = _connectionManager.SubscriptionManager?.PublishingInterval ?? 250;
        var currentCredentials = _connectionManager.Credentials;
        using var dialog = new ConnectDialog(
            _lastEndpoint,
            currentInterval,
            currentCredentials.Type,
            currentCredentials.Username);
        TerminalUi.RunModal(dialog);

        if (dialog.Confirmed)
        {
            var credentials = dialog.SelectedAuthType == AuthenticationType.UserName
                ? new ConnectionCredentials(AuthenticationType.UserName, dialog.Username, dialog.Password)
                : ConnectionCredentials.Anonymous;
            ConnectAsync(dialog.EndpointUrl, dialog.PublishingInterval, credentials).FireAndForget(_logger);
        }
    }

    private Task ConnectAsync(string endpoint, int publishingInterval = 250, ConnectionCredentials? credentials = null)
    {
        var operationGeneration = _connectionManager.RegisterExplicitLifecycleIntent();
        return RunExclusiveOperationAsync(
            () => ConnectCoreAsync(
                endpoint,
                publishingInterval,
                credentials,
                operationGeneration));
    }

    private async Task ConnectCoreAsync(
        string endpoint,
        int publishingInterval,
        ConnectionCredentials? credentials,
        long operationGeneration)
    {
        // Disconnect if already connected
        if (!await DisconnectCoreAsync(operationGeneration))
            return;

        await UiThread.RunAsync(() =>
        {
            StartConnectingAnimation();
            ShowActivity("Connecting...");
        });

        try
        {
            var success = await _connectionManager.ConnectWithIntentAsync(
                endpoint,
                publishingInterval,
                credentials,
                securityMode: null,
                securityPolicy: null,
                samplingInterval: 250,
                queueSize: 10,
                operationGeneration);

            if (success)
            {
                _lastEndpoint = endpoint;
                MarkConfigurationDirty();
                // The connect continuation may resume off the UI thread.
                UiThread.Run(() => _addressSpaceView.Initialize(_connectionManager.NodeBrowser));
            }
        }
        finally
        {
            UiThread.Run(() =>
            {
                StopConnectingAnimation();
                HideActivity();
            });
        }
    }

    private Task DisconnectAsync()
    {
        // Signal explicit user intent before waiting on the UI operation gate so an
        // active automatic reconnect is cancelled immediately rather than allowed to
        // publish Connected first.
        var operationGeneration = _connectionManager.RegisterExplicitLifecycleIntent();
        return RunExclusiveOperationAsync(
            () => DisconnectCoreAsync(operationGeneration));
    }

    private async Task<bool> DisconnectCoreAsync(long? operationGeneration = null)
    {
        var hadLiveState = _connectionManager.IsConnected
            || _connectionManager.SubscriptionManager?.MonitoredVariables.Any() == true;

        // This method is also called from async config-load continuations, so
        // marshal the pre-await recording/timer/dialog work as well as cleanup.
        if (_csvRecordingManager.IsRecording || _csvRecordingManager.IsStopping)
        {
            await StopRecordingAndReportAsync();
        }

        // Async close avoids blocking the UI thread on the OPC UA round-trip.
        var disconnected = true;
        if (operationGeneration.HasValue)
        {
            disconnected = await _connectionManager
                .DisconnectWithIntentAsync(operationGeneration.Value);
        }
        else
        {
            await _connectionManager.DisconnectAsync();
        }

        if (!disconnected)
            return false;

        // The disconnect continuation may resume off the UI thread, so marshal
        // the view updates back onto it.
        await UiThread.RunAsync(() =>
        {
            _addressSpaceView.Clear();
            _monitoredVariablesView.Clear();
            _nodeDetailsView.Clear();

            UpdateConnectionStatus(isConnected: false);
        });

        if (hadLiveState)
        {
            MarkConfigurationDirty();
        }

        return true;
    }

    private Task ReconnectAsync()
    {
        var operationGeneration = _connectionManager.RegisterExplicitLifecycleIntent();
        return RunExclusiveOperationAsync(
            () => ReconnectCoreAsync(explicitOperationGeneration: operationGeneration));
    }

    private async Task ReconnectCoreAsync(
        long? automaticIntentVersion = null,
        long? explicitOperationGeneration = null)
    {
        if (string.IsNullOrEmpty(_lastEndpoint))
        {
            _logger.Warning("No previous connection to reconnect");
            return;
        }

        await UiThread.RunAsync(() =>
        {
            StartConnectingAnimation();
            ShowActivity("Reconnecting...");
        });

        try
        {
            var success = automaticIntentVersion.HasValue
                ? await _connectionManager.ReconnectAutomaticallyAsync(automaticIntentVersion.Value)
                : explicitOperationGeneration.HasValue
                    && await _connectionManager.ReconnectWithIntentAsync(
                        explicitOperationGeneration.Value);

            if (success)
            {
                var monitoredVariables = _connectionManager.SubscriptionManager?
                    .MonitoredVariables
                    .ToList()
                    ?? new List<MonitoredNode>();
                // Reconcile membership before releasing the UI operation gate. A
                // subscribe/unsubscribe that committed while reconnect intent advanced
                // intentionally had its stale event dropped; this authoritative snapshot
                // repairs the table without losing scope/recording selections.
                await UiThread.RunAsync(() =>
                {
                    _addressSpaceView.Initialize(_connectionManager.NodeBrowser);
                    _monitoredVariablesView.ReconcileVariables(monitoredVariables);
                });
                _logger.Info("Reconnected successfully - subscriptions restored");
            }
            else
            {
                _logger.Error("Reconnection failed");
            }
        }
        finally
        {
            UiThread.Run(() =>
            {
                StopConnectingAnimation();
                HideActivity();
            });
        }
    }

    private void RefreshTree()
    {
        if (RejectInteractiveMutationWhileBusy("refresh the address space")) return;

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
        if (RejectInteractiveMutationWhileBusy("write a value")) return;

        if (!_connectionManager.IsConnected)
        {
            _logger.Warning("Not connected");
            return;
        }

        if (_keybindingManager.CurrentContext == KeybindingContext.MonitoredVariables)
        {
            var variable = _monitoredVariablesView.SelectedVariable;
            if (variable == null) return;
            WriteToMonitoredVariable(variable);
        }
        else if (_keybindingManager.CurrentContext == KeybindingContext.AddressSpace)
        {
            var node = _addressSpaceView.SelectedNode;
            if (node == null) return;
            if (node.NodeClass != Opc.Ua.NodeClass.Variable)
            {
                _logger.Warning($"Cannot write to {node.NodeClass} nodes, only Variables");
                return;
            }
            WriteToAddressSpaceNodeAsync(node).FireAndForget(_logger);
        }
    }

    private void WriteToMonitoredVariable(MonitoredNode variable)
    {
        if (!variable.IsWritable)
        {
            _logger.Warning($"Node '{variable.DisplayName}' is not writable");
            TerminalUi.ErrorQuery("Write", $"Node '{variable.DisplayName}' is not writable.", "OK");
            return;
        }

        if (!variable.IsScalar)
        {
            _logger.Warning($"Array writes are not supported for node '{variable.DisplayName}'");
            TerminalUi.ErrorQuery("Write", "Array writes are not currently supported.", "OK");
            return;
        }

        if (!OpcValueConverter.IsWriteSupported(variable.DataType))
        {
            _logger.Warning($"Write not supported for data type {variable.DataType}");
            TerminalUi.ErrorQuery("Write", $"Write not supported for data type: {variable.DataType}", "OK");
            return;
        }

        OpenWriteDialogAndWrite(
            variable.NodeId,
            variable.DisplayName,
            variable.DataType,
            variable.DataTypeName,
            variable.Value,
            variable.ConnectionGeneration);
    }

    private async Task WriteToAddressSpaceNodeAsync(BrowsedNode node)
    {
        var connectionGeneration = node.ConnectionGeneration;
        byte userAccessLevel = 0;
        // Fail closed until the server confirms the node is scalar. Treating a
        // failed ValueRank read as scalar could offer an unsupported array write.
        int valueRank = Opc.Ua.ValueRanks.Any;
        Opc.Ua.BuiltInType builtInType = Opc.Ua.BuiltInType.Variant;
        string dataTypeName = "Unknown";
        string? currentValue = null;

        try
        {
            var snapshot = await _connectionManager.ReadWriteSnapshotAsync(
                node.NodeId,
                connectionGeneration,
                Opc.Ua.Attributes.UserAccessLevel,
                Opc.Ua.Attributes.DataType,
                Opc.Ua.Attributes.ValueRank);
            if (!snapshot.HasValue)
            {
                _logger.Warning("Write cancelled because the connection changed");
                return;
            }

            var attrs = snapshot.Value.Attributes;

            if (attrs.Count >= 3)
            {
                if (Opc.Ua.StatusCode.IsGood(attrs[0].StatusCode) && attrs[0].Value is byte al)
                    userAccessLevel = al;

                if (Opc.Ua.StatusCode.IsGood(attrs[1].StatusCode) && attrs[1].Value is Opc.Ua.NodeId dataTypeNodeId)
                {
                    (builtInType, dataTypeName) = DataTypeResolver.Resolve(dataTypeNodeId);
                }

                if (Opc.Ua.StatusCode.IsGood(attrs[2].StatusCode) && attrs[2].Value is int rank)
                    valueRank = rank;
            }

            var dv = snapshot.Value.Value;
            currentValue = dv?.Value?.ToString();
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to read attributes for write: {ex.Message}");
            return;
        }

        if ((userAccessLevel & Opc.Ua.AccessLevels.CurrentWrite) == 0)
        {
            _logger.Warning($"Node '{node.DisplayName}' is not writable");
            UiThread.Run(() => TerminalUi.ErrorQuery("Write", $"Node '{node.DisplayName}' is not writable.", "OK"));
            return;
        }

        if (valueRank != Opc.Ua.ValueRanks.Scalar)
        {
            _logger.Warning($"Array writes are not supported for node '{node.DisplayName}'");
            UiThread.Run(() => TerminalUi.ErrorQuery("Write", "Array writes are not currently supported.", "OK"));
            return;
        }

        if (!OpcValueConverter.IsWriteSupported(builtInType))
        {
            _logger.Warning($"Write not supported for data type {builtInType}");
            UiThread.Run(() => TerminalUi.ErrorQuery("Write", $"Write not supported for data type: {builtInType}", "OK"));
            return;
        }

        UiThread.Run(() =>
        {
            if (_connectionManager.ConnectionGeneration != connectionGeneration)
            {
                _logger.Warning("Write cancelled because the connection changed");
                return;
            }

            OpenWriteDialogAndWrite(
                node.NodeId,
                node.DisplayName,
                builtInType,
                dataTypeName,
                currentValue,
                connectionGeneration);
        });
    }

    private void OpenWriteDialogAndWrite(
        Opc.Ua.NodeId nodeId,
        string displayName,
        Opc.Ua.BuiltInType dataType,
        string dataTypeName,
        string? currentValue,
        long connectionGeneration)
    {
        using var dialog = new WriteValueDialog(nodeId, displayName, dataType, dataTypeName, currentValue);
        TerminalUi.RunModal(dialog);

        if (!dialog.Confirmed || dialog.ParsedValue == null) return;

        var parsedValue = dialog.ParsedValue;
        PerformWriteAsync(nodeId, displayName, parsedValue, connectionGeneration).FireAndForget(_logger);
    }

    private async Task PerformWriteAsync(
        Opc.Ua.NodeId nodeId,
        string displayName,
        object value,
        long connectionGeneration)
    {
        var status = await _connectionManager.WriteValueAsync(nodeId, value, connectionGeneration);
        if (Opc.Ua.StatusCode.IsGood(status))
        {
            _logger.Info($"Wrote {value} to {displayName}");
        }
        else
        {
            _logger.Error($"Write failed for {displayName}: 0x{status.Code:X8}");
        }
    }

    private void OnNodeSelected(BrowsedNode node)
    {
        _nodeDetailsView.ShowNodeAsync(node).FireAndForget(_logger);
    }

    private void OnMonitoredVariableSelected(MonitoredNode node)
    {
        _nodeDetailsView
            .ShowNodeByIdAsync(node.NodeId, node.ConnectionGeneration)
            .FireAndForget(_logger);
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

        // Update keybinding context based on focused panel
        _keybindingManager.CurrentContext = panel switch
        {
            AddressSpaceView => KeybindingContext.AddressSpace,
            MonitoredVariablesView => KeybindingContext.MonitoredVariables,
            _ => KeybindingContext.Global
        };

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
            // Terminal.Gui 2.4 adornments have no independent scheme; the border/title render
            // from the FrameView's own scheme, so apply the focus scheme to the frame itself.
            frameView.SetScheme(isFocused
                ? theme.FocusedBorderColorScheme
                : theme.BorderColorScheme);
            frameView.SetNeedsLayout();
        }
    }

    /// <summary>
    /// Updates the status bar shortcuts based on which panel has focus.
    /// Uses the KeybindingManager for context-aware shortcuts (lazygit-inspired).
    /// </summary>
    private void UpdateStatusBarShortcuts()
    {
        // Remove existing shortcuts (preserve activity spinner and labels)
        var itemsToRemove = _statusBar.SubViews
            .OfType<Shortcut>()
            .ToList();

        foreach (var item in itemsToRemove)
        {
            _statusBar.Remove(item);
        }

        // Add shortcuts from keybinding manager (context-aware)
        foreach (var binding in _keybindingManager.GetStatusBarBindings())
        {
            _statusBar.Add(new Shortcut(binding.Key, binding.Label, binding.Handler));
        }

        _statusBar.SetNeedsLayout();
    }

    #endregion

    #region Global Keyboard Shortcuts

    /// <summary>
    /// Application-level key handler that intercepts letter/symbol keys before
    /// any view (TreeView, TableView) can consume them for type-ahead search.
    /// Navigation keys (Enter, Space, arrows, etc.) are excluded so that local
    /// view handlers continue to work for those.
    /// </summary>
    private void OnApplicationKeyDown(object? sender, Key e)
    {
        if (e.Handled) return;
        if (!TerminalUi.IsTopRunnable(this)) return; // Don't fire during dialogs

        if (IsViewNavigationKey(e)) return; // Let Enter/Space/etc reach local handlers

        if (_keybindingManager.TryHandle(e))
            e.Handled = true;
    }

    private static bool IsViewNavigationKey(Key key)
    {
        var baseCode = key.KeyCode & ~KeyCode.ShiftMask & ~KeyCode.CtrlMask & ~KeyCode.AltMask;
        return baseCode is KeyCode.Enter or KeyCode.Space or KeyCode.Tab or
               KeyCode.Delete or KeyCode.Backspace or KeyCode.Esc or
               KeyCode.CursorUp or KeyCode.CursorDown or
               KeyCode.CursorLeft or KeyCode.CursorRight or
               KeyCode.Home or KeyCode.End or
               KeyCode.PageUp or KeyCode.PageDown
            || (baseCode >= KeyCode.F1 && baseCode <= KeyCode.F12);
    }

    /// <summary>
    /// Fallback handler for navigation keys that aren't intercepted at application level.
    /// All keybindings are centralized in <see cref="Keybindings.DefaultKeybindings"/>.
    /// </summary>
    protected override bool OnKeyDown(Key key)
    {
        // A Window's default Esc handling requests application stop. Route it
        // through the same unsaved-change guard as Ctrl+Q and the File menu.
        if (IsQuitKey(key))
        {
            RequestQuit();
            return true;
        }

        // Use the centralized keybinding manager for all key handling
        if (_keybindingManager.TryHandle(key))
        {
            return true;
        }

        return base.OnKeyDown(key);
    }

    internal static bool IsQuitKey(Key key) => key.KeyCode == KeyCode.Esc;

    #endregion

    private void OnSubscribeRequested(BrowsedNode node)
    {
        if (RejectInteractiveMutationWhileBusy("change subscriptions")) return;

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

        _connectionManager
            .SubscribeAsync(node.NodeId, node.DisplayName, node.ConnectionGeneration)
            .FireAndForget(_logger);
    }

    private void OnUnsubscribeRequested(MonitoredNode item)
    {
        if (RejectInteractiveMutationWhileBusy("change subscriptions")) return;

        _connectionManager
            .UnsubscribeAsync(item.ClientHandle, item.ConnectionGeneration)
            .FireAndForget(_logger);
    }

    private void OnValueChanged(MonitoredNode variable)
    {
        if (!_connectionManager.IsConnectionGenerationActive(variable.ConnectionGeneration))
            return;

        // Record to CSV if recording is active AND variable is selected for scope/recording
        if (variable.IsSelectedForScope && !variable.IsSyntheticValue)
        {
            _csvRecordingManager.RecordValue(variable);
        }

        UiThread.Run(() =>
        {
            if (_connectionManager.IsConnectionGenerationActive(variable.ConnectionGeneration))
                _monitoredVariablesView.UpdateVariable(variable);
        });
    }

    private void OnConnectionStateChanged(ConnectionState state)
    {
        UiThread.Run(() =>
        {
            CancelStartupStatus();
            var isConnected = state == ConnectionState.Connected;
            UpdateConnectionStatus(isConnected);

            if (isConnected)
            {
                _logger.Info($"Connected to {_connectionManager.CurrentEndpoint}");
            }
        });
    }

    private void OnConnectionError(string message)
    {
        UiThread.Run(() =>
        {
            TerminalUi.ErrorQuery("Connection Error", message, "OK");
        });
    }

    private void OnAutoReconnectTriggered(long intentVersion)
    {
        UiThread.Run(() =>
        {
            _logger.Warning("Connection lost - attempting automatic reconnection...");
            RunExclusiveOperationAsync(() => ReconnectCoreAsync(intentVersion)).FireAndForget(_logger);
        });
    }

    private void CancelStartupStatus()
    {
        if (_startupStatusTimer is null)
        {
            return;
        }

        TerminalUi.RemoveTimeout(_startupStatusTimer);
        _startupStatusTimer = null;
    }

    private void MarkConfigurationDirty()
    {
        if (_isHydratingConfiguration)
        {
            return;
        }

        _configService.MarkDirty();
        UiThread.Run(UpdateWindowTitle);
    }

    private void UpdateConnectionStatus(bool isConnected)
    {
        _isConnected = isConnected;
        var theme = ThemeManager.Current;

        // Update title (plain text)
        Title = " opcilloscope ";

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
        _connectionStatusLabel.SetScheme(new Scheme
        {
            Normal = new Attribute(
                isConnected ? theme.StatusGood : theme.Accent,
                theme.Background),
            Focus = new Attribute(
                isConnected ? theme.StatusGood : theme.Accent,
                theme.Background),
            HotNormal = new Attribute(
                isConnected ? theme.StatusGood : theme.Accent,
                theme.Background),
            HotFocus = new Attribute(
                isConnected ? theme.StatusGood : theme.Accent,
                theme.Background)
        });
    }

    private void ToggleRecording()
    {
        if (RejectInteractiveMutationWhileBusy("change recording state")) return;

        if (_csvRecordingManager.IsRecording)
        {
            OnStopRecordingRequested();
        }
        else if (_csvRecordingManager.IsStopping)
        {
            TerminalUi.Query(
                "Recording",
                "The previous recording is still flushing to storage. Please wait.",
                "OK");
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
        _connectingAnimationTimer = TerminalUi.AddTimeout(TimeSpan.FromMilliseconds(400), () =>
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
            TerminalUi.RemoveTimeout(_connectingAnimationTimer);
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

    private void LaunchScope()
    {
        if (_connectionManager.SubscriptionManager == null)
        {
            TerminalUi.Query("Scope", "Connect to a server first.", "OK");
            return;
        }

        var selectedNodes = _monitoredVariablesView.ScopeSelectedNodes;

        if (selectedNodes.Count == 0)
        {
            TerminalUi.Query("Scope", "Select up to 5 nodes to display in Scope.\nUse Space to toggle selection on monitored variables.", "OK");
            return;
        }

        using var dialog = new ScopeDialog(selectedNodes, _connectionManager.SubscriptionManager);
        TerminalUi.RunModal(dialog);
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
            TerminalUi.Query("Record", "No variables to record. Subscribe to variables first.", "OK");
            return;
        }

        // Check that at least one variable is selected for recording
        var selectedCount = _monitoredVariablesView.ScopeSelectionCount;
        if (selectedCount == 0)
        {
            TerminalUi.Query("Record",
                "No variables selected for recording.\n\n" +
                "Use Space to select variables in the Sel column (◉).\n" +
                "Selected variables will be recorded and shown in Scope.", "OK");
            return;
        }

        // Get default directory and generate filename
        var defaultDir = CsvRecordingManager.EnsureRecordingsDirectory();
        var defaultFilename = CsvRecordingManager.GenerateDefaultRecordingFilename(
            _connectionManager.CurrentEndpoint,
            selectedCount);

        using var dialog = new SaveRecordingDialog(defaultDir, defaultFilename);
        TerminalUi.RunModal(dialog);

        if (dialog.Confirmed && dialog.FilePath != null)
        {
            if (_csvRecordingManager.StartRecording(dialog.FilePath))
            {
                _monitoredVariablesView.UpdateRecordingStatus($"◉ REC ({selectedCount})", true);
                StartRecordingStatusUpdates();
            }
            else
            {
                TerminalUi.ErrorQuery("Recording Error", "Failed to start recording", "OK");
            }
        }
    }

    private void OnStopRecordingRequested()
        => StopRecordingAndReportAsync().FireAndForget(_logger);

    private Task StopRecordingAndReportAsync()
    {
        lock (_recordingStopLock)
        {
            if (_recordingStopTask is { IsCompleted: false })
            {
                return _recordingStopTask;
            }

            _recordingStopTask = StopRecordingAndReportCoreAsync();
            return _recordingStopTask;
        }
    }

    private async Task StopRecordingAndReportCoreAsync()
    {
        if (!_csvRecordingManager.IsRecording && !_csvRecordingManager.IsStopping)
        {
            return;
        }

        await UiThread.RunAsync(() =>
        {
            StopRecordingStatusUpdates();
            _monitoredVariablesView.UpdateRecordingStatus("Finishing...", true);
        });

        // Storage work is awaited asynchronously, so a slow disk never freezes
        // the terminal UI. Do not claim success until the writer has closed.
        var result = await _csvRecordingManager
            .StopRecordingAsync(System.Threading.Timeout.InfiniteTimeSpan)
            .ConfigureAwait(false);

        await UiThread.RunAsync(() =>
        {
            _monitoredVariablesView.UpdateRecordingStatus("", false);

            if (!result.Completed)
            {
                TerminalUi.ErrorQuery(
                    "Recording Incomplete",
                    "The recording file is still open and has not finished writing.",
                    "OK");
                return;
            }

            if (result.HasDataLoss)
            {
                var error = string.IsNullOrEmpty(result.ErrorMessage)
                    ? string.Empty
                    : $"\nStorage error: {result.ErrorMessage}";
                TerminalUi.ErrorQuery(
                    "Recording Incomplete",
                    $"{result.RecordCount} records were written.\n" +
                    $"{result.DroppedRecordCount} records were dropped because the queue was full.\n" +
                    $"{result.FailedRecordCount} records failed during writing.{error}",
                    "OK");
                return;
            }

            TerminalUi.Query(
                "Recording",
                $"Recording saved.\n{result.RecordCount} records written.",
                "OK");
        });
    }

    private void StartRecordingStatusUpdates()
    {
        // Use the UI main-loop timer for periodic updates
        _recordingStatusTimer = TerminalUi.AddTimeout(TimeSpan.FromSeconds(1), () =>
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
            TerminalUi.RemoveTimeout(_recordingStatusTimer);
            _recordingStatusTimer = null;
        }
    }

    private void ShowHelp()
    {
        using var dialog = new HelpDialog(_keybindingManager);
        TerminalUi.RunModal(dialog);
    }

    private void ShowAbout()
    {
        var version = GetDisplayVersion();
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
        TerminalUi.Query("About opcilloscope", about, "OK");
    }

    /// <summary>
    /// Gets the application version for display. MinVer writes the full semver to
    /// <see cref="AssemblyInformationalVersionAttribute"/> (AssemblyVersion is frozen at
    /// MAJOR.0.0.0), so prefer that and strip any "+commitsha" build metadata.
    /// </summary>
    private static string GetDisplayVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrEmpty(informational))
        {
            var metadataIndex = informational.IndexOf('+');
            return metadataIndex >= 0 ? informational[..metadataIndex] : informational;
        }

        // Fall back to the assembly version if the attribute is missing
        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    #region Configuration File Handling

    /// <summary>
    /// Opens a configuration file, prompting to save changes if necessary.
    /// </summary>
    private void OpenConfig()
    {
        if (RejectInteractiveMutationWhileBusy("open a configuration")) return;

        if (_configService.HasUnsavedChanges && !ConfirmDiscardChanges())
            return;

        using var dialog = new Dialogs.OpenConfigDialog();

        TerminalUi.RunModal(dialog);

        if (dialog.Confirmed && dialog.SelectedFilePath != null)
        {
            LoadConfigurationAsync(dialog.SelectedFilePath).FireAndForget(_logger);
        }
    }

    /// <summary>
    /// Saves the current configuration to the current file path.
    /// </summary>
    private void SaveConfig()
    {
        if (RejectInteractiveMutationWhileBusy("save the configuration")) return;

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
        if (RejectInteractiveMutationWhileBusy("save the configuration")) return;

        // Get the default directory and generate a default filename
        var defaultDir = ConfigurationService.GetDefaultConfigDirectory();
        var defaultFilename = ConfigurationService.GenerateDefaultFilename(_connectionManager.CurrentEndpoint);

        using var dialog = new Dialogs.SaveConfigDialog(defaultDir, defaultFilename);

        TerminalUi.RunModal(dialog);

        if (dialog.Confirmed)
        {
            SaveConfigurationAsync(dialog.FilePath).FireAndForget(_logger);
        }
    }

    /// <summary>
    /// Loads a configuration from the specified file path.
    /// </summary>
    private Task LoadConfigurationAsync(string filePath)
    {
        var expectedIntentVersion = _connectionManager.ConnectionIntentVersion;
        return RunExclusiveOperationAsync(
            () => LoadConfigurationCoreAsync(filePath, expectedIntentVersion));
    }

    private async Task LoadConfigurationCoreAsync(
        string filePath,
        long expectedIntentVersion)
    {
        long? registeredGeneration = null;
        var sessionTeardownCompleted = false;
        _isHydratingConfiguration = true;
        try
        {
            await UiThread.RunAsync(() => ShowActivity("Loading configuration..."));
            _logger.Info($"Loading configuration from {filePath}...");

            var config = await _configService.LoadAsync(filePath);

            // Connect to server and subscribe to nodes
            if (!string.IsNullOrEmpty(config.Server.EndpointUrl))
            {
                // Build credentials from config (prompt for password if needed)
                ConnectionCredentials? credentials = null;
                var authType = ConnectionCredentials.ParseAuthType(config.Server.Authentication.Type);
                if (authType == AuthenticationType.UserName
                    && !string.IsNullOrEmpty(config.Server.Authentication.Username))
                {
                    // This continuation may resume off the UI thread, so run the
                    // modal prompt via the UI loop and await its outcome.
                    var (confirmed, password) = await UiThread.RunAsync(() =>
                    {
                        using var pwDialog = new PasswordPromptDialog(
                            config.Server.Authentication.Username,
                            config.Server.EndpointUrl);
                        TerminalUi.RunModal(pwDialog);
                        return (pwDialog.Confirmed, pwDialog.Password);
                    });

                    if (!confirmed)
                    {
                        // Nothing was torn down, but the load already switched the Ctrl+S
                        // target to this file; revert to untitled so a save cannot write
                        // the still-running session's state over it.
                        _configService.Reset();
                        UiThread.Run(UpdateWindowTitle);
                        _logger.Info("Password prompt cancelled - skipping connection");
                        return;
                    }

                    credentials = new ConnectionCredentials(
                        AuthenticationType.UserName,
                        config.Server.Authentication.Username,
                        password);
                }

                // Tear down the current session first: stops any active recording and
                // clears the views, so the UI cannot keep showing dead rows from the
                // old server while (or after) the new connection is attempted.
                if (!_connectionManager.TryRegisterExplicitLifecycleIntent(
                        expectedIntentVersion,
                        out var operationGeneration))
                {
                    AbortLoadedConfigurationForNewerConnectionIntent();
                    return;
                }

                registeredGeneration = operationGeneration;
                if (!await DisconnectCoreAsync(operationGeneration))
                {
                    AbortLoadedConfigurationForNewerConnectionIntent();
                    return;
                }
                sessionTeardownCompleted = true;

                // Honor the config's security and subscription settings (the connect dialog
                // has no UI for these, so the config file is their only source).
                var connected = await _connectionManager.ConnectWithIntentAsync(
                    config.Server.EndpointUrl,
                    config.Settings.PublishingIntervalMs,
                    credentials,
                    config.Server.SecurityMode,
                    config.Server.SecurityPolicy,
                    config.Settings.SamplingIntervalMs,
                    config.Settings.QueueSize,
                    operationGeneration);

                if (connected)
                {
                    _lastEndpoint = config.Server.EndpointUrl;
                    _currentMetadata = config.Metadata;

                    UiThread.Run(() => _addressSpaceView.Initialize(_connectionManager.NodeBrowser));

                    // Subscribe to saved nodes
                    var allSubscriptionsRestored = true;
                    foreach (var node in config.MonitoredNodes.Where(n => n.Enabled))
                    {
                        try
                        {
                            var nodeId = Opc.Ua.NodeId.Parse(node.NodeId);
                            var restored = await _connectionManager.SubscribeAsync(nodeId, node.DisplayName);
                            if (restored is null)
                            {
                                allSubscriptionsRestored = false;
                                _logger.Warning($"Failed to subscribe to {node.DisplayName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            allSubscriptionsRestored = false;
                            _logger.Warning($"Failed to subscribe to {node.DisplayName}: {ex.Message}");
                        }
                    }

                    _recentFiles.Add(filePath);
                    if (allSubscriptionsRestored)
                    {
                        _configService.MarkClean();
                    }
                    else
                    {
                        _configService.MarkDirty();
                    }
                    UiThread.Run(UpdateWindowTitle);

                    var nodeCount = config.MonitoredNodes.Count(n => n.Enabled);
                    _logger.Info($"Configuration loaded: {nodeCount} nodes");
                }
                else
                {
                    // Revert to untitled: leaving the failed file as the Ctrl+S target
                    // would let a save overwrite it with the now-empty session state.
                    _configService.Reset();
                    _currentMetadata = null;

                    _logger.Error($"Failed to connect to {config.Server.EndpointUrl}");
                    UiThread.Run(() =>
                    {
                        UpdateWindowTitle();
                        TerminalUi.ErrorQuery("Connection Failed",
                            $"Could not connect to server:\n{config.Server.EndpointUrl}\n\nThe previous connection has been closed. Use Connect to reconnect.",
                            "OK");
                    });
                }
            }
            else
            {
                // No endpoint URL: tear down any current session (stops recording,
                // clears views) and just adopt the loaded settings.
                if (!_connectionManager.TryRegisterExplicitLifecycleIntent(
                        expectedIntentVersion,
                        out var operationGeneration))
                {
                    AbortLoadedConfigurationForNewerConnectionIntent();
                    return;
                }

                registeredGeneration = operationGeneration;
                if (!await DisconnectCoreAsync(operationGeneration))
                {
                    AbortLoadedConfigurationForNewerConnectionIntent();
                    return;
                }
                sessionTeardownCompleted = true;

                _currentMetadata = config.Metadata;
                _recentFiles.Add(filePath);
                _configService.MarkClean();
                UiThread.Run(UpdateWindowTitle);
                _logger.Info("Configuration loaded (no server connection)");
            }
        }
        catch (Exception ex)
        {
            if (registeredGeneration.HasValue && !sessionTeardownCompleted)
                RestoreSessionAfterAbandonedLoad(registeredGeneration.Value);

            _logger.Error($"Failed to load configuration: {ex.Message}");
            UiThread.Run(() =>
                TerminalUi.ErrorQuery("Error", $"Failed to load configuration:\n{ex.Message}", "OK"));
        }
        finally
        {
            _isHydratingConfiguration = false;
            await UiThread.RunAsync(HideActivity);
        }
    }

    private void AbortLoadedConfigurationForNewerConnectionIntent()
    {
        _configService.Reset();
        _currentMetadata = null;
        UiThread.Run(UpdateWindowTitle);
        _logger.Info("Configuration load abandoned because a newer connection operation was requested");
    }

    private void RestoreSessionAfterAbandonedLoad(long operationGeneration)
    {
        _connectionManager.RestoreSessionAfterAbandonedIntent(operationGeneration);
        if (!_connectionManager.IsConnectionGenerationActive(operationGeneration))
            return;

        UiThread.Run(() =>
        {
            if (!_connectionManager.IsConnectionGenerationActive(operationGeneration))
                return;

            _addressSpaceView.Initialize(_connectionManager.NodeBrowser);
            _nodeDetailsView.Clear();
        });
    }

    /// <summary>
    /// Saves the current configuration to the specified file path.
    /// </summary>
    private Task SaveConfigurationAsync(string filePath)
        => RunExclusiveOperationAsync(
            () => SaveConfigurationCoreAsync(filePath));

    private async Task SaveConfigurationCoreAsync(string filePath)
    {
        try
        {
            await UiThread.RunAsync(() => ShowActivity("Saving configuration..."));

            var monitoredVariables = _connectionManager.SubscriptionManager?.MonitoredVariables
                ?? Enumerable.Empty<MonitoredNode>();

            // Persist the profile actually selected/applied by the active
            // connection. Falling back to model defaults here previously wrote
            // SecurityMode=None for a credentialed secure session, so reload
            // could downgrade or reject the connection.
            ServerConfig? activeServer = null;
            SubscriptionSettings? activeSettings = null;
            if (_connectionManager.IsConnected)
            {
                activeServer = new ServerConfig
                {
                    SecurityMode = _connectionManager.CurrentSecurityMode?.ToString(),
                    SecurityPolicy = _connectionManager.CurrentSecurityPolicy
                };
                activeSettings = new SubscriptionSettings
                {
                    PublishingIntervalMs = _connectionManager.SubscriptionManager?.PublishingInterval ?? 250,
                    SamplingIntervalMs = _connectionManager.SamplingInterval,
                    QueueSize = _connectionManager.QueueSize
                };
            }

            var config = _configService.CaptureCurrentState(
                _connectionManager.CurrentEndpoint,
                _connectionManager.SubscriptionManager?.PublishingInterval ?? 250,
                monitoredVariables,
                _currentMetadata,
                _connectionManager.Credentials,
                existingServer: activeServer,
                existingSettings: activeSettings
            );

            // Update metadata name from filename if not set
            if (string.IsNullOrEmpty(config.Metadata.Name))
            {
                config.Metadata.Name = Path.GetFileNameWithoutExtension(filePath);
            }

            await _configService.SaveAsync(config, filePath);

            _currentMetadata = config.Metadata;
            _recentFiles.Add(filePath);
            UiThread.Run(UpdateWindowTitle);

            _logger.Info($"Configuration saved to {filePath}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to save configuration: {ex.Message}");
            UiThread.Run(() =>
                TerminalUi.ErrorQuery("Error", $"Failed to save:\n{ex.Message}", "OK"));
        }
        finally
        {
            await UiThread.RunAsync(HideActivity);
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
            ? " opcilloscope "
            : $" opcilloscope - {configName}{unsavedMarker} ";

        SetNeedsLayout();
    }

    /// <summary>
    /// Prompts the user to confirm discarding unsaved changes.
    /// </summary>
    /// <returns>True if the user confirms, false to cancel the operation.</returns>
    private bool ConfirmDiscardChanges()
    {
        var result = TerminalUi.Query(
            "Unsaved Changes",
            "You have unsaved changes. Do you want to discard them?",
            "Discard",
            "Cancel");

        return result == 0; // Discard
    }

    internal static bool CanQuit(bool hasUnsavedChanges, Func<bool> confirmDiscard)
        => !hasUnsavedChanges || confirmDiscard();

    private async Task RunExclusiveOperationAsync(Func<Task> operation)
    {
        await _operationGate.WaitAsync();
        Volatile.Write(ref _operationInProgress, 1);

        try
        {
            await operation();
        }
        finally
        {
            Volatile.Write(ref _operationInProgress, 0);
            _operationGate.Release();
        }
    }

    private bool RejectInteractiveMutationWhileBusy(string action)
    {
        if (Volatile.Read(ref _operationInProgress) == 0)
        {
            return false;
        }

        _logger.Warning($"Cannot {action}: another connection or configuration operation is still running");
        TerminalUi.Query(
            "Operation In Progress",
            "Please wait for the current connection or configuration operation to finish.",
            "OK");
        return true;
    }

    private void RequestQuit()
    {
        if (!CanQuit(_configService.HasUnsavedChanges, ConfirmDiscardChanges))
        {
            return;
        }

        if (Interlocked.Exchange(ref _quitInProgress, 1) != 0)
            return;

        RequestQuitCoreAsync().FireAndForget(_logger);
    }

    private async Task RequestQuitCoreAsync()
    {
        try
        {
            if (_csvRecordingManager.IsRecording || _csvRecordingManager.IsStopping)
            {
                var stopTask = StopRecordingAndReportAsync();
                var canStopUi = await AwaitRecordingStopForQuitAsync(
                    stopTask,
                    TimeSpan.FromSeconds(10),
                    PromptForSlowRecordingShutdownAsync).ConfigureAwait(false);
                if (!canStopUi)
                {
                    Interlocked.Exchange(ref _quitInProgress, 0);
                    return;
                }
            }

            await UiThread.RunAsync(TerminalUi.RequestStop).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            _logger.Error($"Quit preparation failed: {ex.Message}");
            Interlocked.Exchange(ref _quitInProgress, 0);
            await UiThread.RunAsync(() => TerminalUi.ErrorQuery(
                "Unable to Quit",
                "The application could not finish preparing to quit. Review the log and try again.",
                "OK")).ConfigureAwait(false);
        }
    }

    internal enum SlowRecordingQuitDecision
    {
        KeepWaiting,
        QuitAnyway,
        Cancel
    }

    internal static async Task<bool> AwaitRecordingStopForQuitAsync(
        Task stopTask,
        TimeSpan timeout,
        Func<Task<SlowRecordingQuitDecision>> getTimeoutDecision)
    {
        ArgumentNullException.ThrowIfNull(stopTask);
        ArgumentNullException.ThrowIfNull(getTimeoutDecision);

        while (!stopTask.IsCompleted)
        {
            var completed = await Task.WhenAny(stopTask, Task.Delay(timeout)).ConfigureAwait(false);
            if (ReferenceEquals(completed, stopTask))
                break;

            var decision = await getTimeoutDecision().ConfigureAwait(false);
            if (decision == SlowRecordingQuitDecision.QuitAnyway)
                return true;
            if (decision == SlowRecordingQuitDecision.Cancel)
                return false;
        }

        // Observe any exception before allowing the UI to stop.
        await stopTask.ConfigureAwait(false);
        return true;
    }

    private async Task<SlowRecordingQuitDecision> PromptForSlowRecordingShutdownAsync()
    {
        var decision = SlowRecordingQuitDecision.Cancel;
        await UiThread.RunAsync(() =>
        {
            var choice = TerminalUi.Query(
                "Recording Still Finishing",
                "The recording file is still being flushed. Quitting now may truncate it.",
                "Keep Waiting",
                "Quit Anyway",
                "Cancel Quit");
            decision = choice switch
            {
                0 => SlowRecordingQuitDecision.KeepWaiting,
                1 => SlowRecordingQuitDecision.QuitAnyway,
                _ => SlowRecordingQuitDecision.Cancel
            };
        }).ConfigureAwait(false);
        return decision;
    }

    /// <summary>
    /// Loads a configuration file from the command line argument.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    public void LoadConfigFromCommandLine(string configPath)
    {
        TerminalUi.AddTimeout(TimeSpan.FromMilliseconds(100), () =>
        {
            LoadConfigurationAsync(configPath).FireAndForget(_logger);
            return false;
        });
    }

    #endregion

    #region IKeybindingActions Implementation

    void DefaultKeybindings.IKeybindingActions.SwitchPane() => _focusManager?.FocusNext();
    void DefaultKeybindings.IKeybindingActions.ShowHelp() => ShowHelp();
    void DefaultKeybindings.IKeybindingActions.SubscribeSelected() => SubscribeSelected();
    void DefaultKeybindings.IKeybindingActions.RefreshTree() => RefreshTree();
    void DefaultKeybindings.IKeybindingActions.UnsubscribeSelected() => UnsubscribeSelected();
    void DefaultKeybindings.IKeybindingActions.ToggleScopeSelection() { /* Handled by MonitoredVariablesView */ }
    void DefaultKeybindings.IKeybindingActions.OpenScope() => LaunchScope();
    void DefaultKeybindings.IKeybindingActions.WriteSelected() => WriteSelected();
    void DefaultKeybindings.IKeybindingActions.OpenConfig() => OpenConfig();
    void DefaultKeybindings.IKeybindingActions.SaveConfig() => SaveConfig();
    void DefaultKeybindings.IKeybindingActions.SaveConfigAs() => SaveConfigAs();
    void DefaultKeybindings.IKeybindingActions.ToggleRecording() => ToggleRecording();
    void DefaultKeybindings.IKeybindingActions.Connect() => ShowConnectDialog();
    void DefaultKeybindings.IKeybindingActions.Disconnect() => DisconnectAsync().FireAndForget(_logger);
    void DefaultKeybindings.IKeybindingActions.Quit() => RequestQuit();

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopRecordingStatusUpdates();
            StopConnectingAnimation();

            // Remove the startup status timer if it hasn't yet self-removed.
            CancelStartupStatus();

            _csvRecordingManager.Dispose();
            ThemeManager.ThemeChanged -= OnThemeChanged;
            _monitoredVariablesView.RecordToggleRequested -= ToggleRecording;

            // Stop focus tracking
            if (_focusManager != null)
            {
                _focusManager.StopTracking();
                _focusManager.FocusChanged -= OnPanelFocusChanged;
            }

            TerminalUi.RemoveKeyDownHandler(OnApplicationKeyDown);

            _connectionManager.Dispose();
        }
        base.Dispose(disposing);
    }
}
