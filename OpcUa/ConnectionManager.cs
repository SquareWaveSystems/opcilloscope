using Opcilloscope.Utilities;

namespace Opcilloscope.OpcUa;

/// <summary>
/// Manages OPC UA connection lifecycle including connect, disconnect, and reconnect operations.
/// Supports proper subscription preservation during reconnection.
/// </summary>
public sealed class ConnectionManager : IDisposable
{
    private readonly OpcUaClientWrapper _client;
    private readonly NodeBrowser _nodeBrowser;
    private readonly Logger _logger;
    private SubscriptionManager? _subscriptionManager;
    private string? _lastEndpoint;
    private ConnectionCredentials _credentials = ConnectionCredentials.Anonymous;
    private bool _disposed;
    private int _isReconnecting;

    // Intervals from the most recent ConnectAsync, so subscription restoration
    // after a reconnect does not silently fall back to the defaults.
    private int _publishingInterval = 250;
    private int _samplingInterval = 250;

    // Stored event handler references for proper unsubscription
    private Action<Models.MonitoredNode>? _valueChangedHandler;
    private Action<Models.MonitoredNode>? _variableAddedHandler;
    private Action<uint>? _variableRemovedHandler;

    /// <summary>
    /// Gets whether there is an active connection.
    /// </summary>
    public bool IsConnected => _client.IsConnected;

    /// <summary>
    /// Gets the current endpoint URL, if connected.
    /// </summary>
    public string? CurrentEndpoint => _client.CurrentEndpoint;

    /// <summary>
    /// Gets the last attempted endpoint URL.
    /// </summary>
    public string? LastEndpoint => _lastEndpoint;

    /// <summary>
    /// Gets the credentials used for the current or last connection.
    /// </summary>
    public ConnectionCredentials Credentials => _credentials;

    /// <summary>
    /// Gets the OPC UA client wrapper for direct session access.
    /// </summary>
    public OpcUaClientWrapper Client => _client;

    /// <summary>
    /// Gets the node browser for address space navigation.
    /// </summary>
    public NodeBrowser NodeBrowser => _nodeBrowser;

    /// <summary>
    /// Gets the subscription manager for monitored variables.
    /// </summary>
    public SubscriptionManager? SubscriptionManager => _subscriptionManager;

    /// <summary>
    /// Raised when connection state changes.
    /// </summary>
    public event Action<ConnectionState>? StateChanged;

    /// <summary>
    /// Raised when a connection error occurs.
    /// </summary>
    public event Action<string>? ConnectionError;

    /// <summary>
    /// Raised when a value changes on a monitored variable.
    /// </summary>
    public event Action<Models.MonitoredNode>? ValueChanged;

    /// <summary>
    /// Raised when a monitored variable is added.
    /// </summary>
    public event Action<Models.MonitoredNode>? VariableAdded;

    /// <summary>
    /// Raised when a monitored variable is removed.
    /// </summary>
    public event Action<uint>? VariableRemoved;

    /// <summary>
    /// Raised when automatic reconnection is triggered due to connection loss.
    /// </summary>
    public event Action? AutoReconnectTriggered;

    /// <summary>
    /// Creates a new connection manager.
    /// </summary>
    /// <param name="logger">Logger for connection diagnostics.</param>
    /// <param name="allowInsecure">
    /// When <c>true</c>, untrusted server certificates are auto-accepted (development only).
    /// When <c>null</c> (the default), <see cref="OpcUaClientWrapper.AllowInsecureByDefault"/> is used.
    /// </param>
    public ConnectionManager(Logger logger, bool? allowInsecure = null)
    {
        _logger = logger;
        _client = new OpcUaClientWrapper(logger, allowInsecure);
        _nodeBrowser = new NodeBrowser(_client, logger);

        _client.Connected += OnClientConnected;
        _client.Disconnected += OnClientDisconnected;
        _client.ConnectionError += OnClientConnectionError;
        _client.ReconnectRequired += OnReconnectRequired;
    }

