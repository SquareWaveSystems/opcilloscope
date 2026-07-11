using Opcilloscope.OpcUa;

namespace Opcilloscope.Tests.OpcUa;

public class ConnectionCredentialsTests
{
    [Theory]
    [InlineData("Anonymous", AuthenticationType.Anonymous)]
    [InlineData("anonymous", AuthenticationType.Anonymous)]
    [InlineData("UserName", AuthenticationType.UserName)]
    [InlineData("username", AuthenticationType.UserName)]
    public void ParseAuthType_KnownValue_ReturnsExpectedType(
        string value,
        AuthenticationType expected)
    {
        Assert.Equal(expected, ConnectionCredentials.ParseAuthType(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Certificate")]
    [InlineData("future-auth-mode")]
    public void ParseAuthType_UnknownValue_ThrowsInsteadOfDowngradingToAnonymous(string? value)
    {
        Assert.Throws<FormatException>(() => ConnectionCredentials.ParseAuthType(value));
    }
}
