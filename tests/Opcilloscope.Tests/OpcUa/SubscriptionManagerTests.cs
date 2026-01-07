using Opc.Ua;
using Opcilloscope.OpcUa;

namespace Opcilloscope.Tests.OpcUa;

public class SubscriptionManagerTests
{
    [Fact]
    public void FormatValue_ReturnsNull_WhenValueIsNull()
    {
        // Act
        var result = SubscriptionManager.FormatValue(null);

        // Assert
        Assert.Equal("null", result);
    }

    [Fact]
    public void FormatValue_FormatsString_AsIs()
    {
        // Arrange
        var value = "Hello World";

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void FormatValue_FormatsInteger_AsIs()
    {
        // Arrange
        var value = 42;

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("42", result);
    }

    [Fact]
    public void FormatValue_FormatsFloat_WithTwoDecimals()
    {
        // Arrange
        var value = 3.14159f;

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("3.14", result);
    }

    [Fact]
    public void FormatValue_FormatsDouble_WithTwoDecimals()
    {
        // Arrange
        var value = 2.71828;

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("2.72", result);
    }

    [Fact]
    public void FormatValue_FormatsDouble_ZeroDecimalPlaces()
    {
        // Arrange
        var value = 100.0;

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("100.00", result);
    }

    [Fact]
    public void FormatValue_FormatsBoolean_True()
    {
        // Arrange
        var value = true;

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("True", result);
    }

    [Fact]
    public void FormatValue_FormatsBoolean_False()
    {
        // Arrange
        var value = false;

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("False", result);
    }

    [Fact]
    public void FormatValue_FormatsDateTime()
    {
        // Arrange
        var value = new DateTime(2024, 6, 15, 10, 30, 45);

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Contains("2024", result);
    }

    [Fact]
    public void FormatValue_FormatsEmptyByteArray()
    {
        // Arrange
        var value = new byte[0];

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("[0 bytes]", result);
    }

    [Fact]
    public void FormatValue_FormatsNonEmptyByteArray()
    {
        // Arrange
        var value = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("[5 bytes]", result);
    }

    [Fact]
    public void FormatValue_FormatsLargeByteArray()
    {
        // Arrange
        var value = new byte[1024];

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("[1024 bytes]", result);
    }

    [Fact]
    public void FormatValue_FormatsEmptyIntArray()
    {
        // Arrange
        var value = new int[0];

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("[0 items]", result);
    }

    [Fact]
    public void FormatValue_FormatsIntArray()
    {
        // Arrange
        var value = new int[] { 1, 2, 3, 4, 5 };

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("[5 items]", result);
    }

    [Fact]
    public void FormatValue_FormatsStringArray()
    {
        // Arrange
        var value = new string[] { "a", "b", "c" };

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("[3 items]", result);
    }

    [Fact]
    public void FormatValue_FormatsDoubleArray()
    {
        // Arrange
        var value = new double[] { 1.1, 2.2, 3.3 };

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("[3 items]", result);
    }

    [Theory]
    [InlineData(0.0f, "0.00")]
    [InlineData(-1.5f, "-1.50")]
    [InlineData(999.999f, "1000.00")]
    [InlineData(0.001f, "0.00")]
    [InlineData(0.005f, "0.01")]
    public void FormatValue_FloatFormatting_VariousValues(float input, string expected)
    {
        // Act
        var result = SubscriptionManager.FormatValue(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0.0, "0.00")]
    [InlineData(-1.5, "-1.50")]
    [InlineData(999.999, "1000.00")]
    [InlineData(0.001, "0.00")]
    [InlineData(0.005, "0.01")]
    public void FormatValue_DoubleFormatting_VariousValues(double input, string expected)
    {
        // Act
        var result = SubscriptionManager.FormatValue(input);

        // Assert
        Assert.Equal(expected, result);
    }
}

public class NodeIdExtensionsTests
{
    [Fact]
    public void EqualsNodeId_ReturnsFalse_WhenFirstIsNull()
    {
        // Arrange
        NodeId? nodeId = null;
        var other = new NodeId(100);

        // Act
        var result = nodeId!.EqualsNodeId(other);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsFalse_WhenSecondIsNull()
    {
        // Arrange
        var nodeId = new NodeId(100);
        NodeId? other = null;

        // Act
        var result = nodeId.EqualsNodeId(other!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsTrue_WhenBothAreNull()
    {
        // Arrange
        NodeId? nodeId = null;
        NodeId? other = null;

        // Act
        var result = nodeId!.EqualsNodeId(other!);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsTrue_WhenSameNumericId()
    {
        // Arrange
        var nodeId = new NodeId(100);
        var other = new NodeId(100);

        // Act
        var result = nodeId.EqualsNodeId(other);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsFalse_WhenDifferentNumericId()
    {
        // Arrange
        var nodeId = new NodeId(100);
        var other = new NodeId(200);

        // Act
        var result = nodeId.EqualsNodeId(other);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsTrue_WhenSameStringId()
    {
        // Arrange
        var nodeId = new NodeId("TestNode", 2);
        var other = new NodeId("TestNode", 2);

        // Act
        var result = nodeId.EqualsNodeId(other);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsFalse_WhenDifferentStringId()
    {
        // Arrange
        var nodeId = new NodeId("TestNode1", 2);
        var other = new NodeId("TestNode2", 2);

        // Act
        var result = nodeId.EqualsNodeId(other);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsFalse_WhenDifferentNamespace()
    {
        // Arrange
        var nodeId = new NodeId("TestNode", 1);
        var other = new NodeId("TestNode", 2);

        // Act
        var result = nodeId.EqualsNodeId(other);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsTrue_WhenSameGuidId()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var nodeId = new NodeId(guid, 3);
        var other = new NodeId(guid, 3);

        // Act
        var result = nodeId.EqualsNodeId(other);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsFalse_WhenDifferentGuidId()
    {
        // Arrange
        var nodeId = new NodeId(Guid.NewGuid(), 3);
        var other = new NodeId(Guid.NewGuid(), 3);

        // Act
        var result = nodeId.EqualsNodeId(other);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsTrue_WhenSameReference()
    {
        // Arrange
        var nodeId = new NodeId(100);

        // Act
        var result = nodeId.EqualsNodeId(nodeId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsTrue_ForWellKnownObjectIds()
    {
        // Arrange
        var nodeId = ObjectIds.RootFolder;
        var other = new NodeId(84, 0); // RootFolder = ns=0;i=84

        // Act
        var result = nodeId.EqualsNodeId(other);

        // Assert
        Assert.True(result);
    }
}
