namespace Opcilloscope.Configuration.Models;

/// <summary>
/// Root configuration model for Opcilloscope configuration files (.opcilloscope).
/// </summary>
public class OpcilloscopeConfig
{
    public string Version { get; set; } = "1.0";
    public ServerConfig Server { get; set; } = new();
    public SubscriptionSettings Settings { get; set; } = new();
    public List<MonitoredNodeConfig> MonitoredNodes { get; set; } = new();
    public ConfigMetadata Metadata { get; set; } = new();
}

/// <summary>
/// OPC UA server connection settings.
/// </summary>
public class ServerConfig
{
    /// <summary>
    /// OPC UA server endpoint URL.
    /// </summary>
    public string EndpointUrl { get; set; } = string.Empty;

    /// <summary>
    /// Requested message security mode (for example: None, Sign, SignAndEncrypt).
    /// Used during endpoint selection when connecting; when None, an unsecured
    /// endpoint is selected.
    /// </summary>
    public string SecurityMode { get; set; } = "None";

    /// <summary>
    /// Requested security policy URI or shorthand (for example:
    /// Basic256Sha256 or the full policy URI).
    /// Used during endpoint selection when connecting; honored when a
    /// matching endpoint exists on the server.
    /// </summary>
    public string? SecurityPolicy { get; set; }

    /// <summary>
    /// Authentication settings for the OPC UA server.
    /// Supports Anonymous and UserName authentication types.
    /// Passwords are never stored in config files; they are prompted at runtime.
    /// </summary>
    public AuthenticationConfig Authentication { get; set; } = new();
}

/// <summary>
/// Authentication configuration for the OPC UA server.
/// </summary>
public class AuthenticationConfig
{
    /// <summary>
    /// Authentication type: Anonymous, UserName, or Certificate.
    /// </summary>
    public string Type { get; set; } = "Anonymous";

    /// <summary>
    /// Username for UserName authentication (password is prompted at runtime for security).
    /// </summary>
    public string? Username { get; set; }
}

/// <summary>
/// OPC UA subscription settings.
/// </summary>
public class SubscriptionSettings
{
    /// <summary>
    /// Publishing interval (in milliseconds) for the OPC UA subscription.
    /// Controls how often the server sends data-change notifications to the client.
    /// This directly determines the data resolution for Scope and CSV recording:
    /// a 1000 ms interval means roughly one data point per second per variable.
    /// Valid range: 100-10000 ms (values outside this range will be clamped by SubscriptionManager).
    /// </summary>
    public int PublishingIntervalMs { get; set; } = 250;

    /// <summary>
    /// Sampling interval (in milliseconds) applied to monitored variables.
    /// Controls how often the server samples the underlying value; 0 means
    /// "as fast as the server allows".
    /// Valid range: 0-10000 ms (values outside this range will be clamped by SubscriptionManager).
    /// </summary>
    public int SamplingIntervalMs { get; set; } = 250;

    /// <summary>
    /// Server-side notification queue size applied to monitored variables.
    /// Values sampled between publishes are queued up to this depth (oldest discarded first).
    /// Valid range: 1-1000 (values outside this range will be clamped by SubscriptionManager).
    /// </summary>
    public uint QueueSize { get; set; } = 10;
}

/// <summary>
/// Configuration for a single monitored node.
/// </summary>
public class MonitoredNodeConfig
{
    public string NodeId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Configuration metadata for human context and tracking.
/// </summary>
public class ConfigMetadata
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
