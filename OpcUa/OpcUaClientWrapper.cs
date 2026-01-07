using Opc.Ua;
using Opc.Ua.Client;
using Opcilloscope.Utilities;

namespace Opcilloscope.OpcUa;

/// <summary>
/// Wrapper around OPC Foundation Client Session providing async connection management.
/// Supports proper OPC UA reconnection with subscription preservation.
/// </summary>
public class OpcUaClientWrapper : IDisposable
{
    private ISession? _session;
    private readonly Logger _logger;
    private string? _currentEndpoint;
    private CancellationTokenSource? _reconnectCts;
    private bool _disposed;
    private ApplicationConfiguration? _appConfig;
    private ConfiguredEndpoint? _lastConfiguredEndpoint;

    public bool IsConnected => _session?.Connected ?? false;
    public string? CurrentEndpoint => _currentEndpoint;
    public ISession? Session => _session;

    /// <summary>
    /// Raised when connection is established (initial or after reconnect).
    /// </summary>
    public event Action? Connected;

    /// <summary>
    /// Raised when connection is lost or closed.
    /// </summary>
    public event Action? Disconnected;

    /// <summary>
    /// Raised when a connection error occurs.
    /// </summary>
    public event Action<string>? ConnectionError;

    /// <summary>
    /// Raised when keep-alive detects connection loss and automatic reconnection should be attempted.
    /// </summary>
    public event Action? ReconnectRequired;

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
            ApplicationName = "Opcilloscope",
            ApplicationType = ApplicationType.Client,
            ApplicationUri = "urn:opcilloscope:Client",
            ProductUri = "urn:opcilloscope",
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Opcilloscope", "pki", "own"),
                    SubjectName = "CN=Opcilloscope, O=Opcilloscope, DC=localhost"
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Opcilloscope", "pki", "issuer")
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Opcilloscope", "pki", "trusted")
                },
                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Opcilloscope", "pki", "rejected")
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

        _logger.Warning("Certificate validation disabled (AutoAcceptUntrustedCertificates=true). Not recommended for production.");

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
            _lastConfiguredEndpoint = endpoint;

#pragma warning disable CS0618 // Session.Create is obsolete but ISessionFactory.CreateAsync requires additional setup
            _session = await Opc.Ua.Client.Session.Create(
                config,
                endpoint,
                false,
                "Opcilloscope Session",
                60000,
                new UserIdentity(new AnonymousIdentityToken()),
                null
            );
