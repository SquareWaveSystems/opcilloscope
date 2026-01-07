using Opc.Ua;
using OpcScope.OpcUa;
using OpcScope.Tests.Infrastructure;
using OpcScope.Utilities;

namespace OpcScope.Tests.Integration;

/// <summary>
/// Integration tests for error handling scenarios.
/// Tests graceful handling of bad NodeIds, connection failures, and access denied errors.
/// </summary>
public class ErrorHandlingIntegrationTests : IntegrationTestBase
{
    private readonly Logger _logger = new();

    public ErrorHandlingIntegrationTests(TestServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task ReadValueAsync_InvalidNodeId_ReturnsNull()
    {
        // Arrange
        var invalidNodeId = new NodeId("NonExistentNode", (ushort)GetNamespaceIndex());

        // Act
        var value = await Client!.ReadValueAsync(invalidNodeId);

        // Assert
        Assert.Null(value);
    }

    [Fact]
    public async Task WriteValueAsync_InvalidNodeId_ReturnsBadStatus()
    {
        // Arrange
        var invalidNodeId = new NodeId("NonExistentNode", (ushort)GetNamespaceIndex());

        // Act
        var result = await Client!.WriteValueAsync(invalidNodeId, "test");

        // Assert
        Assert.True(StatusCode.IsBad(result));
    }

    [Fact]
    public async Task BrowseAsync_InvalidNodeId_ReturnsEmptyList()
    {
        // Arrange
        var invalidNodeId = new NodeId("NonExistentNode", (ushort)GetNamespaceIndex());

        // Act
        var children = await Client!.BrowseAsync(invalidNodeId);

        // Assert
        Assert.Empty(children);
    }

    [Fact]
    public async Task NodeBrowser_GetChildrenAsync_InvalidNode_ReturnsEmptyList()
    {
        // Arrange
        var nodeBrowser = new NodeBrowser(Client!, _logger);
        var invalidNode = new OpcScope.OpcUa.Models.BrowsedNode
        {
            NodeId = new NodeId("NonExistentNode", (ushort)GetNamespaceIndex()),
            DisplayName = "Invalid"
        };

        // Act
        var children = await nodeBrowser.GetChildrenAsync(invalidNode);

        // Assert
        Assert.Empty(children);
    }

    [Fact]
    public async Task NodeBrowser_GetNodeAttributesAsync_InvalidNode_ReturnsNull()
    {
        // Arrange
        var nodeBrowser = new NodeBrowser(Client!, _logger);
        var invalidNodeId = new NodeId("NonExistentNode", (ushort)GetNamespaceIndex());

        // Act
        var attrs = await nodeBrowser.GetNodeAttributesAsync(invalidNodeId);

        // Assert
        Assert.Null(attrs);
    }

    [Fact]
    public async Task SubscriptionManager_AddNodeAsync_NotInitialized_ReturnsNull()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        // Note: InitializeAsync not called
        var nodeId = new NodeId("Counter", (ushort)GetNamespaceIndex());

        // Act
        var node = await subscriptionManager.AddNodeAsync(nodeId, "Counter");

        // Assert
        Assert.Null(node);
    }

    [Fact]
    public async Task SubscriptionManager_RemoveNodeAsync_InvalidHandle_ReturnsFalse()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();

