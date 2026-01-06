using Opc.Ua;
using Opc.Ua.Client;
using OpcScope.Utilities;

namespace OpcScope.OpcUa;

/// <summary>
/// Wrapper around OPC Foundation Client Session providing async connection management.
/// </summary>
public class OpcUaClientWrapper : IDisposable
{
    private ISession? _session;
    private readonly Logger _logger;
    private string? _currentEndpoint;
    private CancellationTokenSource? _reconnectCts;
    private bool _disposed;
    private ApplicationConfiguration? _appConfig;

    public bool IsConnected => _session?.Connected ?? false;
    public string? CurrentEndpoint => _currentEndpoint;
    public ISession? Session => _session;

    public event Action? Connected;
    public event Action? Disconnected;
    public event Action<string>? ConnectionError;

    public OpcUaClientWrapper(Logger? logger = null)
    {
        _logger = logger ?? new Logger();
    }

    private async Task<ApplicationConfiguration> GetApplicationConfigAsync()
    {
        if (_appConfig != null)
            return _appConfig;

        _appConfig = new ApplicationConfiguration
        {
            ApplicationName = "OpcScope",
            ApplicationType = ApplicationType.Client,
            ApplicationUri = "urn:OpcScope:Client",
            ProductUri = "urn:OpcScope",
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "OpcScope", "pki", "own"),
                    SubjectName = "CN=OpcScope, O=OpcScope, DC=localhost"
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "OpcScope", "pki", "issuer")
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "OpcScope", "pki", "trusted")
                },
                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "OpcScope", "pki", "rejected")
                },
                AutoAcceptUntrustedCertificates = true,
                AddAppCertToTrustedStore = false
            },
            TransportConfigurations = new TransportConfigurationCollection(),
            TransportQuotas = new TransportQuotas { OperationTimeout = 30000 },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
        };

        // AutoAcceptUntrustedCertificates is already set above, no need for explicit validator

        await _appConfig.ValidateAsync(ApplicationType.Client);

        return _appConfig;
    }

    public async Task<bool> ConnectAsync(string endpointUrl)
    {
        try
        {
            Disconnect();

            _logger.Info($"Connecting to {endpointUrl}...");

            var config = await GetApplicationConfigAsync();

            // Discover endpoints
            var selectedEndpoint = await DiscoverAndSelectEndpointAsync(config, endpointUrl, useSecurity: false);

            // Create session
            var endpointConfig = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfig);

#pragma warning disable CS0618 // Session.Create is obsolete but ISessionFactory.CreateAsync requires additional setup
            _session = await Opc.Ua.Client.Session.Create(
                config,
                endpoint,
                false,
                "OpcScope Session",
                60000,
                new UserIdentity(new AnonymousIdentityToken()),
                null
            );
#pragma warning restore CS0618

            _session.KeepAlive += Session_KeepAlive;

            _currentEndpoint = endpointUrl;
            _logger.Info($"Connected to {endpointUrl}");
            Connected?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Connection failed: {ex.Message}");
            ConnectionError?.Invoke(ex.Message);
            Disconnect();
            return false;
        }
    }

    private void Session_KeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (e.Status != null && ServiceResult.IsBad(e.Status))
        {
            _logger.Warning($"Keep alive error: {e.Status}");
            if (!session.Connected)
            {
                Disconnected?.Invoke();
            }
        }
    }

    public void Disconnect()
    {
        _reconnectCts?.Cancel();
        _reconnectCts = null;

        if (_session != null)
        {
            try
            {
                _session.KeepAlive -= Session_KeepAlive;
                _session.CloseAsync().GetAwaiter().GetResult();
                _session.Dispose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                // Session cleanup errors are expected during network issues
                System.Diagnostics.Debug.WriteLine($"Session cleanup error (non-critical): {ex.Message}");
            }
            _session = null;
            _currentEndpoint = null;
            Disconnected?.Invoke();
        }
    }

    public async Task<bool> ReconnectAsync()
    {
        if (string.IsNullOrEmpty(_currentEndpoint))
            return false;

        var endpoint = _currentEndpoint;
        Disconnect();

        // Exponential backoff: 1s, 2s, 4s, 8s
        int[] delays = { 1000, 2000, 4000, 8000 };
        _reconnectCts = new CancellationTokenSource();

        for (int i = 0; i < delays.Length; i++)
        {
            if (_reconnectCts.Token.IsCancellationRequested)
                return false;

            _logger.Info($"Reconnection attempt {i + 1}/{delays.Length}...");

            if (await ConnectAsync(endpoint))
                return true;

            try
            {
                await Task.Delay(delays[i], _reconnectCts.Token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        _logger.Error("Reconnection failed after all attempts");
        return false;
    }

    public async Task<ReferenceDescriptionCollection> BrowseAsync(NodeId nodeId)
    {
        if (_session == null)
            throw new InvalidOperationException("Not connected");

        var browser = new Browser(_session)
        {
            BrowseDirection = BrowseDirection.Forward,
            NodeClassMask = (int)NodeClass.Object | (int)NodeClass.Variable | (int)NodeClass.Method,
            ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
            IncludeSubtypes = true,
            ResultMask = (uint)BrowseResultMask.All
        };

        return await browser.BrowseAsync(nodeId);
    }

    public async Task<DataValue?> ReadValueAsync(NodeId nodeId)
    {
        if (_session == null)
            throw new InvalidOperationException("Not connected");

        return await _session.ReadValueAsync(nodeId);
    }

    public async Task<DataValueCollection> ReadAttributesAsync(NodeId nodeId, params uint[] attributeIds)
    {
        if (_session == null)
            throw new InvalidOperationException("Not connected");

        var nodesToRead = new ReadValueIdCollection();
        foreach (var attrId in attributeIds)
        {
            nodesToRead.Add(new ReadValueId
            {
                NodeId = nodeId,
                AttributeId = attrId
            });
        }

        var response = await _session.ReadAsync(
            null,
            0,
            TimestampsToReturn.Both,
            nodesToRead,
            CancellationToken.None
        );

        return response.Results;
    }

    public async Task<StatusCode> WriteValueAsync(NodeId nodeId, object value)
    {
        if (_session == null)
            throw new InvalidOperationException("Not connected");

        var nodesToWrite = new WriteValueCollection
        {
            new WriteValue
            {
                NodeId = nodeId,
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(value))
            }
        };

        var response = await _session.WriteAsync(
            null,
            nodesToWrite,
            CancellationToken.None
        );

        return response.Results?.Count > 0 ? response.Results[0] : StatusCodes.BadUnexpectedError;
    }

    public Task DisconnectAsync()
    {
        Disconnect();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Disconnect();
        }
    }

    private async Task<EndpointDescription> DiscoverAndSelectEndpointAsync(
        ApplicationConfiguration config,
        string endpointUrl,
        bool useSecurity)
    {
        // Discover endpoints from the server
        var uri = new Uri(endpointUrl);
        _logger.Info($"Discovering endpoints at {uri}...");

        var endpointConfig = EndpointConfiguration.Create(config);
#pragma warning disable CS0618 // DiscoveryClient.Create is obsolete but CreateAsync requires ITelemetryContext
        using var client = DiscoveryClient.Create(uri, endpointConfig);
#pragma warning restore CS0618

        EndpointDescriptionCollection endpoints;
        try
        {
            endpoints = await client.GetEndpointsAsync(null);
            _logger.Info($"Found {endpoints.Count} endpoints");
        }
        catch (Exception ex)
        {
            _logger.Error($"Endpoint discovery failed: {ex.GetType().Name} - {ex.Message}");
            if (ex.InnerException != null)
                _logger.Error($"  Inner: {ex.InnerException.Message}");
            throw;
        }

        // Select the best endpoint based on security preference
        EndpointDescription? selectedEndpoint = null;

        foreach (var endpoint in endpoints)
        {
            // Skip endpoints that don't match our security preference
            if (useSecurity)
            {
                if (endpoint.SecurityMode == MessageSecurityMode.None)
                    continue;
            }
            else
            {
                if (endpoint.SecurityMode != MessageSecurityMode.None)
                    continue;
            }

            // Prefer the first matching endpoint
            if (selectedEndpoint == null)
            {
                selectedEndpoint = endpoint;
            }
        }

        // If no endpoint matched our preference, try any endpoint
        if (selectedEndpoint == null && endpoints.Count > 0)
        {
            selectedEndpoint = endpoints[0];
        }

        if (selectedEndpoint == null)
        {
            throw new ServiceResultException(StatusCodes.BadNotConnected,
                $"No suitable endpoint found at {endpointUrl}");
        }

        // Update the endpoint URL to use the requested host if different
        // (handles cases where server returns localhost but we connected via IP/hostname)
        var selectedUri = new Uri(selectedEndpoint.EndpointUrl);
        if (selectedUri.Host != uri.Host)
        {
            var builder = new UriBuilder(selectedEndpoint.EndpointUrl)
            {
                Host = uri.Host
            };
            selectedEndpoint.EndpointUrl = builder.ToString();
        }

        return selectedEndpoint;
    }
}
