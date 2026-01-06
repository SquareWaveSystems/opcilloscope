using Terminal.Gui;
using OpcScope.OpcUa;
using OpcScope.OpcUa.Models;
using OpcScope.App.Themes;

namespace OpcScope.App.Views;

/// <summary>
/// TreeView for browsing the OPC UA address space with lazy loading.
/// Supports theme-aware styling with Terminal.Gui v2 features.
/// </summary>
public class AddressSpaceView : FrameView
{
    private readonly TreeView<BrowsedNode> _treeView;
    private NodeBrowser? _nodeBrowser;
    private BrowsedNode? _rootNode;

    public event Action<BrowsedNode>? NodeSelected;
    public event Action<BrowsedNode>? NodeSubscribeRequested;

    public BrowsedNode? SelectedNode => _treeView.SelectedObject;

    public AddressSpaceView()
    {
        Title = " Address Space ";
        CanFocus = true;

        // Apply initial theme styling
        var theme = ThemeManager.Current;
        BorderStyle = theme.FrameLineStyle;

        _treeView = new TreeView<BrowsedNode>
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            TreeBuilder = new DelegateTreeBuilder<BrowsedNode>(
                GetChildrenForNode,
                HasChildrenForNode
            )
        };

        // Configure tree style for cleaner look
        _treeView.Style.CollapseableSymbol = new Rune('▼');
        _treeView.Style.ExpandableSymbol = new Rune('▶');
        _treeView.Style.LeaveLastRow = false;

        _treeView.SelectionChanged += (_, args) =>
        {
            if (args.NewValue != null)
            {
                NodeSelected?.Invoke(args.NewValue);
            }
        };

        _treeView.KeyDown += HandleKeyDown;
        _treeView.ObjectActivated += HandleObjectActivated;

        Add(_treeView);
    }

    public void Initialize(NodeBrowser nodeBrowser)
    {
        _nodeBrowser = nodeBrowser;
        Refresh();
    }

    public void Refresh()
    {
        if (_nodeBrowser == null) return;

        _rootNode = _nodeBrowser.GetRootNode();
        _treeView.ClearObjects();
        _treeView.AddObject(_rootNode);
        _treeView.Expand(_rootNode);
    }

    public void Clear()
    {
        _treeView.ClearObjects();
        _rootNode = null;
    }

    private IEnumerable<BrowsedNode> GetChildrenForNode(BrowsedNode node)
    {
        if (_nodeBrowser == null)
            return Enumerable.Empty<BrowsedNode>();

        if (node.ChildrenLoaded)
            return node.Children;

        try
        {
            return _nodeBrowser.GetChildrenAsync(node).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load children for {node.DisplayName}: {ex.Message}");
            return Enumerable.Empty<BrowsedNode>();
        }
    }

    private bool HasChildrenForNode(BrowsedNode node)
    {
        return node.HasChildren;
    }

    private void HandleKeyDown(object? _, Key e)
    {
        if (e == Key.Enter || e == Key.Space)
        {
            var selected = _treeView.SelectedObject;
            if (selected != null && selected.NodeClass == Opc.Ua.NodeClass.Variable)
            {
                NodeSubscribeRequested?.Invoke(selected);
                e.Handled = true;
            }
        }
        else if (e == Key.F5)
        {
            Refresh();
            e.Handled = true;
        }
    }

    private void HandleObjectActivated(object? _, ObjectActivatedEventArgs<BrowsedNode> e)
    {
        if (e.ActivatedObject != null && e.ActivatedObject.NodeClass == Opc.Ua.NodeClass.Variable)
        {
            NodeSubscribeRequested?.Invoke(e.ActivatedObject);
        }
    }
}
