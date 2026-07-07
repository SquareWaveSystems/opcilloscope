using Terminal.Gui;
using Opcilloscope.App;
using Opcilloscope.OpcUa;

namespace Opcilloscope;

class Program
{
    static int Main(string[] args)
    {
        bool initialized = false;
        try
        {
            // Parse command-line arguments before initializing the terminal, so that --help/-h
            // prints usage and exits without opening a tty (required for headless/CI environments).
            // Note: If multiple arguments of the same type are provided (e.g., two config files),
            // the last one specified will be used.
            string? autoConnectUrl = null;
            string? configPath = null;

            for (int i = 0; i < args.Length; i++)
            {
                // Config file options: --config <path> or direct path ending with .cfg/.opcilloscope/.json
                if ((args[i] == "--config" || args[i] == "-f") && i + 1 < args.Length)
                {
                    configPath = args[i + 1];
                    i++; // Skip the next argument
                }
                else if (args[i].EndsWith(".cfg", StringComparison.OrdinalIgnoreCase) ||
                         args[i].EndsWith(".opcilloscope", StringComparison.OrdinalIgnoreCase) ||
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
                else if (args[i] == "--insecure")
                {
                    // Development-only: accept untrusted server certificates. Threaded to the
                    // OPC UA client wrapper as the process-wide default (secure-by-default
                    // otherwise). See OpcUaClientWrapper.AllowInsecureByDefault.
                    OpcUaClientWrapper.AllowInsecureByDefault = true;
                }
                else if (args[i] == "--help" || args[i] == "-h")
                {
                    PrintUsage();
                    return 0;
                }
            }

            // Validate the config file path before initializing the terminal, so the error
            // message is printed to the regular screen rather than being lost in the
            // alternate screen buffer (same pattern as --help above).
            if (!string.IsNullOrEmpty(configPath) && !File.Exists(configPath))
            {
                Console.Error.WriteLine($"Error: Configuration file not found: {configPath}");
                return 1;
            }

            // Warn about unimplemented auto-connect before initializing the terminal;
            // once the alternate screen buffer is active the message would be lost.
            if (string.IsNullOrEmpty(configPath) && !string.IsNullOrEmpty(autoConnectUrl))
            {
                Console.Error.WriteLine(
                    $"Warning: Auto-connect via command-line URL ('{autoConnectUrl}') is not currently implemented. " +
                    "Please use a configuration file with an endpoint URL instead.");
            }

#pragma warning disable IL2026 // Terminal.Gui Application.Init uses reflection and is not AOT-compatible
            Application.Init();
#pragma warning restore IL2026
            initialized = true;

            var mainWindow = new MainWindow();
            try
            {
                // Load config file if specified (takes precedence over URL)
                if (!string.IsNullOrEmpty(configPath))
                {
                    mainWindow.LoadConfigFromCommandLine(configPath);
                }

                Application.Run(mainWindow);
            }
            finally
            {
                mainWindow.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
        finally
        {
            if (initialized)
            {
                Application.Shutdown();
            }
        }

        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("opcilloscope - Terminal-based OPC UA Client");
        Console.WriteLine();
        Console.WriteLine("Usage: opcilloscope [options] [file]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -f, --config <file>   Load configuration file (.cfg, .opcilloscope, or .json)");
        Console.WriteLine("      --insecure        Accept untrusted server certificates (development only)");
        Console.WriteLine("  -h, --help            Show this help message");
        Console.WriteLine();
        Console.WriteLine("Note: Direct server connection via --connect or opc.tcp:// URLs is not yet");
        Console.WriteLine("      implemented. Please create a configuration file with the server URL.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  opcilloscope                           Start with empty configuration");
        Console.WriteLine("  opcilloscope production.cfg            Load configuration file");
        Console.WriteLine("  opcilloscope --config config.json      Load configuration file");
        Console.WriteLine();
    }
}
