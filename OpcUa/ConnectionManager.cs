using Opcilloscope.Utilities;
using Opc.Ua;

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
    private string? _securityMode;
    private string? _securityPolicy;
    private int _disposed;
    private readonly object _stateLock = new();
    private ConnectionState _state = ConnectionState.Disconnected;
    private bool _sessionOperationsAllowed;
    // Incremented as soon as a lifecycle intent is expressed, before it waits on
    // the wrapper gate. Long-running reads can use this to reject stale results
    // instead of applying server A data after the user has switched to server B.
    private long _connectionGeneration;
    // Distinguishes a queued automatic reconnect from later explicit user intent.
    // Manual reconnect intentionally remains a fresh-connect operation after an
    // explicit disconnect; automatic reconnect must never have that behaviour.
    private long _connectionIntentVersion;
    private CancellationTokenSource _connectionIntentCts = new();

    // Subscription settings from the most recent ConnectAsync, so subscription
    // restoration after a reconnect does not silently fall back to the defaults.
    private int _publishingInterval = 250;
    private int _samplingInterval = 250;
    private uint _queueSize = 10;

    // Stored event handler references for proper unsubscription
    private Action<Models.MonitoredNode>? _valueChangedHandler;
    private Action<Models.MonitoredNode>? _variableAddedHandler;
    private Action<uint, long>? _variableRemovedHandler;

    /// <summary>
    /// Gets whether there is an active connection.
    /// </summary>
    public bool IsConnected
    {
        get
        {
            lock (_stateLock)
            {
                return _sessionOperationsAllowed && _client.IsConnected;
            }
        }
    }

    /// <summary>
    /// Gets the current endpoint URL, if connected.
    /// </summary>
    public string? CurrentEndpoint => _client.CurrentEndpoint;

    /// <summary>
    /// Gets the actual message security mode selected for the active session.
    /// </summary>
    public MessageSecurityMode? CurrentSecurityMode => _client.CurrentSecurityMode;

    /// <summary>
    /// Gets the full security-policy URI selected for the active session.
    /// </summary>
    public string? CurrentSecurityPolicy => _client.CurrentSecurityPolicy;

    /// <summary>
    /// Gets the last attempted endpoint URL.
    /// </summary>
    public string? LastEndpoint => _lastEndpoint;

    /// <summary>
    /// Gets the credentials used for the current or last connection.
    /// </summary>
    public ConnectionCredentials Credentials => _credentials;

    /// <summary>
    /// Gets the sampling interval from the active or most recently stored profile.
    /// </summary>
    public int SamplingInterval => _samplingInterval;

    /// <summary>
    /// Gets the monitored-item queue size from the active or most recently stored profile.
    /// </summary>
    public uint QueueSize => _queueSize;

    /// <summary>
    /// Gets the current connection generation. It changes immediately whenever a
    /// connect, disconnect, reconnect, or detected connection loss invalidates work
    /// started against the prior session.
    /// </summary>
    public long ConnectionGeneration => Volatile.Read(ref _connectionGeneration);

    internal long ConnectionIntentVersion
    {
        get
        {
            lock (_stateLock)
            {
                return _connectionIntentVersion;
            }
        }
    }

    /// <summary>
    /// Returns whether work stamped with <paramref name="generation"/> may use the
    /// active session. A raw SDK session can remain Connected after keep-alive loss;
    /// only the manager's fully published Connected state is usable.
    /// </summary>
    public bool IsConnectionGenerationActive(long generation)
    {
        lock (_stateLock)
        {
            return _sessionOperationsAllowed
                && _connectionGeneration == generation
                && _client.IsConnected;
        }
    }

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
    public event Action<uint, long>? VariableRemoved;

    /// <summary>
    /// Raised when automatic reconnection is triggered due to connection loss.
    /// </summary>
    public event Action<long>? AutoReconnectTriggered;

    /// <summary>
    /// Creates a new connection manager.
    /// </summary>
    /// <param name="logger">Logger for connection diagnostics.</param>
    /// <param name="allowInsecure">
    /// When <c>true</c>, server certificate validation failures are accepted (development only).
    /// When <c>null</c> (the default), <see cref="OpcUaClientWrapper.AllowInsecureByDefault"/> is used.
    /// </param>
    public ConnectionManager(Logger logger, bool? allowInsecure = null)
    {
        _logger = logger;
        _client = new OpcUaClientWrapper(logger, allowInsecure);
        _nodeBrowser = new NodeBrowser(
            _client,
            logger,
            () => ConnectionGeneration,
            IsConnectionGenerationActive);

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
    /// <param name="securityMode">Requested message security mode (e.g. None, Sign, SignAndEncrypt). An explicit value must match; UserName authentication rejects None.</param>
    /// <param name="securityPolicy">Requested security policy URI or shorthand (e.g. Basic256Sha256). An explicit value must match.</param>
    /// <param name="samplingInterval">Sampling interval in milliseconds for monitored items (0 = as fast as the server allows).</param>
    /// <param name="queueSize">Server-side notification queue size for monitored items.</param>
    /// <returns>True if connection succeeded, false otherwise.</returns>
    public Task<bool> ConnectAsync(
        string endpoint,
        int publishingInterval = 250,
        ConnectionCredentials? credentials = null,
        string? securityMode = null,
        string? securityPolicy = null,
        int samplingInterval = 250,
        uint queueSize = 10)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return Task.FromResult(false);

        var operationGeneration = BeginExplicitLifecycleIntent();
        return ConnectWithIntentAsync(
            endpoint,
            publishingInterval,
            credentials,
            securityMode,
            securityPolicy,
            samplingInterval,
            queueSize,
            operationGeneration);
    }

    internal Task<bool> ConnectWithIntentAsync(
        string endpoint,
        int publishingInterval,
        ConnectionCredentials? credentials,
        string? securityMode,
        string? securityPolicy,
        int samplingInterval,
        uint queueSize,
        long operationGeneration)
    {
        return _client.ExecuteLifecycleAsync(
            () => ConnectCoreAsync(
                endpoint,
                publishingInterval,
                credentials,
                securityMode,
                securityPolicy,
                samplingInterval,
                queueSize,
                operationGeneration));
    }

    private async Task<bool> ConnectCoreAsync(
        string endpoint,
        int publishingInterval,
        ConnectionCredentials? credentials,
        string? securityMode,
        string? securityPolicy,
        int samplingInterval,
        uint queueSize,
        long operationGeneration)
    {
        if (Volatile.Read(ref _disposed) != 0
            || !IsCurrentConnectionIntent(operationGeneration))
            return false;

        // This method already owns the client's shared lifecycle gate, so teardown
        // must use core methods rather than re-entering public lifecycle APIs.
        await DisconnectCoreAsync().ConfigureAwait(false);
        if (!IsCurrentConnectionIntent(operationGeneration))
            return false;

        _lastEndpoint = endpoint;
        _credentials = credentials ?? ConnectionCredentials.Anonymous;
        _securityMode = securityMode;
        _securityPolicy = securityPolicy;
        _publishingInterval = publishingInterval;
        _samplingInterval = samplingInterval;
        _queueSize = queueSize;
        if (!TrySetStateForIntent(operationGeneration, ConnectionState.Connecting))
            return false;

        try
        {
            var success = await _client.ConnectCoreAsync(
                endpoint,
                _credentials,
                _securityMode,
                _securityPolicy).ConfigureAwait(false);

            if (success)
            {
                if (!IsCurrentConnectionIntent(operationGeneration))
                {
                    await DisconnectCoreAsync().ConfigureAwait(false);
                    return false;
                }

                // Preserve the actual selected profile for a later fresh reconnect.
                _securityMode = _client.CurrentSecurityMode?.ToString();
                _securityPolicy = _client.CurrentSecurityPolicy;

                if (!await InitializeSubscriptionAsync(operationGeneration).ConfigureAwait(false))
                {
                    // Without a subscription the session is useless for monitoring;
                    // fail the connect rather than reporting Connected.
                    var msg = "Connected, but the server refused the monitoring subscription. Disconnecting.";
                    _logger.Error(msg);
                    ConnectionError?.Invoke(msg);
                    await DisconnectCoreAsync().ConfigureAwait(false);
                    return false;
                }

                if (!TryPublishConnected(operationGeneration))
                {
                    await DisconnectCoreAsync().ConfigureAwait(false);
                    return false;
                }
            }
            else
            {
                SetState(ConnectionState.Disconnected);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.Error($"Connection failed: {ex.Message}");
            await DisconnectCoreAsync().ConfigureAwait(false);
            SetState(ConnectionState.Disconnected);
            return false;
        }
    }

    /// <summary>
    /// Disconnects from the current server.
    /// </summary>
    public void Disconnect()
    {
        DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously disconnects from the current server without blocking the calling
    /// thread on the OPC UA close round-trip. Preferred over <see cref="Disconnect"/> for
    /// UI callers.
    /// </summary>
    public async Task DisconnectAsync()
    {
        var operationGeneration = BeginExplicitLifecycleIntent();
        await DisconnectWithIntentAsync(operationGeneration).ConfigureAwait(false);
    }

    internal Task<bool> DisconnectWithIntentAsync(long operationGeneration)
    {
        return _client.ExecuteLifecycleAsync(async () =>
        {
            if (!IsCurrentConnectionIntent(operationGeneration))
                return false;

            await DisconnectCoreAsync().ConfigureAwait(false);
            return true;
        });
    }

    private async Task DisconnectCoreAsync()
    {
        // Subscription cleanup must not prevent local session cleanup. Both helpers
        // detach ownership first and absorb non-fatal teardown failures.
        DisposeSubscription();
        await _client.DisconnectCoreAsync().ConfigureAwait(false);
        SetState(ConnectionState.Disconnected);
    }

    /// <summary>
    /// Attempts to reconnect to the last endpoint. An active failed session uses OPC UA
    /// reconnect/transfer to preserve subscriptions; after an explicit disconnect this
    /// performs a fresh connection with the stored endpoint, credentials, security, and
    /// subscription profile.
    /// </summary>
    /// <returns>True if reconnection succeeded, false otherwise.</returns>
    public Task<bool> ReconnectAsync()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return Task.FromResult(false);

        if (string.IsNullOrEmpty(_lastEndpoint))
        {
            _logger.Warning("No previous connection to reconnect");
            return Task.FromResult(false);
        }

        var operationGeneration = BeginExplicitLifecycleIntent();
        return ReconnectWithIntentAsync(operationGeneration);
    }

    internal Task<bool> ReconnectWithIntentAsync(long operationGeneration)
    {
        if (!TryGetConnectionIntentCancellationToken(
                operationGeneration,
                out var cancellationToken))
        {
            return Task.FromResult(false);
        }

        return _client.ExecuteLifecycleAsync(
            () => ReconnectCoreAsync(
                operationGeneration,
                automaticIntentVersion: null,
                cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Performs the automatic reconnect requested by <see cref="AutoReconnectTriggered"/>
    /// only while that request is still the latest connection intent. A later explicit
    /// disconnect/connect/manual reconnect invalidates the token before waiting on the
    /// lifecycle gate, so a delayed UI callback cannot resurrect a closed session.
    /// </summary>
    public Task<bool> ReconnectAutomaticallyAsync(long intentVersion)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return Task.FromResult(false);

        if (!TryGetAutomaticReconnectIntent(
                intentVersion,
                out var operationGeneration,
                out var cancellationToken))
        {
            _logger.Info("Skipped stale automatic reconnect request");
            return Task.FromResult(false);
        }

        return _client.ExecuteLifecycleAsync(async () =>
        {
            if (cancellationToken.IsCancellationRequested
                || !IsCurrentAutomaticReconnectIntent(intentVersion))
            {
                _logger.Info("Skipped stale automatic reconnect request");
                return false;
            }

            return await ReconnectCoreAsync(
                operationGeneration,
                intentVersion,
                cancellationToken).ConfigureAwait(false);
        });
    }

    private async Task<bool> ReconnectCoreAsync(
        long operationGeneration,
        long? automaticIntentVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0
            || string.IsNullOrEmpty(_lastEndpoint)
            || !IsCurrentConnectionIntent(operationGeneration))
            return false;

        if (!TrySetStateForIntent(operationGeneration, ConnectionState.Reconnecting))
            return false;

        // An explicit disconnect clears the wrapper's current endpoint and session,
        // but this manager intentionally retains the full connection profile. In that
        // case manual Reconnect is a fresh connect rather than a session transfer.
        var requiresFreshConnect = _client.Session == null
            || string.IsNullOrEmpty(_client.CurrentEndpoint);

        if (!requiresFreshConnect)
        {
            // Mark monitored variables stale only when preserving an existing
            // subscription; explicit disconnect already disposed them.
            _subscriptionManager?.MarkAllAsStale();
        }

        try
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            var success = requiresFreshConnect
                ? await _client.ConnectCoreAsync(
                    _lastEndpoint,
                    _credentials,
                    _securityMode,
                    _securityPolicy).ConfigureAwait(false)
                : await _client.ReconnectCoreAsync(cancellationToken).ConfigureAwait(false);

            if (success)
            {
                if (cancellationToken.IsCancellationRequested
                    || !IsCurrentConnectionIntent(operationGeneration))
                {
                    await DisconnectCoreAsync().ConfigureAwait(false);
                    return false;
                }

                _securityMode = _client.CurrentSecurityMode?.ToString();
                _securityPolicy = _client.CurrentSecurityPolicy;

                if (requiresFreshConnect)
                {
                    if (!await InitializeSubscriptionAsync(operationGeneration).ConfigureAwait(false))
                    {
                        var msg = "Reconnected, but the server refused the monitoring subscription. Disconnecting.";
                        _logger.Error(msg);
                        ConnectionError?.Invoke(msg);
                        await DisconnectCoreAsync().ConfigureAwait(false);
                        return false;
                    }
                }
                else
                {
                    // Try to restore subscriptions from the prior session.
                    _subscriptionManager?.AdvanceConnectionGeneration(operationGeneration);
                    await RestoreSubscriptionsAsync(operationGeneration).ConfigureAwait(false);
                }

                if (automaticIntentVersion.HasValue
                    && !TryPublishAutomaticReconnect(
                        automaticIntentVersion.Value,
                        operationGeneration))
                {
                    _logger.Info("Closing session created by a superseded automatic reconnect");
                    await DisconnectCoreAsync().ConfigureAwait(false);
                    return false;
                }

                if (!automaticIntentVersion.HasValue)
                {
                    if (!TryPublishConnected(operationGeneration))
                    {
                        await DisconnectCoreAsync().ConfigureAwait(false);
                        return false;
                    }
                }
            }
            else
            {
                SetState(ConnectionState.Disconnected);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.Error($"Reconnection failed: {ex.Message}");
            await DisconnectCoreAsync().ConfigureAwait(false);
            SetState(ConnectionState.Disconnected);
            return false;
        }
    }

    /// <summary>
    /// Restores subscriptions after successful reconnection.
    /// First checks if subscriptions were transferred, otherwise recreates them.
    /// </summary>
    private async Task RestoreSubscriptionsAsync(long operationGeneration)
    {
        if (_subscriptionManager == null)
        {
            // No subscriptions existed - create fresh subscription manager
            if (!await InitializeSubscriptionAsync(operationGeneration).ConfigureAwait(false))
            {
                throw new InvalidOperationException(
                    "Server refused subscription initialization after reconnect.");
            }
            return;
        }

        // Try to reattach to transferred subscriptions
        if (_subscriptionManager.IsSubscriptionValid())
        {
            var reattached = await _subscriptionManager
                .ReattachAfterReconnectAsync()
                .ConfigureAwait(false);
            if (reattached)
            {
                _logger.Info("Subscriptions preserved successfully");
                return;
            }
        }

        // Subscription transfer failed - recreate them
        _logger.Info("Recreating subscriptions after reconnection...");
        var recreated = await _subscriptionManager
            .RecreateSubscriptionsAsync()
            .ConfigureAwait(false);

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
            if (!await InitializeSubscriptionAsync(operationGeneration).ConfigureAwait(false))
            {
                throw new InvalidOperationException(
                    "Server refused fresh subscription initialization after reconnect.");
            }

            foreach (var handle in lostHandles)
            {
                VariableRemoved?.Invoke(handle, operationGeneration);
            }
        }
    }

    /// <summary>
    /// Subscribes to a node for value monitoring.
    /// </summary>
    public Task<Models.MonitoredNode?> SubscribeAsync(
        Opc.Ua.NodeId nodeId,
        string displayName,
        long? expectedGeneration = null)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return Task.FromResult<Models.MonitoredNode?>(null);

        var generation = expectedGeneration ?? ConnectionGeneration;
        // Use the same outer gate as connect/disconnect/reconnect. The manager's
        // mutation gate then serializes ApplyChangesAsync calls within one active
        // subscription; the fixed lock order is lifecycle -> subscription.
        return _client.ExecuteLifecycleAsync(async () =>
        {
            if (Volatile.Read(ref _disposed) != 0
                || !IsConnectionGenerationActive(generation))
                return null;

            var subscriptionManager = _subscriptionManager;
            if (subscriptionManager == null || !_client.IsConnected)
            {
                _logger.Warning("Cannot subscribe: connection changed or is not active");
                return null;
            }

            return await subscriptionManager
                .AddNodeAsync(nodeId, displayName)
                .ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Unsubscribes from a monitored variable.
    /// </summary>
    public Task<bool> UnsubscribeAsync(
        uint clientHandle,
        long? expectedGeneration = null)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return Task.FromResult(false);

        var generation = expectedGeneration ?? ConnectionGeneration;
        return _client.ExecuteLifecycleAsync(async () =>
        {
            if (Volatile.Read(ref _disposed) != 0
                || !IsConnectionGenerationActive(generation))
                return false;

            var subscriptionManager = _subscriptionManager;
            return subscriptionManager != null
                && await subscriptionManager
                    .RemoveNodeAsync(clientHandle)
                    .ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Writes a value to an OPC UA node's Value attribute.
    /// </summary>
    public Task<Opc.Ua.StatusCode> WriteValueAsync(
        Opc.Ua.NodeId nodeId,
        object value,
        long? expectedGeneration = null)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return Task.FromResult((Opc.Ua.StatusCode)Opc.Ua.StatusCodes.BadNotConnected);

        var generation = expectedGeneration ?? ConnectionGeneration;
        return _client.ExecuteLifecycleAsync(async () =>
        {
            var session = _client.Session;
            if (!IsConnectionGenerationActive(generation)
                || session == null
                || !session.Connected)
            {
                _logger.Warning("Cannot write: connection changed or is not active");
                return (Opc.Ua.StatusCode)Opc.Ua.StatusCodes.BadNotConnected;
            }

            return await OpcUaClientWrapper
                .WriteValueCoreAsync(session, nodeId, value)
                .ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Reads the attributes and current value needed by the write dialog from one
    /// pinned session. Returns null if connection intent changes at any point.
    /// </summary>
    public Task<(DataValueCollection Attributes, DataValue? Value)?> ReadWriteSnapshotAsync(
        NodeId nodeId,
        long expectedGeneration,
        params uint[] attributeIds)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return Task.FromResult<(DataValueCollection Attributes, DataValue? Value)?>(null);

        return _client.ExecuteLifecycleAsync<(
            DataValueCollection Attributes,
            DataValue? Value)?>(async () =>
        {
            var session = _client.Session;
            if (!IsConnectionGenerationActive(expectedGeneration)
                || session == null
                || !session.Connected)
            {
                return null;
            }

            var attributes = await OpcUaClientWrapper
                .ReadAttributesCoreAsync(session, nodeId, attributeIds)
                .ConfigureAwait(false);
            var value = await OpcUaClientWrapper
                .ReadValueCoreAsync(session, nodeId)
                .ConfigureAwait(false);

            if (!IsConnectionGenerationActive(expectedGeneration))
                return null;

            return (Attributes: attributes, Value: value);
        });
    }

    private async Task<bool> InitializeSubscriptionAsync(long connectionGeneration)
    {
        var subscriptionManager = new SubscriptionManager(
            _client,
            _logger,
            connectionGeneration)
        {
            PublishingInterval = _publishingInterval,
            SamplingInterval = _samplingInterval,
            QueueSize = _queueSize
        };

        var installed = false;
        try
        {
            var initialized = await subscriptionManager.InitializeAsync().ConfigureAwait(false);
            if (!initialized)
                return false;

            // Store handler references for proper unsubscription.
            // Capture the manager instance as provenance. A retired manager can
            // dispatch an already-queued callback after a fallback manager reuses
            // the same generation and client handles; generation alone cannot
            // distinguish those sources.
            _valueChangedHandler = node =>
            {
                if (ReferenceEquals(_subscriptionManager, subscriptionManager))
                    ValueChanged?.Invoke(node);
            };
            _variableAddedHandler = node =>
            {
                if (ReferenceEquals(_subscriptionManager, subscriptionManager))
                    VariableAdded?.Invoke(node);
            };
            _variableRemovedHandler = (handle, generation) =>
            {
                if (ReferenceEquals(_subscriptionManager, subscriptionManager))
                    VariableRemoved?.Invoke(handle, generation);
            };

            subscriptionManager.ValueChanged += _valueChangedHandler;
            subscriptionManager.VariableAdded += _variableAddedHandler;
            subscriptionManager.VariableRemoved += _variableRemovedHandler;

            _subscriptionManager = subscriptionManager;
            installed = true;
            return true;
        }
        finally
        {
            // Initialization may create a server-side subscription before returning
            // false or throwing. Always dispose that local instance unless ownership
            // was successfully published.
            if (!installed)
            {
                try
                {
                    subscriptionManager.Dispose();
                }
                catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
                {
                    _logger.Warning($"Subscription cleanup error after failed initialization: {ex.Message}");
                }

                _valueChangedHandler = null;
                _variableAddedHandler = null;
                _variableRemovedHandler = null;
            }
        }
    }

    private void DisposeSubscription()
    {
        var subscriptionManager = _subscriptionManager;
        var valueChangedHandler = _valueChangedHandler;
        var variableAddedHandler = _variableAddedHandler;
        var variableRemovedHandler = _variableRemovedHandler;

        // Detach ownership before fallible event removal/disposal so a later lifecycle
        // operation cannot observe and dispose the same subscription manager twice.
        _subscriptionManager = null;
        _valueChangedHandler = null;
        _variableAddedHandler = null;
        _variableRemovedHandler = null;

        if (subscriptionManager == null)
            return;

        try
        {
            if (valueChangedHandler != null)
                subscriptionManager.ValueChanged -= valueChangedHandler;
            if (variableAddedHandler != null)
                subscriptionManager.VariableAdded -= variableAddedHandler;
            if (variableRemovedHandler != null)
                subscriptionManager.VariableRemoved -= variableRemovedHandler;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            _logger.Warning($"Subscription event cleanup error (non-critical): {ex.Message}");
        }
        finally
        {
            try
            {
                subscriptionManager.Dispose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                _logger.Warning($"Subscription dispose error (non-critical): {ex.Message}");
            }
        }
    }

    private void OnClientConnected()
    {
        _logger.Info("Connected successfully");
    }

    private void OnClientDisconnected()
    {
        SetState(ConnectionState.Disconnected);
    }

    private void OnClientConnectionError(string message)
    {
        ConnectionError?.Invoke(message);
    }

    internal void OnReconnectRequired()
    {
        var transitioned = false;
        var intentVersion = 0L;
        CancellationTokenSource? previousIntent = null;
        lock (_stateLock)
        {
            // Keep-alive callbacks can repeat rapidly for one outage. Moving to
            // Reconnecting here suppresses duplicate UI-triggered reconnect tasks.
            if (_state == ConnectionState.Connected && _sessionOperationsAllowed)
            {
                _state = ConnectionState.Reconnecting;
                _sessionOperationsAllowed = false;
                intentVersion = ++_connectionIntentVersion;
                _connectionGeneration++;
                previousIntent = _connectionIntentCts;
                _connectionIntentCts = new CancellationTokenSource();
                transitioned = true;
            }
        }

        if (!transitioned)
            return;

        CancelIntent(previousIntent!);
        _logger.Warning("Connection lost - automatic reconnection triggered");
        StateChanged?.Invoke(ConnectionState.Reconnecting);
        AutoReconnectTriggered?.Invoke(intentVersion);

        // The UI passes this token back to ReconnectAutomaticallyAsync after it has
        // shown feedback. Reusing the manual API here would let a stale callback
        // fresh-connect after an explicit disconnect.
    }

    private long BeginExplicitLifecycleIntent()
    {
        CancellationTokenSource previousIntent;
        long generation;
        lock (_stateLock)
        {
            _connectionIntentVersion++;
            generation = ++_connectionGeneration;
            _sessionOperationsAllowed = false;
            previousIntent = _connectionIntentCts;
            _connectionIntentCts = new CancellationTokenSource();
        }

        CancelIntent(previousIntent);
        return generation;
    }

    internal long RegisterExplicitLifecycleIntent()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return ConnectionGeneration;

        var generation = BeginExplicitLifecycleIntent();
        return generation;
    }

    internal bool TryRegisterExplicitLifecycleIntent(
        long expectedIntentVersion,
        out long connectionGeneration)
    {
        CancellationTokenSource previousIntent;
        lock (_stateLock)
        {
            if (_connectionIntentVersion != expectedIntentVersion)
            {
                connectionGeneration = 0;
                return false;
            }

            _connectionIntentVersion++;
            connectionGeneration = ++_connectionGeneration;
            _sessionOperationsAllowed = false;
            previousIntent = _connectionIntentCts;
            _connectionIntentCts = new CancellationTokenSource();
        }

        CancelIntent(previousIntent);
        return true;
    }

    /// <summary>
    /// Restores usability when a UI operation registered explicit connection intent
    /// but was cancelled before it changed the session (for example, a cancelled
    /// password prompt). A newer intent always wins.
    /// </summary>
    internal void RestoreSessionAfterAbandonedIntent(long operationGeneration)
    {
        var restored = false;
        lock (_stateLock)
        {
            if (_connectionGeneration == operationGeneration
                && _state == ConnectionState.Connected
                && _client.IsConnected)
            {
                _sessionOperationsAllowed = true;
                restored = true;
            }
        }

        if (restored)
            _subscriptionManager?.AdvanceConnectionGeneration(operationGeneration);
    }

    private bool IsCurrentConnectionIntent(long generation)
    {
        lock (_stateLock)
        {
            return _connectionGeneration == generation;
        }
    }

    private bool IsCurrentAutomaticReconnectIntent(long intentVersion)
    {
        lock (_stateLock)
        {
            return _connectionIntentVersion == intentVersion
                && _state == ConnectionState.Reconnecting;
        }
    }

    private bool TryGetAutomaticReconnectIntent(
        long intentVersion,
        out long connectionGeneration,
        out CancellationToken cancellationToken)
    {
        lock (_stateLock)
        {
            if (_connectionIntentVersion != intentVersion
                || _state != ConnectionState.Reconnecting)
            {
                connectionGeneration = 0;
                cancellationToken = default;
                return false;
            }

            connectionGeneration = _connectionGeneration;
            cancellationToken = _connectionIntentCts.Token;
            return true;
        }
    }

    private bool TryGetConnectionIntentCancellationToken(
        long connectionGeneration,
        out CancellationToken cancellationToken)
    {
        lock (_stateLock)
        {
            if (_connectionGeneration != connectionGeneration)
            {
                cancellationToken = default;
                return false;
            }

            cancellationToken = _connectionIntentCts.Token;
            return true;
        }
    }

    private static void CancelIntent(CancellationTokenSource cancellation)
    {
        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private bool TryPublishAutomaticReconnect(
        long intentVersion,
        long connectionGeneration)
    {
        lock (_stateLock)
        {
            if (_connectionIntentVersion != intentVersion
                || _connectionGeneration != connectionGeneration
                || _state != ConnectionState.Reconnecting)
            {
                return false;
            }

            _state = ConnectionState.Connected;
            _sessionOperationsAllowed = true;
        }

        StateChanged?.Invoke(ConnectionState.Connected);
        return true;
    }

    private bool TryPublishConnected(long connectionGeneration)
    {
        lock (_stateLock)
        {
            if (_connectionGeneration != connectionGeneration)
                return false;

            _state = ConnectionState.Connected;
            _sessionOperationsAllowed = true;
        }

        StateChanged?.Invoke(ConnectionState.Connected);
        return true;
    }

    private bool TrySetStateForIntent(
        long connectionGeneration,
        ConnectionState state)
    {
        var changed = false;
        lock (_stateLock)
        {
            if (_connectionGeneration != connectionGeneration)
                return false;

            changed = _state != state;
            _state = state;
            _sessionOperationsAllowed = state == ConnectionState.Connected;
        }

        if (changed)
            StateChanged?.Invoke(state);
        return true;
    }

    private void SetState(ConnectionState state)
    {
        lock (_stateLock)
        {
            if (_state == state)
            {
                _sessionOperationsAllowed = state == ConnectionState.Connected;
                return;
            }

            _state = state;
            _sessionOperationsAllowed = state == ConnectionState.Connected;
        }

        StateChanged?.Invoke(state);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _client.CancelPendingReconnect();
        try
        {
            // Serialize disposal behind any in-flight lifecycle transaction. The
            // bounded wait preserves the existing shutdown behavior; the queued task
            // still owns cleanup if the timeout is reached.
            Task.Run(DisconnectAsync).Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException ex)
        {
            _logger.Warning($"Connection manager disposal warning: {ex.InnerException?.Message ?? ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.Warning($"Connection manager disposal warning: {ex.Message}");
        }
        finally
        {
            _client.Connected -= OnClientConnected;
            _client.Disconnected -= OnClientDisconnected;
            _client.ConnectionError -= OnClientConnectionError;
            _client.ReconnectRequired -= OnReconnectRequired;

            _client.Dispose();
            CancelIntent(_connectionIntentCts);
            _connectionIntentCts.Dispose();
        }
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
