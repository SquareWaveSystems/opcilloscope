using OpcScope.Utilities;

namespace OpcScope.OpcUa;

/// <summary>
/// Manages OPC UA connection lifecycle including connect, disconnect, and reconnect operations.
/// Extracts connection orchestration from the UI layer for better separation of concerns.
/// </summary>
public sealed class ConnectionManager : IDisposable
{
    private readonly OpcUaClientWrapper _client;
    private readonly NodeBrowser _nodeBrowser;
    private readonly Logger _logger;
    private SubscriptionManager? _subscriptionManager;
    private string? _lastEndpoint;
    private bool _disposed;

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

    public ConnectionManager(Logger logger)
    {
        _logger = logger;
        _client = new OpcUaClientWrapper(logger);
        _nodeBrowser = new NodeBrowser(_client, logger);

        _client.Connected += OnClientConnected;
        _client.Disconnected += OnClientDisconnected;
        _client.ConnectionError += OnClientConnectionError;
    }

    /// <summary>
    /// Connects to an OPC UA server.
    /// </summary>
    /// <param name="endpoint">The endpoint URL to connect to.</param>
    /// <returns>True if connection succeeded, false otherwise.</returns>
    public async Task<bool> ConnectAsync(string endpoint)
    {
        Disconnect();

        _lastEndpoint = endpoint;
        StateChanged?.Invoke(ConnectionState.Connecting);

        try
        {
            var success = await _client.ConnectAsync(endpoint);

            if (success)
            {
                await InitializeSubscriptionAsync();
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
    /// Attempts to reconnect to the last endpoint with exponential backoff.
    /// </summary>
    /// <returns>True if reconnection succeeded, false otherwise.</returns>
    public async Task<bool> ReconnectAsync()
    {
        if (string.IsNullOrEmpty(_lastEndpoint))
        {
            _logger.Warning("No previous connection to reconnect");
            return false;
        }

        StateChanged?.Invoke(ConnectionState.Reconnecting);

        try
        {
            var success = await _client.ReconnectAsync();

            if (success)
            {
                await InitializeSubscriptionAsync();
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

    private async Task InitializeSubscriptionAsync()
    {
        _subscriptionManager = new SubscriptionManager(_client, _logger);
        await _subscriptionManager.InitializeAsync();

        _subscriptionManager.ValueChanged += node => ValueChanged?.Invoke(node);
        _subscriptionManager.VariableAdded += node => VariableAdded?.Invoke(node);
        _subscriptionManager.VariableRemoved += handle => VariableRemoved?.Invoke(handle);
    }

    private void DisposeSubscription()
    {
        if (_subscriptionManager != null)
        {
            _subscriptionManager.ValueChanged -= node => ValueChanged?.Invoke(node);
            _subscriptionManager.VariableAdded -= node => VariableAdded?.Invoke(node);
            _subscriptionManager.VariableRemoved -= handle => VariableRemoved?.Invoke(handle);
            _subscriptionManager.Dispose();
            _subscriptionManager = null;
        }
    }

    private void OnClientConnected()
    {
        _logger.Info("Connected successfully");
    }

    private void OnClientDisconnected()
    {
        StateChanged?.Invoke(ConnectionState.Disconnected);
    }

    private void OnClientConnectionError(string message)
    {
        ConnectionError?.Invoke(message);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

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
