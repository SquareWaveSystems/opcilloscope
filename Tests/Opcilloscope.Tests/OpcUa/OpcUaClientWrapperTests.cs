using System.Reflection;
using Moq;
using Opc.Ua;
using Opc.Ua.Client;
using Opcilloscope.OpcUa;
using Opcilloscope.Tests.Infrastructure;

namespace Opcilloscope.Tests.OpcUa;

public class OpcUaClientWrapperTests
{
    [Fact]
    public void FilterEndpointCandidates_AutoSecurity_RemovesNoneAndRequiresSecurity()
    {
        var endpoints = new EndpointDescriptionCollection
        {
            CreateEndpoint(MessageSecurityMode.None, SecurityPolicies.None),
            CreateEndpoint(MessageSecurityMode.SignAndEncrypt, SecurityPolicies.Basic256Sha256)
        };

        var result = OpcUaClientWrapper.FilterEndpointCandidates(
            endpoints,
            AuthenticationType.Anonymous,
            securityMode: null,
            securityPolicy: null,
            endpointUrl: "opc.tcp://server:4840");

        var selected = Assert.Single(result.Candidates);
        Assert.Equal(MessageSecurityMode.SignAndEncrypt, selected.SecurityMode);
        Assert.True(result.UseSecurity);
    }

