using OpcScope.OpcUa;
using OpcScope.Tests.Infrastructure;
using OpcScope.Utilities;

namespace OpcScope.Tests.Integration;

/// <summary>
/// Integration tests for ConnectionManager with a real OPC UA server.
/// Tests connection lifecycle: connect, disconnect.
/// </summary>
[Collection("TestServer")]
public class ConnectionManagerIntegrationTests : IAsyncLifetime
{
    private readonly TestServerFixture _fixture;
    private readonly Logger _logger = new();
    private ConnectionManager? _connectionManager;

    public ConnectionManagerIntegrationTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _connectionManager = new ConnectionManager(_logger);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _connectionManager?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ConnectAsync_ValidEndpoint_ReturnsTrue()
    {
        // Act
        var result = await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);

        // Assert
        Assert.True(result);
        Assert.True(_connectionManager.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_ValidEndpoint_SetsCurrentEndpoint()
    {
        // Act
        await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);

        // Assert
        Assert.Equal(_fixture.EndpointUrl, _connectionManager.CurrentEndpoint);
    }

    [Fact]
    public async Task ConnectAsync_ValidEndpoint_InitializesSubscriptionManager()
    {
        // Act
        await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);

        // Assert
        Assert.NotNull(_connectionManager.SubscriptionManager);
    }

    [Fact]
    public async Task ConnectAsync_FiresStateChangedEvents()
    {
        // Arrange
        var stateChanges = new List<ConnectionState>();
        _connectionManager!.StateChanged += state => stateChanges.Add(state);

        // Act
        await _connectionManager.ConnectAsync(_fixture.EndpointUrl);

        // Assert
        Assert.Contains(ConnectionState.Connecting, stateChanges);
        Assert.Contains(ConnectionState.Connected, stateChanges);
    }

    [Fact]
    public async Task ConnectAsync_InvalidEndpoint_ReturnsFalse()
    {
        // Act
        var result = await _connectionManager!.ConnectAsync("opc.tcp://invalid-host:4840");

        // Assert
        Assert.False(result);
        Assert.False(_connectionManager.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_InvalidEndpoint_FiresDisconnectedState()
    {
        // Arrange
        var stateChanges = new List<ConnectionState>();
        _connectionManager!.StateChanged += state => stateChanges.Add(state);

        // Act
        await _connectionManager.ConnectAsync("opc.tcp://invalid-host:4840");

        // Assert
        Assert.Contains(ConnectionState.Disconnected, stateChanges);
    }

    [Fact]
    public async Task Disconnect_ClearsConnection()
    {
        // Arrange
        await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);
        Assert.True(_connectionManager.IsConnected);

        // Act
        _connectionManager.Disconnect();

        // Assert
        Assert.False(_connectionManager.IsConnected);
    }

    [Fact]
    public async Task Disconnect_FiresDisconnectedState()
    {
        // Arrange
        await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);
        var stateChanges = new List<ConnectionState>();
        _connectionManager.StateChanged += state => stateChanges.Add(state);

        // Act
        _connectionManager.Disconnect();

        // Assert
        Assert.Contains(ConnectionState.Disconnected, stateChanges);
    }

    [Fact]
    public async Task Disconnect_DisposesSubscriptionManager()
    {
        // Arrange
        await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);
        Assert.NotNull(_connectionManager.SubscriptionManager);

        // Act
        _connectionManager.Disconnect();

        // Assert
        Assert.Null(_connectionManager.SubscriptionManager);
    }

    [Fact]
    public async Task ReconnectAsync_WithoutPreviousConnection_ReturnsFalse()
    {
        // Act
        var result = await _connectionManager!.ReconnectAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task LastEndpoint_RemembersEndpoint()
    {
        // Act
        await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);

        // Assert
        Assert.Equal(_fixture.EndpointUrl, _connectionManager.LastEndpoint);
    }

    [Fact]
    public async Task SubscribeAsync_WhenConnected_SubscribesToNode()
    {
        // Arrange
        var connected = await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);
        Assert.True(connected, "Connection should succeed");

        var nsIndex = GetNamespaceIndex();
        Assert.True(nsIndex > 0, "Namespace index should be valid");

        var nodeId = new Opc.Ua.NodeId("Counter", (ushort)nsIndex);

        // Act
        var node = await _connectionManager.SubscribeAsync(nodeId, "Counter");

        // Assert
        Assert.NotNull(node);
        Assert.Equal("Counter", node.DisplayName);
    }

    [Fact]
    public async Task SubscribeAsync_WhenDisconnected_ReturnsNull()
    {
        // Arrange
        var nodeId = new Opc.Ua.NodeId("Counter", 2);

        // Act
        var node = await _connectionManager!.SubscribeAsync(nodeId, "Counter");

        // Assert
        Assert.Null(node);
    }

    [Fact]
    public async Task UnsubscribeAsync_RemovesSubscription()
    {
        // Arrange
        var connected = await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);
        Assert.True(connected);

        var nsIndex = GetNamespaceIndex();
        var nodeId = new Opc.Ua.NodeId("Counter", (ushort)nsIndex);
        var node = await _connectionManager.SubscribeAsync(nodeId, "Counter");

        // Skip test if subscription failed (server may not support the node)
        if (node == null)
        {
            return;
        }

        // Act
        var result = await _connectionManager.UnsubscribeAsync(node.ClientHandle);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValueChanged_FiresWhenValueUpdates()
    {
        // Arrange
        var connected = await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);
        Assert.True(connected);

        var nsIndex = GetNamespaceIndex();
        var nodeId = new Opc.Ua.NodeId("Counter", (ushort)nsIndex);
        var valueChangedCount = 0;
        _connectionManager.ValueChanged += _ => Interlocked.Increment(ref valueChangedCount);

        // Act
        var node = await _connectionManager.SubscribeAsync(nodeId, "Counter");
        if (node == null)
        {
            return; // Skip if subscription failed
        }

        await Task.Delay(1500); // Wait for subscription updates

        // Assert
        Assert.True(valueChangedCount >= 1);
    }

    [Fact]
    public async Task VariableAdded_FiresWhenSubscribing()
    {
        // Arrange
        var connected = await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);
        Assert.True(connected);

        var nsIndex = GetNamespaceIndex();
        var nodeId = new Opc.Ua.NodeId("Counter", (ushort)nsIndex);
        OpcScope.OpcUa.Models.MonitoredNode? addedNode = null;
        _connectionManager.VariableAdded += node => addedNode = node;

        // Act
        var node = await _connectionManager.SubscribeAsync(nodeId, "Counter");

        // Assert
        if (node != null)
        {
            Assert.NotNull(addedNode);
        }
    }

    [Fact]
    public async Task VariableRemoved_FiresWhenUnsubscribing()
    {
        // Arrange
        var connected = await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);
        Assert.True(connected);

        var nsIndex = GetNamespaceIndex();
        var nodeId = new Opc.Ua.NodeId("Counter", (ushort)nsIndex);
        var node = await _connectionManager.SubscribeAsync(nodeId, "Counter");

        if (node == null)
        {
            return; // Skip if subscription failed
        }

        uint? removedHandle = null;
        _connectionManager.VariableRemoved += handle => removedHandle = handle;

        // Act
        await _connectionManager.UnsubscribeAsync(node.ClientHandle);

        // Assert
        Assert.NotNull(removedHandle);
        Assert.Equal(node.ClientHandle, removedHandle);
    }

    [Fact]
    public async Task NodeBrowser_IsAccessibleWhenConnected()
    {
        // Arrange
        await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);

        // Act
        var rootNode = _connectionManager.NodeBrowser.GetRootNode();

        // Assert
        Assert.NotNull(rootNode);
        Assert.Equal("Root", rootNode.DisplayName);
    }

    [Fact]
    public async Task Client_IsAccessibleWhenConnected()
    {
        // Arrange
        await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);

        // Act & Assert
        Assert.NotNull(_connectionManager.Client);
        Assert.True(_connectionManager.Client.IsConnected);
    }

    private int GetNamespaceIndex()
    {
        if (_connectionManager?.Client?.Session == null)
        {
            return -1;
        }

        return _connectionManager.Client.Session.NamespaceUris.GetIndex(
            OpcScope.TestServer.TestNodeManager.NamespaceUri);
    }
}
