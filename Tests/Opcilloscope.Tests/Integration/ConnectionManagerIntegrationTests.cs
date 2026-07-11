using Opcilloscope.OpcUa;
using Opcilloscope.Tests.Infrastructure;
using Opcilloscope.Utilities;
using Opc.Ua;

namespace Opcilloscope.Tests.Integration;

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
    public async Task ConnectAsync_AppliesSubscriptionSettings()
    {
        // Act
        await _connectionManager!.ConnectAsync(
            _fixture.EndpointUrl,
            publishingInterval: 500,
            samplingInterval: 1000,
            queueSize: 25);

        // Assert - settings flow through to the subscription manager
        var subscriptionManager = _connectionManager.SubscriptionManager;
        Assert.NotNull(subscriptionManager);
        Assert.Equal(500, subscriptionManager.PublishingInterval);
        Assert.Equal(1000, subscriptionManager.SamplingInterval);
        Assert.Equal(25u, subscriptionManager.QueueSize);
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
    public async Task ReconnectAsync_AfterExplicitDisconnect_PerformsFreshConnectWithStoredProfile()
    {
        var credentials = new ConnectionCredentials(
            AuthenticationType.UserName,
            "testuser",
            "testpass");

        var connected = await _connectionManager!.ConnectAsync(
            _fixture.EndpointUrl,
            publishingInterval: 500,
            credentials: credentials,
            securityMode: nameof(MessageSecurityMode.SignAndEncrypt),
            securityPolicy: SecurityPolicies.Basic256Sha256,
            samplingInterval: 750,
            queueSize: 25);
        Assert.True(connected);

        await _connectionManager.DisconnectAsync();
        Assert.False(_connectionManager.IsConnected);

        var reconnected = await _connectionManager.ReconnectAsync();

        Assert.True(reconnected);
        Assert.True(_connectionManager.IsConnected);
        Assert.Equal(MessageSecurityMode.SignAndEncrypt, _connectionManager.CurrentSecurityMode);
        Assert.Equal(SecurityPolicies.Basic256Sha256, _connectionManager.CurrentSecurityPolicy);
        Assert.Equal(500, _connectionManager.SubscriptionManager?.PublishingInterval);
        Assert.Equal(750, _connectionManager.SubscriptionManager?.SamplingInterval);
        Assert.Equal(25u, _connectionManager.SubscriptionManager?.QueueSize);
    }

    [Fact]
    public async Task DisconnectAsync_QueuedBehindConnect_CannotBeOvertakenByLateSessionCreation()
    {
        using var manager = new ConnectionManager(_logger, allowInsecure: true);

        var connectTask = manager.ConnectAsync(_fixture.EndpointUrl);
        var disconnectTask = manager.DisconnectAsync();

        var connected = await connectTask;
        await disconnectTask;

        Assert.False(connected);
        Assert.False(manager.IsConnected);
        Assert.Null(manager.SubscriptionManager);
    }

    [Fact]
    public async Task StaleDisconnectIntent_CannotTearDownNewerConnection()
    {
        var staleDisconnect = _connectionManager!.RegisterExplicitLifecycleIntent();
        var newerConnect = _connectionManager.RegisterExplicitLifecycleIntent();

        var connected = await _connectionManager.ConnectWithIntentAsync(
            _fixture.EndpointUrl,
            publishingInterval: 250,
            credentials: null,
            securityMode: null,
            securityPolicy: null,
            samplingInterval: 250,
            queueSize: 10,
            operationGeneration: newerConnect);
        var disconnected = await _connectionManager.DisconnectWithIntentAsync(staleDisconnect);

        Assert.True(connected);
        Assert.False(disconnected);
        Assert.True(_connectionManager.IsConnected);
    }

    [Fact]
    public async Task AbandonedIntent_RestoresExistingSessionAndMonitoredGeneration()
    {
        Assert.True(await _connectionManager!.ConnectAsync(_fixture.EndpointUrl));
        var node = await _connectionManager.SubscribeAsync(
            new NodeId("Counter", (ushort)GetNamespaceIndex()),
            "Counter");
        Assert.NotNull(node);

        var abandonedGeneration = _connectionManager.RegisterExplicitLifecycleIntent();
        Assert.False(_connectionManager.IsConnected);

        _connectionManager.RestoreSessionAfterAbandonedIntent(abandonedGeneration);

        Assert.True(_connectionManager.IsConnected);
        Assert.Equal(abandonedGeneration, node.ConnectionGeneration);
    }

    [Fact]
    public void TryRegisterExplicitIntent_FailsWhenNewerIntentAlreadyExists()
    {
        var expectedVersion = _connectionManager!.ConnectionIntentVersion;
        _connectionManager.RegisterExplicitLifecycleIntent();

        var registered = _connectionManager.TryRegisterExplicitLifecycleIntent(
            expectedVersion,
            out _);

        Assert.False(registered);
    }

    [Fact]
    public async Task AutomaticReconnect_QueuedBeforeExplicitDisconnect_CannotResurrectSession()
    {
        var connected = await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);
        Assert.True(connected);

        long? reconnectIntent = null;
        _connectionManager.AutoReconnectTriggered += intent => reconnectIntent = intent;
        _connectionManager.OnReconnectRequired();
        Assert.NotNull(reconnectIntent);

        await _connectionManager.DisconnectAsync();
        var reconnected = await _connectionManager.ReconnectAutomaticallyAsync(reconnectIntent.Value);

        Assert.False(reconnected);
        Assert.False(_connectionManager.IsConnected);
        Assert.Null(_connectionManager.SubscriptionManager);
    }

    [Fact]
    public async Task KeepAliveAfterExplicitIntent_CannotSupersedeQueuedDisconnect()
    {
        var connected = await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);
        Assert.True(connected);

        var disconnectGeneration = _connectionManager.RegisterExplicitLifecycleIntent();
        var autoReconnectRaised = false;
        _connectionManager.AutoReconnectTriggered += _ => autoReconnectRaised = true;

        _connectionManager.OnReconnectRequired();
        var disconnected = await _connectionManager.DisconnectWithIntentAsync(disconnectGeneration);

        Assert.False(autoReconnectRaised);
        Assert.True(disconnected);
        Assert.False(_connectionManager.IsConnected);
    }

    [Fact]
    public async Task Disconnect_CancelsManualReconnectBeforeWrapperGateIsAcquired()
    {
        Assert.True(await _connectionManager!.ConnectAsync(_fixture.EndpointUrl));
        var gateEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var holdGate = _connectionManager.Client.ExecuteLifecycleAsync(async () =>
        {
            gateEntered.TrySetResult(true);
            await releaseGate.Task;
        });
        await gateEntered.Task;

        var reconnectGeneration = _connectionManager.RegisterExplicitLifecycleIntent();
        var reconnectTask = _connectionManager.ReconnectWithIntentAsync(reconnectGeneration);
        var disconnectGeneration = _connectionManager.RegisterExplicitLifecycleIntent();
        var disconnectTask = _connectionManager.DisconnectWithIntentAsync(disconnectGeneration);
        releaseGate.TrySetResult(true);

        await holdGate;
        Assert.False(await reconnectTask);
        Assert.True(await disconnectTask);
        Assert.False(_connectionManager.IsConnected);
    }

    [Fact]
    public async Task OperationsAfterKeepAliveLoss_AreRejectedUntilReconnectIsPublished()
    {
        var connected = await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);
        Assert.True(connected);

        _connectionManager.OnReconnectRequired();
        var reconnectGeneration = _connectionManager.ConnectionGeneration;
        var namespaceIndex = (ushort)GetNamespaceIndex();
        var nodeId = new NodeId("WritableNumber", namespaceIndex);
        var root = _connectionManager.NodeBrowser.GetRootNode();

        var writeStatus = await _connectionManager.WriteValueAsync(
            nodeId,
            123456789,
            reconnectGeneration);
        var subscription = await _connectionManager.SubscribeAsync(
            nodeId,
            "during reconnect",
            reconnectGeneration);
        var snapshot = await _connectionManager.ReadWriteSnapshotAsync(
            nodeId,
            reconnectGeneration,
            Attributes.Value);
        var children = await _connectionManager.NodeBrowser.GetChildrenAsync(root);

        Assert.False(_connectionManager.IsConnectionGenerationActive(reconnectGeneration));
        Assert.Equal(StatusCodes.BadNotConnected, writeStatus.Code);
        Assert.Null(subscription);
        Assert.Null(snapshot);
        Assert.Empty(children);
        Assert.False(root.ChildrenLoaded);
    }

    [Fact]
    public async Task OperationsFromPriorGeneration_AreRejectedAfterReconnect()
    {
        var connected = await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);
        Assert.True(connected);

        var oldGeneration = _connectionManager.ConnectionGeneration;
        var oldRoot = _connectionManager.NodeBrowser.GetRootNode();
        var namespaceIndex = (ushort)GetNamespaceIndex();
        var writableNode = new NodeId("WritableNumber", namespaceIndex);
        var valueBefore = await _connectionManager.Client.ReadValueAsync(writableNode);

        await _connectionManager.DisconnectAsync();
        connected = await _connectionManager.ConnectAsync(_fixture.EndpointUrl);
        Assert.True(connected);
        Assert.NotEqual(oldGeneration, _connectionManager.ConnectionGeneration);

        var writeStatus = await _connectionManager.WriteValueAsync(
            writableNode,
            123456789,
            oldGeneration);
        var staleSubscription = await _connectionManager.SubscribeAsync(
            writableNode,
            "stale",
            oldGeneration);
        var staleChildren = await _connectionManager.NodeBrowser.GetChildrenAsync(oldRoot);
        var valueAfter = await _connectionManager.Client.ReadValueAsync(writableNode);

        Assert.Equal(StatusCodes.BadNotConnected, writeStatus.Code);
        Assert.Null(staleSubscription);
        Assert.Empty(staleChildren);
        Assert.False(oldRoot.ChildrenLoaded);
        Assert.Equal(valueBefore?.Value, valueAfter?.Value);
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

        Assert.NotNull(node);

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
        Assert.NotNull(node);

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
        Opcilloscope.OpcUa.Models.MonitoredNode? addedNode = null;
        _connectionManager.VariableAdded += node => addedNode = node;

        // Act
        var node = await _connectionManager.SubscribeAsync(nodeId, "Counter");

        // Assert
        Assert.NotNull(node);
        Assert.NotNull(addedNode);
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
        Assert.NotNull(node);

        uint? removedHandle = null;
        _connectionManager.VariableRemoved += (handle, _) => removedHandle = handle;

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
        var session = _connectionManager?.Client.Session
            ?? throw new InvalidOperationException("The test connection has no active OPC UA session.");

        var index = session.NamespaceUris.GetIndex(
            Opcilloscope.TestServer.TestNodeManager.NamespaceUri);
        return index >= 0
            ? index
            : throw new InvalidOperationException("The test server namespace was not registered in the session.");
    }
}
