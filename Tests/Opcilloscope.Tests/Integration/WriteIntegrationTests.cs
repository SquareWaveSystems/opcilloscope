using Opc.Ua;
using Opcilloscope.OpcUa;
using Opcilloscope.Tests.Infrastructure;
using Opcilloscope.Utilities;

namespace Opcilloscope.Tests.Integration;

/// <summary>
/// Integration tests covering the node-write path end-to-end.
/// Wrapper-level write coverage for String/Int32/Boolean lives in OpcUaIntegrationTests;
/// these tests fill the remaining gaps (Double, read-only failure, ConnectionManager pass-through).
/// </summary>
[Collection("TestServer")]
public class WriteIntegrationTests : IAsyncLifetime
{
    private readonly TestServerFixture _fixture;
    private readonly Logger _logger = new();
    private ConnectionManager? _connectionManager;

    public WriteIntegrationTests(TestServerFixture fixture)
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

    private int GetNamespaceIndex()
    {
        var session = _connectionManager!.Client.Session
            ?? throw new InvalidOperationException("Client not connected");
        var index = session.NamespaceUris.GetIndex(Opcilloscope.TestServer.TestNodeManager.NamespaceUri);
        if (index < 0)
            throw new InvalidOperationException("Test server namespace not found");
        return index;
    }

    [Fact]
    public async Task WrapperWrite_Double_RoundTrips()
    {
        using var client = await _fixture.CreateConnectedClientAsync();
        var nsIndex = client.Session!.NamespaceUris.GetIndex(Opcilloscope.TestServer.TestNodeManager.NamespaceUri);
        var nodeId = new NodeId("SineFrequency", (ushort)nsIndex);
        var testValue = 0.42;

        var status = await client.WriteValueAsync(nodeId, testValue);
        Assert.True(StatusCode.IsGood(status));

        var readBack = await client.ReadValueAsync(nodeId);
        Assert.NotNull(readBack);
        Assert.Equal(testValue, (double)readBack!.Value, 6);
    }

    [Fact]
    public async Task WrapperWrite_ReadOnlyNode_ReturnsBadStatus()
    {
        using var client = await _fixture.CreateConnectedClientAsync();
        var nsIndex = client.Session!.NamespaceUris.GetIndex(Opcilloscope.TestServer.TestNodeManager.NamespaceUri);
        // ServerName is a static read-only string in the StaticData folder
        var nodeId = new NodeId("ServerName", (ushort)nsIndex);

        var status = await client.WriteValueAsync(nodeId, "ShouldNotBeWritten");

        Assert.False(StatusCode.IsGood(status), $"Expected non-Good status writing to read-only node, got 0x{status.Code:X8}");
    }

    [Fact]
    public async Task ConnectionManager_WriteValueAsync_RoundTrips()
    {
        await _connectionManager!.ConnectAsync(_fixture.EndpointUrl);
        var nodeId = new NodeId("WritableNumber", (ushort)GetNamespaceIndex());
        var testValue = 7777;

        var status = await _connectionManager.WriteValueAsync(nodeId, testValue);
        Assert.True(StatusCode.IsGood(status));

        var readBack = await _connectionManager.Client.ReadValueAsync(nodeId);
        Assert.NotNull(readBack);
        Assert.Equal(testValue, readBack!.Value);
    }

    [Fact]
    public async Task ConnectionManager_WriteValueAsync_NotConnected_ReturnsBadNotConnected()
    {
        var nodeId = new NodeId("WritableNumber", (ushort)2);

        var status = await _connectionManager!.WriteValueAsync(nodeId, 1);

        Assert.Equal(StatusCodes.BadNotConnected, status.Code);
    }
}
