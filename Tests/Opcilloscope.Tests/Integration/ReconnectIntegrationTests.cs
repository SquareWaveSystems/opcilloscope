using Opc.Ua;
using Opcilloscope.OpcUa;
using Opcilloscope.Utilities;
using Xunit;

namespace Opcilloscope.Tests.Integration;

/// <summary>
/// Verifies automatic reconnection after a transient session loss.
///
/// This test owns a dedicated <see cref="Opcilloscope.TestServer.TestServer"/> instance on
/// its own port so it can stop and restart the server to simulate a network drop without
/// disturbing the shared <c>TestServerFixture</c>. It is deliberately NOT part of any test
/// collection that shares a server.
///
/// IMPORTANT: This test starts/stops a real OPC UA server on a fixed port and is intended to
/// run in CI in isolation. It must NOT be run alongside other worktrees/agents locally, as
/// the fixed port would collide.
/// </summary>
public class ReconnectIntegrationTests
{
    // Fixed port well outside TestServerFixture's dynamic range (48400+).
    private const int DedicatedPort = 49555;

    [Fact]
    public async Task Reconnect_AfterSessionLoss_RestoresConnectionAndResumesValueUpdates()
    {
        var logger = new Logger();
        var server = new Opcilloscope.TestServer.TestServer();
        await server.StartAsync(DedicatedPort);

        var connectionManager = new ConnectionManager(logger);
        try
        {
            var endpoint = $"opc.tcp://localhost:{DedicatedPort}/UA/OpcilloscopeTest";

            var reconnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var postReconnectTick = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var dropOccurred = false;

            // When keep-alive reports the drop, drive the documented reconnect/backoff loop.
            connectionManager.AutoReconnectTriggered += () =>
                connectionManager.ReconnectAsync().FireAndForget(logger);

            connectionManager.StateChanged += state =>
            {
                if (dropOccurred && state == ConnectionState.Connected)
                    reconnected.TrySetResult(true);
            };

            var connected = await connectionManager.ConnectAsync(endpoint, publishingInterval: 250);
            Assert.True(connected, "initial connection should succeed");

            var nsIndex = connectionManager.Client.Session!.NamespaceUris.GetIndex(
                Opcilloscope.TestServer.TestNodeManager.NamespaceUri);
            Assert.True(nsIndex >= 0, "test server namespace should be present");
            var counterId = new NodeId("Counter", (ushort)nsIndex);

            var node = await connectionManager.SubscribeAsync(counterId, "Counter");
            Assert.NotNull(node);

            // A value change that arrives after the drop proves the subscription resumed.
            connectionManager.ValueChanged += changed =>
            {
                if (dropOccurred && changed.ClientHandle == node!.ClientHandle)
                    postReconnectTick.TrySetResult(true);
            };

            // Confirm we are receiving values before forcing the drop.
            await WaitForAsync(() => node!.Timestamp != null, TimeSpan.FromSeconds(15));

            // Force session loss by stopping the server, then bring it back so reconnection
            // (with exponential backoff) can succeed.
            dropOccurred = true;
            await server.StopAsync();
            await Task.Delay(TimeSpan.FromSeconds(2)); // allow keep-alive to detect the drop
            await server.StartAsync(DedicatedPort);

            var reconnectCompleted = await Task.WhenAny(reconnected.Task, Task.Delay(TimeSpan.FromSeconds(45)));
            Assert.True(reconnectCompleted == reconnected.Task,
                "connection manager should report Connected after reconnect");

            var tickCompleted = await Task.WhenAny(postReconnectTick.Task, Task.Delay(TimeSpan.FromSeconds(45)));
            Assert.True(tickCompleted == postReconnectTick.Task,
                "a ValueChanged tick should arrive after reconnect");
        }
        finally
        {
            connectionManager.Dispose();
            await server.StopAsync();
            server.Dispose();
        }
    }

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(100);
        }
    }
}
