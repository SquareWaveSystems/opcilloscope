using System.Text.Json;
using Opcilloscope.Configuration.Models;
using Opcilloscope.OpcUa;
using Opcilloscope.OpcUa.Models;
using Opcilloscope.Utilities;

namespace Opcilloscope.Configuration;

/// <summary>
/// Service for loading, saving, and managing Opcilloscope configuration files.
/// </summary>
public class ConfigurationService
{

    /// <summary>
    /// The most recently loaded configuration, retained so that fields not
    /// surfaced by the UI (security and sampling settings) survive a
    /// load/capture/save round-trip instead of being silently dropped.
    /// </summary>
    private OpcilloscopeConfig? _loadedConfig;

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
    /// Maximum allowed configuration file size (1 MB).
    /// </summary>
    private const long MaxConfigFileSizeBytes = 1024 * 1024;

    /// <summary>
    /// The configuration file format version written by this build.
    /// </summary>
    private const string CurrentConfigVersion = "1.0";

    /// <summary>
    /// The major component of <see cref="CurrentConfigVersion"/>, derived from it
    /// so the two cannot drift apart when the version is bumped.
    /// </summary>
    private static readonly int CurrentConfigMajorVersion =
        int.Parse(CurrentConfigVersion.Split('.')[0]);

    /// <summary>
    /// Loads a configuration from the specified file path.
    /// </summary>
    /// <param name="filePath">Path to the configuration file.</param>
    /// <returns>The loaded configuration.</returns>
    /// <exception cref="InvalidDataException">Thrown if the file contains invalid data.</exception>
    public async Task<OpcilloscopeConfig> LoadAsync(string filePath)
    {
        // Validate file size before reading to prevent memory exhaustion
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > MaxConfigFileSizeBytes)
        {
            throw new InvalidDataException(
                $"Configuration file too large: {fileInfo.Length:N0} bytes (maximum: {MaxConfigFileSizeBytes:N0} bytes)");
        }

        var json = await File.ReadAllTextAsync(filePath);
        var config = JsonSerializer.Deserialize(json, OpcilloscopeJsonContext.Default.OpcilloscopeConfig)
            ?? throw new InvalidDataException("Invalid configuration file");

        // Normalize explicit JSON nulls ("settings": null, etc.) so they behave
        // like absent sections instead of crashing later code with null references.
        NormalizeConfig(config);

        // Handle version migrations if needed
        config = MigrateIfNeeded(config);

        // Validate the loaded configuration
        ValidateConfiguration(config);

        _loadedConfig = config;
        CurrentFilePath = filePath;
        HasUnsavedChanges = false;
        UnsavedChangesStateChanged?.Invoke(false);

