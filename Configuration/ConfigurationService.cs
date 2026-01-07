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
    /// Uses cross-platform appropriate locations:
    /// - Windows: %APPDATA%/opcilloscope/configs/
    /// - macOS: ~/Library/Application Support/opcilloscope/configs/
    /// - Linux: ~/.config/opcilloscope/configs/
    /// </summary>
    /// <returns>Path to the default configuration directory.</returns>
    public static string GetDefaultConfigDirectory()
    {
        string baseDir;
        string appFolder;

        if (OperatingSystem.IsWindows())
        {
            // Windows: %APPDATA%/opcilloscope/configs/
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            appFolder = "opcilloscope";
        }
        else if (OperatingSystem.IsMacOS())
        {
            // macOS: ~/Library/Application Support/opcilloscope/configs/
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            appFolder = "opcilloscope";
        }
        else
        {
            // Linux: ~/.config/opcilloscope/configs/
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(configHome))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                configHome = Path.Combine(home, ".config");
            }
            baseDir = configHome;
            appFolder = "opcilloscope";
        }

        // Fallback if base directory is empty
        if (string.IsNullOrEmpty(baseDir))
        {
            baseDir = Path.GetTempPath();
        }

        var dir = Path.Combine(baseDir, appFolder, "configs");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// The file extension used for configuration files (.opcilloscope).
    /// </summary>
    public const string ConfigFileExtension = ".opcilloscope";

    /// <summary>
    /// Generates a default filename for saving a configuration.
    /// Format: {sanitized_connection_url}_{timestamp}.opcilloscope
    /// </summary>
    /// <param name="connectionUrl">The OPC UA connection URL, or null if not connected.</param>
    /// <returns>A sanitized filename with .opcilloscope extension.</returns>
    public static string GenerateDefaultFilename(string? connectionUrl)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmm");
        string baseName;

        if (!string.IsNullOrEmpty(connectionUrl))
        {
            baseName = SanitizeUrlForFilename(connectionUrl);
        }
        else
        {
            baseName = "config";
        }

        return $"{baseName}_{timestamp}{ConfigFileExtension}";
    }

    /// <summary>
    /// Sanitizes a URL to be used as part of a filename.
    /// Replaces invalid filename characters with underscores.
    /// </summary>
    /// <param name="url">The URL to sanitize.</param>
    /// <returns>A filename-safe string derived from the URL.</returns>
    public static string SanitizeUrlForFilename(string url)
    {
        if (string.IsNullOrEmpty(url))
            return "unknown";

        // Remove protocol prefix
        var sanitized = url;
        if (sanitized.StartsWith("opc.tcp://", StringComparison.OrdinalIgnoreCase))
            sanitized = sanitized.Substring(10);
        else if (sanitized.StartsWith("opc.https://", StringComparison.OrdinalIgnoreCase))
            sanitized = sanitized.Substring(12);
        else if (sanitized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            sanitized = sanitized.Substring(8);
        else if (sanitized.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            sanitized = sanitized.Substring(7);

        // Replace invalid filename characters with underscores
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }

        // Also replace common URL special characters
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

        // Limit length to avoid overly long filenames
        if (sanitized.Length > 50)
        {
            sanitized = sanitized.Substring(0, 50).TrimEnd('_');
        }

        return string.IsNullOrEmpty(sanitized) ? "unknown" : sanitized;
    }

    /// <summary>
    /// Ensures a filename has the correct .opcilloscope extension.
    /// </summary>
    /// <param name="filename">The filename to check.</param>
    /// <returns>The filename with .opcilloscope extension.</returns>
    public static string EnsureConfigExtension(string filename)
    {
        if (string.IsNullOrEmpty(filename))
            return filename;

        if (!filename.EndsWith(ConfigFileExtension, StringComparison.OrdinalIgnoreCase))
        {
            return filename + ConfigFileExtension;
        }

        return filename;
    }
}
