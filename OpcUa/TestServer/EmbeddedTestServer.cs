using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

namespace OpcScope.OpcUa.TestServer;

/// <summary>
/// In-process OPC UA test server that can be started from within the application.
/// Provides test nodes for demonstration and testing purposes.
/// </summary>
public class EmbeddedTestServer : IDisposable
{
    private StandardServer? _server;
    private ApplicationInstance? _application;
    private bool _disposed;

    public const string DefaultEndpointUrl = "opc.tcp://localhost:4840/UA/OpcScopeTest";
    public const string ApplicationName = "OpcScope Test Server";
    public const string ApplicationUri = "urn:opcscope:testserver";

    public string EndpointUrl { get; private set; } = DefaultEndpointUrl;
    public bool IsRunning => _server != null;

    public event Action? Started;
    public event Action? Stopped;
    public event Action<string>? Error;

    /// <summary>
    /// Starts the test server asynchronously.
    /// </summary>
    public async Task StartAsync(int port = 4840)
    {
        if (_server != null)
        {
            throw new InvalidOperationException("Server is already running");
        }

        EndpointUrl = $"opc.tcp://localhost:{port}/UA/OpcScopeTest";

        var config = CreateApplicationConfiguration(port);
        await config.Validate(ApplicationType.Server);

        _application = new ApplicationInstance
        {
            ApplicationName = ApplicationName,
            ApplicationType = ApplicationType.Server,
            ApplicationConfiguration = config
        };

        // Check certificate (create if needed)
        var hasAppCertificate = await _application.CheckApplicationInstanceCertificate(
            silent: true,
            minimumKeySize: 0);

        if (!hasAppCertificate)
        {
            throw new Exception("Application certificate validation failed");
        }

        // Create and start the server
        _server = new TestOpcUaServer();
        await _application.Start(_server);

        Started?.Invoke();
    }

    /// <summary>
    /// Stops the test server.
    /// </summary>
    public async Task StopAsync()
    {
        if (_server != null)
        {
            await Task.Run(() => _server.Stop());
            _server.Dispose();
            _server = null;

            Stopped?.Invoke();
        }
    }

    private ApplicationConfiguration CreateApplicationConfiguration(int port)
    {
        var pkiPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpcScope",
            "TestServer",
            "pki");

        var config = new ApplicationConfiguration
        {
            ApplicationName = ApplicationName,
            ApplicationUri = ApplicationUri,
            ProductUri = "urn:opcscope:testserver:product",
            ApplicationType = ApplicationType.Server,

            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(pkiPath, "own"),
                    SubjectName = $"CN={ApplicationName}, O=OpcScope, DC=localhost"
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(pkiPath, "issuers")
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(pkiPath, "trusted")
                },
                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(pkiPath, "rejected")
                },
                AutoAcceptUntrustedCertificates = true,
                RejectSHA1SignedCertificates = false,
                MinimumCertificateKeySize = 1024
            },

            TransportConfigurations = new TransportConfigurationCollection(),
            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = 15000,
                MaxStringLength = 1048576,
                MaxByteStringLength = 1048576,
                MaxArrayLength = 65535,
                MaxMessageSize = 4194304,
                MaxBufferSize = 65535,
                ChannelLifetime = 300000,
                SecurityTokenLifetime = 3600000
            },

            ServerConfiguration = new ServerConfiguration
            {
                BaseAddresses = { $"opc.tcp://localhost:{port}/UA/OpcScopeTest" },
                MinRequestThreadCount = 5,
                MaxRequestThreadCount = 100,
                MaxQueuedRequestCount = 2000,

                SecurityPolicies = new ServerSecurityPolicyCollection
                {
                    new ServerSecurityPolicy
                    {
                        SecurityMode = MessageSecurityMode.None,
                        SecurityPolicyUri = SecurityPolicies.None
                    }
                },

                UserTokenPolicies = new UserTokenPolicyCollection
                {
                    new UserTokenPolicy(UserTokenType.Anonymous)
                },

                DiagnosticsEnabled = false,
                MaxSessionCount = 100,
                MinSessionTimeout = 10000,
                MaxSessionTimeout = 3600000,
                MaxBrowseContinuationPoints = 10,
                MaxQueryContinuationPoints = 10,
                MaxHistoryContinuationPoints = 100,
                MaxRequestAge = 600000,
                MinPublishingInterval = 100,
                MaxPublishingInterval = 3600000,
                PublishingResolution = 50,
                MaxSubscriptionLifetime = 3600000,
                MaxMessageQueueSize = 100,
                MaxNotificationQueueSize = 100,
                MaxNotificationsPerPublish = 1000,
                MinMetadataSamplingInterval = 1000,
                MaxPublishRequestCount = 20,
                MaxSubscriptionCount = 100,
                MaxEventQueueSize = 10000
            },

            TraceConfiguration = new TraceConfiguration
            {
                OutputFilePath = null,
                DeleteOnLoad = true,
                TraceMasks = 0 // No tracing
            }
        };

        return config;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                StopAsync().GetAwaiter().GetResult();
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// Custom OPC UA server implementation with test nodes.
/// </summary>
internal class TestOpcUaServer : StandardServer
{
    protected override MasterNodeManager CreateMasterNodeManager(
        IServerInternal server,
        ApplicationConfiguration configuration)
    {
        var nodeManagers = new List<INodeManager>
        {
            new TestNodeManager(server, configuration)
        };

        return new MasterNodeManager(server, configuration, null, nodeManagers.ToArray());
    }

    protected override ServerProperties LoadServerProperties()
    {
        return new ServerProperties
        {
            ManufacturerName = "OpcScope",
            ProductName = "OpcScope Test Server",
            ProductUri = "urn:opcscope:testserver:product",
            SoftwareVersion = Utils.GetAssemblySoftwareVersion(),
            BuildNumber = Utils.GetAssemblyBuildNumber(),
            BuildDate = Utils.GetAssemblyTimestamp()
        };
    }
}
