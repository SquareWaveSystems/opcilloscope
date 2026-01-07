using Opcilloscope.Configuration;
using Opcilloscope.Configuration.Models;
using Opcilloscope.OpcUa.Models;
using Opc.Ua;

namespace Opcilloscope.Tests.Configuration;

/// <summary>
/// Tests for ConfigurationService including save/load roundtrips and validation.
/// </summary>
public class ConfigurationServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigurationService _service;

    public ConfigurationServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"OpcilloscopeTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _service = new ConfigurationService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundtripsConfig()
    {
        // Arrange
        var config = CreateTestConfig();
        var filePath = Path.Combine(_tempDir, "test.opcilloscope");

        // Act
        await _service.SaveAsync(config, filePath);
        var loaded = await _service.LoadAsync(filePath);

        // Assert
        Assert.Equal(config.Version, loaded.Version);
        Assert.Equal(config.Server.EndpointUrl, loaded.Server.EndpointUrl);
        Assert.Equal(config.Server.SecurityMode, loaded.Server.SecurityMode);
        Assert.Equal(config.Settings.PublishingIntervalMs, loaded.Settings.PublishingIntervalMs);
        Assert.Equal(config.Settings.SamplingIntervalMs, loaded.Settings.SamplingIntervalMs);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_PreservesMonitoredNodes()
    {
        // Arrange
        var config = CreateTestConfig();
        config.MonitoredNodes = new List<MonitoredNodeConfig>
        {
            new() { NodeId = "ns=2;s=Counter", DisplayName = "Counter", Enabled = true },
            new() { NodeId = "ns=2;s=SineWave", DisplayName = "SineWave", Enabled = true },
            new() { NodeId = "ns=2;s=Disabled", DisplayName = "Disabled", Enabled = false }
        };
        var filePath = Path.Combine(_tempDir, "nodes.opcilloscope");

        // Act
        await _service.SaveAsync(config, filePath);
        var loaded = await _service.LoadAsync(filePath);

        // Assert
        Assert.Equal(3, loaded.MonitoredNodes.Count);
        Assert.Equal("ns=2;s=Counter", loaded.MonitoredNodes[0].NodeId);
        Assert.Equal("Counter", loaded.MonitoredNodes[0].DisplayName);
        Assert.True(loaded.MonitoredNodes[0].Enabled);
        Assert.False(loaded.MonitoredNodes[2].Enabled);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_PreservesMetadata()
    {
        // Arrange
        var config = CreateTestConfig();
        config.Metadata = new ConfigMetadata
        {
            Name = "Production Config",
            Description = "Monitoring production server",
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var filePath = Path.Combine(_tempDir, "meta.opcilloscope");

        // Act
        await _service.SaveAsync(config, filePath);
        var loaded = await _service.LoadAsync(filePath);

        // Assert
        Assert.Equal("Production Config", loaded.Metadata.Name);
        Assert.Equal("Monitoring production server", loaded.Metadata.Description);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), loaded.Metadata.CreatedAt);
    }

    [Fact]
    public async Task SaveAsync_UpdatesLastModified()
    {
        // Arrange
        var config = CreateTestConfig();
        config.Metadata.LastModified = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var filePath = Path.Combine(_tempDir, "modified.opcilloscope");
        var beforeSave = DateTime.UtcNow;

        // Act
        await _service.SaveAsync(config, filePath);
        var loaded = await _service.LoadAsync(filePath);

        // Assert
        Assert.True(loaded.Metadata.LastModified >= beforeSave);
    }

    [Fact]
    public async Task SaveAsync_SetsCurrentFilePath()
    {
        // Arrange
        var config = CreateTestConfig();
        var filePath = Path.Combine(_tempDir, "path.opcilloscope");

        // Act
        await _service.SaveAsync(config, filePath);

        // Assert
        Assert.Equal(filePath, _service.CurrentFilePath);
    }

    [Fact]
    public async Task SaveAsync_ClearsUnsavedChanges()
    {
        // Arrange
        var config = CreateTestConfig();
        var filePath = Path.Combine(_tempDir, "clean.opcilloscope");
        _service.MarkDirty();
        Assert.True(_service.HasUnsavedChanges);

        // Act
        await _service.SaveAsync(config, filePath);

        // Assert
        Assert.False(_service.HasUnsavedChanges);
    }

    [Fact]
    public async Task LoadAsync_SetsCurrentFilePath()
    {
        // Arrange
        var config = CreateTestConfig();
        var filePath = Path.Combine(_tempDir, "load.opcilloscope");
        await _service.SaveAsync(config, filePath);
        _service.Reset();

        // Act
        await _service.LoadAsync(filePath);

        // Assert
        Assert.Equal(filePath, _service.CurrentFilePath);
    }

    [Fact]
    public async Task LoadAsync_ClearsUnsavedChanges()
    {
        // Arrange
        var config = CreateTestConfig();
        var filePath = Path.Combine(_tempDir, "loadclean.opcilloscope");
        await _service.SaveAsync(config, filePath);
        _service.MarkDirty();

        // Act
        await _service.LoadAsync(filePath);

        // Assert
        Assert.False(_service.HasUnsavedChanges);
    }

    [Fact]
    public async Task LoadAsync_InvalidFile_ThrowsInvalidDataException()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "invalid.opcilloscope");
        await File.WriteAllTextAsync(filePath, "not valid json");

        // Act & Assert
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => _service.LoadAsync(filePath));
    }

    [Fact]
    public async Task LoadAsync_NegativePublishingInterval_ThrowsInvalidDataException()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "negative.opcilloscope");
        var json = """
        {
            "version": "1.0",
            "server": { "endpointUrl": "opc.tcp://localhost:4840" },
            "settings": { "publishingIntervalMs": -100 },
            "monitoredNodes": [],
            "metadata": {}
        }
        """;
        await File.WriteAllTextAsync(filePath, json);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(
            () => _service.LoadAsync(filePath));
    }

    [Fact]
    public async Task LoadAsync_EmptyNodeId_ThrowsInvalidDataException()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "emptynode.opcilloscope");
        var json = """
        {
            "version": "1.0",
            "server": { "endpointUrl": "opc.tcp://localhost:4840" },
            "settings": { "publishingIntervalMs": 1000 },
            "monitoredNodes": [
                { "nodeId": "", "displayName": "Bad Node", "enabled": true }
            ],
            "metadata": {}
        }
        """;
        await File.WriteAllTextAsync(filePath, json);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(
            () => _service.LoadAsync(filePath));
    }

    [Fact]
    public async Task LoadAsync_NonexistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "nonexistent.opcilloscope");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _service.LoadAsync(filePath));
    }

    [Fact]
    public void MarkDirty_SetsHasUnsavedChanges()
    {
        // Arrange
        Assert.False(_service.HasUnsavedChanges);

        // Act
        _service.MarkDirty();

        // Assert
        Assert.True(_service.HasUnsavedChanges);
    }

    [Fact]
    public void MarkDirty_FiresEvent()
    {
        // Arrange
        bool? eventValue = null;
        _service.UnsavedChangesStateChanged += v => eventValue = v;

        // Act
        _service.MarkDirty();

        // Assert
        Assert.True(eventValue);
    }

    [Fact]
    public void MarkClean_ClearsHasUnsavedChanges()
    {
        // Arrange
        _service.MarkDirty();
        Assert.True(_service.HasUnsavedChanges);

        // Act
        _service.MarkClean();

        // Assert
        Assert.False(_service.HasUnsavedChanges);
    }

    [Fact]
    public void MarkClean_FiresEvent()
    {
        // Arrange
        _service.MarkDirty();
        bool? eventValue = null;
        _service.UnsavedChangesStateChanged += v => eventValue = v;

        // Act
        _service.MarkClean();

        // Assert
        Assert.False(eventValue);
    }

    [Fact]
    public void Reset_ClearsCurrentFilePath()
    {
        // Arrange
        _service.GetType().GetProperty("CurrentFilePath")!
            .SetValue(_service, "/some/path.opcilloscope");

        // Act
        _service.Reset();

        // Assert
        Assert.Null(_service.CurrentFilePath);
    }

    [Fact]
    public void Reset_ClearsUnsavedChanges()
    {
        // Arrange
        _service.MarkDirty();

        // Act
        _service.Reset();

        // Assert
        Assert.False(_service.HasUnsavedChanges);
    }

    [Fact]
    public void GetDisplayName_NoFile_ReturnsUntitled()
    {
        // Act
        var name = _service.GetDisplayName();

        // Assert
        Assert.Equal("untitled", name);
    }

    [Fact]
    public async Task GetDisplayName_WithFile_ReturnsFileName()
    {
        // Arrange
        var config = CreateTestConfig();
        var filePath = Path.Combine(_tempDir, "myconfig.opcilloscope");
        await _service.SaveAsync(config, filePath);

        // Act
        var name = _service.GetDisplayName();

        // Assert
        Assert.Equal("myconfig", name);
    }

    [Fact]
    public void CaptureCurrentState_CreatesConfigFromState()
    {
        // Arrange
        var endpoint = "opc.tcp://localhost:4840";
        var publishingInterval = 2000;
        var monitoredVariables = new List<MonitoredNode>
        {
            new() { NodeId = new NodeId("Counter", 2), DisplayName = "Counter" },
            new() { NodeId = new NodeId("SineWave", 2), DisplayName = "SineWave" }
        };

        // Act
        var config = _service.CaptureCurrentState(endpoint, publishingInterval, monitoredVariables);

        // Assert
        Assert.Equal(endpoint, config.Server.EndpointUrl);
        Assert.Equal(publishingInterval, config.Settings.PublishingIntervalMs);
        Assert.Equal(2, config.MonitoredNodes.Count);
        Assert.Equal("ns=2;s=Counter", config.MonitoredNodes[0].NodeId);
        Assert.Equal("Counter", config.MonitoredNodes[0].DisplayName);
    }

    [Fact]
    public void CaptureCurrentState_WithExistingMetadata_PreservesMetadata()
    {
        // Arrange
        var existingMetadata = new ConfigMetadata
        {
            Name = "Existing Name",
            Description = "Existing Description",
            CreatedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var config = _service.CaptureCurrentState(
            "opc.tcp://localhost:4840",
            1000,
            new List<MonitoredNode>(),
            existingMetadata);

        // Assert
        Assert.Equal("Existing Name", config.Metadata.Name);
        Assert.Equal("Existing Description", config.Metadata.Description);
        Assert.Equal(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), config.Metadata.CreatedAt);
    }

    [Fact]
    public void CaptureCurrentState_WithNullEndpoint_UsesEmptyString()
    {
        // Act
        var config = _service.CaptureCurrentState(null, 1000, new List<MonitoredNode>());

        // Assert
        Assert.Equal(string.Empty, config.Server.EndpointUrl);
    }

    [Fact]
    public void GetDefaultConfigDirectory_ReturnsValidPath()
    {
        // Act
        var dir = ConfigurationService.GetDefaultConfigDirectory();

        // Assert
        Assert.NotNull(dir);
        Assert.Contains("configs", dir);
        // Should contain opcilloscope (all platforms use lowercase)
        Assert.Contains("opcilloscope", dir);
    }

    [Fact]
    public void GetDefaultConfigDirectory_CreatesDirectory()
    {
        // Act
        var dir = ConfigurationService.GetDefaultConfigDirectory();

        // Assert
        Assert.True(Directory.Exists(dir));
    }

    #region Filename Generation Tests

    [Fact]
    public void SanitizeUrlForFilename_RemovesOpcTcpProtocol()
    {
        // Act
        var result = ConfigurationService.SanitizeUrlForFilename("opc.tcp://localhost:4840");

        // Assert
        Assert.Equal("localhost_4840", result);
    }

    [Fact]
    public void SanitizeUrlForFilename_HandlesIpAddress()
    {
        // Act
        var result = ConfigurationService.SanitizeUrlForFilename("opc.tcp://192.168.1.100:4840");

        // Assert
        Assert.Equal("192.168.1.100_4840", result);
    }

    [Fact]
    public void SanitizeUrlForFilename_RemovesHttpsProtocol()
    {
        // Act
        var result = ConfigurationService.SanitizeUrlForFilename("opc.https://server.example.com:443");

        // Assert
        Assert.Equal("server.example.com_443", result);
    }

    [Fact]
    public void SanitizeUrlForFilename_HandlesPathComponents()
    {
        // Act
        var result = ConfigurationService.SanitizeUrlForFilename("opc.tcp://server:4840/UA/MyServer");

        // Assert
        Assert.Equal("server_4840_UA_MyServer", result);
    }

    [Fact]
    public void SanitizeUrlForFilename_RemovesConsecutiveUnderscores()
    {
        // Act
        var result = ConfigurationService.SanitizeUrlForFilename("opc.tcp://server:4840//path");

        // Assert
        Assert.DoesNotContain("__", result);
    }

    [Fact]
    public void SanitizeUrlForFilename_TruncatesLongUrls()
    {
        // Arrange
        var longUrl = "opc.tcp://" + new string('a', 100) + ".example.com:4840";

        // Act
        var result = ConfigurationService.SanitizeUrlForFilename(longUrl);

        // Assert
        Assert.True(result.Length <= 50, $"Result should be <= 50 chars, was {result.Length}");
    }

    [Fact]
    public void SanitizeUrlForFilename_HandlesEmptyString()
    {
        // Act
        var result = ConfigurationService.SanitizeUrlForFilename("");

        // Assert
        Assert.Equal("unknown", result);
    }

    [Fact]
    public void SanitizeUrlForFilename_HandlesNull()
    {
        // Act
        var result = ConfigurationService.SanitizeUrlForFilename(null!);

        // Assert
        Assert.Equal("unknown", result);
    }

    [Fact]
    public void GenerateDefaultFilename_WithConnectionUrl_ContainsUrlAndTimestamp()
    {
        // Act
        var result = ConfigurationService.GenerateDefaultFilename("opc.tcp://192.168.1.100:4840");

        // Assert
        Assert.Contains("192.168.1.100_4840", result);
        Assert.EndsWith(ConfigurationService.ConfigFileExtension, result);
        // Should contain a timestamp pattern (yyyyMMddHHmm)
        Assert.Matches(@"\d{12}\.cfg$", result);
    }

    [Fact]
    public void GenerateDefaultFilename_WithNullUrl_UsesDefaultName()
    {
        // Act
        var result = ConfigurationService.GenerateDefaultFilename(null);

        // Assert
        Assert.StartsWith("config_", result);
        Assert.EndsWith(ConfigurationService.ConfigFileExtension, result);
    }

    [Fact]
    public void GenerateDefaultFilename_WithEmptyUrl_UsesDefaultName()
    {
        // Act
        var result = ConfigurationService.GenerateDefaultFilename("");

        // Assert
        Assert.StartsWith("config_", result);
        Assert.EndsWith(ConfigurationService.ConfigFileExtension, result);
    }

    [Fact]
    public void EnsureConfigExtension_AddsExtensionWhenMissing()
    {
        // Act
        var result = ConfigurationService.EnsureConfigExtension("myconfig");

        // Assert
        Assert.Equal("myconfig.cfg", result);
    }

    [Fact]
    public void EnsureConfigExtension_DoesNotDuplicateExtension()
    {
        // Act
        var result = ConfigurationService.EnsureConfigExtension("myconfig.cfg");

        // Assert
        Assert.Equal("myconfig.cfg", result);
    }

    [Fact]
    public void EnsureConfigExtension_IsCaseInsensitive()
    {
        // Act
        var result = ConfigurationService.EnsureConfigExtension("myconfig.CFG");

        // Assert
        Assert.Equal("myconfig.CFG", result);
    }

    [Fact]
    public void EnsureConfigExtension_HandlesEmptyString()
    {
        // Act
        var result = ConfigurationService.EnsureConfigExtension("");

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void ConfigFileExtension_IsCfg()
    {
        // Assert
        Assert.Equal(".cfg", ConfigurationService.ConfigFileExtension);
    }

    #endregion

    private static OpcilloscopeConfig CreateTestConfig()
    {
        return new OpcilloscopeConfig
        {
            Version = "1.0",
            Server = new ServerConfig
            {
                EndpointUrl = "opc.tcp://localhost:4840",
                SecurityMode = "None"
            },
            Settings = new SubscriptionSettings
            {
                PublishingIntervalMs = 1000,
                SamplingIntervalMs = 500
            },
            MonitoredNodes = new List<MonitoredNodeConfig>(),
            Metadata = new ConfigMetadata
            {
                Name = "Test Config",
                Description = "Test description"
            }
        };
    }
}
