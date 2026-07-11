namespace Opcilloscope;

internal sealed record CommandLineOptions(
    string? ConfigPath,
    string? AutoConnectUrl,
    bool AllowInsecureCertificates,
    bool ShowHelp);

internal static class CommandLineParser
{
    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        // Help is an immediate, side-effect-free request. Keep it usable even
        // when a shell alias appends stale/invalid arguments after --help.
        if (args.Any(arg => arg is "--help" or "-h"))
        {
            return new CommandLineOptions(null, null, false, ShowHelp: true);
        }

        string? configPath = null;
        string? autoConnectUrl = null;
        var allowInsecure = false;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--config":
                case "-f":
                    configPath = ReadOptionValue(args, ref i, arg);
                    break;

                case "--connect":
                case "-c":
                    autoConnectUrl = ReadOptionValue(args, ref i, arg);
                    break;

                case "--insecure":
                    allowInsecure = true;
                    break;

                default:
                    if (arg.StartsWith('-'))
                    {
                        throw new ArgumentException($"Unknown option: {arg}");
                    }

                    if (arg.StartsWith("opc.tcp://", StringComparison.OrdinalIgnoreCase))
                    {
                        autoConnectUrl = arg;
                    }
                    else if (HasConfigExtension(arg))
                    {
                        configPath = arg;
                    }
                    else
                    {
                        throw new ArgumentException($"Unexpected argument: {arg}");
                    }
                    break;
            }
        }

        return new CommandLineOptions(configPath, autoConnectUrl, allowInsecure, ShowHelp: false);
    }

    private static string ReadOptionValue(IReadOnlyList<string> args, ref int index, string option)
    {
        if (index + 1 >= args.Count
            || string.IsNullOrWhiteSpace(args[index + 1])
            || args[index + 1].StartsWith('-'))
        {
            throw new ArgumentException($"Option {option} requires a value.");
        }

        index++;
        return args[index];
    }

    private static bool HasConfigExtension(string path) =>
        path.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".opcilloscope", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
}
