using Terminal.Gui;
using OpcScope.OpcUa;
using OpcScope.OpcUa.Models;
using LibUA.Core;

namespace OpcScope.App.Views;

/// <summary>
/// Panel showing detailed attributes of the selected node.
/// </summary>
public class NodeDetailsView : FrameView
{
    private readonly Label _detailsLabel;
    private NodeBrowser? _nodeBrowser;

    public NodeDetailsView()
    {
        Title = "Node Details";

        _detailsLabel = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Text = "Select a node to view details"
        };

        Add(_detailsLabel);
    }

    public void Initialize(NodeBrowser nodeBrowser)
    {
        _nodeBrowser = nodeBrowser;
    }

    public void ShowNode(BrowsedNode? node)
    {
        if (node == null || _nodeBrowser == null)
        {
            _detailsLabel.Text = "Select a node to view details";
            return;
        }

        var attrs = _nodeBrowser.GetNodeAttributes(node.NodeId);

        if (attrs == null)
        {
            _detailsLabel.Text = $"NodeId: {node.NodeId}\nFailed to read attributes";
            return;
        }

        var lines = new List<string>
        {
            $"NodeId: {attrs.NodeId}",
            $"NodeClass: {attrs.NodeClass}",
            $"BrowseName: {attrs.BrowseName ?? "N/A"}",
            $"DisplayName: {attrs.DisplayName ?? "N/A"}"
        };

        if (!string.IsNullOrEmpty(attrs.Description))
        {
            lines.Add($"Description: {attrs.Description}");
        }

        if (attrs.NodeClass == NodeClass.Variable)
        {
            lines.Add($"DataType: {attrs.DataType ?? "N/A"}");
            lines.Add($"ValueRank: {FormatValueRank(attrs.ValueRank)}");
            lines.Add($"AccessLevel: {attrs.AccessLevelString}");
        }

        _detailsLabel.Text = string.Join("\n", lines);
    }

    public void Clear()
    {
        _detailsLabel.Text = "Not connected";
    }

    private static string FormatValueRank(int? valueRank)
    {
        if (valueRank == null) return "N/A";
        return valueRank switch
        {
            -3 => "ScalarOrOneDimension",
            -2 => "Any",
            -1 => "Scalar",
            0 => "OneOrMoreDimensions",
            1 => "OneDimension",
            _ => $"{valueRank}D Array"
        };
    }
}
