using Opc.Ua;
using OpcScope.OpcUa;
using OpcScope.Tests.Infrastructure;
using OpcScope.Utilities;

namespace OpcScope.Tests.Integration;

/// <summary>
/// Integration tests for NodeBrowser with a real OPC UA server.
/// Tests address space browsing and lazy loading.
/// </summary>
public class NodeBrowserIntegrationTests : IntegrationTestBase
{
    private readonly Logger _logger = new();
    private OpcScope.OpcUa.NodeBrowser _nodeBrowser = null!;

    public NodeBrowserIntegrationTests(TestServerFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _nodeBrowser = new OpcScope.OpcUa.NodeBrowser(Client!, _logger);
    }

    [Fact]
    public void GetRootNode_ReturnsRootFolder()
    {
        // Act
        var root = _nodeBrowser.GetRootNode();

        // Assert
        Assert.NotNull(root);
        Assert.Equal("Root", root.DisplayName);
        Assert.Equal(ObjectIds.RootFolder, root.NodeId);
        Assert.True(root.HasChildren);
    }

    [Fact]
    public async Task GetChildrenAsync_RootFolder_ReturnsChildren()
    {
        // Arrange
        var root = _nodeBrowser.GetRootNode();

        // Act
        var children = await _nodeBrowser.GetChildrenAsync(root);

        // Assert
        Assert.NotEmpty(children);
        Assert.Contains(children, c => c.DisplayName == "Objects");
        Assert.Contains(children, c => c.DisplayName == "Types");
        Assert.Contains(children, c => c.DisplayName == "Views");
    }

    [Fact]
    public async Task GetChildrenAsync_ObjectsFolder_ContainsTestFolders()
    {
        // Arrange
        var root = _nodeBrowser.GetRootNode();
        var children = await _nodeBrowser.GetChildrenAsync(root);
        var objectsFolder = children.First(c => c.DisplayName == "Objects");

        // Act
        var objectsChildren = await _nodeBrowser.GetChildrenAsync(objectsFolder);

        // Assert
        Assert.Contains(objectsChildren, c => c.DisplayName == "Simulation");
        Assert.Contains(objectsChildren, c => c.DisplayName == "StaticData");
    }

    [Fact]
    public async Task GetChildrenAsync_SimulationFolder_ContainsVariables()
    {
        // Arrange
        var root = _nodeBrowser.GetRootNode();
        var children = await _nodeBrowser.GetChildrenAsync(root);
        var objectsFolder = children.First(c => c.DisplayName == "Objects");
        var objectsChildren = await _nodeBrowser.GetChildrenAsync(objectsFolder);
        var simulationFolder = objectsChildren.First(c => c.DisplayName == "Simulation");

        // Act
        var simulationChildren = await _nodeBrowser.GetChildrenAsync(simulationFolder);

        // Assert
        Assert.Contains(simulationChildren, c => c.DisplayName == "Counter");
        Assert.Contains(simulationChildren, c => c.DisplayName == "RandomValue");
        Assert.Contains(simulationChildren, c => c.DisplayName == "SineWave");
        Assert.Contains(simulationChildren, c => c.DisplayName == "WritableString");
        Assert.Contains(simulationChildren, c => c.DisplayName == "ToggleBoolean");
        Assert.Contains(simulationChildren, c => c.DisplayName == "WritableNumber");
    }

    [Fact]
    public async Task GetChildrenAsync_VariableNodes_HaveCorrectNodeClass()
    {
        // Arrange
        var root = _nodeBrowser.GetRootNode();
        var children = await _nodeBrowser.GetChildrenAsync(root);
        var objectsFolder = children.First(c => c.DisplayName == "Objects");
        var objectsChildren = await _nodeBrowser.GetChildrenAsync(objectsFolder);
        var simulationFolder = objectsChildren.First(c => c.DisplayName == "Simulation");

        // Act
        var simulationChildren = await _nodeBrowser.GetChildrenAsync(simulationFolder);
        var counterNode = simulationChildren.First(c => c.DisplayName == "Counter");

        // Assert
        Assert.Equal(NodeClass.Variable, counterNode.NodeClass);
    }

    [Fact]
    public async Task GetChildrenAsync_VariableNodes_HaveDataTypeName()
    {
        // Arrange
        var root = _nodeBrowser.GetRootNode();
        var children = await _nodeBrowser.GetChildrenAsync(root);
        var objectsFolder = children.First(c => c.DisplayName == "Objects");
        var objectsChildren = await _nodeBrowser.GetChildrenAsync(objectsFolder);
        var simulationFolder = objectsChildren.First(c => c.DisplayName == "Simulation");
        var simulationChildren = await _nodeBrowser.GetChildrenAsync(simulationFolder);

        // Act & Assert
        var counterNode = simulationChildren.First(c => c.DisplayName == "Counter");
        Assert.Equal("Int32", counterNode.DataTypeName);

        var stringNode = simulationChildren.First(c => c.DisplayName == "WritableString");
        Assert.Equal("String", stringNode.DataTypeName);

        var boolNode = simulationChildren.First(c => c.DisplayName == "ToggleBoolean");
        Assert.Equal("Boolean", boolNode.DataTypeName);
    }

