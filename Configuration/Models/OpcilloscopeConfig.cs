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
    /// <para>
    /// Warning: As of version 1.0, this value is not currently used by the
    /// connection logic. It is included in the configuration model to
    /// document intended security settings and to support future versions
    /// of Opcilloscope that negotiate security based on this value.
    /// </para>
    /// </summary>
    public string SecurityMode { get; set; } = "None";

    /// <summary>
    /// Requested security policy URI or shorthand (for example:
    /// Basic256Sha256 or the full policy URI).
    /// <para>
    /// Warning: As of version 1.0, this value is not currently used by the
    /// connection logic. It is provided for forward compatibility and
    /// documentation of the desired security policy.
    /// </para>
    /// </summary>
    public string? SecurityPolicy { get; set; }

    /// <summary>
    /// Authentication settings for the OPC UA server.
    /// <para>
    /// Warning: As of version 1.0, these authentication settings are not
    /// yet applied when sessions are created. They are included for
    /// future support of non-anonymous authentication and to document
    /// the intended credentials strategy.
    /// </para>
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
    /// Default publishing interval (in milliseconds) for OPC UA subscriptions created by Opcilloscope.
    /// This value is applied when configurations are loaded.
    /// Valid range: 100-10000ms (values outside this range will be clamped by SubscriptionManager).
    /// </summary>
    public int PublishingIntervalMs { get; set; } = 250;

    /// <summary>
    /// Default sampling interval (in milliseconds) for monitored variables.
    /// <para>
    /// Note: As of version 1.0, this setting is defined in the configuration model but is not yet
    /// applied by the configuration loading logic. It is reserved for future use.
    /// </para>
    /// </summary>
    public int SamplingIntervalMs { get; set; } = 250;

    /// <summary>
    /// Default queue size for monitored variables.
    /// <para>
    /// Note: As of version 1.0, this setting is defined in the configuration model but is not yet
    /// applied by the configuration loading logic. It is reserved for future use.
    /// </para>
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
