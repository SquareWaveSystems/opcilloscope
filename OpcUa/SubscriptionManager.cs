using Opc.Ua;
using Opc.Ua.Client;
using OpcScope.OpcUa.Models;
using OpcScope.Utilities;

namespace OpcScope.OpcUa;

/// <summary>
/// Manages OPC UA subscriptions and monitored items using proper Publish/Subscribe.
/// </summary>
public class SubscriptionManager : IDisposable
{
    private readonly OpcUaClientWrapper _clientWrapper;
    private readonly Logger _logger;
    private Subscription? _subscription;
    private readonly Dictionary<uint, MonitoredNode> _monitoredItems = new();
    private readonly Dictionary<uint, MonitoredItem> _opcMonitoredItems = new();
    private uint _nextClientHandle = 1;
    private int _publishingInterval = 1000;
    private bool _isInitialized;
    private readonly object _lock = new();

    public event Action<MonitoredNode>? ValueChanged;
    public event Action<MonitoredNode>? ItemAdded;
    public event Action<uint>? ItemRemoved;

    public int PublishingInterval
    {
        get => _publishingInterval;
        set => _publishingInterval = Math.Max(100, Math.Min(10000, value));
    }

    public IReadOnlyCollection<MonitoredNode> MonitoredItems
    {
        get
        {
            lock (_lock)
            {
                return _monitoredItems.Values.ToList();
            }
        }
    }

    public SubscriptionManager(OpcUaClientWrapper clientWrapper, Logger logger)
    {
        _clientWrapper = clientWrapper;
        _logger = logger;
    }

    public bool Initialize()
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
                DisplayName = "OpcScope Subscription",
                PublishingEnabled = true,
                PublishingInterval = _publishingInterval,
                KeepAliveCount = 10,
                LifetimeCount = 100,
                MaxNotificationsPerPublish = 1000,
                Priority = 0
            };

            _clientWrapper.Session.AddSubscription(_subscription);
            _subscription.Create();

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

    public MonitoredNode? AddNode(NodeId nodeId, string displayName)
    {
        if (!_isInitialized || _subscription == null || _clientWrapper.Session == null)
        {
            _logger.Error("Subscription not initialized");
            return null;
        }

        lock (_lock)
        {
            // Check if already monitoring this node
            if (_monitoredItems.Values.Any(m => m.NodeId.EqualsNodeId(nodeId)))
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
            _subscription.ApplyChanges();

            if (ServiceResult.IsBad(monitoredItem.Status.Error))
            {
                _logger.Error($"Failed to create monitored item for {displayName}: {monitoredItem.Status.Error}");
                _subscription.RemoveItem(monitoredItem);
                return null;
            }

            // Create our model item
            var item = new MonitoredNode
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
                _monitoredItems[clientHandle] = item;
                _opcMonitoredItems[clientHandle] = monitoredItem;
            }

            _logger.Info($"Subscribed to {displayName}");
            ItemAdded?.Invoke(item);

            // Read initial value
            ReadInitialValue(item);

            return item;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to add monitored item: {ex.Message}");
            return null;
        }
    }

    private void MonitoredItem_Notification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        try
        {
            if (e.NotificationValue is MonitoredItemNotification notification)
            {
                // Find our item by the OPC monitored item's client handle
                MonitoredNode? item = null;
                lock (_lock)
                {
                    var matchingKvp = _opcMonitoredItems.Where(kvp => kvp.Value.ClientHandle == monitoredItem.ClientHandle).FirstOrDefault();
                    if (matchingKvp.Value != null)
                    {
                        _monitoredItems.TryGetValue(matchingKvp.Key, out item);
                    }
                }

                if (item != null)
                {
                    ProcessValueChange(item, notification.Value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"Error processing notification: {ex.Message}");
        }
    }

    private void ReadInitialValue(MonitoredNode item)
    {
        try
        {
            var value = _clientWrapper.ReadValue(item.NodeId);
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

    public bool RemoveNode(uint clientHandle)
    {
        MonitoredNode? item;
        MonitoredItem? opcItem;

        lock (_lock)
        {
            if (!_monitoredItems.TryGetValue(clientHandle, out item))
                return false;

            _opcMonitoredItems.TryGetValue(clientHandle, out opcItem);
            _monitoredItems.Remove(clientHandle);
            _opcMonitoredItems.Remove(clientHandle);
        }

        if (opcItem != null && _subscription != null)
        {
            try
            {
                opcItem.Notification -= MonitoredItem_Notification;
                _subscription.RemoveItem(opcItem);
                _subscription.ApplyChanges();
            }
            catch (Exception ex)
            {
                _logger.Warning($"Error removing monitored item: {ex.Message}");
            }
        }

        _logger.Info($"Unsubscribed from {item.DisplayName}");
        ItemRemoved?.Invoke(clientHandle);
        return true;
    }

    public bool RemoveNodeByNodeId(NodeId nodeId)
    {
        MonitoredNode? item;
        lock (_lock)
        {
            item = _monitoredItems.Values.FirstOrDefault(m => m.NodeId.EqualsNodeId(nodeId));
        }

        if (item != null)
        {
            return RemoveNode(item.ClientHandle);
        }
        return false;
    }

    private void ProcessValueChange(MonitoredNode item, DataValue dataValue)
    {
        var oldValue = item.Value;
        var newValue = FormatValue(dataValue.Value);

        item.Value = newValue;
        item.Timestamp = dataValue.SourceTimestamp;
        item.StatusCode = (uint)dataValue.StatusCode.Code;

        if (oldValue != newValue)
        {
            item.LastChangeTime = DateTime.Now;
        }

        // Always notify on subscription updates
        ValueChanged?.Invoke(item);
    }

    private static string FormatValue(object? value)
    {
        if (value == null) return "null";
        if (value is byte[] bytes) return $"[{bytes.Length} bytes]";
        if (value is Array arr) return $"[{arr.Length} items]";
        if (value is float f) return f.ToString("F2");
        if (value is double d) return d.ToString("F2");
        return value.ToString() ?? "null";
    }

    public void Clear()
    {
        lock (_lock)
        {
            var handles = _monitoredItems.Keys.ToList();
            foreach (var handle in handles)
            {
                RemoveNode(handle);
            }
        }
    }

    public void Dispose()
    {
        if (_subscription != null && _clientWrapper.Session != null)
        {
            try
            {
                _clientWrapper.Session.RemoveSubscription(_subscription);
                _subscription.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to dispose OPC UA subscription: {ex}");
            }
        }

        _subscription = null;
        _isInitialized = false;
        _monitoredItems.Clear();
        _opcMonitoredItems.Clear();
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