    [Fact]
    public async Task GetChildrenAsync_SetsParentReference()
    {
        // Arrange
        var root = _nodeBrowser.GetRootNode();

        // Act
        var children = await _nodeBrowser.GetChildrenAsync(root);
        var objectsFolder = children.First(c => c.DisplayName == "Objects");

        // Assert
        Assert.Equal(root, objectsFolder.Parent);
    }

    [Fact]
    public async Task GetChildrenAsync_SetsChildrenLoadedFlag()
    {
        // Arrange
        var root = _nodeBrowser.GetRootNode();
        Assert.False(root.ChildrenLoaded);

        // Act
        await _nodeBrowser.GetChildrenAsync(root);

        // Assert
        Assert.True(root.ChildrenLoaded);
    }

    [Fact]
    public async Task GetChildrenAsync_PopulatesChildrenCollection()
    {
        // Arrange
        var root = _nodeBrowser.GetRootNode();
        Assert.Empty(root.Children);

        // Act
        var children = await _nodeBrowser.GetChildrenAsync(root);

        // Assert
        Assert.NotEmpty(root.Children);
        Assert.Equal(children.Count, root.Children.Count);
    }

    [Fact]
    public async Task GetChildrenAsync_FolderNodes_HaveHasChildrenTrue()
    {
        // Arrange
        var root = _nodeBrowser.GetRootNode();
        var children = await _nodeBrowser.GetChildrenAsync(root);

        // Act
        var objectsFolder = children.First(c => c.DisplayName == "Objects");

        // Assert
        Assert.True(objectsFolder.HasChildren);
    }

    [Fact]
    public async Task GetNodeAttributesAsync_ReturnsAttributes()
    {
        // Arrange
        var nodeId = new NodeId("Counter", (ushort)GetNamespaceIndex());

        // Act
        var attrs = await _nodeBrowser.GetNodeAttributesAsync(nodeId);

        // Assert
        Assert.NotNull(attrs);
        Assert.Equal(NodeClass.Variable, attrs.NodeClass);
        Assert.Equal("Counter", attrs.DisplayName);
        Assert.Equal("Int32", attrs.DataType);
    }

    [Fact]
    public async Task GetNodeAttributesAsync_WritableNode_HasWriteAccess()
    {
        // Arrange
        var nodeId = new NodeId("WritableString", (ushort)GetNamespaceIndex());

        // Act
        var attrs = await _nodeBrowser.GetNodeAttributesAsync(nodeId);

        // Assert
        Assert.NotNull(attrs);
        Assert.NotNull(attrs.AccessLevel);
        Assert.Contains("Write", attrs.AccessLevelString);
    }

    [Fact]
    public async Task GetNodeAttributesAsync_ReadOnlyNode_HasReadAccess()
    {
        // Arrange
        var nodeId = new NodeId("ServerName", (ushort)GetNamespaceIndex());

        // Act
        var attrs = await _nodeBrowser.GetNodeAttributesAsync(nodeId);

        // Assert
        Assert.NotNull(attrs);
        Assert.Contains("Read", attrs.AccessLevelString);
    }

    [Fact]
    public async Task LazyLoading_ChildrenNotLoadedUntilRequested()
    {
        // Arrange
        var root = _nodeBrowser.GetRootNode();
        var children = await _nodeBrowser.GetChildrenAsync(root);
        var objectsFolder = children.First(c => c.DisplayName == "Objects");

        // Assert - Objects folder knows it has children but hasn't loaded them
        Assert.True(objectsFolder.HasChildren);
        Assert.False(objectsFolder.ChildrenLoaded);
        Assert.Empty(objectsFolder.Children);

        // Act - Load children
        await _nodeBrowser.GetChildrenAsync(objectsFolder);

        // Assert - Now children are loaded
        Assert.True(objectsFolder.ChildrenLoaded);
        Assert.NotEmpty(objectsFolder.Children);
    }

    [Fact]
    public async Task GetChildrenAsync_ClearsExistingChildren()
    {
        // Arrange
        var root = _nodeBrowser.GetRootNode();
        await _nodeBrowser.GetChildrenAsync(root);
        var initialCount = root.Children.Count;

        // Act - Browse again
        await _nodeBrowser.GetChildrenAsync(root);

        // Assert - Children should be same count (not duplicated)
        Assert.Equal(initialCount, root.Children.Count);
    }
}
