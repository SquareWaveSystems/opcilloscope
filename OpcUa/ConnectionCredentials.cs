namespace Opcilloscope.OpcUa;

/// <summary>
/// Specifies the type of authentication to use when connecting to an OPC UA server.
/// </summary>
public enum AuthenticationType
{
    Anonymous,
    UserName
}

/// <summary>
/// Carries authentication parameters through the connection pipeline.
/// Password is held in memory only and never persisted to disk.
/// </summary>
public record ConnectionCredentials(
    AuthenticationType Type,
    string? Username = null,
    string? Password = null)
{
    public static readonly ConnectionCredentials Anonymous = new(AuthenticationType.Anonymous);

    /// <summary>
    /// Verifies that the credential shape is valid before endpoint discovery or
    /// session creation. Unknown enum values and blank usernames fail closed.
    /// </summary>
    public void Validate()
    {
        if (!Enum.IsDefined(Type))
        {
            throw new ArgumentOutOfRangeException(
                nameof(Type),
                Type,
                "Unsupported OPC UA authentication type.");
        }

        if (Type == AuthenticationType.UserName && string.IsNullOrWhiteSpace(Username))
        {
            throw new ArgumentException(
                "A non-empty username is required for UserName authentication.",
                nameof(Username));
        }
    }

    /// <summary>
    /// Parses a string from configuration into an AuthenticationType. Unknown or
    /// missing values are rejected instead of silently downgrading to Anonymous.
    /// </summary>
    public static AuthenticationType ParseAuthType(string? value)
    {
        if (string.Equals(value, nameof(AuthenticationType.Anonymous), StringComparison.OrdinalIgnoreCase))
            return AuthenticationType.Anonymous;

        if (string.Equals(value, nameof(AuthenticationType.UserName), StringComparison.OrdinalIgnoreCase))
            return AuthenticationType.UserName;

        throw new FormatException(
            $"Unsupported authentication type '{value ?? "<null>"}'. " +
            $"Expected '{nameof(AuthenticationType.Anonymous)}' or '{nameof(AuthenticationType.UserName)}'.");
    }
}
