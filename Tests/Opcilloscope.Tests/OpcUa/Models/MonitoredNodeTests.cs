using Opc.Ua;
using Opcilloscope.OpcUa.Models;

namespace Opcilloscope.Tests.OpcUa.Models;

public class MonitoredNodeTests
{
    [Fact]
    public void MonitoredNode_IsGood_ReturnsTrueWhenStatusCodeIsZero()
    {
        // Arrange
        var node = new MonitoredNode
        {
            NodeId = new NodeId(1000),
            DisplayName = "Test",
            StatusCode = 0
        };

        // Assert
        Assert.True(node.IsGood);
        Assert.False(node.IsUncertain);
        Assert.False(node.IsBad);
    }

    [Fact]
    public void MonitoredNode_IsBad_ReturnsTrueWhenBadBitSet()
    {
        // Arrange
        var node = new MonitoredNode
        {
            NodeId = new NodeId(1000),
            DisplayName = "Test",
            StatusCode = 0x80000000 // Bad status code
        };

        // Assert
        Assert.False(node.IsGood);
        Assert.False(node.IsUncertain);
        Assert.True(node.IsBad);
    }

    [Fact]
    public void MonitoredNode_IsUncertain_ReturnsTrueWhenUncertainBitSet()
    {
        // Arrange
        var node = new MonitoredNode
        {
            NodeId = new NodeId(1000),
            DisplayName = "Test",
            StatusCode = 0x40000000 // Uncertain status code
        };

        // Assert
        Assert.False(node.IsGood);
        Assert.True(node.IsUncertain);
        Assert.False(node.IsBad);
    }

    [Fact]
    public void MonitoredNode_StatusString_ReturnsGoodForZeroStatus()
    {
        // Arrange
        var node = new MonitoredNode { StatusCode = 0 };

        // Assert
        Assert.Equal("Good", node.StatusString);
    }

    [Fact]
    public void MonitoredNode_StatusString_ReturnsBadForBadStatus()
    {
        // Arrange
        var node = new MonitoredNode { StatusCode = 0x80070000 };

        // Assert
        Assert.StartsWith("Bad", node.StatusString);
        Assert.Contains("0x80070000", node.StatusString);
    }

    [Fact]
    public void MonitoredNode_StatusString_ReturnsUncertainForUncertainStatus()
    {
        // Arrange
        var node = new MonitoredNode { StatusCode = 0x40000000 };

        // Assert
        Assert.StartsWith("Uncertain", node.StatusString);
    }

    [Fact]
    public void MonitoredNode_TimestampString_ReturnsFormattedTime()
    {
        // Arrange
        var testTime = new DateTime(2024, 1, 15, 14, 30, 45);
        var node = new MonitoredNode { Timestamp = testTime };

        // Assert
        Assert.Equal("14:30:45", node.TimestampString);
    }

    [Fact]
    public void MonitoredNode_TimestampString_ReturnsDashWhenNull()
    {
        // Arrange
        var node = new MonitoredNode { Timestamp = null };

        // Assert
        Assert.Equal("-", node.TimestampString);
    }

    [Fact]
    public void MonitoredNode_RecentlyChanged_ReturnsTrueWithinThreshold()
    {
        // Arrange
        var node = new MonitoredNode
        {
            LastChangeTime = DateTime.Now.AddMilliseconds(-100) // 100ms ago
        };

        // Assert
        Assert.True(node.RecentlyChanged);
    }

    [Fact]
    public void MonitoredNode_RecentlyChanged_ReturnsFalseAfterThreshold()
    {
        // Arrange
        var node = new MonitoredNode
        {
            LastChangeTime = DateTime.Now.AddSeconds(-2) // 2 seconds ago
        };

        // Assert
        Assert.False(node.RecentlyChanged);
    }
}
