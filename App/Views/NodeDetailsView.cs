using Terminal.Gui;
using Opc.Ua;
using OpcScope.OpcUa.Models;
using OpcScope.App.Themes;
using AppThemeManager = OpcScope.App.Themes.ThemeManager;

namespace OpcScope.App.Views;

/// <summary>
/// Panel showing detailed attributes of the selected node.
/// Uses Terminal.Gui v2 layout features for cleaner presentation.
/// </summary>
public class NodeDetailsView : FrameView
{
    private readonly Label _detailsLabel;
    private OpcScope.OpcUa.NodeBrowser? _nodeBrowser;

    public NodeDetailsView()
    {
        Title = " Node Details ";

        // Apply theme styling
        var theme = AppThemeManager.Current;
        BorderStyle = theme.FrameLineStyle;

        _detailsLabel = new Label
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
            Height = Dim.Fill(),
            Text = "Select a node to view details",
            TextAlignment = Alignment.Start
        };

        Add(_detailsLabel);
    }

    public void Initialize(OpcScope.OpcUa.NodeBrowser nodeBrowser)
    {
        _nodeBrowser = nodeBrowser;
    }

    public async Task ShowNodeAsync(BrowsedNode? node)
    {
        if (node == null || _nodeBrowser == null)
        {
            Application.Invoke(() => _detailsLabel.Text = "Select a node to view details");
            return;
        }

        var attrs = await _nodeBrowser.GetNodeAttributesAsync(node.NodeId);

        Application.Invoke(() =>
        {
            if (attrs == null)
            {
                _detailsLabel.Text = $"NodeId: {node.NodeId}\nFailed to read attributes";
                return;
            }

            // Build a cleaner inline format for the details bar
            var parts = new List<string>
            {
                $"NodeId: {attrs.NodeId}",
                $"Class: {attrs.NodeClass}",
                $"Name: {attrs.DisplayName ?? attrs.BrowseName ?? "N/A"}"
            };

            if (attrs.NodeClass == NodeClass.Variable)
            {
                parts.Add($"Type: {attrs.DataType ?? "N/A"}");
                parts.Add($"Access: {attrs.AccessLevelString}");
            }

            if (!string.IsNullOrEmpty(attrs.Description))
            {
                parts.Add($"Desc: {TruncateString(attrs.Description, 40)}");
            }

            _detailsLabel.Text = string.Join("  │  ", parts);
        });
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

    private static string TruncateString(string str, int maxLength)
    {
        if (string.IsNullOrEmpty(str))
            return string.Empty;
        if (str.Length <= maxLength)
            return str;
        return str[..(maxLength - 3)] + "...";
    }
}