        // Act
        var result = await subscriptionManager.RemoveNodeAsync(99999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SubscriptionManager_RemoveNodeByNodeIdAsync_NonExistent_ReturnsFalse()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();
        var nonExistentNodeId = new NodeId("NonExistent", 99);

        // Act
        var result = await subscriptionManager.RemoveNodeByNodeIdAsync(nonExistentNodeId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ConnectionManager_ConnectAsync_InvalidHost_DoesNotThrow()
    {
        // Arrange
        using var connectionManager = new ConnectionManager(_logger);

        // Act & Assert - should not throw
        var result = await connectionManager.ConnectAsync("opc.tcp://invalid-host-12345:4840");
        Assert.False(result);
    }

    [Fact]
    public async Task ConnectionManager_ConnectAsync_InvalidPort_DoesNotThrow()
    {
        // Arrange
        using var connectionManager = new ConnectionManager(_logger);

        // Act & Assert - should not throw
        var result = await connectionManager.ConnectAsync("opc.tcp://localhost:99999");
        Assert.False(result);
    }

    [Fact]
    public async Task ConnectionManager_ConnectAsync_InvalidProtocol_DoesNotThrow()
    {
        // Arrange
        using var connectionManager = new ConnectionManager(_logger);

        // Act & Assert - should not throw
        var result = await connectionManager.ConnectAsync("http://localhost:4840");
        Assert.False(result);
    }

    [Fact]
    public async Task ConnectionManager_ReconnectAsync_NoLastEndpoint_ReturnsFalse()
    {
        // Arrange
        using var connectionManager = new ConnectionManager(_logger);

        // Act
        var result = await connectionManager.ReconnectAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ConnectionManager_SubscribeAsync_WhenDisconnected_ReturnsNull()
    {
        // Arrange
        using var connectionManager = new ConnectionManager(_logger);
        var nodeId = new NodeId("Counter", 2);

        // Act
        var node = await connectionManager.SubscribeAsync(nodeId, "Counter");

        // Assert
        Assert.Null(node);
    }

    [Fact]
    public async Task ConnectionManager_UnsubscribeAsync_WhenDisconnected_ReturnsFalse()
    {
        // Arrange
        using var connectionManager = new ConnectionManager(_logger);

        // Act
        var result = await connectionManager.UnsubscribeAsync(1);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ReadAttributesAsync_InvalidNodeId_ReturnsEmptyResults()
    {
        // Arrange
        var invalidNodeId = new NodeId("NonExistentNode", (ushort)GetNamespaceIndex());

        // Act
        var results = await Client!.ReadAttributesAsync(invalidNodeId, Attributes.Value);

        // Assert - should return results but with bad status
        Assert.NotNull(results);
    }

    [Fact]
    public async Task WriteValueAsync_ReadOnlyNode_ReturnsBadStatus()
    {
        // Arrange - ServerName is read-only
        var nodeId = new NodeId("ServerName", (ushort)GetNamespaceIndex());

        // Act
        var result = await Client!.WriteValueAsync(nodeId, "NewName");

        // Assert
        Assert.True(StatusCode.IsBad(result));
    }

    [Fact]
    public async Task WriteValueAsync_WrongDataType_ReturnsBadStatus()
    {
        // Arrange - WritableNumber expects Int32
        var nodeId = new NodeId("WritableNumber", (ushort)GetNamespaceIndex());

        // Act - Try to write a string to an Int32 node
        var result = await Client!.WriteValueAsync(nodeId, "not a number");

        // Assert
        Assert.True(StatusCode.IsBad(result));
    }

    [Fact]
    public async Task SubscriptionManager_AddDuplicateNode_DoesNotThrow()
    {
        // Arrange
        using var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();
        var nodeId = new NodeId("Counter", (ushort)GetNamespaceIndex());

        // Act - Add same node twice
        await subscriptionManager.AddNodeAsync(nodeId, "Counter");
        var duplicate = await subscriptionManager.AddNodeAsync(nodeId, "Counter");

        // Assert
        Assert.Null(duplicate);
        Assert.Single(subscriptionManager.MonitoredVariables);
    }

    [Fact]
    public async Task NodeBrowser_WhenDisconnected_ReturnsEmptyList()
    {
        // Arrange
        var disconnectedClient = new OpcUaClientWrapper(_logger);
        var nodeBrowser = new NodeBrowser(disconnectedClient, _logger);
        var root = nodeBrowser.GetRootNode();

        // Act
        var children = await nodeBrowser.GetChildrenAsync(root);

        // Assert
        Assert.Empty(children);
    }

    [Fact]
    public async Task NodeBrowser_GetNodeAttributesAsync_WhenDisconnected_ReturnsNull()
    {
        // Arrange
        var disconnectedClient = new OpcUaClientWrapper(_logger);
        var nodeBrowser = new NodeBrowser(disconnectedClient, _logger);

        // Act
        var attrs = await nodeBrowser.GetNodeAttributesAsync(ObjectIds.RootFolder);

        // Assert
        Assert.Null(attrs);
    }

    [Fact]
    public async Task SubscriptionManager_Dispose_WhileMonitoring_DoesNotThrow()
    {
        // Arrange
        var subscriptionManager = new SubscriptionManager(Client!, _logger);
        await subscriptionManager.InitializeAsync();
        await subscriptionManager.AddNodeAsync(
            new NodeId("Counter", (ushort)GetNamespaceIndex()), "Counter");
        await subscriptionManager.AddNodeAsync(
            new NodeId("SineWave", (ushort)GetNamespaceIndex()), "SineWave");

        // Act & Assert - should not throw
        subscriptionManager.Dispose();
    }

    [Fact]
    public async Task ConnectionManager_Dispose_WhileConnected_DoesNotThrow()
    {
        // Arrange
        var connectionManager = new ConnectionManager(_logger);
        await connectionManager.ConnectAsync(Fixture.EndpointUrl);
        await connectionManager.SubscribeAsync(
            new NodeId("Counter", (ushort)GetNamespaceIndex()), "Counter");

        // Act & Assert - should not throw
        connectionManager.Dispose();
    }
}
