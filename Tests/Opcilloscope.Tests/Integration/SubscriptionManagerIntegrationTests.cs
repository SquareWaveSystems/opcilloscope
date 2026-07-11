using Opc.Ua;
using Opcilloscope.OpcUa;
using Opcilloscope.Tests.Infrastructure;
using Opcilloscope.Utilities;

namespace Opcilloscope.Tests.Integration;

/// <summary>
/// Integration tests for SubscriptionManager with a real OPC UA server.
/// Tests subscription lifecycle: subscribe, receive updates, unsubscribe.
/// </summary>
public class SubscriptionManagerIntegrationTests : IntegrationTestBase
{
    private readonly Logger _logger = new();

    public SubscriptionManagerIntegrationTests(TestServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task InitializeAsync_CreatesSubscription_Successfully()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);

        // Act
        var result = await subscriptionManager.InitializeAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task AddNodeAsync_SubscribesToNode_Successfully()
    {
        // Arrange
        const long connectionGeneration = 41;
        using var subscriptionManager = new SubscriptionManager(
            Client!,
            _logger,
            connectionGeneration);
        await subscriptionManager.InitializeAsync();
        var nodeId = new NodeId("Counter", (ushort)GetNamespaceIndex());

        // Act
        var node = await subscriptionManager.AddNodeAsync(nodeId, "Counter");

        // Assert
        Assert.NotNull(node);
        Assert.Equal("Counter", node.DisplayName);
        Assert.Equal(nodeId, node.NodeId);
        Assert.Equal(connectionGeneration, node.ConnectionGeneration);
        Assert.Single(subscriptionManager.MonitoredVariables);

        subscriptionManager.AdvanceConnectionGeneration(42);

        Assert.Equal(42, node.ConnectionGeneration);
        Assert.All(
            subscriptionManager.MonitoredVariables,
            monitored => Assert.Equal(42, monitored.ConnectionGeneration));
    }

    [Fact]
    public async Task AddNodeAsync_ReadsInitialValue()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();
        var nodeId = new NodeId("ServerName", (ushort)GetNamespaceIndex());

        // Act
        var node = await subscriptionManager.AddNodeAsync(nodeId, "ServerName");

