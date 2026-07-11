using System.Globalization;
using Opc.Ua;
using Opc.Ua.Client;
using Opcilloscope.OpcUa.Models;
using Opcilloscope.Utilities;

namespace Opcilloscope.OpcUa;

/// <summary>
/// Manages OPC UA subscriptions and monitored variables using proper Publish/Subscribe.
/// Supports subscription preservation and restoration during reconnection.
/// </summary>
public class SubscriptionManager : IDisposable, IAsyncDisposable
{
    private readonly OpcUaClientWrapper _clientWrapper;
    private readonly Logger _logger;
    private long _connectionGeneration;
    private Subscription? _subscription;
    private readonly Dictionary<uint, MonitoredNode> _monitoredVariables = new();
    private readonly Dictionary<uint, MonitoredItem> _opcMonitoredItems = new();
    // Reverse lookup: OPC MonitoredItem.ClientHandle -> our ClientHandle for O(1) notification handling
    private readonly Dictionary<uint, uint> _opcHandleToClientHandle = new();
    private uint _nextClientHandle = 1;
    private int _publishingInterval = 250;
    private int _samplingInterval = 250;
    private uint _queueSize = 10;
    private bool _isInitialized;
    private readonly object _lock = new();
    // OPC Foundation Subscription mutations are not safe to overlap. This gate
    // covers the complete local/server transaction (including ApplyChangesAsync)
    // and is also acquired by reconnect cleanup and disposal.
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private int _disposeStarted;

    /// <summary>
    /// Raised when a monitored variable value changes.
    /// </summary>
    public event Action<MonitoredNode>? ValueChanged;

    /// <summary>
    /// Raised when a new monitored variable is added.
    /// </summary>
    public event Action<MonitoredNode>? VariableAdded;

    /// <summary>
    /// Raised when a monitored variable is removed.
    /// </summary>
    public event Action<uint, long>? VariableRemoved;

    public int PublishingInterval
    {
        get => _publishingInterval;
        set => _publishingInterval = Math.Max(100, Math.Min(10000, value));
    }

    /// <summary>
    /// Sampling interval in milliseconds applied to monitored items.
    /// 0 requests the server's fastest practical rate.
    /// </summary>
    public int SamplingInterval
    {
        get => _samplingInterval;
        set => _samplingInterval = Math.Max(0, Math.Min(60000, value));
    }

    /// <summary>
    /// Server-side notification queue size applied to newly created monitored items.
    /// </summary>
    public uint QueueSize
    {
        get => _queueSize;
        set => _queueSize = Math.Max(1, Math.Min(1000, value));
    }

    public IReadOnlyCollection<MonitoredNode> MonitoredVariables
    {
        get
        {
            lock (_lock)
            {
                return _monitoredVariables.Values.ToList();
            }
        }
    }

    public SubscriptionManager(
        OpcUaClientWrapper clientWrapper,
        Logger logger,
        long connectionGeneration = 0)
    {
        _clientWrapper = clientWrapper;
        _logger = logger;
        _connectionGeneration = connectionGeneration;
    }

    /// <summary>
    /// Advances the provenance of nodes retained across a successful reconnect.
    /// New SubscriptionManager instances receive their generation in the
    /// constructor; transferred/recreated subscriptions keep their models and
    /// therefore need those models advanced in place before values resume.
    /// </summary>
    internal void AdvanceConnectionGeneration(long connectionGeneration)
    {
        lock (_lock)
        {
            _connectionGeneration = connectionGeneration;
            foreach (var variable in _monitoredVariables.Values)
            {
                variable.ConnectionGeneration = connectionGeneration;
            }
        }
    }

