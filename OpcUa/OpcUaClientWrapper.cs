using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
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
    private ConnectionCredentials _credentials = ConnectionCredentials.Anonymous;
    private readonly bool _allowInsecure;
    private string? _securityMode;
    private string? _securityPolicy;

    /// <summary>
    /// Process-wide default for whether untrusted server certificates are auto-accepted.
    /// Set once at startup from the <c>--insecure</c> CLI flag (see <c>Program.cs</c>).
    /// Wrapper instances created without an explicit <c>allowInsecure</c> argument inherit
    /// this value. Defaults to <c>false</c> (secure-by-default).
    /// </summary>
    public static bool AllowInsecureByDefault { get; set; }

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

    /// <summary>
    /// Creates a new client wrapper.
    /// </summary>
    /// <param name="logger">Optional logger.</param>
    /// <param name="allowInsecure">
    /// When <c>true</c>, untrusted server certificates are auto-accepted (development only).
    /// When <c>null</c> (the default), the value of <see cref="AllowInsecureByDefault"/> is used.
    /// </param>
    public OpcUaClientWrapper(Logger? logger = null, bool? allowInsecure = null)
    {
        _logger = logger ?? new Logger();
        _allowInsecure = allowInsecure ?? AllowInsecureByDefault;
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
                        "opcilloscope", "pki", "own"),
                    SubjectName = "CN=Opcilloscope, O=Opcilloscope, DC=localhost"
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "opcilloscope", "pki", "issuer")
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "opcilloscope", "pki", "trusted")
                },
                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "opcilloscope", "pki", "rejected")
                },
                // Secure-by-default: never blanket-accept. Acceptance is decided per
                // certificate by OnCertificateValidation, gated on the --insecure flag.
                AutoAcceptUntrustedCertificates = false,
                AddAppCertToTrustedStore = false
            },
            TransportConfigurations = new TransportConfigurationCollection(),
            TransportQuotas = new TransportQuotas { OperationTimeout = 30000 },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
        };

        await _appConfig.ValidateAsync(ApplicationType.Client);

        // Decide certificate acceptance per certificate rather than blanket-accepting.
        _appConfig.CertificateValidator.CertificateValidation += OnCertificateValidation;

        // Create client application certificate if it doesn't exist (needed for secure channels)
        var appInstance = new ApplicationInstance(_appConfig, null);
        var hasCertificate = await appInstance.CheckApplicationInstanceCertificatesAsync(silent: true);
        if (!hasCertificate)
        {
            _logger.Warning("Client certificate could not be created. Secure channel connections may fail.");
        }

        if (_allowInsecure)
        {
            _logger.Warning("Insecure mode enabled (--insecure): untrusted server certificates will be auto-accepted. Not recommended for production.");
        }
        else
        {
            _logger.Info("Certificate validation enabled. Untrusted server certificates will be rejected (re-run with --insecure to override).");
        }

        return _appConfig;
    }

    /// <summary>
    /// Decides whether to accept a server certificate that failed validation.
    /// Without <c>--insecure</c>, untrusted certificates are rejected with a clear,
    /// actionable log message. With <c>--insecure</c>, they are accepted (development only).
    /// </summary>
    private void OnCertificateValidation(CertificateValidator sender, CertificateValidationEventArgs e)
    {
        // Only intervene on validation failures; good certificates pass through untouched.
        if (ServiceResult.IsGood(e.Error))
            return;

        if (_allowInsecure)
        {
            _logger.Warning($"Accepting untrusted server certificate (--insecure): '{e.Certificate?.Subject}' [{e.Error.StatusCode}]");
            e.AcceptAll = true;
            e.Accept = true;
            return;
        }

        var trustedStorePath = _appConfig?.SecurityConfiguration?.TrustedPeerCertificates?.StorePath;
        _logger.Error(
            $"Server certificate rejected ({e.Error.StatusCode}): '{e.Certificate?.Subject}'. Connection refused. " +
            "Re-run with --insecure to accept untrusted certificates (development only), " +
            $"or add the trusted certificate to the PKI store at: {trustedStorePath}");
        e.Accept = false;
    }

    public async Task<bool> ConnectAsync(
        string endpointUrl,
        ConnectionCredentials? credentials = null,
        string? securityMode = null,
        string? securityPolicy = null)
    {
        try
        {
            Disconnect();

            _credentials = credentials ?? ConnectionCredentials.Anonymous;
            _securityMode = securityMode;
            _securityPolicy = securityPolicy;
            _logger.Info($"Connecting to {endpointUrl}...");

            var config = await GetApplicationConfigAsync();

            // Select the strongest endpoint matching the requested security settings.
            var selectedEndpoint = await DiscoverAndSelectEndpointAsync(config, endpointUrl, _securityMode, _securityPolicy);

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
                CreateUserIdentity(),
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
        catch (ServiceResultException sre) when (
            sre.StatusCode == StatusCodes.BadUserAccessDenied ||
            sre.StatusCode == StatusCodes.BadIdentityTokenInvalid ||
            sre.StatusCode == StatusCodes.BadIdentityTokenRejected)
        {
            var msg = sre.StatusCode == StatusCodes.BadUserAccessDenied
                ? "Authentication failed: invalid username or password."
                : "Authentication rejected by server: " + sre.Message;
            _logger.Error(msg);
            ConnectionError?.Invoke(msg);
            Disconnect();
            return false;
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
        // A bad keep-alive status indicates the connection is unhealthy. On a transient
        // TCP drop the SDK can still report session.Connected == true, so do NOT gate on
        // it - that previously prevented auto-reconnect from ever firing for network
        // drops. Re-entrancy / duplicate triggers are guarded in ConnectionManager.
        if (e.Status != null && ServiceResult.IsBad(e.Status))
        {
            _logger.Warning($"Keep alive error: {e.Status} - reconnection required");
            ReconnectRequired?.Invoke();
        }
    }

    /// <summary>
    /// Synchronously disconnects the current session. Kept for back-compat with
    /// existing synchronous callers; UI callers should prefer <see cref="DisconnectAsync"/>
    /// to avoid blocking the UI thread on the close round-trip.
    /// </summary>
    public void Disconnect()
    {
        DisposeReconnectCts();

        var session = _session;
        if (session != null)
        {
            _session = null;
            _currentEndpoint = null;
            try
            {
                session.KeepAlive -= Session_KeepAlive;
                session.CloseAsync().GetAwaiter().GetResult();
                session.Dispose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                // Session cleanup errors are expected during network issues
                _logger.Warning($"Session cleanup error (non-critical): {ex.Message}");
            }
            Disconnected?.Invoke();
        }
    }

    /// <summary>
    /// Cancels and disposes the reconnect cancellation token source, if any.
    /// </summary>
    private void DisposeReconnectCts()
    {
        if (_reconnectCts != null)
        {
            try { _reconnectCts.Cancel(); } catch (ObjectDisposedException) { }
            _reconnectCts.Dispose();
            _reconnectCts = null;
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

        DisposeReconnectCts();
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
                catch (Exception ex)
                {
                    _logger.Warning($"Session cleanup error during reconnection: {ex.Message}");
                }
                _session = null;
            }

            // Rediscover endpoint in case server configuration changed
            var selectedEndpoint = await DiscoverAndSelectEndpointAsync(config, _currentEndpoint, _securityMode, _securityPolicy);
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
                CreateUserIdentity(),
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

    /// <summary>
    /// Asynchronously disconnects the current session without blocking the calling thread
    /// on the OPC UA close round-trip. Preferred over <see cref="Disconnect"/> for UI callers.
    /// </summary>
    public async Task DisconnectAsync()
    {
        DisposeReconnectCts();

        var session = _session;
        if (session != null)
        {
            _session = null;
            _currentEndpoint = null;
            try
            {
                session.KeepAlive -= Session_KeepAlive;
                await session.CloseAsync().ConfigureAwait(false);
                session.Dispose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                // Session cleanup errors are expected during network issues
                _logger.Warning($"Session cleanup error (non-critical): {ex.Message}");
            }
            Disconnected?.Invoke();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            try
            {
                // Bounded synchronous wait to avoid deadlocks when disposed from a
                // synchronization context (mirrors SubscriptionManager.Dispose).
                Task.Run(async () => await DisconnectAsync()).Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException ex)
            {
                _logger.Warning($"Disposal warning: {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.Warning($"Disposal warning: {ex.Message}");
            }
        }
    }

    private UserIdentity CreateUserIdentity()
    {
        if (_credentials.Type == AuthenticationType.UserName)
        {
            return new UserIdentity(
                _credentials.Username,
                System.Text.Encoding.UTF8.GetBytes(_credentials.Password ?? string.Empty));
        }

        return new UserIdentity();
    }

    private async Task<EndpointDescription> DiscoverAndSelectEndpointAsync(
        ApplicationConfiguration config,
        string endpointUrl,
        string? securityMode,
        string? securityPolicy)
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

        if (endpoints.Count == 0)
        {
            throw new ServiceResultException(StatusCodes.BadNotConnected,
                $"No endpoints offered by server at {endpointUrl}");
        }

        // Security is requested either by an explicit, non-"None" SecurityMode, or whenever
        // credentials are supplied: username/password tokens must never be sent over an
        // unencrypted channel. SelectEndpoint falls back to a None endpoint only if the server
        // offers no secure endpoint, so this never hard-fails a None-only server.
        bool securityRequested = !string.IsNullOrEmpty(securityMode)
            && !string.Equals(securityMode, nameof(MessageSecurityMode.None), StringComparison.OrdinalIgnoreCase);
        bool useSecurity = securityRequested || _credentials.Type != AuthenticationType.Anonymous;

        // If a specific SecurityMode/SecurityPolicy was requested, honor it by narrowing the
        // candidate set to exact matches; fall back to all endpoints if none match.
        var candidates = endpoints;
        if (useSecurity && (!string.IsNullOrEmpty(securityMode) || !string.IsNullOrEmpty(securityPolicy)))
        {
            var matches = endpoints
                .Where(ep => MatchesSecurityMode(ep, securityMode) && MatchesSecurityPolicy(ep, securityPolicy))
                .ToList();

            if (matches.Count > 0)
            {
                candidates = new EndpointDescriptionCollection(matches);
            }
            else
            {
                _logger.Warning(
                    $"No endpoint matched requested SecurityMode='{securityMode}' / SecurityPolicy='{securityPolicy}'. " +
                    "Selecting the strongest available endpoint instead.");
            }
        }

        // CoreClientUtils.SelectEndpoint sorts by SecurityLevel and returns the strongest
        // endpoint matching the security preference (instead of the first match).
        // telemetry context is optional; the SDK tolerates a null context.
        var selectedEndpoint = CoreClientUtils.SelectEndpoint(config, uri, candidates, useSecurity, null!);

        if (selectedEndpoint == null)
        {
            throw new ServiceResultException(StatusCodes.BadNotConnected,
                $"No suitable endpoint found at {endpointUrl}");
        }

        _logger.Info($"Selected endpoint: {selectedEndpoint.SecurityMode} / {selectedEndpoint.SecurityPolicyUri}");

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

    private static bool MatchesSecurityMode(EndpointDescription endpoint, string? securityMode)
        => string.IsNullOrEmpty(securityMode)
           || string.Equals(endpoint.SecurityMode.ToString(), securityMode, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesSecurityPolicy(EndpointDescription endpoint, string? securityPolicy)
        => string.IsNullOrEmpty(securityPolicy)
           || string.Equals(endpoint.SecurityPolicyUri, securityPolicy, StringComparison.OrdinalIgnoreCase)
           || endpoint.SecurityPolicyUri?.EndsWith("#" + securityPolicy, StringComparison.OrdinalIgnoreCase) == true
           || endpoint.SecurityPolicyUri?.EndsWith("/" + securityPolicy, StringComparison.OrdinalIgnoreCase) == true;
}
