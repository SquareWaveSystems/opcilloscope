using LibUA.Core;
using OpcScope.OpcUa.Models;

namespace OpcScope.Tests.OpcUa.Models;

public class BrowsedNodeTests
{
    [Fact]
    public void BrowsedNode_DefaultValues()
    {
        // Arrange & Act
        var node = new BrowsedNode();

        // Assert
        Assert.Equal(NodeId.Zero, node.NodeId);
        Assert.Equal(string.Empty, node.BrowseName);
        Assert.Equal(string.Empty, node.DisplayName);
        Assert.Equal(NodeClass.Unspecified, node.NodeClass);
        Assert.Null(node.DataType);
        Assert.Null(node.DataTypeName);
        Assert.True(node.HasChildren); // Default true until proven otherwise
        Assert.False(node.ChildrenLoaded);
        Assert.Empty(node.Children);
        Assert.Null(node.Parent);
    }

    [Fact]
    public void BrowsedNode_Children_DefaultsToEmptyList()
    {
        // Arrange
        var node = new BrowsedNode();

        // Assert
        Assert.NotNull(node.Children);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void BrowsedNode_ChildrenLoaded_DefaultsToFalse()
    {
        // Arrange
        var node = new BrowsedNode();

        // Assert
        Assert.False(node.ChildrenLoaded);
    }

    [Theory]
    [InlineData(NodeClass.Object, "[O]")]
    [InlineData(NodeClass.Variable, "[V]")]
    [InlineData(NodeClass.Method, "[M]")]
    [InlineData(NodeClass.ObjectType, "[OT]")]
    [InlineData(NodeClass.VariableType, "[VT]")]
    [InlineData(NodeClass.ReferenceType, "[RT]")]
    [InlineData(NodeClass.DataType, "[DT]")]
    [InlineData(NodeClass.View, "[Vw]")]
    [InlineData(NodeClass.Unspecified, "[?]")]
    public void BrowsedNode_NodeClassIcon_ReturnsCorrectIcon(NodeClass nodeClass, string expectedIcon)
    {
        // Arrange
        var node = new BrowsedNode
        {
            NodeId = new NodeId(0, (uint)1),
            NodeClass = nodeClass
        };

        // Assert
        Assert.Equal(expectedIcon, node.NodeClassIcon);
    }

    [Fact]
    public void BrowsedNode_ToString_IncludesIconAndDisplayName()
    {
        // Arrange
        var node = new BrowsedNode
        {
            NodeId = new NodeId(1, (uint)1000),
            DisplayName = "TestNode",
            NodeClass = NodeClass.Object
        };

        // Act
        var str = node.ToString();

        // Assert
        Assert.Contains("[O]", str);
        Assert.Contains("TestNode", str);
    }

    [Fact]
    public void BrowsedNode_ToString_IncludesDataTypeNameForVariable()
    {
        // Arrange
        var node = new BrowsedNode
        {
            NodeId = new NodeId(1, (uint)1000),
            DisplayName = "TestVariable",
            NodeClass = NodeClass.Variable,
            DataTypeName = "Int32"
        };

        // Act
        var str = node.ToString();

        // Assert
        Assert.Contains("[V]", str);
        Assert.Contains("TestVariable", str);
        Assert.Contains("[Int32]", str);
    }

    [Fact]
    public void BrowsedNode_ToString_DoesNotIncludeDataTypeForNonVariable()
    {
        // Arrange
        var node = new BrowsedNode
        {
            NodeId = new NodeId(1, (uint)1000),
            DisplayName = "TestObject",
            NodeClass = NodeClass.Object,
            DataTypeName = "SomeType" // Should be ignored for Object nodes
        };

        // Act
        var str = node.ToString();

        // Assert
        Assert.Contains("[O]", str);
        Assert.Contains("TestObject", str);
        Assert.DoesNotContain("SomeType", str);
    }

    [Fact]
    public void BrowsedNode_CanSetParent()
    {
        // Arrange
        var parent = new BrowsedNode
        {
            NodeId = new NodeId(0, (uint)85),
            DisplayName = "Objects"
        };

        var child = new BrowsedNode
        {
            NodeId = new NodeId(0, (uint)2253),
            DisplayName = "Server",
            Parent = parent
        };

        // Assert
        Assert.Equal(parent, child.Parent);
        Assert.Equal("Objects", child.Parent.DisplayName);
    }

    [Fact]
    public void BrowsedNode_CanAddChildren()
    {
        // Arrange
        var parent = new BrowsedNode
        {
            NodeId = new NodeId(0, (uint)85),
            DisplayName = "Objects"
        };

        var child = new BrowsedNode
        {
            NodeId = new NodeId(0, (uint)2253),
            DisplayName = "Server",
            Parent = parent
        };

        // Act
        parent.Children.Add(child);

        // Assert
        Assert.Single(parent.Children);
        Assert.Equal(child, parent.Children[0]);
    }
}
