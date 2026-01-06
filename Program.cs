using Terminal.Gui;
using OpcScope.App;

namespace OpcScope;

class Program
{
    static int Main(string[] args)
    {
        try
        {
#pragma warning disable IL2026 // Terminal.Gui Application.Init uses reflection and is not AOT-compatible
            Application.Init();
#pragma warning restore IL2026

            // Parse command-line arguments
            // Note: If multiple arguments of the same type are provided (e.g., two config files),
            // the last one specified will be used.
            string? autoConnectUrl = null;
            string? configPath = null;

            for (int i = 0; i < args.Length; i++)
            {
                // Config file options: --config <path> or direct path ending with .opcscope/.json
                if ((args[i] == "--config" || args[i] == "-f") && i + 1 < args.Length)
                {
                    configPath = args[i + 1];
                    i++; // Skip the next argument
                }
                else if (args[i].EndsWith(".opcscope", StringComparison.OrdinalIgnoreCase) ||
                         (args[i].EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(args[i])))
                {
                    configPath = args[i];
                }
                // Connection URL options: --connect <url> or direct opc.tcp:// URL
                // Note: Direct URL connection is not currently implemented; use config files instead.
                else if ((args[i] == "--connect" || args[i] == "-c") && i + 1 < args.Length)
                {
                    autoConnectUrl = args[i + 1];
                    i++; // Skip the next argument
                }
                else if (args[i].StartsWith("opc.tcp://"))
                {
                    autoConnectUrl = args[i];
                }
                else if (args[i] == "--help" || args[i] == "-h")
                {
                    PrintUsage();
                    return 0;
                }
            }

            var mainWindow = new MainWindow();

            // Load config file if specified (takes precedence over URL)
            if (!string.IsNullOrEmpty(configPath))
            {
                if (!File.Exists(configPath))
                {
                    Console.Error.WriteLine($"Error: Configuration file not found: {configPath}");
                    Application.Shutdown();
                    return 1;
                }
                mainWindow.LoadConfigFromCommandLine(configPath);
            }
            // Otherwise, if auto-connect URL provided, show warning (not yet implemented)
            else if (!string.IsNullOrEmpty(autoConnectUrl))
            {
                Console.Error.WriteLine(
                    $"Warning: Auto-connect via command-line URL ('{autoConnectUrl}') is not currently implemented. " +
                    "Please use a configuration file with an endpoint URL instead.");
            }

            Application.Run(mainWindow);
            mainWindow.Dispose();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
        finally
        {
            Application.Shutdown();
        }

        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("OPC Scope - Terminal-based OPC UA Client");
        Console.WriteLine();
        Console.WriteLine("Usage: opcscope [options] [file]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -f, --config <file>   Load configuration file (.opcscope or .json)");
        Console.WriteLine("  -h, --help            Show this help message");
        Console.WriteLine();
        Console.WriteLine("Note: Direct server connection via --connect or opc.tcp:// URLs is not yet");
        Console.WriteLine("      implemented. Please create a configuration file with the server URL.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  opcscope                           Start with empty configuration");
        Console.WriteLine("  opcscope production.opcscope       Load configuration file");
        Console.WriteLine("  opcscope --config config.json      Load configuration file");
        Console.WriteLine();
    }
}