    public async Task<bool> InitializeAsync()
    {
        await _mutationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            return Volatile.Read(ref _disposeStarted) == 0
                && await InitializeCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    private async Task<bool> InitializeCoreAsync()
    {
        if (_clientWrapper.Session == null || !_clientWrapper.IsConnected)
        {
            _logger.Error("Cannot initialize subscription: not connected");
            return false;
        }

        try
        {
            // Create subscription
            _subscription = new Subscription(_clientWrapper.Session.DefaultSubscription)
            {
                DisplayName = "Opcilloscope Subscription",
                PublishingEnabled = true,
                PublishingInterval = _publishingInterval,
                KeepAliveCount = 10,
                LifetimeCount = 100,
                MaxNotificationsPerPublish = 1000,
                Priority = 0
            };

            _clientWrapper.Session.AddSubscription(_subscription);
            await _subscription.CreateAsync();

            _isInitialized = true;
            _logger.Info($"Subscription created (ID: {_subscription.Id}, Interval: {_publishingInterval}ms)");

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to create subscription: {ex.Message}");
            return false;
        }
    }

    public async Task<MonitoredNode?> AddNodeAsync(NodeId nodeId, string displayName)
    {
        await _mutationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _disposeStarted) != 0)
                return null;

            return await AddNodeCoreAsync(nodeId, displayName).ConfigureAwait(false);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    private async Task<MonitoredNode?> AddNodeCoreAsync(NodeId nodeId, string displayName)
    {
        if (!_isInitialized || _subscription == null || _clientWrapper.Session == null)
        {
            _logger.Error("Subscription not initialized");
            return null;
        }

        uint clientHandle;
        lock (_lock)
        {
            // Check if already monitoring this node
            if (_monitoredVariables.Values.Any(m => m.NodeId.EqualsNodeId(nodeId)))
            {
                _logger.Warning($"Node {displayName} is already being monitored");
                return null;
            }

            // Allocate the handle under the lock: it keys three dictionaries, and an
            // unsynchronized increment lets two concurrent adds collide on one handle.
            clientHandle = _nextClientHandle++;
        }

        var subscription = _subscription;
        MonitoredItem? monitoredItem = null;
        var publishedLocally = false;
        try
        {
            // Create OPC UA monitored item
            monitoredItem = new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = displayName,
                StartNodeId = nodeId,
                AttributeId = Attributes.Value,
                SamplingInterval = _samplingInterval,
                QueueSize = _queueSize,
                DiscardOldest = true
            };

            monitoredItem.Notification += MonitoredItem_Notification;

            // Add to subscription and create the monitored item on the server
            subscription.AddItem(monitoredItem);
            await subscription.ApplyChangesAsync().ConfigureAwait(false);

            if (!monitoredItem.Status.Created || ServiceResult.IsBad(monitoredItem.Status.Error))
            {
                var status = monitoredItem.Status.Error?.ToString()
                    ?? "the server did not create the monitored item";
                _logger.Error($"Failed to create monitored item for {displayName}: {status}");
                await RollbackMonitoredItemsCoreAsync(
                    subscription,
                    [monitoredItem],
                    $"failed add for {displayName}").ConfigureAwait(false);
                return null;
            }

            // DisposeAsync marks disposal before waiting for this gate. Do not
            // publish a successful item after teardown has already been requested.
            if (Volatile.Read(ref _disposeStarted) != 0)
            {
                await RollbackMonitoredItemsCoreAsync(
                    subscription,
                    [monitoredItem],
                    $"cancelled add for {displayName}").ConfigureAwait(false);
                return null;
            }

            // Create our model variable
            var variable = new MonitoredNode
            {
                ClientHandle = clientHandle,
                NodeId = nodeId,
                DisplayName = displayName,
                Value = "(pending)",
                RawValue = "(pending)",
                StatusCode = 0 // Good
            };

            lock (_lock)
            {
                // Read the generation at publication time while sharing the
                // same lock as AdvanceConnectionGeneration. An add that overlaps
                // a reconnect can therefore never publish the prior generation.
                variable.ConnectionGeneration = _connectionGeneration;
                _monitoredVariables[clientHandle] = variable;
                _opcMonitoredItems[clientHandle] = monitoredItem;
                _opcHandleToClientHandle[monitoredItem.ClientHandle] = clientHandle;
                publishedLocally = true;
            }

            _logger.Info($"Subscribed to {displayName}");
            try
            {
                VariableAdded?.Invoke(variable);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                // Observer failures must not turn a committed item into a null
                // result while leaving it present in the dictionaries.
                _logger.Warning($"Variable-added handler failed for {displayName}: {ex.Message}");
            }

            // Read initial value and node attributes (AccessLevel, DataType) in parallel
            await Task.WhenAll(
                ReadInitialValueAsync(variable),
                ReadNodeAttributesAsync(variable)
            ).ConfigureAwait(false);

            return variable;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to add monitored variable: {ex.Message}");
            if (monitoredItem != null)
            {
                if (publishedLocally)
                {
                    lock (_lock)
                    {
                        _monitoredVariables.Remove(clientHandle);
                        _opcMonitoredItems.Remove(clientHandle);
                        _opcHandleToClientHandle.Remove(monitoredItem.ClientHandle);
                    }
                }

                await RollbackMonitoredItemsCoreAsync(
                    subscription,
                    [monitoredItem],
                    $"exception while adding {displayName}").ConfigureAwait(false);
            }
            return null;
        }
    }

    /// <summary>
    /// Detaches and removes locally-added monitored items, then best-effort applies
    /// the removal in case a preceding create reached the server before failing.
    /// The caller must hold <see cref="_mutationGate"/>.
    /// </summary>
    private async Task RollbackMonitoredItemsCoreAsync(
        Subscription subscription,
        IEnumerable<MonitoredItem> monitoredItems,
        string context)
    {
        var removedAny = false;
        foreach (var item in monitoredItems)
        {
            item.Notification -= MonitoredItem_Notification;
            try
            {
                subscription.RemoveItem(item);
                removedAny = true;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                _logger.Warning($"Failed to remove local monitored item during {context}: {ex.Message}");
            }
        }

        if (!removedAny || !subscription.Created)
            return;

        try
        {
            await subscription.ApplyChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            _logger.Warning($"Failed to apply monitored-item rollback during {context}: {ex.Message}");
        }
    }

    private void MonitoredItem_Notification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        try
        {
            if (e.NotificationValue is MonitoredItemNotification notification)
            {
                // Find our variable using O(1) reverse lookup
                MonitoredNode? variable = null;
                lock (_lock)
                {
                    if (_opcHandleToClientHandle.TryGetValue(monitoredItem.ClientHandle, out var clientHandle))
                    {
                        _monitoredVariables.TryGetValue(clientHandle, out variable);
                    }
                }

                if (variable != null)
                {
                    ProcessValueChange(variable, notification.Value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"Error processing notification: {ex.Message}");
        }
    }

    private async Task ReadInitialValueAsync(MonitoredNode item)
    {
        try
        {
            var session = _clientWrapper.Session;
            if (session == null)
                return;

            var value = await OpcUaClientWrapper
                .ReadValueCoreAsync(session, item.NodeId)
                .ConfigureAwait(false);
            if (value != null)
            {
                item.Value = FormatValue(value.Value);
                item.RawValue = FormatRawValue(value.Value);
                item.Timestamp = value.SourceTimestamp;
                item.StatusCode = (uint)value.StatusCode.Code;
                item.IsSyntheticValue = false;
                ValueChanged?.Invoke(item);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"Failed to read initial value for {item.DisplayName}: {ex.Message}");
        }
    }

    private async Task ReadNodeAttributesAsync(MonitoredNode item)
    {
        try
        {
            var session = _clientWrapper.Session;
            if (session == null)
                return;

            // Read both server-wide and per-user access plus value shape. The
            // write dialog currently supports scalar values only.
            var results = await OpcUaClientWrapper.ReadAttributesCoreAsync(
                    session,
                    item.NodeId,
                    Attributes.AccessLevel,
                    Attributes.UserAccessLevel,
                    Attributes.DataType,
                    Attributes.ValueRank)
                .ConfigureAwait(false);

            if (results.Count >= 4)
            {
                // AccessLevel
                if (StatusCode.IsGood(results[0].StatusCode) && results[0].Value is byte accessLevel)
                {
                    item.AccessLevel = accessLevel;
                }

                // UserAccessLevel is the permission that applies to the active
                // identity and must drive write affordances.
                if (StatusCode.IsGood(results[1].StatusCode) && results[1].Value is byte userAccessLevel)
                {
                    item.UserAccessLevel = userAccessLevel;
                }

                // DataType - this is a NodeId that we need to resolve
                if (StatusCode.IsGood(results[2].StatusCode) && results[2].Value is NodeId dataTypeNodeId)
                {
                    var (builtInType, typeName) = DataTypeResolver.Resolve(dataTypeNodeId);
                    item.DataType = builtInType;
                    item.DataTypeName = typeName;
                }

                if (StatusCode.IsGood(results[3].StatusCode) && results[3].Value is int valueRank)
                {
                    item.ValueRank = valueRank;
                }
            }

            // Notify UI about updated attributes
            ValueChanged?.Invoke(item);
        }
        catch (Exception ex)
        {
            _logger.Warning($"Failed to read attributes for {item.DisplayName}: {ex.Message}");
        }
    }

    public async Task<bool> RemoveNodeAsync(uint clientHandle)
    {
        await _mutationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _disposeStarted) != 0)
                return false;

            return await RemoveNodeCoreAsync(clientHandle).ConfigureAwait(false);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    private async Task<bool> RemoveNodeCoreAsync(uint clientHandle)
    {
        MonitoredNode? variable;
        MonitoredItem? opcItem;

        lock (_lock)
        {
            if (!_monitoredVariables.TryGetValue(clientHandle, out variable))
                return false;

            _opcMonitoredItems.TryGetValue(clientHandle, out opcItem);

            // Clean up reverse lookup
            if (opcItem != null)
            {
                _opcHandleToClientHandle.Remove(opcItem.ClientHandle);
            }

            _monitoredVariables.Remove(clientHandle);
            _opcMonitoredItems.Remove(clientHandle);
        }

        if (opcItem != null && _subscription != null)
        {
            try
            {
                opcItem.Notification -= MonitoredItem_Notification;
                _subscription.RemoveItem(opcItem);
                await _subscription.ApplyChangesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Warning($"Error removing monitored variable: {ex.Message}");
            }
        }

        _logger.Info($"Unsubscribed from {variable.DisplayName}");
        try
        {
            VariableRemoved?.Invoke(clientHandle, _connectionGeneration);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            _logger.Warning($"Variable-removed handler failed for {variable.DisplayName}: {ex.Message}");
        }
        return true;
    }

    private void ProcessValueChange(MonitoredNode variable, DataValue dataValue)
    {
        var oldValue = variable.Value;
        var newValue = FormatValue(dataValue.Value);

        variable.Value = newValue;
        variable.RawValue = FormatRawValue(dataValue.Value);
        variable.Timestamp = dataValue.SourceTimestamp;
        variable.StatusCode = (uint)dataValue.StatusCode.Code;
        variable.IsSyntheticValue = false;

        if (oldValue != newValue)
        {
            variable.LastChangeTime = DateTime.Now;
        }

        // Always notify on subscription updates
        ValueChanged?.Invoke(variable);
    }

    /// <summary>
    /// Formats a value for on-screen display. Intentionally culture-aware and
    /// truncated ("F2") for readability; never use this for data export -
    /// use <see cref="FormatRawValue"/> instead.
    /// </summary>
    internal static string FormatValue(object? value)
    {
        if (value == null) return "null";
        if (value is byte[] bytes) return $"[{bytes.Length} bytes]";
        if (value is Array arr) return $"[{arr.Length} items]";
        if (value is float f) return f.ToString("F2");
        if (value is double d) return d.ToString("F2");
        return value.ToString() ?? "null";
    }

    /// <summary>
    /// Formats a value for data export (CSV recording): full precision,
    /// culture-invariant. Floating point uses round-trip formatting, DateTime
    /// uses ISO 8601 ("O"), other IFormattable types use InvariantCulture, and
    /// arrays are serialized as their actual elements joined with ';'
    /// (e.g. "1;2;3"), so the field never needs CSV comma-escaping for the
    /// separator itself. Within array elements, '\' and ';' are escaped as
    /// "\\" and "\;" so multi-element string arrays remain unambiguous.
    /// </summary>
    internal static string FormatRawValue(object? value)
    {
        if (value == null) return "null";
        if (value is string s) return s;
        // Round-trip floating point: default ToString is shortest round-trippable
        // on modern .NET; pin the culture so the decimal separator is always '.'.
        if (value is float f) return f.ToString(CultureInfo.InvariantCulture);
        if (value is double d) return d.ToString(CultureInfo.InvariantCulture);
        // ISO 8601 round-trip format for timestamps embedded in values.
        if (value is DateTime dt) return dt.ToString("O", CultureInfo.InvariantCulture);
        if (value is Array arr)
        {
            // Serialize the actual elements (semicolon-joined) instead of the
            // lossy "[N items]" display placeholder. Escape '\' and ';' inside
            // elements so ["a;b","c"] is distinguishable from ["a","b","c"].
            var parts = new List<string>(arr.Length);
            foreach (var element in arr)
            {
                parts.Add(FormatRawValue(element).Replace("\\", "\\\\").Replace(";", "\\;"));
            }
            return string.Join(";", parts);
        }
        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }
        return value.ToString() ?? "null";
    }

    public async Task ClearAsync()
    {
        await _mutationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _disposeStarted) != 0)
                return;

            await ClearCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    private async Task ClearCoreAsync()
    {
        List<uint> handles;
        lock (_lock)
        {
            handles = _monitoredVariables.Keys.ToList();
        }
        foreach (var handle in handles)
        {
            await RemoveNodeCoreAsync(handle).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets the underlying OPC UA subscription for transfer during reconnection.
    /// </summary>
    public Subscription? GetOpcSubscription() => _subscription;

    /// <summary>
    /// Gets information about all currently monitored nodes for restoration.
    /// </summary>
    public IReadOnlyList<(NodeId NodeId, string DisplayName)> GetMonitoredNodeInfo()
    {
        lock (_lock)
        {
            return _monitoredVariables.Values
                .Select(v => (v.NodeId, v.DisplayName))
                .ToList();
        }
    }

    /// <summary>
    /// Checks if the subscription is still valid after reconnection.
    /// If the session transferred subscriptions successfully, this will return true.
    /// </summary>
    public bool IsSubscriptionValid()
    {
        if (_subscription == null || _clientWrapper.Session == null)
            return false;

        // Check if our subscription is still in the session
        return _clientWrapper.Session.Subscriptions.Contains(_subscription) &&
               _subscription.Created;
    }

    /// <summary>
    /// Reattaches to a transferred subscription after session reconnection.
    /// Call this when Session.Reconnect() or TransferSubscriptions succeeded.
    /// </summary>
    public async Task<bool> ReattachAfterReconnectAsync()
    {
        await _mutationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            return Volatile.Read(ref _disposeStarted) == 0
                && await ReattachAfterReconnectCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    private async Task<bool> ReattachAfterReconnectCoreAsync()
    {
        if (_clientWrapper.Session == null)
        {
            _logger.Error("Cannot reattach: no session");
            return false;
        }

        // Check if our subscription was transferred successfully
        if (_subscription != null && _clientWrapper.Session.Subscriptions.Contains(_subscription))
        {
            // Subscription was transferred - refresh notification handlers as defensive programming.
            // While handlers should persist through transfer, explicitly re-wiring ensures they're
            // correctly attached to the MonitoredItem instances in the new session context.
            _logger.Info("Subscription was preserved - refreshing notification handlers");

            lock (_lock)
            {
                foreach (var item in _opcMonitoredItems.Values)
                {
                    item.Notification -= MonitoredItem_Notification;
                    item.Notification += MonitoredItem_Notification;
                }
            }

            // Read current values to update UI
            await RefreshAllValuesAsync().ConfigureAwait(false);

            return true;
        }

        // Subscription wasn't transferred - need to recreate
        _logger.Warning("Subscription was not preserved - needs recreation");
        return false;
    }

    /// <summary>
    /// Recreates subscriptions after reconnection when transfer failed.
    /// Preserves the MonitoredNode models and recreates the OPC UA subscription.
    /// </summary>
    public async Task<bool> RecreateSubscriptionsAsync()
    {
        await _mutationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            return Volatile.Read(ref _disposeStarted) == 0
                && await RecreateSubscriptionsCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    private async Task<bool> RecreateSubscriptionsCoreAsync()
    {
        if (_clientWrapper.Session == null || !_clientWrapper.IsConnected)
        {
            _logger.Error("Cannot recreate subscriptions: not connected");
            return false;
        }

        // Capture current monitored nodes before clearing OPC UA state
        List<(uint ClientHandle, NodeId NodeId, string DisplayName)> nodesToRestore;
        lock (_lock)
        {
            nodesToRestore = _monitoredVariables.Values
                .Select(v => (v.ClientHandle, v.NodeId, v.DisplayName))
                .ToList();
        }

        // Replace the old OPC subscription even when it contains no monitored
        // nodes; otherwise recreation would leak a second empty subscription.
        await CleanupOpcSubscriptionCoreAsync().ConfigureAwait(false);

        if (nodesToRestore.Count == 0)
        {
            _logger.Info("No subscriptions to recreate");
            // Still need to create empty subscription for future use
            return await InitializeCoreAsync().ConfigureAwait(false);
        }

        _logger.Info($"Recreating {nodesToRestore.Count} subscription(s)...");

        // Create new subscription
        if (!await InitializeCoreAsync().ConfigureAwait(false))
        {
            _logger.Error("Failed to create new subscription");
            return false;
        }

        // Build all replacement items first, but do not publish their handle maps
        // until the server has accepted every item. The boolean method contract is
        // intentionally all-or-nothing so ConnectionManager can run its loss fallback.
        var subscription = _subscription!;
        var pendingItems = new List<(uint ClientHandle, string DisplayName, MonitoredItem Item)>();
        var constructionFailed = false;
        foreach (var (clientHandle, nodeId, displayName) in nodesToRestore)
        {
            try
            {
                var monitoredItem = new MonitoredItem(subscription.DefaultItem)
                {
                    DisplayName = displayName,
                    StartNodeId = nodeId,
                    AttributeId = Attributes.Value,
                    SamplingInterval = _samplingInterval,
                    QueueSize = _queueSize,
                    DiscardOldest = true
                };

                monitoredItem.Notification += MonitoredItem_Notification;
                subscription.AddItem(monitoredItem);
                pendingItems.Add((clientHandle, displayName, monitoredItem));
            }
            catch (Exception ex)
            {
                constructionFailed = true;
                _logger.Warning($"Failed to recreate monitored item for {displayName}: {ex.Message}");
            }
        }

        // Apply all changes at once
        if (pendingItems.Count > 0)
        {
            try
            {
                await subscription.ApplyChangesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to apply subscription changes: {ex.Message}");
                await RollbackMonitoredItemsCoreAsync(
                    subscription,
                    pendingItems.Select(p => p.Item),
                    "failed subscription recreation").ConfigureAwait(false);
                return false;
            }
        }

        var failedItems = pendingItems
            .Where(p => !p.Item.Status.Created || ServiceResult.IsBad(p.Item.Status.Error))
            .ToList();
        var restored = pendingItems.Count - failedItems.Count;

        foreach (var failed in failedItems)
        {
            var status = failed.Item.Status.Error?.ToString()
                ?? "the server did not create the monitored item";
            _logger.Warning($"Failed to recreate monitored item for {failed.DisplayName}: {status}");
        }

        if (constructionFailed || failedItems.Count > 0 || pendingItems.Count != nodesToRestore.Count)
        {
            await RollbackMonitoredItemsCoreAsync(
                subscription,
                pendingItems.Select(p => p.Item),
                "partial subscription recreation").ConfigureAwait(false);
            _logger.Error(
                $"Restored {restored} of {nodesToRestore.Count} subscription(s); " +
                "reporting failure so stale UI handles can be removed.");
            return false;
        }

        lock (_lock)
        {
            foreach (var pending in pendingItems)
            {
                _opcMonitoredItems[pending.ClientHandle] = pending.Item;
                _opcHandleToClientHandle[pending.Item.ClientHandle] = pending.ClientHandle;
            }
        }

        _logger.Info($"Restored {restored} of {nodesToRestore.Count} subscription(s)");

        // Read current values to update UI
        await RefreshAllValuesAsync().ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Refreshes all monitored variable values by reading from the server.
    /// Uses parallel reads for better performance with remote servers.
    /// </summary>
    private async Task RefreshAllValuesAsync()
    {
        List<MonitoredNode> variables;
        lock (_lock)
        {
            variables = _monitoredVariables.Values.ToList();
        }

        // Read all values in parallel for better performance
        await Task.WhenAll(variables.Select(ReadInitialValueAsync));
    }

    /// <summary>
    /// Cleans up the OPC UA subscription objects without clearing our MonitoredNode models.
    /// </summary>
    private async Task CleanupOpcSubscriptionCoreAsync()
    {
        lock (_lock)
        {
            foreach (var item in _opcMonitoredItems.Values)
            {
                item.Notification -= MonitoredItem_Notification;
            }
            _opcMonitoredItems.Clear();
            _opcHandleToClientHandle.Clear();
        }

        var subscription = _subscription;
        _subscription = null;
        if (subscription != null)
        {
            try
            {
                if (_clientWrapper.Session != null && _clientWrapper.Session.Subscriptions.Contains(subscription))
                {
                    await _clientWrapper.Session
                        .RemoveSubscriptionAsync(subscription)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to cleanup OPC subscription: {ex.Message}");
            }
            finally
            {
                subscription.Dispose();
            }
        }

        _isInitialized = false;
    }

    /// <summary>
    /// Marks all monitored variables as stale (pending reconnection).
    /// </summary>
    public void MarkAllAsStale()
    {
        List<MonitoredNode> staleVariables;
        lock (_lock)
        {
            staleVariables = _monitoredVariables.Values.ToList();
            foreach (var variable in staleVariables)
            {
                variable.Value = "(reconnecting...)";
                variable.RawValue = "(reconnecting...)";
                variable.StatusCode = StatusCodes.UncertainInitialValue;
                variable.IsSyntheticValue = true;
            }
        }

        // Raise events outside the lock to avoid invoking handlers while holding it.
        foreach (var variable in staleVariables)
        {
            ValueChanged?.Invoke(variable);
        }
    }

    /// <summary>
    /// Asynchronously disposes of the subscription manager and cleans up OPC UA resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            GC.SuppressFinalize(this);
            return;
        }

        await _mutationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await CleanupOpcSubscriptionCoreAsync().ConfigureAwait(false);

            lock (_lock)
            {
                _monitoredVariables.Clear();
                _opcMonitoredItems.Clear();
                _opcHandleToClientHandle.Clear();
            }
        }
        finally
        {
            _mutationGate.Release();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Synchronously disposes of the subscription manager.
    /// Uses a timeout to avoid deadlocks when called from a synchronization context.
    /// </summary>
    public void Dispose()
    {
        try
        {
            // Use Task.Run to avoid deadlock when called from a synchronization context
            Task.Run(async () => await DisposeAsync()).Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException ex)
        {
            _logger?.Warning($"Disposal warning: {ex.InnerException?.Message ?? ex.Message}");
        }
        catch (Exception ex)
        {
            _logger?.Warning($"Disposal warning: {ex.Message}");
        }

        GC.SuppressFinalize(this);
    }
}

// Extension method for NodeId comparison
public static class NodeIdExtensions
{
    public static bool EqualsNodeId(this NodeId nodeId, NodeId other)
    {
        if (nodeId == null && other == null) return true;
        if (nodeId == null || other == null) return false;
        return nodeId.Equals(other);
    }
}
