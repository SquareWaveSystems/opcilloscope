using Opcilloscope.Utilities;

namespace Opcilloscope.Tests.Utilities;

public class ConnectionIdentifierTests
{
    [Fact]
    public void Generate_WithOpcTcpUrl_ReturnsHostPortTimestamp()
    {
        // Arrange
        var timestamp = new DateTime(2026, 1, 7, 12, 34, 0);

        // Act
        var result = ConnectionIdentifier.Generate("opc.tcp://192.168.1.67:50000", timestamp);

        // Assert
        Assert.Equal("192.168.1.67_50000_202601071234", result);
    }

    [Fact]
    public void Generate_WithLocalhost_ReturnsHostPortTimestamp()
    {
        // Arrange
        var timestamp = new DateTime(2026, 1, 7, 12, 34, 0);

        // Act
        var result = ConnectionIdentifier.Generate("opc.tcp://localhost:4840", timestamp);

        // Assert
        Assert.Equal("localhost_4840_202601071234", result);
    }

    [Fact]
    public void Generate_WithPath_IgnoresPath()
    {
        // Arrange
        var timestamp = new DateTime(2026, 1, 7, 12, 34, 0);

        // Act
        var result = ConnectionIdentifier.Generate("opc.tcp://server:4840/UA/MyServer", timestamp);

        // Assert
        Assert.Equal("server_4840_202601071234", result);
    }

    [Fact]
    public void Generate_WithNullUrl_ReturnsConfigPrefix()
    {
        // Arrange
        var timestamp = new DateTime(2026, 1, 7, 12, 34, 0);

        // Act
        var result = ConnectionIdentifier.Generate(null, timestamp);

        // Assert
        Assert.Equal("config_202601071234", result);
    }

    [Fact]
    public void Generate_WithEmptyUrl_ReturnsConfigPrefix()
    {
        // Arrange
        var timestamp = new DateTime(2026, 1, 7, 12, 34, 0);

        // Act
        var result = ConnectionIdentifier.Generate("", timestamp);

        // Assert
        Assert.Equal("config_202601071234", result);
    }

    [Fact]
    public void Generate_WithCustomTimestampFormat_UsesFormat()
    {
        // Arrange
        var timestamp = new DateTime(2026, 1, 7, 12, 34, 56);

        // Act
        var result = ConnectionIdentifier.Generate("opc.tcp://localhost:4840", timestamp, "yyyyMMddHHmmss");

        // Assert
        Assert.Equal("localhost_4840_20260107123456", result);
    }

    [Fact]
    public void ExtractHostPort_WithOpcTcpProtocol_RemovesProtocol()
    {
        // Act
        var result = ConnectionIdentifier.ExtractHostPort("opc.tcp://192.168.1.67:50000");

        // Assert
        Assert.Equal("192.168.1.67_50000", result);
    }

    [Fact]
    public void ExtractHostPort_WithOpcHttpsProtocol_RemovesProtocol()
    {
        // Act
        var result = ConnectionIdentifier.ExtractHostPort("opc.https://server.example.com:443");

        // Assert
        Assert.Equal("server.example.com_443", result);
    }

    [Fact]
    public void ExtractHostPort_WithHttpsProtocol_RemovesProtocol()
    {
        // Act
        var result = ConnectionIdentifier.ExtractHostPort("https://server.example.com:443");

        // Assert
        Assert.Equal("server.example.com_443", result);
    }

    [Fact]
    public void ExtractHostPort_WithPath_IgnoresPath()
    {
        // Act
        var result = ConnectionIdentifier.ExtractHostPort("opc.tcp://server:4840/UA/MyServer");

        // Assert
        Assert.Equal("server_4840", result);
    }

    [Fact]
    public void ExtractHostPort_WithNoPort_ReturnsHostOnly()
    {
        // Act
        var result = ConnectionIdentifier.ExtractHostPort("opc.tcp://server/path");

        // Assert
        Assert.Equal("server", result);
    }

    [Fact]
    public void ExtractHostPort_WithNullUrl_ReturnsUnknown()
    {
        // Act
        var result = ConnectionIdentifier.ExtractHostPort(null);

        // Assert
        Assert.Equal("unknown", result);
    }

    [Fact]
    public void ExtractHostPort_WithEmptyUrl_ReturnsUnknown()
    {
        // Act
        var result = ConnectionIdentifier.ExtractHostPort("");

        // Assert
        Assert.Equal("unknown", result);
    }

    [Fact]
    public void ExtractHostPort_WithIpv4Address_PreservesFormat()
    {
        // Act
        var result = ConnectionIdentifier.ExtractHostPort("opc.tcp://10.0.0.1:4840");

        // Assert
        Assert.Equal("10.0.0.1_4840", result);
    }

    [Fact]
    public void LimitLength_WithShortIdentifier_ReturnsUnchanged()
    {
        // Act
        var result = ConnectionIdentifier.LimitLength("short_id_123", 50);

        // Assert
        Assert.Equal("short_id_123", result);
    }

    [Fact]
    public void LimitLength_WithLongIdentifier_Truncates()
    {
        // Arrange
        var longId = new string('a', 60);

        // Act
        var result = ConnectionIdentifier.LimitLength(longId, 50);

        // Assert
        Assert.Equal(50, result.Length);
    }

    [Fact]
    public void LimitLength_TrimsTrailingUnderscores()
    {
        // Arrange
        var id = "test____________________________end";

        // Act
        var result = ConnectionIdentifier.LimitLength(id, 20);

        // Assert
        Assert.DoesNotMatch(@"_$", result);
    }

    [Fact]
    public void Generate_UsesCurrentTimeWhenTimestampNull()
    {
        // Act
        var before = DateTime.Now;
        var result = ConnectionIdentifier.Generate("opc.tcp://localhost:4840");
        var after = DateTime.Now;

        // Assert - should contain a 12-digit timestamp
        Assert.Matches(@"localhost_4840_\d{12}$", result);

        // Verify the timestamp is within the expected range
        var expectedPrefix = "localhost_4840_";
        var timestampPart = result.Substring(expectedPrefix.Length);
        var parsedTimestamp = DateTime.ParseExact(timestampPart, "yyyyMMddHHmm", null);
        Assert.True(parsedTimestamp >= before.AddSeconds(-60) && parsedTimestamp <= after.AddSeconds(60));
    }

    [Fact]
    public void ExtractHostPort_EnsuresUnderscoreBetweenIpAndPort()
    {
        // This tests the specific issue mentioned in the task:
        // IP and port should be separated by underscore

        // Act
        var result = ConnectionIdentifier.ExtractHostPort("opc.tcp://192.168.1.67:50000");

        // Assert - verify there's an underscore between IP and port
        Assert.Equal("192.168.1.67_50000", result);
        Assert.Contains("_", result);
        Assert.DoesNotContain("6750000", result); // This would be the bug case
    }
}
