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
}
