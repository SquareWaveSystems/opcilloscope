using Opcilloscope.Configuration;
using Opcilloscope.Configuration.Models;
using Opcilloscope.OpcUa;
using Opcilloscope.Tests.Infrastructure;

namespace Opcilloscope.Tests.Integration;

/// <summary>
/// Integration tests for OPC UA authentication (anonymous and username/password).
/// Test credentials match those configured in TestOpcUaServer.
/// </summary>
public class AuthenticationIntegrationTests : IClassFixture<TestServerFixture>
{
    private const string TestUsername = "testuser";
    private const string TestPassword = "testpass";

    private readonly TestServerFixture _fixture;

    public AuthenticationIntegrationTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConnectAnonymous_Succeeds()
    {
        using var client = new OpcUaClientWrapper();
        var result = await client.ConnectAsync(_fixture.EndpointUrl);
        Assert.True(result);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task ConnectWithValidCredentials_Succeeds()
    {
        var credentials = new ConnectionCredentials(
            AuthenticationType.UserName,
            TestUsername,
            TestPassword);

        using var client = new OpcUaClientWrapper();
        var result = await client.ConnectAsync(_fixture.EndpointUrl, credentials);
        Assert.True(result);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task ConnectWithInvalidPassword_Fails()
    {
        var credentials = new ConnectionCredentials(
            AuthenticationType.UserName,
            TestUsername,
            "wrongpassword");

        using var client = new OpcUaClientWrapper();
        var result = await client.ConnectAsync(_fixture.EndpointUrl, credentials);
        Assert.False(result);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task ConnectWithInvalidUsername_Fails()
    {
        var credentials = new ConnectionCredentials(
            AuthenticationType.UserName,
            "nonexistentuser",
            "somepassword");

        using var client = new OpcUaClientWrapper();
        var result = await client.ConnectAsync(_fixture.EndpointUrl, credentials);
        Assert.False(result);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task ConnectWithCredentials_CanBrowse()
    {
        var credentials = new ConnectionCredentials(
            AuthenticationType.UserName,
            TestUsername,
            TestPassword);

        using var client = new OpcUaClientWrapper();
        await client.ConnectAsync(_fixture.EndpointUrl, credentials);

        var references = await client.BrowseAsync(Opc.Ua.ObjectIds.RootFolder);
        Assert.NotNull(references);
        Assert.True(references.Count > 0);
    }

    [Fact]
    public async Task ConnectWithInvalidPassword_RaisesFriendlyErrorMessage()
    {
        var credentials = new ConnectionCredentials(
            AuthenticationType.UserName,
            TestUsername,
            "wrongpassword");

        using var client = new OpcUaClientWrapper();
        string? errorMessage = null;
        client.ConnectionError += msg => errorMessage = msg;

        await client.ConnectAsync(_fixture.EndpointUrl, credentials);

        Assert.NotNull(errorMessage);
        Assert.Contains("Authentication", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfigDrivenConnection_AnonymousAuth_Succeeds()
    {
        // Simulate loading a config file with anonymous auth and connecting
        var config = new OpcilloscopeConfig
        {
            Server = new ServerConfig
            {
                EndpointUrl = _fixture.EndpointUrl,
                Authentication = new AuthenticationConfig { Type = "Anonymous" }
            },
            Settings = new SubscriptionSettings { PublishingIntervalMs = 250 }
        };

        var authType = ConnectionCredentials.ParseAuthType(config.Server.Authentication.Type);
        Assert.Equal(AuthenticationType.Anonymous, authType);

        using var client = new OpcUaClientWrapper();
        var result = await client.ConnectAsync(config.Server.EndpointUrl);
        Assert.True(result);
    }

    [Fact]
    public async Task ConfigDrivenConnection_UserNameAuth_Succeeds()
    {
        // Simulate loading a config file with UserName auth, then supplying the password
        var config = new OpcilloscopeConfig
        {
            Server = new ServerConfig
            {
                EndpointUrl = _fixture.EndpointUrl,
                Authentication = new AuthenticationConfig
                {
                    Type = "UserName",
                    Username = TestUsername
                }
            },
            Settings = new SubscriptionSettings { PublishingIntervalMs = 250 }
        };

        // Parse auth type from config (same logic as MainWindow.LoadConfigurationAsync)
        var authType = ConnectionCredentials.ParseAuthType(config.Server.Authentication.Type);
        Assert.Equal(AuthenticationType.UserName, authType);

        // Build credentials (password would come from PasswordPromptDialog in the real app)
        var credentials = new ConnectionCredentials(
            AuthenticationType.UserName,
            config.Server.Authentication.Username,
            TestPassword);

        using var client = new OpcUaClientWrapper();
        var result = await client.ConnectAsync(config.Server.EndpointUrl, credentials);
        Assert.True(result);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task ConfigRoundTrip_SaveWithAuth_LoadAndConnect()
    {
        // Full round-trip: capture state with credentials → save → load → connect
        var configService = new ConfigurationService();
        var credentials = new ConnectionCredentials(
            AuthenticationType.UserName,
            TestUsername,
            TestPassword);

        // Capture and save
        var config = configService.CaptureCurrentState(
            _fixture.EndpointUrl,
            250,
            [],
            credentials: credentials);

        var tempFile = Path.Combine(Path.GetTempPath(), $"auth_test_{Guid.NewGuid()}.cfg");
        try
        {
            await configService.SaveAsync(config, tempFile);

            // Load config back
            var loaded = await configService.LoadAsync(tempFile);
            Assert.Equal("UserName", loaded.Server.Authentication.Type);
            Assert.Equal(TestUsername, loaded.Server.Authentication.Username);

            // Connect using loaded config + runtime password
            var authType = ConnectionCredentials.ParseAuthType(loaded.Server.Authentication.Type);
            var loadedCredentials = new ConnectionCredentials(
                authType,
                loaded.Server.Authentication.Username,
                TestPassword); // password supplied at runtime, not from config

            using var client = new OpcUaClientWrapper();
            var result = await client.ConnectAsync(loaded.Server.EndpointUrl, loadedCredentials);
            Assert.True(result);
            Assert.True(client.IsConnected);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