    /// <summary>
    /// Connects to an OPC UA server.
    /// </summary>
    /// <param name="endpoint">The endpoint URL to connect to.</param>
    /// <param name="publishingInterval">Publishing interval in milliseconds for the subscription.</param>
    /// <param name="credentials">Authentication credentials (defaults to anonymous).</param>
    /// <param name="securityMode">Requested message security mode (e.g. None, Sign, SignAndEncrypt). When null/None, an unsecured endpoint is selected.</param>
    /// <param name="securityPolicy">Requested security policy URI or shorthand (e.g. Basic256Sha256). Honored when a matching endpoint exists.</param>
    /// <param name="samplingInterval">Sampling interval in milliseconds for monitored items.</param>
    /// <returns>True if connection succeeded, false otherwise.</returns>
    public async Task<bool> ConnectAsync(
        string endpoint,
        int publishingInterval = 250,
        ConnectionCredentials? credentials = null,
        string? securityMode = null,
        string? securityPolicy = null,
        int samplingInterval = 250)
    {
        // Async teardown: the synchronous Disconnect() blocks on the OPC UA close
        // round-trip (up to the transport timeout against a dead server), which froze
        // the UI thread when reconnecting over an existing or dead connection.
        await DisconnectAsync();

        _lastEndpoint = endpoint;
        _credentials = credentials ?? ConnectionCredentials.Anonymous;
        _publishingInterval = publishingInterval;
        _samplingInterval = samplingInterval;
        StateChanged?.Invoke(ConnectionState.Connecting);

        try
        {
            var success = await _client.ConnectAsync(endpoint, _credentials, securityMode, securityPolicy);

            if (success)
            {
                if (!await InitializeSubscriptionAsync())
                {
                    // Without a subscription the session is useless for monitoring;
                    // fail the connect rather than reporting Connected.
                    var msg = "Connected, but the server refused the monitoring subscription. Disconnecting.";
                    _logger.Error(msg);
                    ConnectionError?.Invoke(msg);
                    await DisconnectAsync();
                    return false;
                }

                StateChanged?.Invoke(ConnectionState.Connected);
            }
            else
            {
                StateChanged?.Invoke(ConnectionState.Disconnected);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.Error($"Connection failed: {ex.Message}");
            StateChanged?.Invoke(ConnectionState.Disconnected);
            return false;
        }
    }

    /// <summary>
    /// Disconnects from the current server.
    /// </summary>
    public void Disconnect()
    {
        DisposeSubscription();
        _client.Disconnect();
        StateChanged?.Invoke(ConnectionState.Disconnected);
    }

    /// <summary>
    /// Asynchronously disconnects from the current server without blocking the calling
    /// thread on the OPC UA close round-trip. Preferred over <see cref="Disconnect"/> for
    /// UI callers.
    /// </summary>
    public async Task DisconnectAsync()
    {
        DisposeSubscription();
        await _client.DisconnectAsync().ConfigureAwait(false);
        StateChanged?.Invoke(ConnectionState.Disconnected);
    }

    /// <summary>
    /// Attempts to reconnect to the last endpoint preserving subscriptions.
    /// Uses OPC UA session reconnect/transfer to maintain monitored variables.
    /// </summary>
    /// <returns>True if reconnection succeeded, false otherwise.</returns>
    public async Task<bool> ReconnectAsync()
    {
        if (string.IsNullOrEmpty(_lastEndpoint))
        {
            _logger.Warning("No previous connection to reconnect");
            return false;
        }

        if (Interlocked.CompareExchange(ref _isReconnecting, 1, 0) != 0)
        {
            _logger.Warning("Reconnection already in progress");
            return false;
        }
        StateChanged?.Invoke(ConnectionState.Reconnecting);

        // Mark all monitored variables as stale during reconnection
        _subscriptionManager?.MarkAllAsStale();

        try
        {
            var success = await _client.ReconnectAsync();

            if (success)
            {
                // Try to restore subscriptions
                await RestoreSubscriptionsAsync();
                StateChanged?.Invoke(ConnectionState.Connected);
            }
            else
            {
                StateChanged?.Invoke(ConnectionState.Disconnected);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.Error($"Reconnection failed: {ex.Message}");
            StateChanged?.Invoke(ConnectionState.Disconnected);
            return false;
        }
        finally
        {
            Interlocked.Exchange(ref _isReconnecting, 0);
        }
    }

    /// <summary>
    /// Restores subscriptions after successful reconnection.
    /// First checks if subscriptions were transferred, otherwise recreates them.
    /// </summary>
    private async Task RestoreSubscriptionsAsync()
    {
        if (_subscriptionManager == null)
        {
            // No subscriptions existed - create fresh subscription manager
            await InitializeSubscriptionAsync();
            return;
        }

        // Try to reattach to transferred subscriptions
        if (_subscriptionManager.IsSubscriptionValid())
        {
            var reattached = await _subscriptionManager.ReattachAfterReconnectAsync();
            if (reattached)
            {
                _logger.Info("Subscriptions preserved successfully");
                return;
            }
        }

        // Subscription transfer failed - recreate them
        _logger.Info("Recreating subscriptions after reconnection...");
        var recreated = await _subscriptionManager.RecreateSubscriptionsAsync();

        if (!recreated)
        {
            _logger.Warning("Failed to recreate subscriptions - initializing fresh");
            // Last resort: start fresh. The monitored nodes are gone, so tell the UI -
            // otherwise their rows sit at "(reconnecting...)" forever with handles the
            // new subscription manager knows nothing about.
            var lostHandles = _subscriptionManager.MonitoredVariables
                .Select(v => v.ClientHandle)
                .ToList();

            DisposeSubscription();
            await InitializeSubscriptionAsync();

            foreach (var handle in lostHandles)
            {
                VariableRemoved?.Invoke(handle);
            }
        }
    }

    /// <summary>
    /// Subscribes to a node for value monitoring.
    /// </summary>
    public Task<Models.MonitoredNode?> SubscribeAsync(Opc.Ua.NodeId nodeId, string displayName)
    {
        if (_subscriptionManager == null)
        {
            _logger.Warning("Cannot subscribe: not connected");
            return Task.FromResult<Models.MonitoredNode?>(null);
        }

        return _subscriptionManager.AddNodeAsync(nodeId, displayName);
    }

    /// <summary>
    /// Unsubscribes from a monitored variable.
    /// </summary>
    public Task<bool> UnsubscribeAsync(uint clientHandle)
    {
        return _subscriptionManager?.RemoveNodeAsync(clientHandle) ?? Task.FromResult(false);
    }

    /// <summary>
    /// Writes a value to an OPC UA node's Value attribute.
    /// </summary>
    public Task<Opc.Ua.StatusCode> WriteValueAsync(Opc.Ua.NodeId nodeId, object value)
    {
        if (!_client.IsConnected)
        {
            _logger.Warning("Cannot write: not connected");
            return Task.FromResult((Opc.Ua.StatusCode)Opc.Ua.StatusCodes.BadNotConnected);
        }

        return _client.WriteValueAsync(nodeId, value);
    }

    private async Task<bool> InitializeSubscriptionAsync()
    {
        _subscriptionManager = new SubscriptionManager(_client, _logger);
        _subscriptionManager.PublishingInterval = _publishingInterval;
        _subscriptionManager.SamplingInterval = _samplingInterval;
        var initialized = await _subscriptionManager.InitializeAsync();

        // Store handler references for proper unsubscription
        _valueChangedHandler = node => ValueChanged?.Invoke(node);
        _variableAddedHandler = node => VariableAdded?.Invoke(node);
        _variableRemovedHandler = handle => VariableRemoved?.Invoke(handle);

        _subscriptionManager.ValueChanged += _valueChangedHandler;
        _subscriptionManager.VariableAdded += _variableAddedHandler;
        _subscriptionManager.VariableRemoved += _variableRemovedHandler;

        return initialized;
    }

    private void DisposeSubscription()
    {
        if (_subscriptionManager != null)
        {
            if (_valueChangedHandler != null)
                _subscriptionManager.ValueChanged -= _valueChangedHandler;
            if (_variableAddedHandler != null)
                _subscriptionManager.VariableAdded -= _variableAddedHandler;
            if (_variableRemovedHandler != null)
                _subscriptionManager.VariableRemoved -= _variableRemovedHandler;

            _subscriptionManager.Dispose();
            _subscriptionManager = null;
        }

        _valueChangedHandler = null;
        _variableAddedHandler = null;
        _variableRemovedHandler = null;
    }

    private void OnClientConnected()
    {
        _logger.Info("Connected successfully");
    }

    private void OnClientDisconnected()
    {
        if (Interlocked.CompareExchange(ref _isReconnecting, 0, 0) == 0)
        {
            StateChanged?.Invoke(ConnectionState.Disconnected);
        }
    }

    private void OnClientConnectionError(string message)
    {
        ConnectionError?.Invoke(message);
    }

    private void OnReconnectRequired()
    {
        if (Interlocked.CompareExchange(ref _isReconnecting, 0, 0) != 0)
            return;

        _logger.Warning("Connection lost - automatic reconnection triggered");
        AutoReconnectTriggered?.Invoke();

        // Note: The UI layer should call ReconnectAsync() when it receives AutoReconnectTriggered
        // This allows the UI to show appropriate feedback during reconnection
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _client.Connected -= OnClientConnected;
        _client.Disconnected -= OnClientDisconnected;
        _client.ConnectionError -= OnClientConnectionError;
        _client.ReconnectRequired -= OnReconnectRequired;

        DisposeSubscription();
        _client.Dispose();
    }
}

/// <summary>
/// Connection state for UI updates.
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting
}
