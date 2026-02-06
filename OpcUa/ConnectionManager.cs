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
    private bool _disposed;
    private int _isReconnecting;

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

    public ConnectionManager(Logger logger)
    {
        _logger = logger;
        _client = new OpcUaClientWrapper(logger);
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
    /// <returns>True if connection succeeded, false otherwise.</returns>
    public async Task<bool> ConnectAsync(string endpoint, int publishingInterval = 250)
    {
        Disconnect();

        _lastEndpoint = endpoint;
        StateChanged?.Invoke(ConnectionState.Connecting);

        try
        {
            var success = await _client.ConnectAsync(endpoint);

            if (success)
            {
                await InitializeSubscriptionAsync(publishingInterval);
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
            // Last resort: start fresh (will lose monitored nodes)
            DisposeSubscription();
            await InitializeSubscriptionAsync();
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

    private async Task InitializeSubscriptionAsync(int publishingInterval = 250)
    {
        _subscriptionManager = new SubscriptionManager(_client, _logger);
        _subscriptionManager.PublishingInterval = publishingInterval;
        await _subscriptionManager.InitializeAsync();

        // Store handler references for proper unsubscription
        _valueChangedHandler = node => ValueChanged?.Invoke(node);
        _variableAddedHandler = node => VariableAdded?.Invoke(node);
        _variableRemovedHandler = handle => VariableRemoved?.Invoke(handle);

        _subscriptionManager.ValueChanged += _valueChangedHandler;
        _subscriptionManager.VariableAdded += _variableAddedHandler;
        _subscriptionManager.VariableRemoved += _variableRemovedHandler;
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
