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
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private CancellationTokenSource? _reconnectCts;
    private readonly object _reconnectCtsLock = new();
    private int _disposed;
    private ApplicationConfiguration? _appConfig;
    private ConfiguredEndpoint? _lastConfiguredEndpoint;
    private ConnectionCredentials _credentials = ConnectionCredentials.Anonymous;
    private readonly bool _allowInsecure;
    private readonly string _pkiRootPath;
    private string? _securityMode;
    private string? _securityPolicy;
    private int _currentSecurityMode = -1;
    private string? _currentSecurityPolicy;

    /// <summary>
    /// Process-wide default for whether server certificate validation failures are accepted.
    /// Set once at startup from the <c>--insecure</c> CLI flag (see <c>Program.cs</c>).
    /// Wrapper instances created without an explicit <c>allowInsecure</c> argument inherit
    /// this value. Defaults to <c>false</c> (secure-by-default).
    /// </summary>
    public static bool AllowInsecureByDefault { get; set; }

    // Assembly-wide test seam so integration tests never create certificates in
    // the interactive user's real PKI store. Production code leaves this null.
    internal static string? PkiRootPathOverrideForTests { get; set; }

    public bool IsConnected => _session?.Connected ?? false;
    public string? CurrentEndpoint => _currentEndpoint;
    public ISession? Session => _session;

    /// <summary>
    /// Gets the message security mode of the endpoint used by the active session.
    /// This is the actual discovered selection, not merely the requested mode.
    /// </summary>
    public MessageSecurityMode? CurrentSecurityMode
    {
        get
        {
            var value = Volatile.Read(ref _currentSecurityMode);
            return value < 0 ? null : (MessageSecurityMode)value;
        }
    }

    /// <summary>
    /// Gets the full security-policy URI of the endpoint used by the active session.
    /// This is the actual discovered selection, not merely the requested policy.
    /// </summary>
    public string? CurrentSecurityPolicy => Volatile.Read(ref _currentSecurityPolicy);

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
    /// When <c>true</c>, server certificate validation failures are accepted (development only).
    /// When <c>null</c> (the default), the value of <see cref="AllowInsecureByDefault"/> is used.
    /// </param>
    public OpcUaClientWrapper(Logger? logger = null, bool? allowInsecure = null)
        : this(logger, allowInsecure, GetDefaultPkiRootPath())
    {
    }

    // Test seam for keeping generated certificates and trust decisions out of the
    // interactive user's real PKI directories.
    internal OpcUaClientWrapper(Logger? logger, bool? allowInsecure, string pkiRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pkiRootPath);

        _logger = logger ?? new Logger();
        _allowInsecure = allowInsecure ?? AllowInsecureByDefault;
        _pkiRootPath = Path.GetFullPath(pkiRootPath);
    }

    private static string GetDefaultPkiRootPath()
        => PkiRootPathOverrideForTests is { Length: > 0 } overridePath
            ? Path.GetFullPath(overridePath)
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "opcilloscope",
                "pki");

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
                    StorePath = Path.Combine(_pkiRootPath, "own"),
                    SubjectName = "CN=Opcilloscope, O=Opcilloscope, DC=localhost"
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(_pkiRootPath, "issuer")
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(_pkiRootPath, "trusted")
                },
                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(_pkiRootPath, "rejected")
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
            _logger.Warning("Insecure mode enabled (--insecure): server certificate validation is disabled. Not recommended for production.");
        }
        else
        {
            _logger.Info("Certificate validation enabled. Invalid server certificates will be rejected (re-run with --insecure to override).");
        }

        return _appConfig;
    }

    /// <summary>
    /// Decides whether to accept a server certificate that failed validation.
    /// Without <c>--insecure</c>, certificate validation failures are rejected with a clear,
    /// actionable log message. With <c>--insecure</c>, they are accepted (development only).
    /// </summary>
    private void OnCertificateValidation(CertificateValidator sender, CertificateValidationEventArgs e)
    {
        // Only intervene on validation failures; good certificates pass through untouched.
        if (ServiceResult.IsGood(e.Error))
            return;

        if (_allowInsecure)
        {
            _logger.Warning($"Bypassing server certificate validation (--insecure): '{e.Certificate?.Subject}' [{e.Error.StatusCode}]");
            e.AcceptAll = true;
            e.Accept = true;
            return;
        }

        var trustedStorePath = _appConfig?.SecurityConfiguration?.TrustedPeerCertificates?.StorePath;
        _logger.Error(
            $"Server certificate rejected ({e.Error.StatusCode}): '{e.Certificate?.Subject}'. Connection refused. " +
            "Re-run with --insecure to bypass server certificate validation (development only), " +
            $"or add the trusted certificate to the PKI store at: {trustedStorePath}");
        e.Accept = false;
    }

    public Task<bool> ConnectAsync(
        string endpointUrl,
        ConnectionCredentials? credentials = null,
        string? securityMode = null,
        string? securityPolicy = null)
    {
        CancelPendingReconnect();
        return ExecuteLifecycleAsync(
            () => ConnectCoreAsync(endpointUrl, credentials, securityMode, securityPolicy));
    }

    /// <summary>
    /// Runs a complete lifecycle transaction under the wrapper's single async gate.
    /// ConnectionManager uses this same gate so session and subscription state change
    /// as one serialized operation rather than through two independently locked layers.
    /// </summary>
    internal async Task<T> ExecuteLifecycleAsync<T>(Func<Task<T>> operation)
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    internal async Task ExecuteLifecycleAsync(Func<Task> operation)
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await operation().ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Runs a complete application operation against one pinned session while holding
    /// the lifecycle gate. Connection changes therefore wait for the operation, and an
    /// operation queued behind a connection change cannot accidentally capture the old
    /// session and continue through the new one.
    /// </summary>
    internal Task<T> ExecuteSessionOperationAsync<T>(Func<ISession, Task<T>> operation)
        => ExecuteLifecycleAsync(async () =>
        {
            var session = _session;
            if (session == null || !session.Connected)
                throw new InvalidOperationException("Not connected");

            return await operation(session).ConfigureAwait(false);
        });

    /// <summary>
    /// Connect implementation for callers that already own <see cref="_lifecycleGate"/>.
    /// Never call a public lifecycle method from this method: doing so would wait on
    /// the non-reentrant gate and deadlock.
    /// </summary>
    internal async Task<bool> ConnectCoreAsync(
        string endpointUrl,
        ConnectionCredentials? credentials = null,
        string? securityMode = null,
        string? securityPolicy = null)
    {
        try
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(OpcUaClientWrapper));

            await DisconnectCoreAsync().ConfigureAwait(false);

            _credentials = credentials ?? ConnectionCredentials.Anonymous;
            _credentials.Validate();
            _securityMode = NormalizeSecuritySetting(securityMode);
            _securityPolicy = NormalizeSecuritySetting(securityPolicy);
            _logger.Info($"Connecting to {endpointUrl}...");

            var config = await GetApplicationConfigAsync().ConfigureAwait(false);

            // Select the strongest endpoint matching the requested security settings.
            var selectedEndpoint = await DiscoverAndSelectEndpointAsync(
                config,
                endpointUrl,
                _securityMode,
                _securityPolicy).ConfigureAwait(false);

            // Create session
            var endpointConfig = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfig);