#pragma warning restore CS0618

            // Configure session for subscription preservation during reconnection
            _session.DeleteSubscriptionsOnClose = false;
            _session.TransferSubscriptionsOnReconnect = true;

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
                _logger.Warning("Connection lost - reconnection required");
                ReconnectRequired?.Invoke();
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

    /// <summary>
    /// Attempts to reconnect preserving the existing session and subscriptions.
    /// Uses OPC UA Session.Reconnect first, then falls back to recreating the session
    /// with subscription transfer.
    /// </summary>
    /// <returns>True if reconnection succeeded, false otherwise.</returns>
    public async Task<bool> ReconnectAsync()
    {
        if (_session == null && string.IsNullOrEmpty(_currentEndpoint))
        {
            _logger.Warning("No session or endpoint to reconnect");
            return false;
        }

        _reconnectCts = new CancellationTokenSource();

        // Exponential backoff: 1s, 2s, 4s, 8s
        int[] delays = { 1000, 2000, 4000, 8000 };

        for (int attempt = 0; attempt < delays.Length; attempt++)
        {
            if (_reconnectCts.Token.IsCancellationRequested)
                return false;

            _logger.Info($"Reconnection attempt {attempt + 1}/{delays.Length}...");

            try
            {
                // Strategy 1: Try to reconnect the existing session (preserves subscriptions automatically)
                if (_session != null)
                {
                    var reconnectResult = await TrySessionReconnectAsync();
                    if (reconnectResult)
                    {
                        _logger.Info("Session reconnected successfully (subscriptions preserved)");
                        Connected?.Invoke();
                        return true;
                    }
                }

                // Strategy 2: Recreate session and transfer subscriptions
                var recreateResult = await TryRecreateSessionAsync();
                if (recreateResult)
                {
                    _logger.Info("Session recreated successfully (subscriptions transferred)");
                    Connected?.Invoke();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"Reconnection attempt {attempt + 1} failed: {ex.Message}");
            }

            // Wait before next attempt
            try
            {
                await Task.Delay(delays[attempt], _reconnectCts.Token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        _logger.Error("Reconnection failed after all attempts");
        Disconnected?.Invoke();
        return false;
    }

    /// <summary>
    /// Tries to reconnect the existing session using OPC UA Reconnect service.
    /// This preserves the session ID and all subscriptions automatically.
    /// </summary>
    private async Task<bool> TrySessionReconnectAsync()
    {
        if (_session == null)
            return false;

        try
        {
            _logger.Info("Attempting session reconnect...");
            await _session.ReconnectAsync(_reconnectCts?.Token ?? CancellationToken.None);
            return _session.Connected;
        }
        catch (ServiceResultException ex)
        {
            _logger.Warning($"Session reconnect failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Recreates the session and transfers existing subscriptions to it.
    /// Used when direct reconnect fails (e.g., session timed out on server).
    /// </summary>
    private async Task<bool> TryRecreateSessionAsync()
    {
        if (_lastConfiguredEndpoint == null || string.IsNullOrEmpty(_currentEndpoint))
            return false;

        try
        {
            _logger.Info("Recreating session...");

            var config = await GetApplicationConfigAsync();

            // Capture existing subscriptions before closing old session
            SubscriptionCollection? subscriptionsToTransfer = null;
            if (_session?.Subscriptions != null && _session.Subscriptions.Any())
            {
                subscriptionsToTransfer = new SubscriptionCollection(_session.Subscriptions);
                _logger.Info($"Captured {subscriptionsToTransfer.Count} subscription(s) for transfer");
            }

            // Clean up old session without deleting subscriptions on server
            if (_session != null)
            {
                _session.KeepAlive -= Session_KeepAlive;
                try
                {
                    // Dispose without CloseAsync() - this intentionally skips sending CloseSession
                    // to the server, allowing server-side subscriptions to remain active for transfer.
                    // With DeleteSubscriptionsOnClose=false, we want the subscriptions to persist
                    // on the server so we can transfer them to the new session.
                    _session.Dispose();
                }
                catch { /* Ignore cleanup errors during reconnection */ }
                _session = null;
            }

            // Rediscover endpoint in case server configuration changed
            var selectedEndpoint = await DiscoverAndSelectEndpointAsync(config, _currentEndpoint, useSecurity: false);
            var endpointConfig = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfig);
            _lastConfiguredEndpoint = endpoint;

            // Create new session
#pragma warning disable CS0618
            _session = await Opc.Ua.Client.Session.Create(
                config,
                endpoint,
                false,
                "Opcilloscope Session",
                60000,
                new UserIdentity(new AnonymousIdentityToken()),
                null
            );
#pragma warning restore CS0618

            _session.DeleteSubscriptionsOnClose = false;
            _session.TransferSubscriptionsOnReconnect = true;
            _session.KeepAlive += Session_KeepAlive;

            // Transfer subscriptions to new session
            if (subscriptionsToTransfer != null && subscriptionsToTransfer.Count > 0)
            {
                var transferred = await TransferSubscriptionsAsync(subscriptionsToTransfer);
                _logger.Info($"Transferred {transferred} of {subscriptionsToTransfer.Count} subscription(s)");
            }

            return _session.Connected;
        }
        catch (Exception ex)
        {
            _logger.Error($"Session recreation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Transfers subscriptions from a previous session to the current session.
    /// </summary>
    private async Task<int> TransferSubscriptionsAsync(SubscriptionCollection subscriptions)
    {
        if (_session == null || subscriptions == null)
            return 0;

        int transferred = 0;

        try
        {
            // Use the OPC UA TransferSubscriptions service
            var success = await _session.TransferSubscriptionsAsync(
                subscriptions,
                sendInitialValues: true,
                _reconnectCts?.Token ?? CancellationToken.None);

            if (success)
            {
                transferred = subscriptions.Count;
            }
            else
            {
                _logger.Warning("TransferSubscriptions returned false - subscriptions may need to be recreated");
            }
        }
        catch (ServiceResultException ex) when (ex.StatusCode == StatusCodes.BadNothingToDo)
        {
            // No subscriptions to transfer - this is OK
            _logger.Info("No subscriptions to transfer (BadNothingToDo)");
        }
        catch (ServiceResultException ex) when (ex.StatusCode == StatusCodes.BadSubscriptionIdInvalid)
        {
            // Server doesn't have these subscriptions anymore - they need to be recreated
            _logger.Warning("Server subscriptions expired - subscriptions will need to be recreated");
        }
        catch (Exception ex)
        {
            _logger.Warning($"Subscription transfer failed: {ex.Message}");
        }

        return transferred;
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
