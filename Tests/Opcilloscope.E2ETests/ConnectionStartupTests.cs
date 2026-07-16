using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Opcilloscope.E2ETests;

/// <summary>
/// Published-binary regressions for command-line connection startup.
/// </summary>
[Collection("E2E")]
public sealed class ConnectionStartupTests
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);
    private readonly PublishedBinaryFixture _fixture;

    public ConnectionStartupTests(PublishedBinaryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConfigFileConnection_RemainsConnectedAfterStartupBannerCompletes()
    {
        await RunWithTestServerAsync(async (server, tempRoot, environment) =>
        {
            var configPath = Path.Combine(tempRoot, "issue-178.cfg");
            await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(new
            {
                version = "1.0",
                server = new
                {
                    endpointUrl = server.EndpointUrl,
                    securityMode = "None",
                    securityPolicy = "None",
                },
                settings = new
                {
                    publishingIntervalMs = 250,
                    samplingIntervalMs = 100,
                },
                monitoredNodes = Array.Empty<object>(),
            }));

            using var application = new OpcilloscopeSession(
                _fixture.BinaryPath,
                ["--insecure", configPath],
                rows: 35,
                cols: 120,
                extraEnvironment: environment);

            Assert.True(
                application.WaitForText("Connected to", ConnectTimeout),
                RenderedScreen(application));

            await Task.Delay(TimeSpan.FromSeconds(5));
            var snapshot = application.Snapshot();
            var statusLine = snapshot.Split('\n')
                .Reverse()
                .FirstOrDefault(line => line.Contains("Connected", StringComparison.Ordinal));

            Assert.NotNull(statusLine);
            Assert.DoesNotContain("Not Connected", statusLine, StringComparison.Ordinal);
            Assert.Contains("Connected", statusLine, StringComparison.Ordinal);

            QuitAndAssertCleanExit(application, snapshot);
        });
    }

    [Fact]
    public async Task ConnectOption_ConnectsDirectlyToEndpoint()
    {
        await RunWithTestServerAsync((server, _, environment) =>
        {
            using var application = new OpcilloscopeSession(
                _fixture.BinaryPath,
                ["--insecure", "--connect", server.EndpointUrl],
                rows: 35,
                cols: 120,
                extraEnvironment: environment);

            Assert.True(
                application.WaitForText("Connected to", ConnectTimeout),
                RenderedScreen(application));
            Assert.True(
                application.WaitForText("● Connected", ConnectTimeout),
                RenderedScreen(application));
            Assert.DoesNotContain("not currently implemented", application.Snapshot());

            QuitAndAssertCleanExit(application, application.Snapshot());
            return Task.CompletedTask;
        });
    }

    private static async Task RunWithTestServerAsync(
        Func<Opcilloscope.TestServer.TestServer, string, IReadOnlyDictionary<string, string>, Task> test)
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            $"opcilloscope-connection-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var port = ReservePort();
            await using var server = new Opcilloscope.TestServer.TestServer(
                Path.Combine(tempRoot, "server-pki"));
            await server.StartAsync(port);

            var environment = new Dictionary<string, string>
            {
                ["XDG_CONFIG_HOME"] = Path.Combine(tempRoot, "config"),
                ["XDG_DATA_HOME"] = Path.Combine(tempRoot, "data"),
            };
            await test(server, tempRoot, environment);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void QuitAndAssertCleanExit(OpcilloscopeSession application, string snapshot)
    {
        application.SendByte(0x11);
        if (!application.WaitForExit(TimeSpan.FromSeconds(1)))
        {
            Assert.True(
                application.WaitForText("Unsaved Changes", TimeSpan.FromSeconds(2)),
                "Application neither exited nor displayed the unsaved-changes prompt.\n" + snapshot);
            // MessageBox focuses its last button (Cancel) by default. Move left
            // to Discard, then accept it.
            application.Send("\x1b[D\r");
        }

        Assert.True(
            application.WaitForExit(TimeSpan.FromSeconds(5)),
            $"Application did not exit after Ctrl+Q.\n{snapshot}");
        Assert.Equal(0, application.ExitCode);
    }

    private static int ReservePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string RenderedScreen(OpcilloscopeSession application) =>
        "Rendered screen was:\n" + application.Snapshot();
}
