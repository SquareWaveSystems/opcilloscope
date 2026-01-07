using Opc.Ua;
using Opc.Ua.Client;
using OpcScope.OpcUa;
using OpcScope.TestServer;

namespace OpcScope.Tests.Infrastructure;

/// <summary>
/// xUnit fixture that manages the in-process OPC UA test server lifecycle.
/// Use with IClassFixture&lt;TestServerFixture&gt; for class-level sharing,
/// or ICollectionFixture for collection-level sharing.
/// </summary>
public class TestServerFixture : IAsyncLifetime
{
    private static readonly object _portLock = new();
    private static int _nextPort = 48400; // Use higher port range to avoid conflicts with existing OPC UA servers

    private OpcScope.TestServer.TestServer? _server;
    private int _port;

    /// <summary>
    /// The endpoint URL for connecting to the test server.
    /// </summary>
    public string EndpointUrl => $"opc.tcp://localhost:{_port}/UA/OpcScopeTest";

    /// <summary>
    /// The test server instance.
    /// </summary>
    public OpcScope.TestServer.TestServer Server => _server ?? throw new InvalidOperationException("Server not started");

    /// <summary>
    /// Indicates whether the server is running.
    /// </summary>
    public bool IsRunning => _server?.IsRunning ?? false;

    private static int AllocatePort()
    {
        // Allocate a unique port to avoid conflicts when running tests in parallel
        lock (_portLock)
        {
            return _nextPort++;
        }
    }

    public async Task InitializeAsync()
    {
        _port = AllocatePort();

        _server = new OpcScope.TestServer.TestServer();
        await _server.StartAsync(_port);
    }

    public async Task DisposeAsync()
    {
        if (_server != null)
        {
            await _server.StopAsync();
            _server.Dispose();
            _server = null;
        }
    }

    /// <summary>
    /// Creates a connected OpcUaClientWrapper for testing.
    /// </summary>
    public async Task<OpcUaClientWrapper> CreateConnectedClientAsync()
    {
        var client = new OpcUaClientWrapper();
        await client.ConnectAsync(EndpointUrl);
        return client;
    }
}

/// <summary>
/// Base class for integration tests that need a running OPC UA server.
/// Provides convenient access to the test server and connected client.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<TestServerFixture>, IAsyncLifetime
{
    protected TestServerFixture Fixture { get; }
    protected OpcUaClientWrapper? Client { get; private set; }

    protected IntegrationTestBase(TestServerFixture fixture)
    {
        Fixture = fixture;
    }

    public virtual async Task InitializeAsync()
    {
        Client = await Fixture.CreateConnectedClientAsync();
    }

    public virtual async Task DisposeAsync()
    {
        if (Client != null)
        {
            await Client.DisconnectAsync();
            Client = null;
        }
    }

    /// <summary>
    /// Gets a NodeId for a simulation node by name.
    /// </summary>
    protected NodeId GetSimulationNodeId(string nodeName)
    {
        return new NodeId(nodeName, (ushort)GetNamespaceIndex());
    }

    /// <summary>
    /// Gets a NodeId for a static data node by name.
    /// </summary>
    protected NodeId GetStaticDataNodeId(string nodeName)
    {
        return new NodeId(nodeName, (ushort)GetNamespaceIndex());
    }

    /// <summary>
    /// Gets the namespace index for the test server namespace.
    /// </summary>
    protected int GetNamespaceIndex()
    {
        if (Client?.Session == null)
        {
            throw new InvalidOperationException("Client not connected");
        }

        var index = Client.Session.NamespaceUris.GetIndex(OpcScope.TestServer.TestNodeManager.NamespaceUri);
        if (index < 0)
        {
            // Namespace not found - list available namespaces for debugging
            var available = string.Join(", ", Client.Session.NamespaceUris.ToArray());
            throw new InvalidOperationException(
                $"Test server namespace '{OpcScope.TestServer.TestNodeManager.NamespaceUri}' not found. " +
                $"Available namespaces: [{available}]");
        }
        return index;
    }
}

/// <summary>
/// xUnit collection definition for sharing a single test server across multiple test classes.
/// Usage: [Collection("TestServer")]
/// </summary>
[CollectionDefinition("TestServer")]
public class TestServerCollection : ICollectionFixture<TestServerFixture>
{
    // This class has no code, and is never created.
    // Its purpose is to be the place to apply [CollectionDefinition]
    // and all the ICollectionFixture<> interfaces.
}
