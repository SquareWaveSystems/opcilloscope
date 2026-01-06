namespace OpcScope.TestServer;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var port = 4840;
        var showHelp = false;

        // Simple argument parsing
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-p":
                case "--port":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedPort))
                    {
                        port = parsedPort;
                        i++;
                    }
                    else
                    {
                        Console.Error.WriteLine("Error: --port requires a valid port number");
                        return 1;
                    }
                    break;
                case "-h":
                case "--help":
                    showHelp = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    showHelp = true;
                    break;
            }
        }

        if (showHelp)
        {
            PrintHelp();
            return 0;
        }

        Console.WriteLine("OpcScope Test Server");
        Console.WriteLine("====================");
        Console.WriteLine();

        var server = new TestServer();
        var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine();
            Console.WriteLine("Shutting down...");
        };

        try
        {
            await server.StartAsync(port);

            Console.WriteLine($"Server started successfully!");
            Console.WriteLine($"Endpoint: {server.EndpointUrl}");
            Console.WriteLine();
            Console.WriteLine("Available nodes:");
            Console.WriteLine("  Simulation/Counter      - Int32, increments every second");
            Console.WriteLine("  Simulation/RandomValue  - Double, random 0-100");
            Console.WriteLine("  Simulation/SineWave     - Double, oscillating value");
            Console.WriteLine("  Simulation/WritableString - String, writable");
            Console.WriteLine("  Simulation/ToggleBoolean  - Boolean, writable");
            Console.WriteLine("  Simulation/WritableNumber - Int32, writable");
            Console.WriteLine("  StaticData/ServerName   - String");
            Console.WriteLine("  StaticData/StartTime    - DateTime");
            Console.WriteLine("  StaticData/Version      - String");
            Console.WriteLine("  StaticData/ArrayOfInts  - Int32[]");
            Console.WriteLine();
            Console.WriteLine("Press Ctrl+C to stop the server...");

            // Wait for cancellation
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when Ctrl+C is pressed
            }

            await server.StopAsync();
            Console.WriteLine("Server stopped.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    static void PrintHelp()
    {
        Console.WriteLine("OpcScope Test Server - A standalone OPC UA test server");
        Console.WriteLine();
        Console.WriteLine("Usage: OpcScope.TestServer [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -p, --port <port>  Port to listen on (default: 4840)");
        Console.WriteLine("  -h, --help         Show this help message");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("  OpcScope.TestServer --port 4841");
    }
}
