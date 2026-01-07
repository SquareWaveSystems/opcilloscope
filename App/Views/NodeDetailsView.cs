using Terminal.Gui;
using Opc.Ua;
using Opcilloscope.OpcUa.Models;
using Opcilloscope.App.Themes;
using Opcilloscope.Utilities;
using ThemeManager = Opcilloscope.App.Themes.ThemeManager;

namespace Opcilloscope.App.Views;

/// <summary>
/// Panel showing detailed attributes of the selected node.
/// Uses Terminal.Gui v2 layout features for cleaner presentation.
/// </summary>
public class NodeDetailsView : FrameView
{
    private readonly Label _detailsLabel;
    private readonly Button _copyButton;
    private Opcilloscope.OpcUa.NodeBrowser? _nodeBrowser;
    private NodeId? _currentNodeId;

    public NodeDetailsView()
    {
        Title = " Node Details ";
        CanFocus = true;

        // Apply theme styling
        var theme = ThemeManager.Current;
        BorderStyle = theme.FrameLineStyle;

        // Copy button in top-right corner of the frame
        _copyButton = new Button
        {
            Text = "Copy",
            X = Pos.AnchorEnd(8),
            Y = 0,
            Height = 1,
            ShadowStyle = ShadowStyle.None,
            ColorScheme = theme.ButtonColorScheme,
            Enabled = false
        };
        _copyButton.Accepting += OnCopyClicked;

        _detailsLabel = new Label
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(9), // Leave space for Copy button
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

        Add(_copyButton);
        Add(_detailsLabel);
    }

    private void OnThemeChanged(AppTheme theme)
    {
        Application.Invoke(() =>
        {
            // Update copy button styling
            _copyButton.ColorScheme = theme.ButtonColorScheme;

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

    public void Initialize(Opcilloscope.OpcUa.NodeBrowser nodeBrowser)
    {
        _nodeBrowser = nodeBrowser;
    }

    public async Task ShowNodeAsync(BrowsedNode? node)
    {
        if (node == null || _nodeBrowser == null)
        {
            _currentNodeId = null;
            Application.Invoke(() =>
            {
                _detailsLabel.Text = "Select a node to view details";
                _copyButton.Enabled = false;
                SetMutedColor();
            });
            return;
        }

        _currentNodeId = node.NodeId;
        var attrs = await _nodeBrowser.GetNodeAttributesAsync(node.NodeId);

        Application.Invoke(() =>
        {
            if (attrs == null)
            {
                _detailsLabel.Text = $"NodeId: {node.NodeId}\nFailed to read attributes";
                _copyButton.Enabled = false;
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
            _copyButton.Enabled = true;
            SetNormalColor();
        });
    }

    public void Clear()
    {
        _currentNodeId = null;
        _detailsLabel.Text = "Not connected";
        _copyButton.Enabled = false;
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

    private async void OnCopyClicked(object? sender, CommandEventArgs e)
    {
        if (_currentNodeId == null || _nodeBrowser == null)
            return;

        var originalText = _copyButton.Text;

        try
        {
            _copyButton.Text = "...";
            _copyButton.Enabled = false;

            var attributes = await _nodeBrowser.ReadAllNodeAttributesAsync(_currentNodeId);

            Application.Invoke(() =>
            {
                if (attributes == null || attributes.Count == 0)
                {
                    _copyButton.Text = "Err";
                    Application.AddTimeout(TimeSpan.FromSeconds(1), () =>
                    {
                        _copyButton.Text = originalText;
                        _copyButton.Enabled = _currentNodeId != null;
                        return false;
                    });
                    return;
                }

                var formatted = NodeAttributeFormatter.Format(attributes);
                Clipboard.TrySetClipboardData(formatted);

                // Brief visual feedback
                _copyButton.Text = "OK";
                Application.AddTimeout(TimeSpan.FromSeconds(1), () =>
                {
                    _copyButton.Text = originalText;
                    _copyButton.Enabled = _currentNodeId != null;
                    return false;
                });
            });
        }
        catch
        {
            Application.Invoke(() =>
            {
                _copyButton.Text = "Err";
                Application.AddTimeout(TimeSpan.FromSeconds(1), () =>
                {
                    _copyButton.Text = originalText;
                    _copyButton.Enabled = _currentNodeId != null;
                    return false;
                });
            });
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _copyButton.Accepting -= OnCopyClicked;
            ThemeManager.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }
}
