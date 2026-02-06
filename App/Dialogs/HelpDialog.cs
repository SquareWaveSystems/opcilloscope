using Terminal.Gui;
using Opcilloscope.App.Keybindings;
using Opcilloscope.App.Themes;
using Attribute = Terminal.Gui.Attribute;
using ThemeManager = Opcilloscope.App.Themes.ThemeManager;

namespace Opcilloscope.App.Dialogs;

/// <summary>
/// Help dialog displaying all keyboard shortcuts in a formatted layout.
/// Can auto-generate help from KeybindingManager or use static content.
/// </summary>
public class HelpDialog : Dialog
{
    private readonly KeybindingManager? _keybindingManager;

    public HelpDialog(KeybindingManager? keybindingManager = null)
    {
        _keybindingManager = keybindingManager;

        Title = " opcilloscope - Help ";
        Width = 64;
        Height = Dim.Fill(2);

        var theme = ThemeManager.Current;

        // Apply theme styling with emphasized border (double-line)
        ColorScheme = theme.MainColorScheme;
        BorderStyle = theme.EmphasizedBorderStyle;

        // Create content view with the help text
        var contentView = new TextView
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
            Height = Dim.Fill(2),
            ReadOnly = true,
            WordWrap = true,
            ColorScheme = new ColorScheme
            {
                Normal = new Attribute(theme.Foreground, theme.Background),
                Focus = new Attribute(theme.Foreground, theme.Background),
                HotNormal = new Attribute(theme.Foreground, theme.Background),
                HotFocus = new Attribute(theme.Foreground, theme.Background),
                Disabled = new Attribute(theme.MutedText, theme.Background)
            }
        };

        contentView.Text = keybindingManager != null
            ? GenerateHelpFromBindings(keybindingManager)
            : GetStaticHelpText();

        // OK button - highlighted with accent color (default action)
        var defaultButtonScheme = new ColorScheme
        {
            Normal = new Attribute(theme.Accent, theme.Background),
            Focus = new Attribute(theme.AccentBright, theme.Background),
            HotNormal = new Attribute(theme.Accent, theme.Background),
            HotFocus = new Attribute(theme.AccentBright, theme.Background),
            Disabled = new Attribute(theme.MutedText, theme.Background)
        };

        var okButton = new Button
        {
            Text = "OK",
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1),
            IsDefault = true,
            ColorScheme = defaultButtonScheme
        };
        okButton.Accepting += (_, _) => RequestStop();

        Add(contentView);
        Add(okButton);

        okButton.SetFocus();

        // Subscribe to theme changes
        ThemeManager.ThemeChanged += OnThemeChanged;
    }

    /// <summary>
    /// Generates help text from the KeybindingManager.
    /// </summary>
    private static string GenerateHelpFromBindings(KeybindingManager manager)
    {
        var lines = new List<string> { "" };

        foreach (var group in manager.GetAllBindingsGroupedByCategory())
        {
            lines.Add(group.Key.ToUpperInvariant());

            foreach (var binding in group.OrderBy(b => b.StatusBarPriority))
            {
                var keyDisplay = binding.KeyDisplay.PadRight(16);
                lines.Add($"  {keyDisplay}{binding.Description}");
            }

            lines.Add("");
        }

        // Add tips section
        lines.Add("TIPS");
        lines.Add("  - Press ? for context-sensitive quick help");
        lines.Add("  - Only Variable nodes can be subscribed");
        lines.Add("  - Select up to 5 variables for Scope/Recording");
        lines.Add("  - Status bar shows context-specific shortcuts");
        lines.Add("  - Publishing Interval (Settings) controls how often");
        lines.Add("    the server sends data and affects Scope, Trend,");
        lines.Add("    and CSV data resolution");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Returns static help text (fallback when no KeybindingManager is provided).
    /// </summary>
    private static string GetStaticHelpText()
    {
        return @"
NAVIGATION
  Tab              Switch between panes
  Arrow Keys       Navigate within pane
  ?                Show help
  M                Open menu

ADDRESS SPACE
  Enter            Subscribe to selected node
  Space            Expand/collapse tree node
  R                Refresh

MONITORED VARIABLES
  Delete           Unsubscribe from item
  Space            Toggle selection (for Scope/Recording)
  W                Write value to node
  T                Show trend plot
  S                Open Scope with selected

SCOPE VIEW CONTROLS
  Space            Pause/resume plotting
  +/-              Adjust vertical scale
  R                Reset to auto-scale
  [/]              Widen/narrow time window
  Left/Right       Move cursor (when paused)

APPLICATION
  Ctrl+O           Open configuration
  Ctrl+S           Save configuration
  Ctrl+Shift+S     Save As
  Ctrl+R           Start/stop recording
  Ctrl+Q           Quit

TIPS
  - Press ? for help anytime
  - Only Variable nodes can be subscribed
  - Select up to 5 variables for Scope/Recording
  - Status bar shows context-specific shortcuts
  - Publishing Interval (Settings) controls how often
    the server sends data and affects Scope, Trend,
    and CSV data resolution
";
    }

    private void OnThemeChanged(AppTheme theme)
    {
        Application.Invoke(() =>
        {
            ColorScheme = theme.MainColorScheme;
            BorderStyle = theme.EmphasizedBorderStyle;
            SetNeedsLayout();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }
}
