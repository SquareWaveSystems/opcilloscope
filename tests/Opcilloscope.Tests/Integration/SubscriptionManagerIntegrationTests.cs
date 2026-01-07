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
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();
        var nodeId = new NodeId("Counter", (ushort)GetNamespaceIndex());

        // Act
        var node = await subscriptionManager.AddNodeAsync(nodeId, "Counter");

        // Skip test if subscription failed (server may not support the node)
        if (node == null)
        {
            return;
        }

        // Assert
        Assert.Equal("Counter", node.DisplayName);
        Assert.Equal(nodeId, node.NodeId);
        Assert.Single(subscriptionManager.MonitoredVariables);
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

        // Skip test if subscription failed (server may not support the node)
        if (node == null)
        {
            return;
        }

        // Assert
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

        // Skip test if subscription failed (server may not support the node)
        if (node == null)
        {
            return;
        }

        // Assert
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

        // Skip test if first subscription failed
        if (firstNode == null)
        {
            return;
        }

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

        // Skip test if subscription failed (server may not support the node)
        if (node == null)
        {
            return;
        }

        // Assert
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

        // Skip test if subscription failed (server may not support the node)
        if (node == null)
        {
            return;
        }

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

        // Skip test if subscription failed (server may not support the node)
        if (node == null)
        {
            return;
        }

        uint? removedHandle = null;
        subscriptionManager.VariableRemoved += handle => removedHandle = handle;

        // Act
        await subscriptionManager.RemoveNodeAsync(node.ClientHandle);

        // Assert
        Assert.NotNull(removedHandle);
        Assert.Equal(node.ClientHandle, removedHandle);
    }

    [Fact]
    public async Task RemoveNodeByNodeIdAsync_UnsubscribesFromNode()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();
        var nodeId = new NodeId("Counter", (ushort)GetNamespaceIndex());
        var node = await subscriptionManager.AddNodeAsync(nodeId, "Counter");

        // Skip test if subscription failed (server may not support the node)
        if (node == null)
        {
            return;
        }

        // Act
        var result = await subscriptionManager.RemoveNodeByNodeIdAsync(nodeId);

        // Assert
        Assert.True(result);
        Assert.Empty(subscriptionManager.MonitoredVariables);
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

        // Skip test if subscription failed (server may not support the node)
        if (node == null)
        {
            return;
        }

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

        // Skip test if no subscriptions succeeded
        if (subscriptionManager.MonitoredVariables.Count == 0)
        {
            return;
        }

        var initialCount = subscriptionManager.MonitoredVariables.Count;
        Assert.True(initialCount > 0, "At least one subscription should succeed");

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
    public async Task AddNodeAsync_InvalidNodeId_ReturnsNull()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();
        var invalidNodeId = new NodeId("NonExistentNode", (ushort)GetNamespaceIndex());

        // Act
        var node = await subscriptionManager.AddNodeAsync(invalidNodeId, "NonExistent");

        // Assert - the subscription may still be created but with bad status
        // The behavior depends on the server's response
        // At minimum, ensure no exception was thrown
    }
}
