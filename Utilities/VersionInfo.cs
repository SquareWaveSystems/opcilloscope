using System.Reflection;

namespace Opcilloscope.Utilities;

/// <summary>
/// Provides the user-facing application version written by MinVer.
/// </summary>
internal static class VersionInfo
{
    public static string DisplayVersion { get; } = GetDisplayVersion();

    private static string GetDisplayVersion()
    {
        var assembly = typeof(VersionInfo).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrEmpty(informational))
        {
            var metadataIndex = informational.IndexOf('+');
            return metadataIndex >= 0 ? informational[..metadataIndex] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
