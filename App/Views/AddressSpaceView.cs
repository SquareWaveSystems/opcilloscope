using System.Text;
using Terminal.Gui;
using Opcilloscope.Utilities;
using Opcilloscope.OpcUa;
using Opcilloscope.OpcUa.Models;
using Opcilloscope.App.Themes;
using ThemeManager = Opcilloscope.App.Themes.ThemeManager;

namespace Opcilloscope.App.Views;

/// <summary>
/// TreeView for browsing the OPC UA address space with lazy loading.
/// Supports theme-aware styling with Terminal.Gui v2 features.
/// </summary>
public class AddressSpaceView : FrameView
{
    private readonly TreeView<BrowsedNode> _treeView;
    private readonly Label _emptyStateLabel;
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

        _treeView.SelectionChanged += (_, args) =>
        {
            if (args.NewValue != null)
            {
                NodeSelected?.Invoke(args.NewValue);
            }
        };

        _treeView.KeyDown += HandleKeyDown;
        _treeView.Activated += HandleObjectActivated;

        // Create empty state label
        _emptyStateLabel = new Label
        {
            X = Pos.Center(),
            Y = Pos.Center(),
            Text = "",
        }.WithScheme(new Scheme { Normal = new Attribute(theme.MutedText, theme.Background) });

        Add(_treeView);
        Add(_emptyStateLabel);

        // Subscribe to theme changes
        ThemeManager.ThemeChanged += OnThemeChanged;

        // Initially show empty state
        _treeView.Visible = false;
        _emptyStateLabel.Visible = true;
    }

    private void OnThemeChanged(AppTheme theme)
    {
        UiThread.Run(() =>
        {
            _emptyStateLabel.SetScheme(new Scheme
            {
                Normal = new Attribute(theme.MutedText, theme.Background)
            });
            SetNeedsLayout();
        });
    }

    public void Initialize(NodeBrowser nodeBrowser)
    {
        _nodeBrowser = nodeBrowser;
        _emptyStateLabel.Visible = false;
        _treeView.Visible = true;
        _ = RefreshAsync();
    }

    public void Refresh()
    {
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_nodeBrowser == null) return;

        _rootNode = _nodeBrowser.GetRootNode();

        // Pre-load root children in background before updating UI
        try
        {
            await _nodeBrowser.GetChildrenAsync(_rootNode);
        }
        catch
        {
            // Ignore errors during initial load
        }

        // Update UI on main thread
        UiThread.Run(() =>
        {
            _treeView.ClearObjects();
            _treeView.AddObject(_rootNode);
            if (_rootNode.ChildrenLoaded)
            {
                _treeView.Expand(_rootNode);
            }
        });
    }

    public void Clear()
    {
        _treeView.ClearObjects();
        _rootNode = null;
        _treeView.Visible = false;
        _emptyStateLabel.Visible = true;
    }

    private IEnumerable<BrowsedNode> GetChildrenForNode(BrowsedNode node)
    {
        if (_nodeBrowser == null)
            return Enumerable.Empty<BrowsedNode>();

        if (node.ChildrenLoaded)
            return node.Children;

        // Load children asynchronously to avoid blocking UI
        // Return empty now, then refresh when loaded
        _ = LoadChildrenAsync(node);
        return Enumerable.Empty<BrowsedNode>();
    }

    private async Task LoadChildrenAsync(BrowsedNode node)
    {
        if (_nodeBrowser == null) return;

        try
        {
            await _nodeBrowser.GetChildrenAsync(node);

            // Refresh the tree on UI thread after children are loaded
            UiThread.Run(() =>
            {
                _treeView.RefreshObject(node);
                if (node.ChildrenLoaded && node.Children.Count > 0)
                {
                    _treeView.Expand(node);
                }
            });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load children for {node.DisplayName}: {ex.Message}");
            // Ignore load errors
        }
    }

    private bool HasChildrenForNode(BrowsedNode node)
    {
        return node.HasChildren;
    }

    private void HandleKeyDown(object? _, Key e)
    {
        if (e == Key.Enter)
        {
            var selected = _treeView.SelectedObject;
            if (selected != null)
            {
                // Toggle expand/collapse if node has children
                if (selected.HasChildren)
                {
                    if (_treeView.IsExpanded(selected))
                    {
                        _treeView.Collapse(selected);
                    }
                    else
                    {
                        _treeView.Expand(selected);
                    }
                }

                // Also subscribe if it's a Variable node
                if (selected.NodeClass == Opc.Ua.NodeClass.Variable)
                {
                    NodeSubscribeRequested?.Invoke(selected);
                }

                e.Handled = true;
            }
        }
        else if (e == Key.Space)
        {
            // Space only subscribes (original behavior for variables)
            var selected = _treeView.SelectedObject;
            if (selected != null && selected.NodeClass == Opc.Ua.NodeClass.Variable)
            {
                NodeSubscribeRequested?.Invoke(selected);
                e.Handled = true;
            }
        }
    }

    private void HandleObjectActivated(object? _, EventArgs<ICommandContext?> e)
    {
        // Terminal.Gui 2.4 replaced TreeView.ObjectActivated (which carried the object)
        // with the generic Activated command event; read the current selection instead.
        // Safe from activate/select races: Activated is raised synchronously on the UI
        // thread by the command that acted on the selection, so it still matches.
        var activated = _treeView.SelectedObject;
        if (activated != null && activated.NodeClass == Opc.Ua.NodeClass.Variable)
        {
            NodeSubscribeRequested?.Invoke(activated);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            _treeView.KeyDown -= HandleKeyDown;
            _treeView.Activated -= HandleObjectActivated;
        }
        base.Dispose(disposing);
    }
}
