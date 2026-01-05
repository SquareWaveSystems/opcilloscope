using LibUA.Core;
using OpcScope.OpcUa.Models;
using OpcScope.Utilities;

namespace OpcScope.OpcUa;

/// <summary>
/// Manages monitored items with polling-based value updates.
/// Uses polling instead of OPC UA subscriptions for LibUA compatibility.
/// </summary>
public class SubscriptionManager : IDisposable
{
    private readonly OpcUaClientWrapper _clientWrapper;
    private readonly Logger _logger;
    private readonly Dictionary<uint, MonitoredNode> _monitoredItems = new();
    private uint _nextClientHandle = 1;
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;
    private int _pollingInterval = 1000;
    private bool _isInitialized;
    private readonly object _lock = new();

    public event Action<MonitoredNode>? ValueChanged;
    public event Action<MonitoredNode>? ItemAdded;
    public event Action<uint>? ItemRemoved;

    public int PublishingInterval
    {
        get => _pollingInterval;
        set => _pollingInterval = Math.Max(100, Math.Min(10000, value));
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
            _isInitialized = true;
            _logger.Info($"Subscription manager initialized (Polling Interval: {_pollingInterval}ms)");

            // Start polling loop
            StartPollingLoop();

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to initialize subscription manager: {ex.Message}");
            return false;
        }
    }

    public MonitoredNode? AddNode(NodeId nodeId, string displayName)
    {
        if (!_isInitialized || _clientWrapper.UnderlyingClient == null)
        {
            _logger.Error("Subscription manager not initialized");
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

            var item = new MonitoredNode
            {
                ClientHandle = clientHandle,
                MonitoredItemId = clientHandle, // Use clientHandle as ID for polling approach
                NodeId = nodeId,
                DisplayName = displayName,
                Value = "(pending)",
                StatusCode = 0 // Good
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
                item.StatusCode = value.StatusCode.HasValue ? (uint)value.StatusCode.Value : 0;
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
        lock (_lock)
        {
            if (!_monitoredItems.TryGetValue(clientHandle, out item))
                return false;

            _monitoredItems.Remove(clientHandle);
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
            item = _monitoredItems.Values.FirstOrDefault(m => m.NodeId.Equals(nodeId));
        }

        if (item != null)
        {
            return RemoveNode(item.ClientHandle);
        }
        return false;
    }

    private void StartPollingLoop()
    {
        _pollCts = new CancellationTokenSource();
        _pollTask = Task.Run(async () => await PollingLoopAsync(_pollCts.Token));
    }

    private async Task PollingLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_clientWrapper.IsConnected && _clientWrapper.UnderlyingClient != null)
                {
                    PollValues();
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"Polling error: {ex.Message}");
            }

            try
            {
                await Task.Delay(_pollingInterval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void PollValues()
    {
        List<MonitoredNode> itemsToRead;
        lock (_lock)
        {
            itemsToRead = _monitoredItems.Values.ToList();
        }

        if (itemsToRead.Count == 0)
            return;

        foreach (var item in itemsToRead)
        {
            try
            {
                var value = _clientWrapper.ReadValue(item.NodeId);
                if (value != null)
                {
                    ProcessValueChange(item, value);
                }
            }
            catch
            {
                // Ignore individual read errors
            }
        }
    }

    private void ProcessValueChange(MonitoredNode item, DataValue dataValue)
    {
        var oldValue = item.Value;
        var newValue = FormatValue(dataValue.Value);

        item.Value = newValue;
        item.Timestamp = dataValue.SourceTimestamp;
        item.StatusCode = dataValue.StatusCode.HasValue ? (uint)dataValue.StatusCode.Value : 0;

        if (oldValue != newValue)
        {
            item.LastChangeTime = DateTime.Now;
            ValueChanged?.Invoke(item);
        }
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
        _pollCts?.Cancel();

        try
        {
            _pollTask?.Wait(1000);
        }
        catch { }

        _isInitialized = false;
        _monitoredItems.Clear();
    }
}