        return config;
    }

    /// <summary>
    /// Replaces sections deserialized as explicit JSON nulls with their default
    /// instances, matching the behavior of absent fields. System.Text.Json assigns
    /// null over the property initializers when the file contains e.g. "settings": null.
    /// </summary>
    /// <param name="config">The configuration to normalize.</param>
    private static void NormalizeConfig(OpcilloscopeConfig config)
    {
        if (string.IsNullOrEmpty(config.Version))
        {
            config.Version = CurrentConfigVersion;
        }

        if (config.Server is null)
        {
            config.Server = new ServerConfig();
        }

        if (config.Server.Authentication is null)
        {
            config.Server.Authentication = new AuthenticationConfig();
        }

        if (config.Settings is null)
        {
            config.Settings = new SubscriptionSettings();
        }

        if (config.MonitoredNodes is null)
        {
            config.MonitoredNodes = new List<MonitoredNodeConfig>();
        }

        if (config.Metadata is null)
        {
            config.Metadata = new ConfigMetadata();
        }
    }

    /// <summary>
    /// Validates a configuration object for common issues.
    /// </summary>
    /// <param name="config">The configuration to validate.</param>
    /// <exception cref="InvalidDataException">Thrown if validation fails.</exception>
    private void ValidateConfiguration(OpcilloscopeConfig config)
    {
        // Backstop null checks with meaningful messages; LoadAsync normalizes
        // explicit JSON nulls before validation, but callers constructing
        // configurations programmatically may still pass null sections.
        if (config.Server is null)
        {
            throw new InvalidDataException("Configuration is missing the 'server' section.");
        }

        if (config.Settings is null)
        {
            throw new InvalidDataException("Configuration is missing the 'settings' section.");
        }

        if (config.MonitoredNodes is null)
        {
            throw new InvalidDataException("Configuration is missing the 'monitoredNodes' section.");
        }

        if (config.Metadata is null)
        {
            throw new InvalidDataException("Configuration is missing the 'metadata' section.");
        }

        var authentication = config.Server.Authentication
            ?? throw new InvalidDataException("Configuration is missing the server authentication section.");
        if (!Enum.TryParse<AuthenticationType>(authentication.Type, ignoreCase: true, out var authType)
            || authType is not AuthenticationType.Anonymous and not AuthenticationType.UserName)
        {
            throw new InvalidDataException(
                $"Unsupported authentication type '{authentication.Type}'. Expected Anonymous or UserName.");
        }

        if (authType == AuthenticationType.UserName && string.IsNullOrWhiteSpace(authentication.Username))
        {
            throw new InvalidDataException("UserName authentication requires a non-empty username.");
        }

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

        // Write atomically: write to a temp file in the same directory, then
        // replace the target so a crash mid-write cannot corrupt the config.
        var tempPath = filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, filePath, overwrite: true);

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
    /// <param name="credentials">Current connection credentials (password is never persisted).</param>
    /// <param name="existingServer">
    /// Optional server config whose security fields should be preserved. When null,
    /// the most recently loaded configuration's server settings are used.
    /// </param>
    /// <param name="existingSettings">
    /// Optional subscription settings whose sampling/queue fields should be preserved.
    /// When null, the most recently loaded configuration's settings are used.
    /// </param>
    /// <returns>A new configuration object representing the current state.</returns>
    public OpcilloscopeConfig CaptureCurrentState(
        string? endpointUrl,
        int publishingInterval,
        IEnumerable<MonitoredNode> monitoredVariables,
        ConfigMetadata? existingMetadata = null,
        ConnectionCredentials? credentials = null,
        ServerConfig? existingServer = null,
        SubscriptionSettings? existingSettings = null)
    {
        // Preserve fields that the UI does not currently surface (security mode/policy,
        // sampling interval, queue size) so a load/save round-trip does not drop them.
        var sourceServer = existingServer ?? _loadedConfig?.Server;
        var sourceSettings = existingSettings ?? _loadedConfig?.Settings;

        var server = new ServerConfig
        {
            EndpointUrl = endpointUrl ?? string.Empty,
            Authentication = new AuthenticationConfig
            {
                Type = (credentials?.Type ?? AuthenticationType.Anonymous).ToString(),
                Username = credentials?.Type == AuthenticationType.UserName ? credentials.Username : null
            }
        };
        if (sourceServer is not null)
        {
            server.SecurityMode = sourceServer.SecurityMode;
            server.SecurityPolicy = sourceServer.SecurityPolicy;
        }

        var settings = new SubscriptionSettings
        {
            PublishingIntervalMs = publishingInterval
        };
        if (sourceSettings is not null)
        {
            settings.SamplingIntervalMs = sourceSettings.SamplingIntervalMs;
            settings.QueueSize = sourceSettings.QueueSize;
        }

        var monitoredNodes = monitoredVariables.Select(m => new MonitoredNodeConfig
        {
            NodeId = m.NodeId.ToString(),
            DisplayName = m.DisplayName,
            Enabled = true
        }).ToList();

        // Preserve disabled entries from the loaded configuration so a load/save
        // round-trip does not silently delete them (only enabled nodes are
        // subscribed on load, so they never appear in the live list). Entries the
        // user actively unsubscribed were enabled in the loaded config and are
        // intentionally dropped.
        if (_loadedConfig?.MonitoredNodes is not null)
        {
            var liveNodeIds = monitoredNodes
                .Select(n => n.NodeId)
                .ToHashSet(StringComparer.Ordinal);

            monitoredNodes.AddRange(_loadedConfig.MonitoredNodes
                .Where(n => !n.Enabled && !liveNodeIds.Contains(n.NodeId))
                .Select(n => new MonitoredNodeConfig
                {
                    NodeId = n.NodeId,
                    DisplayName = n.DisplayName,
                    Enabled = false
                }));
        }

        return new OpcilloscopeConfig
        {
            Server = server,
            Settings = settings,
            MonitoredNodes = monitoredNodes,
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
        _loadedConfig = null;
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
    /// <exception cref="InvalidDataException">
    /// Thrown if the configuration was created by a newer major version of opcilloscope.
    /// </exception>
    private OpcilloscopeConfig MigrateIfNeeded(OpcilloscopeConfig config)
    {
        // Reject files from a newer major version: unknown fields are dropped on
        // deserialization, so loading and re-saving would silently destroy data.
        // Null, empty, or unparseable versions are treated as the current version.
        var version = config.Version;
        if (!string.IsNullOrWhiteSpace(version)
            && int.TryParse(version.Split('.')[0], out var major)
            && major > CurrentConfigMajorVersion)
        {
            throw new InvalidDataException(
                $"Configuration file version '{version}' was created by a newer version of opcilloscope. " +
                $"This build supports configuration version {CurrentConfigVersion}. " +
                "Please upgrade opcilloscope to open this file.");
        }

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
            // (.NET maps SpecialFolder.ApplicationData to Application Support.)
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
    /// The file extension used for configuration files (.cfg).
    /// </summary>
    public const string ConfigFileExtension = ".cfg";

    /// <summary>
    /// Generates a default filename for saving a configuration.
    /// Format: {host}_{port}_{timestamp}.cfg
    /// </summary>
    /// <param name="connectionUrl">The OPC UA connection URL, or null if not connected.</param>
    /// <returns>A sanitized filename with .cfg extension.</returns>
    public static string GenerateDefaultFilename(string? connectionUrl)
    {
        var identifier = ConnectionIdentifier.Generate(connectionUrl);
        return $"{identifier}{ConfigFileExtension}";
    }

    /// <summary>
    /// Ensures a filename has the correct .cfg extension.
    /// </summary>
    /// <param name="filename">The filename to check.</param>
    /// <returns>The filename with .cfg extension.</returns>
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
