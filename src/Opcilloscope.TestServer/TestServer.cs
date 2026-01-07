using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

namespace Opcilloscope.TestServer;

/// <summary>
/// Standalone OPC UA test server for Opcilloscope testing and development.
/// </summary>
public class TestServer : IAsyncDisposable, IDisposable
{
    private StandardServer? _server;
    private ApplicationInstance? _application;
    private bool _disposed;

    public const string ApplicationName = "Opcilloscope Test Server";
    public const string ApplicationUri = "urn:opcilloscope:testserver";

    public string EndpointUrl { get; private set; } = string.Empty;
    public bool IsRunning => _server != null;

    /// <summary>
    /// Starts the test server on the specified port.
    /// </summary>
    public async Task StartAsync(int port = 4840)
    {
        if (_server != null)
        {
            throw new InvalidOperationException("Server is already running");
        }

        EndpointUrl = $"opc.tcp://localhost:{port}/UA/OpcilloscopeTest";

        var config = CreateApplicationConfiguration(port);
        await config.ValidateAsync(ApplicationType.Server);

        // Pass null for certificate validator to use default validation
        _application = new ApplicationInstance(config, null);

        // Check certificate (create if needed)
        var hasAppCertificate = await _application.CheckApplicationInstanceCertificatesAsync(
            silent: true);

        if (!hasAppCertificate)
        {
            throw new InvalidOperationException(
                $"Application certificate validation failed. " +
                $"An application instance certificate may be missing, invalid, or untrusted.");
        }

        // Create and start the server
        _server = new TestOpcUaServer();
        await _application.StartAsync(_server);
    }

    /// <summary>
    /// Stops the test server.
    /// </summary>
    public async Task StopAsync()
    {
        if (_server != null)
        {
            await _server.StopAsync();
            _server.Dispose();
            _server = null;
        }
    }

    private ApplicationConfiguration CreateApplicationConfiguration(int port)
    {
        var pkiPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Opcilloscope",
            "TestServer",
            "pki");

        var config = new ApplicationConfiguration
        {
            ApplicationName = ApplicationName,
            ApplicationUri = ApplicationUri,
            ProductUri = "urn:opcilloscope:testserver:product",
            ApplicationType = ApplicationType.Server,

            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(pkiPath, "own"),
                    SubjectName = $"CN={ApplicationName}, O=Opcilloscope, DC=localhost"
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
                // WARNING: Auto-accepting untrusted certificates is appropriate for test/development
                // environments only. NEVER use this setting in production.
                AutoAcceptUntrustedCertificates = true,
                RejectSHA1SignedCertificates = false,
                MinimumCertificateKeySize = 2048
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
                BaseAddresses = { $"opc.tcp://localhost:{port}/UA/OpcilloscopeTest" },
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
                TraceMasks = 0
            }
        };

        return config;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await StopAsync();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Synchronous disposal. Note: This blocks on async cleanup.
    /// Prefer using DisposeAsync() when possible.
    /// WARNING: This method uses GetAwaiter().GetResult() which can cause deadlocks
    /// in certain synchronization contexts (e.g., ASP.NET request contexts).
    /// This is acceptable for a test server typically run from a console application.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            if (_server != null)
            {
                // Use async method even in sync Dispose to ensure proper cleanup
                // Note: This can potentially cause deadlocks in some synchronization contexts
                _server.StopAsync().GetAwaiter().GetResult();
                _server.Dispose();
                _server = null;
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
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
            ManufacturerName = "Opcilloscope",
            ProductName = "Opcilloscope Test Server",
            ProductUri = "urn:opcilloscope:testserver:product",
            SoftwareVersion = Utils.GetAssemblySoftwareVersion(),
            BuildNumber = Utils.GetAssemblyBuildNumber(),
            BuildDate = Utils.GetAssemblyTimestamp()
        };
    }
}
