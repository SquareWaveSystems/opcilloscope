using Terminal.Gui;
using Opc.Ua;
using OpcScope.OpcUa.Models;
using OpcScope.App.Themes;
using ThemeManager = OpcScope.App.Themes.ThemeManager;

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
        CanFocus = true;

        // Apply theme styling
        var theme = ThemeManager.Current;
        BorderStyle = theme.FrameLineStyle;

        _detailsLabel = new Label
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
            Height = Dim.Fill(),
            Text = "Select a node to view details",
            TextAlignment = Alignment.Start,
            ColorScheme = new ColorScheme
            {
                Normal = new Terminal.Gui.Attribute(theme.MutedText, theme.Background)
            }
        };

        // Subscribe to theme changes
        ThemeManager.ThemeChanged += OnThemeChanged;

        Add(_detailsLabel);
    }

    private void OnThemeChanged(AppTheme theme)
    {
        Application.Invoke(() =>
        {
            // When showing empty state, keep muted color
            if (_detailsLabel.Text == "Select a node to view details" ||
                _detailsLabel.Text == "Not connected")
            {
                _detailsLabel.ColorScheme = new ColorScheme
                {
                    Normal = new Terminal.Gui.Attribute(theme.MutedText, theme.Background)
                };
            }
            else
            {
                _detailsLabel.ColorScheme = new ColorScheme
                {
                    Normal = new Terminal.Gui.Attribute(theme.Foreground, theme.Background)
                };
            }
            SetNeedsLayout();
        });
    }

    public void Initialize(OpcScope.OpcUa.NodeBrowser nodeBrowser)
    {
        _nodeBrowser = nodeBrowser;
    }

    public async Task ShowNodeAsync(BrowsedNode? node)
    {
        if (node == null || _nodeBrowser == null)
        {
            Application.Invoke(() =>
            {
                _detailsLabel.Text = "Select a node to view details";
                SetMutedColor();
            });
            return;
        }

        var attrs = await _nodeBrowser.GetNodeAttributesAsync(node.NodeId);

        Application.Invoke(() =>
        {
            if (attrs == null)
            {
                _detailsLabel.Text = $"NodeId: {node.NodeId}\nFailed to read attributes";
                SetNormalColor();
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
            SetNormalColor();
        });
    }

    public void Clear()
    {
        _detailsLabel.Text = "Not connected";
        SetMutedColor();
    }

    private void SetMutedColor()
    {
        var theme = ThemeManager.Current;
        _detailsLabel.ColorScheme = new ColorScheme
        {
            Normal = new Terminal.Gui.Attribute(theme.MutedText, theme.Background)
        };
    }

    private void SetNormalColor()
    {
        var theme = ThemeManager.Current;
        _detailsLabel.ColorScheme = new ColorScheme
        {
            Normal = new Terminal.Gui.Attribute(theme.Foreground, theme.Background)
        };
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
