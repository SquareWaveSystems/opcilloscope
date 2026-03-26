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
}
