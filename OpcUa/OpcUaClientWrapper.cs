using LibUA;
using LibUA.Core;
using OpcScope.Utilities;

namespace OpcScope.OpcUa;

/// <summary>
/// Wrapper around LibUA Client providing async connection management.
/// </summary>
public class OpcUaClientWrapper : IDisposable
{
    private Client? _client;
    private readonly Logger _logger;
    private string? _currentEndpoint;
    private CancellationTokenSource? _reconnectCts;
    private bool _disposed;
    private bool _isConnected;

    public bool IsConnected => _isConnected && _client != null;
    public string? CurrentEndpoint => _currentEndpoint;
    public Client? UnderlyingClient => _client;

    public event Action? Connected;
    public event Action? Disconnected;
    public event Action<string>? ConnectionError;

    public OpcUaClientWrapper(Logger logger)
    {
        _logger = logger;
    }

    public async Task<bool> ConnectAsync(string endpointUrl)
    {
        try
        {
            Disconnect();

            _logger.Info($"Connecting to {endpointUrl}...");

            var uri = new Uri(endpointUrl);
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 4840;

            var appDesc = new ApplicationDescription(
                "urn:OpcScope:Client",
                "urn:OpcScope",
                new LocalizedText("OpcScope"),
                ApplicationType.Client,
                null, null, null
            );

            _client = new Client(host, port, 10000);

            // Connect socket
            var connectResult = await Task.Run(() => _client.Connect());
            if (connectResult != StatusCode.Good)
            {
                throw new Exception($"Connect failed: 0x{(uint)connectResult:X8}");
            }

            // Open secure channel (anonymous/none)
            var openResult = await Task.Run(() => _client.OpenSecureChannel(
                MessageSecurityMode.None,
                SecurityPolicy.None,
                null
            ));
            if (openResult != StatusCode.Good)
            {
                throw new Exception($"OpenSecureChannel failed: 0x{(uint)openResult:X8}");
            }

            // Create session
            var createResult = await Task.Run(() => _client.CreateSession(
                appDesc,
                "OpcScope Session",
                300
            ));
            if (createResult != StatusCode.Good)
            {
                throw new Exception($"CreateSession failed: 0x{(uint)createResult:X8}");
            }

            // Activate session with anonymous identity
            var activateResult = await Task.Run(() => _client.ActivateSession(
                new UserIdentityAnonymousToken("anonymous"),
                Array.Empty<string>()
            ));
            if (activateResult != StatusCode.Good)
            {
                throw new Exception($"ActivateSession failed: 0x{(uint)activateResult:X8}");
            }

            _isConnected = true;
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

    public void Disconnect()
    {
        _reconnectCts?.Cancel();
        _reconnectCts = null;
        _isConnected = false;

        if (_client != null)
        {
            try
            {
                _client.Dispose();
            }
            catch
            {
                // Ignore dispose errors
            }
            _client = null;
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

    public List<ReferenceDescription> Browse(NodeId nodeId)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected");

        _client.Browse(
            new[]
            {
                new BrowseDescription(
                    nodeId,
                    BrowseDirection.Forward,
                    NodeId.Zero,
                    true,
                    0xFFFFFFFF,
                    BrowseResultMask.All
                )
            },
            10000,
            out var results
        );

        if (results != null && results.Length > 0 && results[0].Refs != null)
        {
            return results[0].Refs.ToList();
        }

        return new List<ReferenceDescription>();
    }

    public DataValue? ReadValue(NodeId nodeId)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected");

        _client.Read(
            new[]
            {
                new ReadValueId(nodeId, NodeAttribute.Value, null, new QualifiedName(0, null))
            },
            out var results
        );

        return results?.FirstOrDefault();
    }

    public DataValue[] ReadAttributes(NodeId nodeId, params NodeAttribute[] attributes)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected");

        var readValues = attributes.Select(a =>
            new ReadValueId(nodeId, a, null, new QualifiedName(0, null))
        ).ToArray();

        _client.Read(readValues, out var results);

        return results ?? Array.Empty<DataValue>();
    }

    public StatusCode WriteValue(NodeId nodeId, object value, NodeId dataType)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected");

        _client.Write(
            new[]
            {
                new WriteValue(
                    nodeId,
                    NodeAttribute.Value,
                    null,
                    new DataValue(value, StatusCode.Good, DateTime.UtcNow)
                )
            },
            out var results
        );

        if (results != null && results.Length > 0)
        {
            return (StatusCode)results[0];
        }
        return StatusCode.BadUnexpectedError;
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
