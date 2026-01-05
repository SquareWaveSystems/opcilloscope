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
                ApplicationCertificate = new CertificateIdentifier(),
                AutoAcceptUntrustedCertificates = true,
                AddAppCertToTrustedStore = false
            },
            TransportConfigurations = new TransportConfigurationCollection(),
            TransportQuotas = new TransportQuotas { OperationTimeout = 30000 },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
        };

        await _appConfig.Validate(ApplicationType.Client);

        // Accept all certificates for simplicity
        _appConfig.CertificateValidator = new CertificateValidator();
        _appConfig.CertificateValidator.CertificateValidation += (sender, e) =>
        {
            e.Accept = true;
        };

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
            var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointUrl, useSecurity: false, 10000);

            // Create session
            var endpointConfig = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfig);

            _session = await Opc.Ua.Client.Session.Create(
                config,
                endpoint,
                false,
                "OpcScope Session",
                60000,
                new UserIdentity(new AnonymousIdentityToken()),
                null
            );

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
                _session.Close();
                _session.Dispose();
            }
            catch
            {
                // Ignore dispose errors
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

    public ReferenceDescriptionCollection Browse(NodeId nodeId)
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

        return browser.Browse(nodeId);
    }

    public DataValue? ReadValue(NodeId nodeId)
    {
        if (_session == null)
            throw new InvalidOperationException("Not connected");

        return _session.ReadValue(nodeId);
    }

    public DataValueCollection ReadAttributes(NodeId nodeId, params uint[] attributeIds)
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

        _session.Read(
            null,
            0,
            TimestampsToReturn.Both,
            nodesToRead,
            out var results,
            out _
        );

        return results;
    }

    public StatusCode WriteValue(NodeId nodeId, object value)
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

        _session.Write(
            null,
            nodesToWrite,
            out var results,
            out _
        );

        return results?.Count > 0 ? results[0] : StatusCodes.BadUnexpectedError;
    }

    public Task<DataValue> ReadValueAsync(NodeId nodeId)
    {
        return Task.FromResult(ReadValue(nodeId) ?? new DataValue(StatusCodes.BadNodeIdUnknown));
    }

    public Task<StatusCode> WriteValueAsync(NodeId nodeId, object value)
    {
        return Task.FromResult(WriteValue(nodeId, value));
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
}