#pragma warning disable CS0618 // Session.Create is obsolete but ISessionFactory.CreateAsync requires additional setup
            var newSession = await Opc.Ua.Client.Session.Create(
                config,
                endpoint,
                false,
                "Opcilloscope Session",
                60000,
                CreateUserIdentity(),
                null).ConfigureAwait(false);
#pragma warning restore CS0618

            var published = false;
            try
            {
                // Configure session for subscription preservation during reconnection.
                newSession.DeleteSubscriptionsOnClose = false;
                newSession.TransferSubscriptionsOnReconnect = true;
                newSession.KeepAlive += Session_KeepAlive;

                _session = newSession;
                published = true;
                _lastConfiguredEndpoint = endpoint;
                _currentEndpoint = endpointUrl;
                SetCurrentSecurityProfile(selectedEndpoint);
            }
            catch
            {
                // Session.Create succeeded, so this local session must be retired even
                // if subsequent setup fails before ownership transfers to _session.
                if (!published)
                {
                    try { newSession.Dispose(); }
                    catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
                    {
                        _logger.Warning($"Session cleanup error after failed setup: {ex.Message}");
                    }
                }
                throw;
            }

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
            await DisconnectCoreAsync().ConfigureAwait(false);
            ConnectionError?.Invoke(msg);
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"Connection failed: {ex.Message}");
            await DisconnectCoreAsync().ConfigureAwait(false);
            ConnectionError?.Invoke(ex.Message);
            return false;
        }
    }

    private void Session_KeepAlive(ISession session, KeepAliveEventArgs e)
    {
        // Keep-alive callbacks can already be queued when a session is detached.
        // Never let stale session A mark a newly installed session B unhealthy.
        if (!IsCurrentSessionCallback(session))
            return;

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

    internal bool IsCurrentSessionCallback(ISession session)
        => Volatile.Read(ref _disposed) == 0 && ReferenceEquals(session, _session);

    /// <summary>
    /// Synchronously disconnects the current session. Kept for back-compat with
    /// existing synchronous callers; UI callers should prefer <see cref="DisconnectAsync"/>
    /// to avoid blocking the UI thread on the close round-trip.
    /// </summary>
    public void Disconnect()
    {
        CancelPendingReconnect();
        DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Cancels an in-flight reconnect before a connect or disconnect waits for the
    /// lifecycle gate. The reconnect operation remains the sole owner responsible
    /// for disposing its token source.
    /// </summary>
    internal void CancelPendingReconnect()
    {
        CancellationTokenSource? cts;
        lock (_reconnectCtsLock)
        {
            cts = _reconnectCts;
        }

        if (cts != null)
        {
            try { cts.Cancel(); } catch (ObjectDisposedException) { }
        }
    }

    private void CompleteReconnect(CancellationTokenSource cts)
    {
        lock (_reconnectCtsLock)
        {
            if (ReferenceEquals(_reconnectCts, cts))
                _reconnectCts = null;
        }

        cts.Dispose();
    }

    /// <summary>
    /// Attempts to reconnect preserving the existing session and subscriptions.
    /// Uses OPC UA Session.Reconnect first, then falls back to recreating the session
    /// with subscription transfer.
    /// </summary>
    /// <returns>True if reconnection succeeded, false otherwise.</returns>
    public Task<bool> ReconnectAsync() => ExecuteLifecycleAsync(() => ReconnectCoreAsync());

    /// <summary>
    /// Reconnect implementation for callers that already own the lifecycle gate.
    /// </summary>
    internal async Task<bool> ReconnectCoreAsync(
        CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return false;

        if (_session == null && string.IsNullOrEmpty(_currentEndpoint))
        {
            _logger.Warning("No session or endpoint to reconnect");
            return false;
        }

        var cts = cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : new CancellationTokenSource();
        lock (_reconnectCtsLock)
        {
            _reconnectCts = cts;
        }
        var token = cts.Token;

        try
        {
            // Exponential backoff: 1s, 2s, 4s, 8s
            int[] delays = { 1000, 2000, 4000, 8000 };

            for (int attempt = 0; attempt < delays.Length; attempt++)
            {
                if (token.IsCancellationRequested)
                    return false;

                _logger.Info($"Reconnection attempt {attempt + 1}/{delays.Length}...");

                try
                {
                    // Strategy 1: Try to reconnect the existing session (preserves subscriptions automatically)
                    if (_session != null)
                    {
                        var reconnectResult = await TrySessionReconnectAsync(token).ConfigureAwait(false);
                        if (reconnectResult && !token.IsCancellationRequested)
                        {
                            _logger.Info("Session reconnected successfully (subscriptions preserved)");
                            Connected?.Invoke();
                            return true;
                        }
                    }

                    if (token.IsCancellationRequested)
                        return false;

                    // Strategy 2: Recreate session and transfer subscriptions
                    var recreateResult = await TryRecreateSessionAsync(token).ConfigureAwait(false);
                    if (recreateResult)
                    {
                        if (token.IsCancellationRequested)
                        {
                            // The user disconnected while the session was being recreated;
                            // don't resurrect a connection they asked to close. Call the core
                            // method because this operation already owns the lifecycle gate.
                            _logger.Info("Reconnect cancelled after session recreation - closing the new session");
                            await DisconnectCoreAsync().ConfigureAwait(false);
                            return false;
                        }

                        _logger.Info("Session recreated successfully (subscriptions transferred)");
                        Connected?.Invoke();
                        return true;
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Reconnection attempt {attempt + 1} failed: {ex.Message}");
                }

                // Wait before next attempt
                try
                {
                    await Task.Delay(delays[attempt], token).ConfigureAwait(false);
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
        finally
        {
            CompleteReconnect(cts);
        }
    }

    /// <summary>
    /// Tries to reconnect the existing session using OPC UA Reconnect service.
    /// This preserves the session ID and all subscriptions automatically.
    /// </summary>
    private async Task<bool> TrySessionReconnectAsync(CancellationToken cancellationToken)
    {
        var session = _session;
        if (session == null)
            return false;

        try
        {
            _logger.Info("Attempting session reconnect...");
            await session.ReconnectAsync(cancellationToken).ConfigureAwait(false);
            return ReferenceEquals(session, _session) && session.Connected;
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
    private async Task<bool> TryRecreateSessionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var endpointUrl = _currentEndpoint;
        if (_lastConfiguredEndpoint == null || string.IsNullOrEmpty(endpointUrl))
            return false;

        var oldSession = _session;
        ISession? newSession = null;
        var installed = false;

        try
        {
            _logger.Info("Recreating session...");

            var config = await GetApplicationConfigAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            // Capture existing subscriptions before replacing the session
            SubscriptionCollection? subscriptionsToTransfer = null;
            if (oldSession?.Subscriptions != null && oldSession.Subscriptions.Any())
            {
                subscriptionsToTransfer = new SubscriptionCollection(oldSession.Subscriptions);
                _logger.Info($"Captured {subscriptionsToTransfer.Count} subscription(s) for transfer");
            }

            // Rediscover endpoint in case server configuration changed
            var selectedEndpoint = await DiscoverAndSelectEndpointAsync(
                config,
                endpointUrl,
                _securityMode,
                _securityPolicy).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var endpointConfig = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfig);
            cancellationToken.ThrowIfCancellationRequested();

            // Create the new session before touching the old one, so a failure here
            // leaves the existing state unchanged for the next retry attempt.
#pragma warning disable CS0618
            newSession = await Opc.Ua.Client.Session.Create(
                config,
                endpoint,
                false,
                "Opcilloscope Session",
                60000,
                CreateUserIdentity(),
                null).ConfigureAwait(false);
#pragma warning restore CS0618

            cancellationToken.ThrowIfCancellationRequested();
            newSession.DeleteSubscriptionsOnClose = false;
            newSession.TransferSubscriptionsOnReconnect = true;
            newSession.KeepAlive += Session_KeepAlive;
            _session = newSession;
            installed = true;
            _lastConfiguredEndpoint = endpoint;
            SetCurrentSecurityProfile(selectedEndpoint);

            // Transfer subscriptions while the old session is still alive. The client-side
            // half of TransferSubscriptionsAsync detaches each subscription from its previous
            // session; if that session is already disposed this throws, the SDK swallows it
            // and reports failure - after the server-side transfer already succeeded - leaving
            // orphaned subscriptions on the server and forcing a duplicate recreate.
            if (subscriptionsToTransfer != null && subscriptionsToTransfer.Count > 0)
            {
                var transferred = await TransferSubscriptionsAsync(
                    subscriptionsToTransfer,
                    cancellationToken).ConfigureAwait(false);
                _logger.Info($"Transferred {transferred} of {subscriptionsToTransfer.Count} subscription(s)");
            }

            return newSession.Connected;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"Session recreation failed: {ex.Message}");
            return false;
        }
        finally
        {
            if (installed)
            {
                // Once the new session is installed, always retire the old local
                // session even if transfer or status inspection throws. Do not send
                // CloseSession here: it could delete server-side subscriptions that
                // were just transferred.
                if (oldSession != null && !ReferenceEquals(oldSession, newSession))
                    DisposeSessionWithoutClose(oldSession, "during reconnection");
            }
            else if (newSession != null)
            {
                // Session.Create completed but ownership was never published.
                DisposeSessionWithoutClose(newSession, "after failed reconnection setup");
            }
        }
    }

    /// <summary>
    /// Transfers subscriptions from a previous session to the current session.
    /// </summary>
    private async Task<int> TransferSubscriptionsAsync(SubscriptionCollection subscriptions, CancellationToken cancellationToken)
    {
        var session = _session;
        if (session == null || subscriptions == null)
            return 0;

        int transferred = 0;

        try
        {
            // Use the OPC UA TransferSubscriptions service
            var success = await session.TransferSubscriptionsAsync(
                subscriptions,
                sendInitialValues: true,
                cancellationToken).ConfigureAwait(false);

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

    public Task<ReferenceDescriptionCollection> BrowseAsync(NodeId nodeId)
        => ExecuteSessionOperationAsync(session => BrowseCoreAsync(session, nodeId));

    internal static async Task<ReferenceDescriptionCollection> BrowseCoreAsync(
        ISession session,
        NodeId nodeId)
    {
        var browser = new Browser(session)
        {
            BrowseDirection = BrowseDirection.Forward,
            // A zero mask requests all node classes. Restricting this to Object,
            // Variable, and Method hid ObjectType, VariableType, ReferenceType,
            // DataType, and View nodes from standard hierarchy browsing.
            NodeClassMask = 0,
            ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
            IncludeSubtypes = true,
            ResultMask = (uint)BrowseResultMask.All
        };

        return await browser.BrowseAsync(nodeId).ConfigureAwait(false);
    }

    public Task<DataValue?> ReadValueAsync(NodeId nodeId)
        => ExecuteSessionOperationAsync(session => ReadValueCoreAsync(session, nodeId));

    internal static async Task<DataValue?> ReadValueCoreAsync(ISession session, NodeId nodeId)
        => await session.ReadValueAsync(nodeId).ConfigureAwait(false);

    public Task<DataValueCollection> ReadAttributesAsync(NodeId nodeId, params uint[] attributeIds)
        => ExecuteSessionOperationAsync(
            session => ReadAttributesCoreAsync(session, nodeId, attributeIds));

    internal static async Task<DataValueCollection> ReadAttributesCoreAsync(
        ISession session,
        NodeId nodeId,
        params uint[] attributeIds)
    {
        var nodesToRead = new ReadValueIdCollection();
        foreach (var attrId in attributeIds)
        {
            nodesToRead.Add(new ReadValueId
            {
                NodeId = nodeId,
                AttributeId = attrId
            });
        }

        var response = await session.ReadAsync(
            null,
            0,
            TimestampsToReturn.Both,
            nodesToRead,
            CancellationToken.None
        );

        return response.Results;
    }

    public Task<StatusCode> WriteValueAsync(NodeId nodeId, object value)
        => ExecuteSessionOperationAsync(
            session => WriteValueCoreAsync(session, nodeId, value));

    internal static async Task<StatusCode> WriteValueCoreAsync(
        ISession session,
        NodeId nodeId,
        object value)
    {
        var nodesToWrite = new WriteValueCollection
        {
            new WriteValue
            {
                NodeId = nodeId,
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(value))
            }
        };

        var response = await session.WriteAsync(
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
    public Task DisconnectAsync()
    {
        CancelPendingReconnect();
        return ExecuteLifecycleAsync(DisconnectCoreAsync);
    }

    /// <summary>
    /// Disconnect implementation for callers that already own the lifecycle gate.
    /// The session is detached before any fallible cleanup, and Dispose always runs
    /// even when event removal or CloseAsync throws.
    /// </summary>
    internal async Task DisconnectCoreAsync()
    {
        var session = _session;
        _session = null;
        _currentEndpoint = null;
        ClearCurrentSecurityProfile();

        if (session == null)
            return;

        try
        {
            try
            {
                session.KeepAlive -= Session_KeepAlive;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                _logger.Warning($"Session event cleanup error (non-critical): {ex.Message}");
            }

            await session.CloseAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Session close errors are expected during network issues.
            _logger.Warning($"Session close error (non-critical): {ex.Message}");
        }
        finally
        {
            try
            {
                session.Dispose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                _logger.Warning($"Session dispose error (non-critical): {ex.Message}");
            }
        }

        Disconnected?.Invoke();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        CancelPendingReconnect();
        try
        {
            // Bounded synchronous wait to avoid deadlocks when disposed from a
            // synchronization context (mirrors SubscriptionManager.Dispose). Use
            // the core method because the disposed flag intentionally blocks connect.
            Task.Run(() => ExecuteLifecycleAsync(DisconnectCoreAsync))
                .Wait(TimeSpan.FromSeconds(5));
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

    private void DisposeSessionWithoutClose(ISession session, string context)
    {
        try
        {
            session.KeepAlive -= Session_KeepAlive;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            _logger.Warning($"Session event cleanup error {context}: {ex.Message}");
        }
        finally
        {
            try
            {
                session.Dispose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                _logger.Warning($"Session dispose error {context}: {ex.Message}");
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
        var hasRequestedMode = !string.IsNullOrEmpty(securityMode);

        if (_credentials.Type == AuthenticationType.UserName
            && hasRequestedMode
            && string.Equals(
                securityMode,
                nameof(MessageSecurityMode.None),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Username credentials require an encrypted or signed OPC UA endpoint; " +
                "SecurityMode=None is not permitted. The --insecure option only controls " +
                "certificate trust and never permits plaintext credentials.");
        }

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
            endpoints = await client.GetEndpointsAsync(null).ConfigureAwait(false);
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

        var (candidates, useSecurity) = FilterEndpointCandidates(
            endpoints,
            _credentials.Type,
            securityMode,
            securityPolicy,
            endpointUrl);

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

        if (_credentials.Type != AuthenticationType.Anonymous
            && selectedEndpoint.SecurityMode == MessageSecurityMode.None)
        {
            throw new ServiceResultException(
                StatusCodes.BadSecurityChecksFailed,
                "Endpoint selection refused SecurityMode=None for username credentials.");
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

    internal static (EndpointDescriptionCollection Candidates, bool UseSecurity) FilterEndpointCandidates(
        EndpointDescriptionCollection endpoints,
        AuthenticationType authenticationType,
        string? securityMode,
        string? securityPolicy,
        string endpointUrl)
    {
        var hasRequestedMode = !string.IsNullOrEmpty(securityMode);
        var hasRequestedPolicy = !string.IsNullOrEmpty(securityPolicy);
        var hasExplicitSecurityProfile = hasRequestedMode || hasRequestedPolicy;
        var explicitlyAllowsNone = hasRequestedMode
            && string.Equals(
                securityMode,
                nameof(MessageSecurityMode.None),
                StringComparison.OrdinalIgnoreCase);
        var explicitlyAllowsSignOnly = hasRequestedMode
            && string.Equals(
                securityMode,
                nameof(MessageSecurityMode.Sign),
                StringComparison.OrdinalIgnoreCase);

        if (hasRequestedPolicy
            && IsNoneSecurityPolicy(securityPolicy!)
            && !explicitlyAllowsNone)
        {
            throw new ServiceResultException(
                StatusCodes.BadSecurityChecksFailed,
                "SecurityPolicy=None does not opt into plaintext transport by itself; " +
                "set SecurityMode=None explicitly as well.");
        }

        // An explicit mode or policy is a contract, not a preference. Narrow to exact
        // matches and fail closed instead of silently choosing a different profile.
        var candidates = endpoints;
        if (hasExplicitSecurityProfile)
        {
            var matches = endpoints
                .Where(ep => MatchesSecurityMode(ep, securityMode) && MatchesSecurityPolicy(ep, securityPolicy))
                .ToList();

            if (matches.Count == 0)
            {
                throw new ServiceResultException(
                    StatusCodes.BadSecurityChecksFailed,
                    $"No endpoint matched requested SecurityMode='{securityMode ?? "<any>"}' / " +
                    $"SecurityPolicy='{securityPolicy ?? "<any>"}' at {endpointUrl}.");
            }

            candidates = new EndpointDescriptionCollection(matches);
        }

        // Unless the mode explicitly opts into Sign-only or None, require encryption.
        // This prevents an omitted/partial profile from silently exposing browse,
        // read, or write payloads on a Sign-only channel. UserName credentials always
        // require at least signing, even if a caller requests None.
        var requireEncryption = !explicitlyAllowsSignOnly && !explicitlyAllowsNone;
        var requireSecuredEndpoint = authenticationType == AuthenticationType.UserName
            || !explicitlyAllowsNone;
        if (requireEncryption)
        {
            var encryptedCandidates = candidates
                .Where(ep => ep.SecurityMode == MessageSecurityMode.SignAndEncrypt)
                .ToList();

            if (encryptedCandidates.Count == 0)
            {
                throw new ServiceResultException(
                    StatusCodes.BadSecurityChecksFailed,
                    "The server offered no SignAndEncrypt OPC UA endpoint. " +
                    "To deliberately allow signed-but-unencrypted traffic, set SecurityMode=Sign; " +
                    "for unauthenticated plaintext, set SecurityMode=None with anonymous authentication.");
            }

            candidates = new EndpointDescriptionCollection(encryptedCandidates);
        }
        else if (requireSecuredEndpoint)
        {
            var signedCandidates = candidates
                .Where(ep => ep.SecurityMode is MessageSecurityMode.Sign or MessageSecurityMode.SignAndEncrypt)
                .ToList();
            if (signedCandidates.Count == 0)
            {
                throw new ServiceResultException(
                    StatusCodes.BadSecurityChecksFailed,
                    "Username credentials require an encrypted or signed OPC UA endpoint.");
            }

            candidates = new EndpointDescriptionCollection(signedCandidates);
        }

        // Endpoint security and user-token security are separate OPC UA contracts.
        // Keep only endpoints that advertise the requested identity type, and only
        // expose compatible policies to Session.Create so it cannot select an
        // incompatible first policy from a mixed collection. In particular, a
        // username token with SecurityPolicy=None is invalid on a Sign-only channel:
        // the password would not be encrypted.
        var identityCandidates = new EndpointDescriptionCollection();
        foreach (var endpoint in candidates)
        {
            var compatiblePolicies = new UserTokenPolicyCollection();
            if (endpoint.UserIdentityTokens != null)
            {
                foreach (var policy in endpoint.UserIdentityTokens)
                {
                    if (IsCompatibleUserTokenPolicy(endpoint, policy, authenticationType))
                        compatiblePolicies.Add((UserTokenPolicy)policy.Clone());
                }
            }

            if (compatiblePolicies.Count == 0)
                continue;

            var compatibleEndpoint = (EndpointDescription)endpoint.Clone();
            compatibleEndpoint.UserIdentityTokens = compatiblePolicies;
            identityCandidates.Add(compatibleEndpoint);
        }

        if (identityCandidates.Count == 0)
        {
            var identityType = authenticationType == AuthenticationType.UserName
                ? nameof(AuthenticationType.UserName)
                : nameof(AuthenticationType.Anonymous);
            var userNameSecurityRequirement = authenticationType == AuthenticationType.UserName
                ? " UserName policies with SecurityPolicy=None are compatible only with " +
                  "SignAndEncrypt endpoints; Sign endpoints require an encrypted user-token policy."
                : string.Empty;

            throw new ServiceResultException(
                StatusCodes.BadIdentityTokenRejected,
                $"No endpoint at {endpointUrl} offered a compatible {identityType} user-token policy." +
                userNameSecurityRequirement);
        }

        candidates = identityCandidates;

        var requestedSecureMode = hasRequestedMode
            && !string.Equals(
                securityMode,
                nameof(MessageSecurityMode.None),
                StringComparison.OrdinalIgnoreCase);
        var requestedSecurePolicy = hasRequestedPolicy && !IsNoneSecurityPolicy(securityPolicy!);
        var useSecurity = requireSecuredEndpoint || requestedSecureMode || requestedSecurePolicy;
        return (candidates, useSecurity);
    }

    private static bool MatchesSecurityMode(EndpointDescription endpoint, string? securityMode)
        => string.IsNullOrEmpty(securityMode)
           || string.Equals(endpoint.SecurityMode.ToString(), securityMode, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesSecurityPolicy(EndpointDescription endpoint, string? securityPolicy)
        => string.IsNullOrEmpty(securityPolicy)
           || string.Equals(endpoint.SecurityPolicyUri, securityPolicy, StringComparison.OrdinalIgnoreCase)
           || endpoint.SecurityPolicyUri?.EndsWith("#" + securityPolicy, StringComparison.OrdinalIgnoreCase) == true
           || endpoint.SecurityPolicyUri?.EndsWith("/" + securityPolicy, StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsCompatibleUserTokenPolicy(
        EndpointDescription endpoint,
        UserTokenPolicy policy,
        AuthenticationType authenticationType)
    {
        var requiredTokenType = authenticationType switch
        {
            AuthenticationType.Anonymous => UserTokenType.Anonymous,
            AuthenticationType.UserName => UserTokenType.UserName,
            _ => throw new ArgumentOutOfRangeException(
                nameof(authenticationType),
                authenticationType,
                "Unsupported OPC UA authentication type.")
        };

        if (policy.TokenType != requiredTokenType)
            return false;

        if (authenticationType == AuthenticationType.Anonymous)
            return true;

        // A null/empty user-token policy inherits the endpoint SecurityPolicy.
        // A Username/None token is still confidential on SignAndEncrypt because
        // the SecureChannel encrypts the ActivateSession request. It is invalid on
        // Sign, where the password would otherwise be transmitted in clear text.
        var tokenSecurityPolicy = string.IsNullOrWhiteSpace(policy.SecurityPolicyUri)
            ? endpoint.SecurityPolicyUri
            : policy.SecurityPolicyUri;
        if (string.IsNullOrWhiteSpace(tokenSecurityPolicy))
            return false;

        // Discovery data must use the canonical URI because the SDK later uses
        // this value verbatim to choose encryption algorithms. Do not accept the
        // shorthand forms allowed for human-entered configuration.
        if (string.Equals(tokenSecurityPolicy, SecurityPolicies.None, StringComparison.Ordinal))
        {
            return endpoint.SecurityMode == MessageSecurityMode.SignAndEncrypt
                && !string.IsNullOrWhiteSpace(endpoint.SecurityPolicyUri)
                && !string.Equals(
                    endpoint.SecurityPolicyUri,
                    SecurityPolicies.None,
                    StringComparison.Ordinal);
        }

        // Non-None user-token encryption requires the server certificate carried
        // by the EndpointDescription. Full trust and algorithm compatibility are
        // validated by the OPC UA stack during session creation; reject a missing
        // certificate here before selecting an unusable endpoint.
        return SecurityPolicies.IsValidSecurityPolicyUri(tokenSecurityPolicy)
            && endpoint.ServerCertificate is { Length: > 0 };
    }

    private static bool IsNoneSecurityPolicy(string securityPolicy)
        => string.Equals(securityPolicy, SecurityPolicies.None, StringComparison.OrdinalIgnoreCase)
           || string.Equals(securityPolicy, "None", StringComparison.OrdinalIgnoreCase)
           || securityPolicy.EndsWith("#None", StringComparison.OrdinalIgnoreCase)
           || securityPolicy.EndsWith("/None", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeSecuritySetting(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void SetCurrentSecurityProfile(EndpointDescription endpoint)
    {
        Volatile.Write(ref _currentSecurityMode, (int)endpoint.SecurityMode);
        Volatile.Write(ref _currentSecurityPolicy, endpoint.SecurityPolicyUri);

        // Pin subsequent session recreation to the profile that was actually
        // negotiated, even when the original request left mode or policy open.
        _securityMode = endpoint.SecurityMode.ToString();
        _securityPolicy = endpoint.SecurityPolicyUri;
    }

    private void ClearCurrentSecurityProfile()
    {
        Volatile.Write(ref _currentSecurityMode, -1);
        Volatile.Write(ref _currentSecurityPolicy, null);
    }
}
