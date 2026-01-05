using LibUA;
using LibUA.Core;
using OpcScope.OpcUa.Models;
using OpcScope.Utilities;

namespace OpcScope.OpcUa;

/// <summary>
/// Manages OPC UA subscriptions and monitored items with publish loop.
/// </summary>
public class SubscriptionManager : IDisposable
{
    private readonly OpcUaClientWrapper _clientWrapper;
    private readonly Logger _logger;
    private uint _subscriptionId;
    private readonly Dictionary<uint, MonitoredNode> _monitoredItems = new();
    private uint _nextClientHandle = 1;
    private CancellationTokenSource? _publishCts;
    private Task? _publishTask;
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
        if (_clientWrapper.UnderlyingClient == null || !_clientWrapper.IsConnected)
        {
            _logger.Error("Cannot initialize subscription: not connected");
            return false;
        }

        try
        {
            var client = _clientWrapper.UnderlyingClient;

            // Create subscription
            var result = client.CreateSubscription(
                (double)_publishingInterval,
                10,     // LifetimeCount
                3,      // MaxKeepAliveCount
                1000,   // MaxNotificationsPerPublish
                true,   // PublishingEnabled
                0,      // Priority
                out _subscriptionId
            );

            if (result != StatusCode.Good)
            {
                throw new Exception($"CreateSubscription failed: 0x{result:X8}");
            }

            _isInitialized = true;
            _logger.Info($"Subscription created (ID: {_subscriptionId}, Interval: {_publishingInterval}ms)");

            // Start publish loop
            StartPublishLoop();

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
        if (!_isInitialized || _clientWrapper.UnderlyingClient == null)
        {
            _logger.Error("Subscription not initialized");
            return null;
        }

        lock (_lock)
        {
            // Check if already monitoring this node
            if (_monitoredItems.Values.Any(m => m.NodeId.Equals(nodeId)))
            {
                _logger.Warning($"Node {displayName} is already being monitored");
                return null;
            }
        }

        try
        {
            var clientHandle = _nextClientHandle++;
            var client = _clientWrapper.UnderlyingClient;

            client.CreateMonitoredItems(
                _subscriptionId,
                TimestampsToReturn.Both,
                new[]
                {
                    new MonitoredItemCreateRequest(
                        new ReadValueId(nodeId, NodeAttribute.Value, null, new QualifiedName(0, null)),
                        MonitoringMode.Reporting,
                        new MonitoringParameters(
                            clientHandle,
                            500,    // SamplingInterval
                            null,   // Filter
                            100,    // QueueSize
                            true    // DiscardOldest
                        )
                    )
                },
                out var results
            );

            if (results != null && results.Length > 0 && results[0].StatusCode == StatusCode.Good)
            {
                var item = new MonitoredNode
                {
                    ClientHandle = clientHandle,
                    MonitoredItemId = results[0].MonitoredItemId,
                    NodeId = nodeId,
                    DisplayName = displayName,
                    Value = "(pending)",
                    StatusCode = StatusCode.Good
                };

                lock (_lock)
                {
                    _monitoredItems[clientHandle] = item;
                }

                _logger.Info($"Subscribed to {displayName}");
                ItemAdded?.Invoke(item);

                // Read initial value
                ReadInitialValue(item);

                return item;
            }
            else
            {
                var statusCode = results?.FirstOrDefault()?.StatusCode ?? StatusCode.Bad;
                _logger.Error($"Failed to create monitored item for {displayName}: 0x{statusCode:X8}");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to add monitored item: {ex.Message}");
            return null;
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
                item.StatusCode = value.StatusCode;
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"Failed to read initial value for {item.DisplayName}: {ex.Message}");
        }
    }

    public bool RemoveNode(uint clientHandle)
    {
        if (!_isInitialized || _clientWrapper.UnderlyingClient == null)
            return false;

        MonitoredNode? item;
        lock (_lock)
        {
            if (!_monitoredItems.TryGetValue(clientHandle, out item))
                return false;
        }

        try
        {
            var client = _clientWrapper.UnderlyingClient;

            client.DeleteMonitoredItems(
                _subscriptionId,
                new[] { item.MonitoredItemId },
                out _
            );

            lock (_lock)
            {
                _monitoredItems.Remove(clientHandle);
            }

            _logger.Info($"Unsubscribed from {item.DisplayName}");
            ItemRemoved?.Invoke(clientHandle);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to remove monitored item: {ex.Message}");
            return false;
        }
    }

    public bool RemoveNodeByNodeId(NodeId nodeId)
    {
        MonitoredNode? item;
        lock (_lock)
        {
            item = _monitoredItems.Values.FirstOrDefault(m => m.NodeId.Equals(nodeId));
        }

        if (item != null)
        {
            return RemoveNode(item.ClientHandle);
        }
        return false;
    }

    private void StartPublishLoop()
    {
        _publishCts = new CancellationTokenSource();
        _publishTask = Task.Run(async () => await PublishLoopAsync(_publishCts.Token));
    }

    private async Task PublishLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_clientWrapper.IsConnected && _clientWrapper.UnderlyingClient != null)
                {
                    Poll();
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"Publish error: {ex.Message}");
            }

            try
            {
                await Task.Delay(100, ct); // Poll every 100ms
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void Poll()
    {
        var client = _clientWrapper.UnderlyingClient;
        if (client == null) return;

        try
        {
            client.Publish(
                new[] { _subscriptionId },
                out var statusCode,
                out var results,
                out var diagnosticInfos,
                out var acknowledgeResults
            );

            if (results != null)
            {
                foreach (var notification in results)
                {
                    if (notification is DataChangeNotification dcn && dcn.MonitoredItems != null)
                    {
                        foreach (var monitoredItem in dcn.MonitoredItems)
                        {
                            ProcessValueChange(monitoredItem.ClientHandle, monitoredItem.Value);
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore poll errors - may be disconnecting
        }
    }

    private void ProcessValueChange(uint clientHandle, DataValue dataValue)
    {
        MonitoredNode? item;
        lock (_lock)
        {
            if (!_monitoredItems.TryGetValue(clientHandle, out item))
                return;
        }

        var oldValue = item.Value;
        item.Value = FormatValue(dataValue.Value);
        item.Timestamp = dataValue.SourceTimestamp;
        item.StatusCode = dataValue.StatusCode;

        if (oldValue != item.Value)
        {
            item.LastChangeTime = DateTime.Now;
        }

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
        _publishCts?.Cancel();

        try
        {
            _publishTask?.Wait(1000);
        }
        catch { }

        if (_isInitialized && _clientWrapper.IsConnected && _clientWrapper.UnderlyingClient != null)
        {
            try
            {
                _clientWrapper.UnderlyingClient.DeleteSubscriptions(
                    new[] { _subscriptionId },
                    out _
                );
            }
            catch { }
        }

        _isInitialized = false;
        _monitoredItems.Clear();
    }
}
