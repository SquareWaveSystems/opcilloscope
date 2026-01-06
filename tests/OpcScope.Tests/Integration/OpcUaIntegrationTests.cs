using Opc.Ua;
using OpcScope.Tests.Infrastructure;

namespace OpcScope.Tests.Integration;

/// <summary>
/// Integration tests demonstrating the in-process OPC UA test server.
/// </summary>
public class OpcUaIntegrationTests : IntegrationTestBase
{
    public OpcUaIntegrationTests(TestServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public void Server_IsRunning()
    {
        Assert.True(Fixture.IsRunning);
    }

    [Fact]
    public void Client_IsConnected()
    {
        Assert.NotNull(Client);
        Assert.True(Client!.IsConnected);
    }

    [Fact]
    public async Task CanBrowseRootFolder()
    {
        var references = await Client!.BrowseAsync(ObjectIds.RootFolder);
        Assert.NotNull(references);
        Assert.True(references.Count > 0);
    }

    [Fact]
    public async Task CanBrowseObjectsFolder()
    {
        var references = await Client!.BrowseAsync(ObjectIds.ObjectsFolder);
        Assert.NotNull(references);

        // Should contain our Simulation and StaticData folders
        var folderNames = references.Select(r => r.BrowseName.Name).ToList();
        Assert.Contains("Simulation", folderNames);
        Assert.Contains("StaticData", folderNames);
    }

    [Fact]
    public async Task CanReadStaticServerName()
    {
        var nodeId = new NodeId("ServerName", (ushort)GetNamespaceIndex());
        var value = await Client!.ReadValueAsync(nodeId);

        Assert.NotNull(value);
        Assert.Equal("OpcScope Test Server", value.Value);
    }

    [Fact]
    public async Task CanReadStaticVersion()
    {
        var nodeId = new NodeId("Version", (ushort)GetNamespaceIndex());
        var value = await Client!.ReadValueAsync(nodeId);

        Assert.NotNull(value);
        Assert.Equal("1.0.0", value.Value);
    }

    [Fact]
    public async Task CanReadArrayOfInts()
    {
        var nodeId = new NodeId("ArrayOfInts", (ushort)GetNamespaceIndex());
        var value = await Client!.ReadValueAsync(nodeId);

        Assert.NotNull(value);
        var array = value.Value as int[];
        Assert.NotNull(array);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, array);
    }

    [Fact]
    public async Task CanReadCounter()
    {
        var nodeId = new NodeId("Counter", (ushort)GetNamespaceIndex());
        var value = await Client!.ReadValueAsync(nodeId);

        Assert.NotNull(value);
        Assert.IsType<int>(value.Value);
    }

    [Fact]
    public async Task CanWriteAndReadString()
    {
        var nodeId = new NodeId("WritableString", (ushort)GetNamespaceIndex());
        var testValue = $"Test value {Guid.NewGuid()}";

        // Write
        var writeResult = await Client!.WriteValueAsync(nodeId, testValue);
        Assert.True(StatusCode.IsGood(writeResult));

        // Read back
        var readValue = await Client!.ReadValueAsync(nodeId);
        Assert.Equal(testValue, readValue.Value);
    }

    [Fact]
    public async Task CanWriteAndReadNumber()
    {
        var nodeId = new NodeId("WritableNumber", (ushort)GetNamespaceIndex());
        var testValue = 12345;

        // Write
        var writeResult = await Client!.WriteValueAsync(nodeId, testValue);
        Assert.True(StatusCode.IsGood(writeResult));

        // Read back
        var readValue = await Client!.ReadValueAsync(nodeId);
        Assert.Equal(testValue, readValue.Value);
    }

    [Fact]
    public async Task CanToggleBoolean()
    {
        var nodeId = new NodeId("ToggleBoolean", (ushort)GetNamespaceIndex());

        // Read current
        var currentValue = await Client!.ReadValueAsync(nodeId);
        var currentBool = (bool)currentValue.Value;

        // Write opposite
        var writeResult = await Client!.WriteValueAsync(nodeId, !currentBool);
        Assert.True(StatusCode.IsGood(writeResult));

        // Read back
        var newValue = await Client!.ReadValueAsync(nodeId);
        Assert.Equal(!currentBool, newValue.Value);
    }

    [Fact]
    public async Task CounterIncrements()
    {
        var nodeId = new NodeId("Counter", (ushort)GetNamespaceIndex());

        // Read first value
        var value1 = await Client!.ReadValueAsync(nodeId);
        var counter1 = (int)value1.Value;

        // Wait for simulation tick
        await Task.Delay(1100);

        // Read second value
        var value2 = await Client!.ReadValueAsync(nodeId);
        var counter2 = (int)value2.Value;

        Assert.True(counter2 > counter1, $"Counter should increment: {counter1} -> {counter2}");
    }
}

/// <summary>
/// Tests using collection fixture for shared server across multiple test classes.
/// </summary>
[Collection("TestServer")]
public class AdditionalIntegrationTests
{
    private readonly TestServerFixture _fixture;

    public AdditionalIntegrationTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void ServerEndpointUrl_IsNotNull()
    {
        Assert.NotNull(_fixture.EndpointUrl);
        Assert.StartsWith("opc.tcp://", _fixture.EndpointUrl);
    }
}
