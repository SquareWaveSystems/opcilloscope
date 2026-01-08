using Opc.Ua;
using Opc.Ua.Client;
using Opcilloscope.OpcUa.Models;
using Opcilloscope.Utilities;

namespace Opcilloscope.OpcUa;

/// <summary>
/// Manages OPC UA subscriptions and monitored variables using proper Publish/Subscribe.
/// Supports subscription preservation and restoration during reconnection.
/// </summary>
public class SubscriptionManager : IDisposable
{
    private readonly OpcUaClientWrapper _clientWrapper;
    private readonly Logger _logger;
    private Subscription? _subscription;
    private readonly Dictionary<uint, MonitoredNode> _monitoredVariables = new();
    private readonly Dictionary<uint, MonitoredItem> _opcMonitoredItems = new();
    // Reverse lookup: OPC MonitoredItem.ClientHandle -> our ClientHandle for O(1) notification handling
    private readonly Dictionary<uint, uint> _opcHandleToClientHandle = new();
    private uint _nextClientHandle = 1;
    private int _publishingInterval = 1000;
    private bool _isInitialized;
    private readonly object _lock = new();

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
    public event Action<uint>? VariableRemoved;

    public int PublishingInterval
    {
        get => _publishingInterval;
        set => _publishingInterval = Math.Max(100, Math.Min(10000, value));
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

    public SubscriptionManager(OpcUaClientWrapper clientWrapper, Logger logger)
    {
        _clientWrapper = clientWrapper;
        _logger = logger;
    }

    public async Task<bool> InitializeAsync()
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
        if (!_isInitialized || _subscription == null || _clientWrapper.Session == null)
        {
            _logger.Error("Subscription not initialized");
            return null;
        }

        lock (_lock)
        {
            // Check if already monitoring this node
            if (_monitoredVariables.Values.Any(m => m.NodeId.EqualsNodeId(nodeId)))
            {
                _logger.Warning($"Node {displayName} is already being monitored");
                return null;
            }
        }

        try
        {
            var clientHandle = _nextClientHandle++;

            // Create OPC UA monitored item
            var monitoredItem = new MonitoredItem(_subscription.DefaultItem)
            {
                DisplayName = displayName,
                StartNodeId = nodeId,
                AttributeId = Attributes.Value,
                SamplingInterval = 500,
                QueueSize = 10,
                DiscardOldest = true
            };

            monitoredItem.Notification += MonitoredItem_Notification;

            // Add to subscription and create the monitored item on the server
            _subscription.AddItem(monitoredItem);
            await _subscription.ApplyChangesAsync();

            if (ServiceResult.IsBad(monitoredItem.Status.Error))
            {
                _logger.Error($"Failed to create monitored item for {displayName}: {monitoredItem.Status.Error}");
                _subscription.RemoveItem(monitoredItem);
                return null;
            }

            // Create our model variable
            var variable = new MonitoredNode
            {
                ClientHandle = clientHandle,
                MonitoredItemId = monitoredItem.ClientHandle,
                NodeId = nodeId,
                DisplayName = displayName,
                Value = "(pending)",
                StatusCode = 0 // Good
            };

            lock (_lock)
            {
                _monitoredVariables[clientHandle] = variable;
                _opcMonitoredItems[clientHandle] = monitoredItem;
                _opcHandleToClientHandle[monitoredItem.ClientHandle] = clientHandle;
            }

            _logger.Info($"Subscribed to {displayName}");
            VariableAdded?.Invoke(variable);

            // Read initial value and node attributes (AccessLevel, DataType) in parallel
            await Task.WhenAll(
                ReadInitialValueAsync(variable),
                ReadNodeAttributesAsync(variable)
            );

            return variable;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to add monitored variable: {ex.Message}");
            return null;
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
            var value = await _clientWrapper.ReadValueAsync(item.NodeId);
            if (value != null)
            {
                item.Value = FormatValue(value.Value);
                item.Timestamp = value.SourceTimestamp;
                item.StatusCode = (uint)value.StatusCode.Code;
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
            // Read AccessLevel and DataType attributes
            var results = await _clientWrapper.ReadAttributesAsync(
                item.NodeId,
                Attributes.AccessLevel,
                Attributes.DataType);

            if (results.Count >= 2)
            {
                // AccessLevel
                if (StatusCode.IsGood(results[0].StatusCode) && results[0].Value is byte accessLevel)
                {
                    item.AccessLevel = accessLevel;
                }

                // DataType - this is a NodeId that we need to resolve
                if (StatusCode.IsGood(results[1].StatusCode) && results[1].Value is NodeId dataTypeNodeId)
                {
                    var (builtInType, typeName) = ResolveDataType(dataTypeNodeId);
                    item.DataType = builtInType;
                    item.DataTypeName = typeName;
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

    private (BuiltInType, string) ResolveDataType(NodeId dataTypeNodeId)
    {
        // Compare against standard OPC UA DataType NodeIds explicitly for clarity and maintainability
        if (dataTypeNodeId.NamespaceIndex == 0 && dataTypeNodeId.IdType == IdType.Numeric)
        {
            if (dataTypeNodeId.Equals(DataTypeIds.Boolean))
                return (BuiltInType.Boolean, "Boolean");
            if (dataTypeNodeId.Equals(DataTypeIds.SByte))
                return (BuiltInType.SByte, "SByte");
            if (dataTypeNodeId.Equals(DataTypeIds.Byte))
                return (BuiltInType.Byte, "Byte");
            if (dataTypeNodeId.Equals(DataTypeIds.Int16))
                return (BuiltInType.Int16, "Int16");
            if (dataTypeNodeId.Equals(DataTypeIds.UInt16))
                return (BuiltInType.UInt16, "UInt16");
            if (dataTypeNodeId.Equals(DataTypeIds.Int32))
                return (BuiltInType.Int32, "Int32");
            if (dataTypeNodeId.Equals(DataTypeIds.UInt32))
                return (BuiltInType.UInt32, "UInt32");
            if (dataTypeNodeId.Equals(DataTypeIds.Int64))
                return (BuiltInType.Int64, "Int64");
            if (dataTypeNodeId.Equals(DataTypeIds.UInt64))
                return (BuiltInType.UInt64, "UInt64");
            if (dataTypeNodeId.Equals(DataTypeIds.Float))
                return (BuiltInType.Float, "Float");
            if (dataTypeNodeId.Equals(DataTypeIds.Double))
                return (BuiltInType.Double, "Double");
            if (dataTypeNodeId.Equals(DataTypeIds.String))
                return (BuiltInType.String, "String");
            if (dataTypeNodeId.Equals(DataTypeIds.DateTime))
                return (BuiltInType.DateTime, "DateTime");
            if (dataTypeNodeId.Equals(DataTypeIds.Guid))
                return (BuiltInType.Guid, "Guid");
            if (dataTypeNodeId.Equals(DataTypeIds.ByteString))
                return (BuiltInType.ByteString, "ByteString");

            // Fallback for other namespace 0 numeric types
            return (BuiltInType.Variant, dataTypeNodeId.ToString());
        }

        return (BuiltInType.Variant, dataTypeNodeId.ToString());
    }

    public async Task<bool> RemoveNodeAsync(uint clientHandle)
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
                await _subscription.ApplyChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.Warning($"Error removing monitored variable: {ex.Message}");
            }
        }

        _logger.Info($"Unsubscribed from {variable.DisplayName}");
        VariableRemoved?.Invoke(clientHandle);
        return true;
    }

    public async Task<bool> RemoveNodeByNodeIdAsync(NodeId nodeId)
    {
        MonitoredNode? variable;
        lock (_lock)
        {
            variable = _monitoredVariables.Values.FirstOrDefault(m => m.NodeId.EqualsNodeId(nodeId));
        }

        if (variable != null)
        {
            return await RemoveNodeAsync(variable.ClientHandle);
        }
        return false;
    }

    private void ProcessValueChange(MonitoredNode variable, DataValue dataValue)
    {
        var oldValue = variable.Value;
        var newValue = FormatValue(dataValue.Value);

        variable.Value = newValue;
        variable.Timestamp = dataValue.SourceTimestamp;
        variable.StatusCode = (uint)dataValue.StatusCode.Code;

        if (oldValue != newValue)
        {
            variable.LastChangeTime = DateTime.Now;
        }

        // Always notify on subscription updates
        ValueChanged?.Invoke(variable);
    }

    internal static string FormatValue(object? value)
    {
        if (value == null) return "null";
        if (value is byte[] bytes) return $"[{bytes.Length} bytes]";
        if (value is Array arr) return $"[{arr.Length} items]";
        if (value is float f) return f.ToString("F2");
        if (value is double d) return d.ToString("F2");
        return value.ToString() ?? "null";
    }

    public async Task ClearAsync()
    {
        List<uint> handles;
        lock (_lock)
        {
            handles = _monitoredVariables.Keys.ToList();
        }
        foreach (var handle in handles)
        {
            await RemoveNodeAsync(handle);
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
            await RefreshAllValuesAsync();

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

        if (nodesToRestore.Count == 0)
        {
            _logger.Info("No subscriptions to recreate");
            // Still need to create empty subscription for future use
            return await InitializeAsync();
        }

        _logger.Info($"Recreating {nodesToRestore.Count} subscription(s)...");

        // Clean up old OPC UA objects (but keep our MonitoredNode models)
        await CleanupOpcSubscriptionAsync();

        // Create new subscription
        if (!await InitializeAsync())
        {
            _logger.Error("Failed to create new subscription");
            return false;
        }

        // Recreate monitored items
        int restored = 0;
        foreach (var (clientHandle, nodeId, displayName) in nodesToRestore)
        {
            try
            {
                var monitoredItem = new MonitoredItem(_subscription!.DefaultItem)
                {
                    DisplayName = displayName,
                    StartNodeId = nodeId,
                    AttributeId = Attributes.Value,
                    SamplingInterval = 500,
                    QueueSize = 10,
                    DiscardOldest = true
                };

                monitoredItem.Notification += MonitoredItem_Notification;
                _subscription.AddItem(monitoredItem);

                lock (_lock)
                {
                    _opcMonitoredItems[clientHandle] = monitoredItem;
                    _opcHandleToClientHandle[monitoredItem.ClientHandle] = clientHandle;
                }

                restored++;
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to recreate monitored item for {displayName}: {ex.Message}");
            }
        }

        // Apply all changes at once
        if (restored > 0)
        {
            try
            {
                await _subscription!.ApplyChangesAsync();
                _logger.Info($"Restored {restored} of {nodesToRestore.Count} subscription(s)");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to apply subscription changes: {ex.Message}");
                return false;
            }
        }

        // Read current values to update UI
        await RefreshAllValuesAsync();

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
    private async Task CleanupOpcSubscriptionAsync()
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

        if (_subscription != null)
        {
            try
            {
                if (_clientWrapper.Session != null && _clientWrapper.Session.Subscriptions.Contains(_subscription))
                {
                    await _clientWrapper.Session.RemoveSubscriptionAsync(_subscription);
                }
                _subscription.Dispose();
            }
            catch { /* Ignore cleanup errors */ }
            _subscription = null;
        }

        _isInitialized = false;
    }

    /// <summary>
    /// Marks all monitored variables as stale (pending reconnection).
    /// </summary>
    public void MarkAllAsStale()
    {
        lock (_lock)
        {
            foreach (var variable in _monitoredVariables.Values)
            {
                variable.Value = "(reconnecting...)";
                variable.StatusCode = StatusCodes.UncertainInitialValue;
                ValueChanged?.Invoke(variable);
            }
        }
    }

    public void Dispose()
    {
        if (_subscription != null && _clientWrapper.Session != null)
        {
            try
            {
                _clientWrapper.Session.RemoveSubscriptionAsync(_subscription).GetAwaiter().GetResult();
                _subscription.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to dispose OPC UA subscription: {ex}");
            }
        }

        _subscription = null;
        _isInitialized = false;
        _monitoredVariables.Clear();
        _opcMonitoredItems.Clear();
        _opcHandleToClientHandle.Clear();
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
