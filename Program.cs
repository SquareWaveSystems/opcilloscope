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

            // Parse command-line arguments for auto-connect
            string? autoConnectUrl = null;
            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == "--connect" || args[i] == "-c") && i + 1 < args.Length)
                {
                    autoConnectUrl = args[i + 1];
                    break;
                }
                else if (args[i].StartsWith("opc.tcp://"))
                {
                    autoConnectUrl = args[i];
                    break;
                }
            }

            var mainWindow = new MainWindow();

            // If auto-connect URL provided, trigger connection after UI is ready
            if (!string.IsNullOrEmpty(autoConnectUrl))
            {
                Application.AddTimeout(TimeSpan.FromMilliseconds(100), () =>
                {
                    // Invoke connection via reflection or public method
                    // For now, just log that we would auto-connect
                    return false;
                });
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
}
