using Opc.Ua;
using Opcilloscope.Tests.Infrastructure;

namespace Opcilloscope.Tests.Integration;

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
        Assert.Equal("Opcilloscope Test Server", value.Value);
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

        // Read back and verify
        var readValue = await Client!.ReadValueAsync(nodeId);
        Assert.Equal(testValue, readValue!.Value);
    }

    [Fact]
    public async Task CanWriteAndReadNumber()
    {
        var nodeId = new NodeId("WritableNumber", (ushort)GetNamespaceIndex());
        var testValue = 12345;

        // Write
        var writeResult = await Client!.WriteValueAsync(nodeId, testValue);
        Assert.True(StatusCode.IsGood(writeResult));

        // Read back and verify
        var readValue = await Client!.ReadValueAsync(nodeId);
        Assert.Equal(testValue, readValue!.Value);
    }

    [Fact]
    public async Task CanToggleBoolean()
    {
        var nodeId = new NodeId("ToggleBoolean", (ushort)GetNamespaceIndex());

        // Read current value
        var currentValue = await Client!.ReadValueAsync(nodeId);
        var currentBool = currentValue!.Value is bool b && b;

        // Write opposite
        var writeResult = await Client!.WriteValueAsync(nodeId, !currentBool);
        Assert.True(StatusCode.IsGood(writeResult));

        // Read back and verify
        var newValue = await Client!.ReadValueAsync(nodeId);
        Assert.Equal(!currentBool, newValue!.Value);
    }

    [Fact]
    public async Task CounterIncrements()
    {
        var nodeId = new NodeId("Counter", (ushort)GetNamespaceIndex());

        // Read first value
        var value1 = await Client!.ReadValueAsync(nodeId);
        var counter1 = value1!.Value is int c1 ? c1 : 0;

        // Wait for simulation tick
        await Task.Delay(1100);

        // Read second value
        var value2 = await Client!.ReadValueAsync(nodeId);
        var counter2 = value2!.Value is int c2 ? c2 : 0;

        Assert.True(counter2 > counter1, $"Counter should increment: {counter1} -> {counter2}");
    }

    [Fact]
    public async Task CanReadSineFrequency()
    {
        var nodeId = new NodeId("SineFrequency", (ushort)GetNamespaceIndex());
        var value = await Client!.ReadValueAsync(nodeId);

        Assert.NotNull(value);
        Assert.IsType<double>(value.Value);
    }

    [Fact]
    public async Task CanWriteAndReadSineFrequency()
    {
        var nodeId = new NodeId("SineFrequency", (ushort)GetNamespaceIndex());

        // Read original value to restore later
        var originalValue = await Client!.ReadValueAsync(nodeId);
        var original = (double)originalValue!.Value;

        var testValue = 0.5;

        // Write
        var writeResult = await Client!.WriteValueAsync(nodeId, testValue);
        Assert.True(StatusCode.IsGood(writeResult));

        // Read back and verify
        var readValue = await Client!.ReadValueAsync(nodeId);
        Assert.Equal(testValue, (double)readValue!.Value);

        // Restore original value to avoid affecting other tests
        await Client!.WriteValueAsync(nodeId, original);
    }

    [Fact]
    public async Task CanReadTriangleWave()
    {
        var nodeId = new NodeId("TriangleWave", (ushort)GetNamespaceIndex());
        var value = await Client!.ReadValueAsync(nodeId);

        Assert.NotNull(value);
        Assert.IsType<double>(value.Value);
        var doubleValue = (double)value.Value;
        Assert.InRange(doubleValue, 0, 100);
    }

    [Fact]
    public async Task CanWriteAndReadTriangleFrequency()
    {
        var nodeId = new NodeId("TriangleFrequency", (ushort)GetNamespaceIndex());
        var originalValue = await Client!.ReadValueAsync(nodeId);
        var original = (double)originalValue!.Value;

        var testValue = 0.3;

        var writeResult = await Client!.WriteValueAsync(nodeId, testValue);
        Assert.True(StatusCode.IsGood(writeResult));

        var readValue = await Client!.ReadValueAsync(nodeId);
        Assert.Equal(testValue, (double)readValue!.Value);

        await Client!.WriteValueAsync(nodeId, original);
    }

    [Fact]
    public async Task CanReadSquareWave()
    {
        var nodeId = new NodeId("SquareWave", (ushort)GetNamespaceIndex());
        var value = await Client!.ReadValueAsync(nodeId);

        Assert.NotNull(value);
        Assert.IsType<double>(value.Value);
        var doubleValue = (double)value.Value;
        Assert.True(doubleValue == 0 || doubleValue == 100, $"Square wave should be 0 or 100, got {doubleValue}");
    }

    [Fact]
    public async Task CanWriteAndReadSquareFrequency()
    {
        var nodeId = new NodeId("SquareFrequency", (ushort)GetNamespaceIndex());
        var originalValue = await Client!.ReadValueAsync(nodeId);
        var original = (double)originalValue!.Value;

        var testValue = 0.2;

        var writeResult = await Client!.WriteValueAsync(nodeId, testValue);
        Assert.True(StatusCode.IsGood(writeResult));

        var readValue = await Client!.ReadValueAsync(nodeId);
        Assert.Equal(testValue, (double)readValue!.Value);

        await Client!.WriteValueAsync(nodeId, original);
    }

    [Fact]
    public async Task CanWriteAndReadSquareDutyCycle()
    {
        var nodeId = new NodeId("SquareDutyCycle", (ushort)GetNamespaceIndex());
        var originalValue = await Client!.ReadValueAsync(nodeId);
        var original = (double)originalValue!.Value;

        var testValue = 0.75;

        var writeResult = await Client!.WriteValueAsync(nodeId, testValue);
        Assert.True(StatusCode.IsGood(writeResult));

        var readValue = await Client!.ReadValueAsync(nodeId);
        Assert.Equal(testValue, (double)readValue!.Value);

        await Client!.WriteValueAsync(nodeId, original);
    }

    [Fact]
    public async Task CanReadSawtoothWave()
    {
        var nodeId = new NodeId("SawtoothWave", (ushort)GetNamespaceIndex());
        var value = await Client!.ReadValueAsync(nodeId);

        Assert.NotNull(value);
        Assert.IsType<double>(value.Value);
        var doubleValue = (double)value.Value;
        Assert.InRange(doubleValue, 0, 100);
    }

    [Fact]
    public async Task CanWriteAndReadSawtoothFrequency()
    {
        var nodeId = new NodeId("SawtoothFrequency", (ushort)GetNamespaceIndex());
        var originalValue = await Client!.ReadValueAsync(nodeId);
        var original = (double)originalValue!.Value;

        var testValue = 0.4;

        var writeResult = await Client!.WriteValueAsync(nodeId, testValue);
        Assert.True(StatusCode.IsGood(writeResult));

        var readValue = await Client!.ReadValueAsync(nodeId);
        Assert.Equal(testValue, (double)readValue!.Value);

        await Client!.WriteValueAsync(nodeId, original);
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
