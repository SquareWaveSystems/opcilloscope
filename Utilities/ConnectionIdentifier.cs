namespace Opcilloscope.Utilities;

/// <summary>
/// Utility for generating standardized connection identifier strings.
/// Format: {host}_{port}_{timestamp} with underscores separating each component.
/// </summary>
public static class ConnectionIdentifier
{
    /// <summary>
    /// Generates a standardized connection identifier string from an endpoint URL.
    /// Format: {host}_{port}_{timestamp}
    /// </summary>
    /// <param name="endpointUrl">The OPC UA endpoint URL (e.g., "opc.tcp://192.168.1.67:50000").</param>
    /// <param name="timestamp">Optional timestamp. If null, uses current time.</param>
    /// <param name="timestampFormat">Format string for the timestamp. Default is "yyyyMMddHHmm".</param>
    /// <returns>A standardized identifier string (e.g., "192.168.1.67_50000_202601071234").</returns>
    public static string Generate(string? endpointUrl, DateTime? timestamp = null, string timestampFormat = "yyyyMMddHHmm")
    {
        var ts = (timestamp ?? DateTime.Now).ToString(timestampFormat);

        if (string.IsNullOrEmpty(endpointUrl))
            return $"config_{ts}";

        var hostPort = ExtractHostPort(endpointUrl);
        return $"{hostPort}_{ts}";
    }

    /// <summary>
    /// Extracts the host and port from an endpoint URL in a filename-safe format.
    /// Format: {host}_{port}
    /// </summary>
    /// <param name="endpointUrl">The OPC UA endpoint URL.</param>
    /// <returns>A filename-safe string with host and port separated by underscore.</returns>
    public static string ExtractHostPort(string? endpointUrl)
    {
        if (string.IsNullOrEmpty(endpointUrl))
            return "unknown";

        var url = endpointUrl;

        // Remove protocol prefix
        url = RemoveProtocolPrefix(url);

        // Extract host and port before any path
        var pathIndex = url.IndexOf('/');
        if (pathIndex >= 0)
            url = url.Substring(0, pathIndex);

        // Parse host and port
        var lastColonIndex = url.LastIndexOf(':');
        if (lastColonIndex > 0)
        {
            var host = url.Substring(0, lastColonIndex);
            var port = url.Substring(lastColonIndex + 1);

            // Sanitize host (replace dots are ok, but sanitize other chars)
            host = SanitizeComponent(host);
            port = SanitizeComponent(port);

            return $"{host}_{port}";
        }

        // No port specified, just sanitize the host
        return SanitizeComponent(url);
    }

    /// <summary>
    /// Sanitizes a URL component for use in filenames.
    /// Replaces invalid filename characters with underscores.
    /// </summary>
    private static string SanitizeComponent(string component)
    {
        if (string.IsNullOrEmpty(component))
            return "unknown";

        var sanitized = component;

        // Replace invalid filename characters with underscores
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }

        // Replace common URL special characters
        sanitized = sanitized
            .Replace(':', '_')
            .Replace('/', '_')
            .Replace('\\', '_')
            .Replace('?', '_')
            .Replace('&', '_')
            .Replace('=', '_');

        // Remove consecutive underscores
        while (sanitized.Contains("__"))
        {
            sanitized = sanitized.Replace("__", "_");
        }

        // Trim underscores from start and end
        sanitized = sanitized.Trim('_');

        return string.IsNullOrEmpty(sanitized) ? "unknown" : sanitized;
    }

    /// <summary>
    /// Removes the protocol prefix from a URL.
    /// </summary>
    private static string RemoveProtocolPrefix(string url)
    {
        if (url.StartsWith("opc.tcp://", StringComparison.OrdinalIgnoreCase))
            return url.Substring(10);
        if (url.StartsWith("opc.https://", StringComparison.OrdinalIgnoreCase))
            return url.Substring(12);
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url.Substring(8);
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return url.Substring(7);
        return url;
    }

    /// <summary>
    /// Limits the length of an identifier to avoid overly long filenames.
    /// </summary>
    /// <param name="identifier">The identifier to limit.</param>
    /// <param name="maxLength">Maximum length. Default is 50.</param>
    /// <returns>The limited identifier.</returns>
    public static string LimitLength(string identifier, int maxLength = 50)
    {
        if (string.IsNullOrEmpty(identifier) || identifier.Length <= maxLength)
            return identifier;

        return identifier.Substring(0, maxLength).TrimEnd('_');
    }
}
