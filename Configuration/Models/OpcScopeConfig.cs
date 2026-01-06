namespace OpcScope.Configuration.Models;

/// <summary>
/// Root configuration model for OpcScope configuration files (.opcscope).
/// </summary>
public class OpcScopeConfig
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
    public string EndpointUrl { get; set; } = string.Empty;
    public string SecurityMode { get; set; } = "None";
    public string? SecurityPolicy { get; set; }
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
    public int PublishingIntervalMs { get; set; } = 1000;
    public int SamplingIntervalMs { get; set; } = 500;
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