    [Fact]
    public void FilterEndpointCandidates_AutoSecurity_NoneOnlyServerFailsClosed()
    {
        var endpoints = new EndpointDescriptionCollection
        {
            CreateEndpoint(MessageSecurityMode.None, SecurityPolicies.None)
        };

        var error = Assert.Throws<ServiceResultException>(() =>
            OpcUaClientWrapper.FilterEndpointCandidates(
                endpoints,
                AuthenticationType.Anonymous,
                securityMode: null,
                securityPolicy: null,
                endpointUrl: "opc.tcp://server:4840"));

        Assert.Contains("no SignAndEncrypt", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FilterEndpointCandidates_ExplicitNone_AllowsNoneOnlyServer()
    {
        var endpoints = new EndpointDescriptionCollection
        {
            CreateEndpoint(MessageSecurityMode.None, SecurityPolicies.None)
        };

        var result = OpcUaClientWrapper.FilterEndpointCandidates(
            endpoints,
            AuthenticationType.Anonymous,
            nameof(MessageSecurityMode.None),
            SecurityPolicies.None,
            "opc.tcp://server:4840");

        var selected = Assert.Single(result.Candidates);
        Assert.Equal(MessageSecurityMode.None, selected.SecurityMode);
        Assert.False(result.UseSecurity);
    }

    [Fact]
    public void FilterEndpointCandidates_NonePolicyWithoutExplicitNoneMode_FailsClosed()
    {
        var endpoints = new EndpointDescriptionCollection
        {
            CreateEndpoint(MessageSecurityMode.None, SecurityPolicies.None)
        };

        var error = Assert.Throws<ServiceResultException>(() =>
            OpcUaClientWrapper.FilterEndpointCandidates(
                endpoints,
                AuthenticationType.Anonymous,
                securityMode: null,
                securityPolicy: SecurityPolicies.None,
                endpointUrl: "opc.tcp://server:4840"));

        Assert.Contains("SecurityMode=None", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FilterEndpointCandidates_AutoSecurity_SignOnlyServerFailsClosed()
    {
        var endpoints = new EndpointDescriptionCollection
        {
            CreateEndpoint(MessageSecurityMode.Sign, SecurityPolicies.Basic256Sha256)
        };

        var error = Assert.Throws<ServiceResultException>(() =>
            OpcUaClientWrapper.FilterEndpointCandidates(
                endpoints,
                AuthenticationType.Anonymous,
                securityMode: null,
                securityPolicy: null,
                endpointUrl: "opc.tcp://server:4840"));

        Assert.Contains("no SignAndEncrypt", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FilterEndpointCandidates_ExplicitSign_AllowsSignOnlyServer()
    {
        var endpoints = new EndpointDescriptionCollection
        {
            CreateEndpoint(MessageSecurityMode.Sign, SecurityPolicies.Basic256Sha256)
        };

        var result = OpcUaClientWrapper.FilterEndpointCandidates(
            endpoints,
            AuthenticationType.Anonymous,
            nameof(MessageSecurityMode.Sign),
            SecurityPolicies.Basic256Sha256,
            "opc.tcp://server:4840");

        var selected = Assert.Single(result.Candidates);
        Assert.Equal(MessageSecurityMode.Sign, selected.SecurityMode);
        Assert.True(result.UseSecurity);
    }

    [Fact]
    public void FilterEndpointCandidates_Anonymous_RemovesEndpointsWithoutAnonymousPolicy()
    {
        var userNameOnly = CreateEndpoint(
            MessageSecurityMode.SignAndEncrypt,
            SecurityPolicies.Basic256Sha256,
            CreateUserTokenPolicy("username", UserTokenType.UserName));
        var anonymous = CreateEndpoint(
            MessageSecurityMode.SignAndEncrypt,
            SecurityPolicies.Basic256Sha256,
            CreateUserTokenPolicy("anonymous", UserTokenType.Anonymous));
        var endpoints = new EndpointDescriptionCollection { userNameOnly, anonymous };

        var result = OpcUaClientWrapper.FilterEndpointCandidates(
            endpoints,
            AuthenticationType.Anonymous,
            securityMode: null,
            securityPolicy: null,
            endpointUrl: "opc.tcp://server:4840");

        var selected = Assert.Single(result.Candidates);
        Assert.Equal("anonymous", Assert.Single(selected.UserIdentityTokens).PolicyId);
    }

    [Fact]
    public void FilterEndpointCandidates_UserName_RemovesEndpointsWithoutUserNamePolicy()
    {
        var anonymousOnly = CreateEndpoint(
            MessageSecurityMode.SignAndEncrypt,
            SecurityPolicies.Basic256Sha256,
            CreateUserTokenPolicy("anonymous", UserTokenType.Anonymous));
        var userName = CreateEndpoint(
            MessageSecurityMode.SignAndEncrypt,
            SecurityPolicies.Basic256Sha256,
            CreateUserTokenPolicy("username", UserTokenType.UserName));
        var endpoints = new EndpointDescriptionCollection { anonymousOnly, userName };

        var result = OpcUaClientWrapper.FilterEndpointCandidates(
            endpoints,
            AuthenticationType.UserName,
            securityMode: null,
            securityPolicy: null,
            endpointUrl: "opc.tcp://server:4840");

        var selected = Assert.Single(result.Candidates);
        Assert.Equal("username", Assert.Single(selected.UserIdentityTokens).PolicyId);
    }

    [Theory]
    [InlineData(AuthenticationType.Anonymous, UserTokenType.UserName)]
    [InlineData(AuthenticationType.UserName, UserTokenType.Anonymous)]
    public void FilterEndpointCandidates_NoMatchingIdentityPolicyFailsClosed(
        AuthenticationType authenticationType,
        UserTokenType offeredTokenType)
    {
        var endpoints = new EndpointDescriptionCollection
        {
            CreateEndpoint(
                MessageSecurityMode.SignAndEncrypt,
                SecurityPolicies.Basic256Sha256,
                CreateUserTokenPolicy("wrong-type", offeredTokenType))
        };

        var error = Assert.Throws<ServiceResultException>(() =>
            OpcUaClientWrapper.FilterEndpointCandidates(
                endpoints,
                authenticationType,
                securityMode: null,
                securityPolicy: null,
                endpointUrl: "opc.tcp://server:4840"));

        Assert.True(error.StatusCode == StatusCodes.BadIdentityTokenRejected);
        Assert.Contains(authenticationType.ToString(), error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FilterEndpointCandidates_UserNameSign_KeepsOnlyEncryptedUserTokenPolicies()
    {
        var endpoint = CreateEndpoint(
            MessageSecurityMode.Sign,
            SecurityPolicies.Basic256Sha256,
            CreateUserTokenPolicy("plaintext", UserTokenType.UserName, SecurityPolicies.None),
            CreateUserTokenPolicy("encrypted", UserTokenType.UserName, SecurityPolicies.Basic256Sha256));
        var endpoints = new EndpointDescriptionCollection { endpoint };

        var result = OpcUaClientWrapper.FilterEndpointCandidates(
            endpoints,
            AuthenticationType.UserName,
            nameof(MessageSecurityMode.Sign),
            SecurityPolicies.Basic256Sha256,
            "opc.tcp://server:4840");

        var selected = Assert.Single(result.Candidates);
        var tokenPolicy = Assert.Single(selected.UserIdentityTokens);
        Assert.Equal("encrypted", tokenPolicy.PolicyId);
        Assert.Equal(2, endpoint.UserIdentityTokens.Count);
    }

    [Fact]
    public void FilterEndpointCandidates_UserNameSign_NoneUserTokenPolicyOnlyFailsClosed()
    {
        var endpoints = new EndpointDescriptionCollection
        {
            CreateEndpoint(
                MessageSecurityMode.Sign,
                SecurityPolicies.Basic256Sha256,
                CreateUserTokenPolicy("plaintext", UserTokenType.UserName, SecurityPolicies.None))
        };

        var error = Assert.Throws<ServiceResultException>(() =>
            OpcUaClientWrapper.FilterEndpointCandidates(
                endpoints,
                AuthenticationType.UserName,
                nameof(MessageSecurityMode.Sign),
                SecurityPolicies.Basic256Sha256,
                "opc.tcp://server:4840"));

        Assert.True(error.StatusCode == StatusCodes.BadIdentityTokenRejected);
        Assert.Contains("compatible UserName", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SignAndEncrypt", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FilterEndpointCandidates_UserNameSign_EmptyUserTokenPolicyInheritsEndpointPolicy()
    {
        var endpoints = new EndpointDescriptionCollection
        {
            CreateEndpoint(
                MessageSecurityMode.Sign,
                SecurityPolicies.Basic256Sha256,
                CreateUserTokenPolicy("inherited", UserTokenType.UserName))
        };

        var result = OpcUaClientWrapper.FilterEndpointCandidates(
            endpoints,
            AuthenticationType.UserName,
            nameof(MessageSecurityMode.Sign),
            SecurityPolicies.Basic256Sha256,
            "opc.tcp://server:4840");

        Assert.Equal("inherited", Assert.Single(Assert.Single(result.Candidates).UserIdentityTokens).PolicyId);
    }

    [Fact]
    public void FilterEndpointCandidates_UserNameSignAndEncrypt_AllowsNoneUserTokenPolicy()
    {
        var endpoints = new EndpointDescriptionCollection
        {
            CreateEndpoint(
                MessageSecurityMode.SignAndEncrypt,
                SecurityPolicies.Basic256Sha256,
                CreateUserTokenPolicy("channel-encrypted", UserTokenType.UserName, SecurityPolicies.None))
        };

        var result = OpcUaClientWrapper.FilterEndpointCandidates(
            endpoints,
            AuthenticationType.UserName,
            nameof(MessageSecurityMode.SignAndEncrypt),
            SecurityPolicies.Basic256Sha256,
            "opc.tcp://server:4840");

        Assert.Equal(
            "channel-encrypted",
            Assert.Single(Assert.Single(result.Candidates).UserIdentityTokens).PolicyId);
    }

    [Theory]
    [InlineData("None")]
    [InlineData("urn:example:unsupported-security-policy")]
    public void FilterEndpointCandidates_UserName_InvalidUserTokenSecurityPolicyFailsClosed(
        string tokenSecurityPolicy)
    {
        var endpoints = new EndpointDescriptionCollection
        {
            CreateEndpoint(
                MessageSecurityMode.SignAndEncrypt,
                SecurityPolicies.Basic256Sha256,
                CreateUserTokenPolicy(
                    "unsupported",
                    UserTokenType.UserName,
                    tokenSecurityPolicy))
        };

        var error = Assert.Throws<ServiceResultException>(() =>
            OpcUaClientWrapper.FilterEndpointCandidates(
                endpoints,
                AuthenticationType.UserName,
                securityMode: null,
                securityPolicy: null,
                endpointUrl: "opc.tcp://server:4840"));

        Assert.True(error.StatusCode == StatusCodes.BadIdentityTokenRejected);
    }

    [Fact]
    public void FilterEndpointCandidates_UserNameEncryptedToken_MissingServerCertificateFailsClosed()
    {
        var endpoint = CreateEndpoint(
            MessageSecurityMode.SignAndEncrypt,
            SecurityPolicies.Basic256Sha256,
            CreateUserTokenPolicy("encrypted", UserTokenType.UserName, SecurityPolicies.Basic256Sha256));
        endpoint.ServerCertificate = default;
        var endpoints = new EndpointDescriptionCollection { endpoint };

        var error = Assert.Throws<ServiceResultException>(() =>
            OpcUaClientWrapper.FilterEndpointCandidates(
                endpoints,
                AuthenticationType.UserName,
                securityMode: null,
                securityPolicy: null,
                endpointUrl: "opc.tcp://server:4840"));

        Assert.True(error.StatusCode == StatusCodes.BadIdentityTokenRejected);
    }

    [Fact]
    public async Task DisconnectAsync_WhenCloseThrows_StillDisposesDetachedSession()
    {
        var session = new Mock<ISession>(MockBehavior.Loose);
        session.Setup(candidate => candidate.CloseAsync())
            .ThrowsAsync(new InvalidOperationException("close failed"));

        using var client = new OpcUaClientWrapper();
        typeof(OpcUaClientWrapper)
            .GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(client, session.Object);

        await client.DisconnectAsync();

        session.Verify(candidate => candidate.Dispose(), Times.Once);
        Assert.Null(client.Session);
    }

    [Fact]
    public async Task DisconnectAsync_WhenKeepAliveRemovalThrows_StillClosesAndDisposesSession()
    {
        var session = new Mock<ISession>(MockBehavior.Loose);
        session.SetupRemove(
                candidate => candidate.KeepAlive -= It.IsAny<KeepAliveEventHandler>())
            .Throws(new InvalidOperationException("event removal failed"));
        session.Setup(candidate => candidate.CloseAsync())
            .Returns(Task.FromResult((Opc.Ua.StatusCode)Opc.Ua.StatusCodes.Good));

        using var client = new OpcUaClientWrapper();
        typeof(OpcUaClientWrapper)
            .GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(client, session.Object);

        await client.DisconnectAsync();

        session.Verify(candidate => candidate.CloseAsync(), Times.Once);
        session.Verify(candidate => candidate.Dispose(), Times.Once);
        Assert.Null(client.Session);
    }

    [Fact]
    public void IsCurrentSessionCallback_RejectsDetachedSession()
    {
        var detached = new Mock<ISession>().Object;
        var current = new Mock<ISession>().Object;
        using var client = new OpcUaClientWrapper();
        typeof(OpcUaClientWrapper)
            .GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(client, current);

        Assert.False(client.IsCurrentSessionCallback(detached));
        Assert.True(client.IsCurrentSessionCallback(current));
    }

    private static EndpointDescription CreateEndpoint(
        MessageSecurityMode securityMode,
        string securityPolicy,
        params UserTokenPolicy[] userIdentityTokens) => new()
        {
            EndpointUrl = "opc.tcp://server:4840",
            SecurityMode = securityMode,
            SecurityPolicyUri = securityPolicy,
            ServerCertificate = new byte[] { 0x01 },
            UserIdentityTokens = userIdentityTokens.Length == 0
                ? new UserTokenPolicyCollection
                {
                    CreateUserTokenPolicy("anonymous", UserTokenType.Anonymous),
                    CreateUserTokenPolicy("username", UserTokenType.UserName)
                }
                : new UserTokenPolicyCollection(userIdentityTokens)
        };

    private static UserTokenPolicy CreateUserTokenPolicy(
        string policyId,
        UserTokenType tokenType,
        string? securityPolicy = null) => new(tokenType)
        {
            PolicyId = policyId,
            SecurityPolicyUri = securityPolicy
        };
}

[Collection("TestServer")]
public class OpcUaClientWrapperLifecycleTests
{
    private readonly TestServerFixture _fixture;

    public OpcUaClientWrapperLifecycleTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DisconnectAsync_QueuedBehindConnect_CannotBeOvertakenByLateSessionCreation()
    {
        using var client = new OpcUaClientWrapper(allowInsecure: true);

        var connectTask = client.ConnectAsync(_fixture.EndpointUrl);
        var disconnectTask = client.DisconnectAsync();

        var connected = await connectTask;
        await disconnectTask;

        Assert.True(connected);
        Assert.False(client.IsConnected);
        Assert.Null(client.Session);
    }
}
