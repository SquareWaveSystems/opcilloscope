using Terminal.Gui;
using Opcilloscope.App;
using Opcilloscope.OpcUa;
using Opcilloscope.Utilities;

namespace Opcilloscope;

class Program
{
    static int Main(string[] args)
    {
        IApplication? app = null;
        try
        {
            CommandLineOptions options;
            try
            {
                options = CommandLineParser.Parse(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Console.Error.WriteLine("Run 'opcilloscope --help' for usage.");
                return 2;
            }

            if (options.ShowHelp)
            {
                PrintUsage();
                return 0;
            }

            if (options.ShowVersion)
            {
                Console.WriteLine($"opcilloscope {VersionInfo.DisplayVersion}");
                return 0;
            }

            OpcUaClientWrapper.AllowInsecureByDefault = options.AllowInsecureCertificates;

            // Validate the config file path before initializing the terminal, so the error
            // message is printed to the regular screen rather than being lost in the
            // alternate screen buffer (same pattern as --help above).
            if (!string.IsNullOrEmpty(options.ConfigPath) && !File.Exists(options.ConfigPath))
            {
                Console.Error.WriteLine($"Error: Configuration file not found: {options.ConfigPath}");
                return 1;
            }

            app = Application.Create();
            TerminalUi.App = app;
            app.Init();

            var mainWindow = new MainWindow();
            try
            {
                // Load config file if specified (takes precedence over URL)
                if (!string.IsNullOrEmpty(options.ConfigPath))
                {
                    mainWindow.LoadConfigFromCommandLine(options.ConfigPath);
                }
                else if (!string.IsNullOrEmpty(options.AutoConnectUrl))
                {
                    mainWindow.ConnectFromCommandLine(options.AutoConnectUrl);
                }

                app.Run(mainWindow);
            }
            finally
            {
                // Cancel awaited UI dispatches before disposing the window. Once
                // Run returns there will be no further main-loop iteration to
                // execute callbacks queued by late network continuations.
                TerminalUi.BeginShutdown();
                mainWindow.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
        finally
        {
            // Disposing the application shuts down the terminal (the
            // instance-based replacement for the legacy Application.Shutdown).
            app?.Dispose();
            TerminalUi.App = null;
        }

        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("opcilloscope - terminal-based OPC UA client");
        Console.WriteLine();
        Console.WriteLine("Usage: opcilloscope [options] [file]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -f, --config <file>   Load configuration file (.cfg, .opcilloscope, or .json)");
        Console.WriteLine("  -c, --connect <url>   Connect directly to an OPC UA endpoint");
        Console.WriteLine("      --insecure        Disable server certificate validation (development only)");
        Console.WriteLine("  -V, --version         Show version information");
        Console.WriteLine("  -h, --help            Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  opcilloscope                           Start with empty configuration");
        Console.WriteLine("  opcilloscope production.cfg            Load configuration file");
        Console.WriteLine("  opcilloscope --config config.json      Load configuration file");
        Console.WriteLine("  opcilloscope --connect opc.tcp://localhost:4840");
        Console.WriteLine();
    }
}
