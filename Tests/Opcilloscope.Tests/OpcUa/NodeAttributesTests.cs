using Opc.Ua;
using Opcilloscope.OpcUa;
using OpcilloscopeNodeAttributes = Opcilloscope.OpcUa.NodeAttributes;

namespace Opcilloscope.Tests.OpcUa;

public class NodeAttributesTests
{
    [Fact]
    public void NodeAttributes_DefaultValues()
    {
        // Arrange & Act
        var attrs = new OpcilloscopeNodeAttributes();

        // Assert
        Assert.Equal(ObjectIds.RootFolder, attrs.NodeId);
        Assert.Equal(NodeClass.Unspecified, attrs.NodeClass);
        Assert.Null(attrs.BrowseName);
        Assert.Null(attrs.DisplayName);
        Assert.Null(attrs.Description);
        Assert.Null(attrs.DataType);
        Assert.Null(attrs.ValueRank);
        Assert.Null(attrs.AccessLevel);
        Assert.Null(attrs.UserAccessLevel);
    }

    [Fact]
    public void AccessLevelString_ReturnsNA_WhenAccessLevelIsNull()
    {
        // Arrange
        var attrs = new OpcilloscopeNodeAttributes { AccessLevel = null };

        // Assert
        Assert.Equal("N/A", attrs.AccessLevelString);
    }

    [Fact]
    public void AccessLevelString_ReturnsNone_WhenAccessLevelIsZero()
    {
        // Arrange
        var attrs = new OpcilloscopeNodeAttributes { AccessLevel = 0 };

        // Assert
        Assert.Equal("None", attrs.AccessLevelString);
    }

    [Fact]
    public void AccessLevelString_ReturnsRead_WhenReadBitSet()
    {
        // Arrange
        var attrs = new OpcilloscopeNodeAttributes { AccessLevel = 0x01 };

        // Assert
        Assert.Equal("Read", attrs.AccessLevelString);
    }

    [Fact]
    public void AccessLevelString_ReturnsWrite_WhenWriteBitSet()
    {
        // Arrange
        var attrs = new OpcilloscopeNodeAttributes { AccessLevel = 0x02 };

        // Assert
        Assert.Equal("Write", attrs.AccessLevelString);
    }

    [Fact]
    public void AccessLevelString_ReturnsHistoryRead_WhenHistoryReadBitSet()
    {
        // Arrange
        var attrs = new OpcilloscopeNodeAttributes { AccessLevel = 0x04 };

        // Assert
        Assert.Equal("HistoryRead", attrs.AccessLevelString);
    }

    [Fact]
    public void AccessLevelString_ReturnsHistoryWrite_WhenHistoryWriteBitSet()
    {
        // Arrange
        var attrs = new OpcilloscopeNodeAttributes { AccessLevel = 0x08 };

        // Assert
        Assert.Equal("HistoryWrite", attrs.AccessLevelString);
    }

    [Fact]
    public void AccessLevelString_ReturnsReadWrite_WhenBothBitsSet()
    {
        // Arrange
        var attrs = new OpcilloscopeNodeAttributes { AccessLevel = 0x03 }; // Read + Write

        // Assert
        Assert.Equal("Read, Write", attrs.AccessLevelString);
    }

    [Fact]
    public void AccessLevelString_ReturnsAllAccess_WhenAllBitsSet()
    {
        // Arrange
        var attrs = new OpcilloscopeNodeAttributes { AccessLevel = 0x0F }; // All four bits

        // Assert
        Assert.Equal("Read, Write, HistoryRead, HistoryWrite", attrs.AccessLevelString);
    }

    [Theory]
    [InlineData(0x01, "Read")]
    [InlineData(0x02, "Write")]
    [InlineData(0x03, "Read, Write")]
    [InlineData(0x04, "HistoryRead")]
    [InlineData(0x05, "Read, HistoryRead")]
    [InlineData(0x06, "Write, HistoryRead")]
    [InlineData(0x07, "Read, Write, HistoryRead")]
    [InlineData(0x08, "HistoryWrite")]
    [InlineData(0x09, "Read, HistoryWrite")]
    [InlineData(0x0A, "Write, HistoryWrite")]
    [InlineData(0x0B, "Read, Write, HistoryWrite")]
    [InlineData(0x0C, "HistoryRead, HistoryWrite")]
    [InlineData(0x0D, "Read, HistoryRead, HistoryWrite")]
    [InlineData(0x0E, "Write, HistoryRead, HistoryWrite")]
    [InlineData(0x0F, "Read, Write, HistoryRead, HistoryWrite")]
    public void AccessLevelString_ReturnsCorrectCombination(byte accessLevel, string expected)
    {
        // Arrange
        var attrs = new OpcilloscopeNodeAttributes { AccessLevel = accessLevel };

        // Assert
        Assert.Equal(expected, attrs.AccessLevelString);
    }

    [Fact]
    public void NodeAttributes_CanSetAllProperties()
    {
        // Arrange
        var nodeId = new NodeId(1234);

        // Act
        var attrs = new OpcilloscopeNodeAttributes
        {
            NodeId = nodeId,
            NodeClass = NodeClass.Variable,
            BrowseName = "TestBrowseName",
            DisplayName = "Test Display Name",
            Description = "Test Description",
            DataType = "Int32",
            ValueRank = -1,
            AccessLevel = 0x03,
            UserAccessLevel = 0x01
        };

        // Assert
        Assert.Equal(nodeId, attrs.NodeId);
        Assert.Equal(NodeClass.Variable, attrs.NodeClass);
        Assert.Equal("TestBrowseName", attrs.BrowseName);
        Assert.Equal("Test Display Name", attrs.DisplayName);
        Assert.Equal("Test Description", attrs.Description);
        Assert.Equal("Int32", attrs.DataType);
        Assert.Equal(-1, attrs.ValueRank);
        Assert.Equal((byte)0x03, attrs.AccessLevel);
        Assert.Equal((byte)0x01, attrs.UserAccessLevel);
    }
}
