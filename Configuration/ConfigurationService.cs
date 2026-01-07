using System.Text.Json;
using Opcilloscope.Configuration.Models;
using Opcilloscope.OpcUa.Models;

namespace Opcilloscope.Configuration;

/// <summary>
/// Service for loading, saving, and managing Opcilloscope configuration files.
/// </summary>
public class ConfigurationService
{

    /// <summary>
    /// Path to the currently loaded configuration file, or null if no file is loaded.
    /// </summary>
    public string? CurrentFilePath { get; private set; }

    /// <summary>
    /// Indicates whether there are unsaved changes to the configuration.
    /// </summary>
    public bool HasUnsavedChanges { get; set; }

    /// <summary>
    /// Event fired when the unsaved changes state changes.
    /// </summary>
    public event Action<bool>? UnsavedChangesStateChanged;

    /// <summary>
    /// Marks the configuration as having unsaved changes.
    /// </summary>
    public void MarkDirty()
    {
        if (!HasUnsavedChanges)
        {
            HasUnsavedChanges = true;
            UnsavedChangesStateChanged?.Invoke(true);
        }
    }

    /// <summary>
    /// Marks the configuration as saved (no unsaved changes).
    /// </summary>
    public void MarkClean()
    {
        if (HasUnsavedChanges)
        {
            HasUnsavedChanges = false;
            UnsavedChangesStateChanged?.Invoke(false);
        }
    }

    /// <summary>
    /// Loads a configuration from the specified file path.
    /// </summary>
    /// <param name="filePath">Path to the configuration file.</param>
    /// <returns>The loaded configuration.</returns>
    /// <exception cref="InvalidDataException">Thrown if the file contains invalid data.</exception>
    public async Task<OpcilloscopeConfig> LoadAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var config = JsonSerializer.Deserialize(json, OpcilloscopeJsonContext.Default.OpcilloscopeConfig)
            ?? throw new InvalidDataException("Invalid configuration file");

        // Handle version migrations if needed
        config = MigrateIfNeeded(config);

        // Validate the loaded configuration
        ValidateConfiguration(config);

        CurrentFilePath = filePath;
        HasUnsavedChanges = false;
        UnsavedChangesStateChanged?.Invoke(false);

        return config;
    }

    /// <summary>
    /// Validates a configuration object for common issues.
    /// </summary>
    /// <param name="config">The configuration to validate.</param>
    /// <exception cref="InvalidDataException">Thrown if validation fails.</exception>
    private void ValidateConfiguration(OpcilloscopeConfig config)
    {
        // Validate publishing interval
        if (config.Settings.PublishingIntervalMs < 0)
        {
            throw new InvalidDataException($"Invalid PublishingIntervalMs: {config.Settings.PublishingIntervalMs}. Must be non-negative.");
        }

        // Validate sampling interval
        if (config.Settings.SamplingIntervalMs < 0)
        {
            throw new InvalidDataException($"Invalid SamplingIntervalMs: {config.Settings.SamplingIntervalMs}. Must be non-negative.");
        }

        // Validate monitored nodes
        foreach (var node in config.MonitoredNodes)
        {
            if (string.IsNullOrWhiteSpace(node.NodeId))
            {
                throw new InvalidDataException($"Monitored node '{node.DisplayName}' has empty or invalid NodeId.");
            }
        }
    }

    /// <summary>
    /// Saves the configuration to the specified file path.
    /// </summary>
    /// <param name="config">The configuration to save.</param>
    /// <param name="filePath">Path to save the configuration to.</param>
    public async Task SaveAsync(OpcilloscopeConfig config, string filePath)
    {
        config.Metadata.LastModified = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(config, OpcilloscopeJsonContext.Default.OpcilloscopeConfig);
        await File.WriteAllTextAsync(filePath, json);

        CurrentFilePath = filePath;
        HasUnsavedChanges = false;
        UnsavedChangesStateChanged?.Invoke(false);
    }

    /// <summary>
    /// Captures the current application state into a configuration object.
    /// </summary>
    /// <param name="endpointUrl">The current server endpoint URL.</param>
    /// <param name="publishingInterval">The current publishing interval in ms.</param>
    /// <param name="monitoredVariables">The current monitored variables.</param>
    /// <param name="existingMetadata">Optional existing metadata to preserve.</param>
    /// <returns>A new configuration object representing the current state.</returns>
    public OpcilloscopeConfig CaptureCurrentState(
        string? endpointUrl,
        int publishingInterval,
        IEnumerable<MonitoredNode> monitoredVariables,
        ConfigMetadata? existingMetadata = null)
    {
        return new OpcilloscopeConfig
        {
            Server = new ServerConfig
            {
                EndpointUrl = endpointUrl ?? string.Empty
            },
            Settings = new SubscriptionSettings
            {
                PublishingIntervalMs = publishingInterval
            },
            MonitoredNodes = monitoredVariables.Select(m => new MonitoredNodeConfig
            {
                NodeId = m.NodeId.ToString(),
                DisplayName = m.DisplayName,
                Enabled = true
            }).ToList(),
            Metadata = existingMetadata ?? new ConfigMetadata
            {
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            }
        };
    }

    /// <summary>
    /// Resets the configuration service to a clean state (no file loaded).
    /// </summary>
    public void Reset()
    {
        CurrentFilePath = null;
        HasUnsavedChanges = false;
        UnsavedChangesStateChanged?.Invoke(false);
    }

    /// <summary>
    /// Gets the display name for the current configuration.
    /// </summary>
    /// <returns>The configuration name or "untitled" if no file is loaded.</returns>
    public string GetDisplayName()
    {
        if (string.IsNullOrEmpty(CurrentFilePath))
            return "untitled";

        return Path.GetFileNameWithoutExtension(CurrentFilePath);
    }

    /// <summary>
    /// Handles version migrations for configuration files.
    /// </summary>
    private OpcilloscopeConfig MigrateIfNeeded(OpcilloscopeConfig config)
    {
        // Future: handle "1.0" -> "1.1" migrations, etc.
        // For now, just return the config as-is
        return config;
    }

    /// <summary>
    /// Gets the default directory for configuration files.
    /// </summary>
    /// <returns>Path to the default configuration directory.</returns>
    public static string GetDefaultConfigDirectory()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Opcilloscope",
            "Configurations"
        );
        Directory.CreateDirectory(dir);
        return dir;
    }
}