        // Assert
        Assert.NotNull(node);
        Assert.Equal("Opcilloscope Test Server", node.Value);
    }

    [Fact]
    public async Task AddNodeAsync_ReadsNodeAttributes()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();
        var nodeId = new NodeId("WritableString", (ushort)GetNamespaceIndex());

        // Act
        var node = await subscriptionManager.AddNodeAsync(nodeId, "WritableString");

        // Assert
        Assert.NotNull(node);
        Assert.NotNull(node.DataTypeName);
        Assert.Equal("String", node.DataTypeName);
    }

    [Fact]
    public async Task AddNodeAsync_DuplicateNode_ReturnsNull()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();
        var nodeId = new NodeId("Counter", (ushort)GetNamespaceIndex());

        // Act
        var firstNode = await subscriptionManager.AddNodeAsync(nodeId, "Counter");

        Assert.NotNull(firstNode);

        var duplicate = await subscriptionManager.AddNodeAsync(nodeId, "Counter");

        // Assert
        Assert.Null(duplicate);
        Assert.Single(subscriptionManager.MonitoredVariables);
    }

    [Fact]
    public async Task AddNodeAsync_FiresVariableAddedEvent()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();
        var nodeId = new NodeId("Counter", (ushort)GetNamespaceIndex());
        Opcilloscope.OpcUa.Models.MonitoredNode? addedNode = null;
        subscriptionManager.VariableAdded += node => addedNode = node;

        // Act
        var node = await subscriptionManager.AddNodeAsync(nodeId, "Counter");

        // Assert
        Assert.NotNull(node);
        Assert.NotNull(addedNode);
        Assert.Equal("Counter", addedNode.DisplayName);
    }

    [Fact]
    public async Task RemoveNodeAsync_UnsubscribesFromNode_Successfully()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();
        var nodeId = new NodeId("Counter", (ushort)GetNamespaceIndex());
        var node = await subscriptionManager.AddNodeAsync(nodeId, "Counter");

        Assert.NotNull(node);

        // Act
        var result = await subscriptionManager.RemoveNodeAsync(node.ClientHandle);

        // Assert
        Assert.True(result);
        Assert.Empty(subscriptionManager.MonitoredVariables);
    }

    [Fact]
    public async Task RemoveNodeAsync_FiresVariableRemovedEvent()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();
        var nodeId = new NodeId("Counter", (ushort)GetNamespaceIndex());
        var node = await subscriptionManager.AddNodeAsync(nodeId, "Counter");

        Assert.NotNull(node);

        uint? removedHandle = null;
        subscriptionManager.VariableRemoved += (handle, _) => removedHandle = handle;

        // Act
        await subscriptionManager.RemoveNodeAsync(node.ClientHandle);

        // Assert
        Assert.NotNull(removedHandle);
        Assert.Equal(node.ClientHandle, removedHandle);
    }

    [Fact]
    public async Task ValueChanged_ReceivesUpdates_WhenValueChanges()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();
        var nodeId = new NodeId("Counter", (ushort)GetNamespaceIndex());
        var valueChangedCount = 0;
        subscriptionManager.ValueChanged += _ => Interlocked.Increment(ref valueChangedCount);

        // Act
        var node = await subscriptionManager.AddNodeAsync(nodeId, "Counter");

        Assert.NotNull(node);

        // Wait for at least one subscription notification (counter updates every second)
        await Task.Delay(1500);

        // Assert
        Assert.True(valueChangedCount >= 1, $"Expected at least 1 value change, got {valueChangedCount}");
    }

    [Fact]
    public async Task ClearAsync_RemovesAllNodes()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();
        var node1 = await subscriptionManager.AddNodeAsync(new NodeId("Counter", (ushort)GetNamespaceIndex()), "Counter");
        var node2 = await subscriptionManager.AddNodeAsync(new NodeId("SineWave", (ushort)GetNamespaceIndex()), "SineWave");
        var node3 = await subscriptionManager.AddNodeAsync(new NodeId("RandomValue", (ushort)GetNamespaceIndex()), "RandomValue");

        Assert.NotNull(node1);
        Assert.NotNull(node2);
        Assert.NotNull(node3);
        Assert.Equal(3, subscriptionManager.MonitoredVariables.Count);

        // Act
        await subscriptionManager.ClearAsync();

        // Assert
        Assert.Empty(subscriptionManager.MonitoredVariables);
    }

    [Fact]
    public async Task PublishingInterval_CanBeChanged()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);

        // Act
        subscriptionManager.PublishingInterval = 2000;
        await subscriptionManager.InitializeAsync();

        // Assert
        Assert.Equal(2000, subscriptionManager.PublishingInterval);
    }

    [Fact]
    public async Task PublishingInterval_ClampsToMinimum()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);

        // Act
        subscriptionManager.PublishingInterval = 50; // Below minimum of 100

        // Assert
        Assert.Equal(100, subscriptionManager.PublishingInterval);
    }

    [Fact]
    public async Task PublishingInterval_ClampsToMaximum()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);

        // Act
        subscriptionManager.PublishingInterval = 20000; // Above maximum of 10000

        // Assert
        Assert.Equal(10000, subscriptionManager.PublishingInterval);
    }

    [Fact]
    public async Task SamplingInterval_AppliedToNewMonitoredItems()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        subscriptionManager.SamplingInterval = 1000;
        subscriptionManager.QueueSize = 25;
        await subscriptionManager.InitializeAsync();
        var nodeId = new NodeId("Counter", (ushort)GetNamespaceIndex());

        // Act
        var node = await subscriptionManager.AddNodeAsync(nodeId, "Counter");

        // Assert
        Assert.NotNull(node);
        Assert.Equal(1000, subscriptionManager.SamplingInterval);
        Assert.Equal(25u, subscriptionManager.QueueSize);
    }

    // SamplingInterval clamping is covered by the unit tests in
    // SubscriptionManagerTests (0-60000 range).

    [Theory]
    [InlineData(0u, 1u)]      // zero clamps to minimum of 1
    [InlineData(10u, 10u)]
    [InlineData(5000u, 1000u)] // above maximum clamps to 1000
    public void QueueSize_ClampsToValidRange(uint input, uint expected)
    {
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);

        subscriptionManager.QueueSize = input;

        Assert.Equal(expected, subscriptionManager.QueueSize);
    }

    [Fact]
    public async Task AddNodeAsync_InvalidNodeId_ReturnsNull()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();
        var invalidNodeId = new NodeId("NonExistentNode", (ushort)GetNamespaceIndex());

        // Act
        var node = await subscriptionManager.AddNodeAsync(invalidNodeId, "NonExistent");

        // Assert - a bad monitored item status causes AddNodeAsync to clean up
        // and return null per its implementation contract.
        Assert.Null(node);
        Assert.Empty(subscriptionManager.MonitoredVariables);
        Assert.Equal((uint?)0, subscriptionManager.GetOpcSubscription()?.MonitoredItemCount);
    }

    [Fact]
    public async Task AddNodeAsync_WhenVariableAddedHandlerThrows_KeepsReturnAndStateConsistent()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();
        var nodeId = new NodeId("Counter", (ushort)GetNamespaceIndex());
        subscriptionManager.VariableAdded += _ => throw new InvalidOperationException("test handler failure");

        // Act
        var node = await subscriptionManager.AddNodeAsync(nodeId, "Counter");

        // Assert - an observer failure must not turn a committed server item into a
        // null result while leaving it hidden in the manager as a ghost.
        Assert.NotNull(node);
        Assert.Single(subscriptionManager.MonitoredVariables);
        Assert.Equal((uint?)1, subscriptionManager.GetOpcSubscription()?.MonitoredItemCount);
    }

    [Fact]
    public async Task RecreateSubscriptionsAsync_WhenServerRejectsOneItem_ReturnsFalseAndRollsBackItems()
    {
        // Arrange - changing the retained model's init-only NodeId simulates a node
        // disappearing from the server between the original session and recreation.
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();
        var node = await subscriptionManager.AddNodeAsync(
            new NodeId("Counter", (ushort)GetNamespaceIndex()),
            "Counter");
        Assert.NotNull(node);

        var missingNodeId = new NodeId("RemovedBeforeReconnect", (ushort)GetNamespaceIndex());
        typeof(Opcilloscope.OpcUa.Models.MonitoredNode)
            .GetProperty(nameof(Opcilloscope.OpcUa.Models.MonitoredNode.NodeId))!
            .SetValue(node, missingNodeId);

        // Act
        var recreated = await subscriptionManager.RecreateSubscriptionsAsync();

        // Assert - the caller can now execute its explicit loss fallback instead of
        // being told that a rejected monitored item was fully restored.
        Assert.False(recreated);
        Assert.Equal((uint?)0, subscriptionManager.GetOpcSubscription()?.MonitoredItemCount);
    }

    [Fact]
    public async Task AddAndRemoveNodeAsync_ConcurrentMutationsLeaveConsistentState()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();
        var nodeId = new NodeId("Counter", (ushort)GetNamespaceIndex());
        var original = await subscriptionManager.AddNodeAsync(nodeId, "Counter");
        Assert.NotNull(original);

        // Act - RemoveNodeAsync reaches its first ApplyChangesAsync before returning
        // control, so the add attempts to mutate the same OPC subscription concurrently
        // unless SubscriptionManager serializes the complete mutation transaction.
        var removeTask = subscriptionManager.RemoveNodeAsync(original.ClientHandle);
        var addTask = subscriptionManager.AddNodeAsync(nodeId, "Counter replacement");
        await Task.WhenAll(removeTask, addTask);

        // Assert
        Assert.True(await removeTask);
        Assert.NotNull(await addTask);
        Assert.Single(subscriptionManager.MonitoredVariables);
        Assert.Equal((uint?)1, subscriptionManager.GetOpcSubscription()?.MonitoredItemCount);
    }
}
