using Terminal.Gui;
using Opcilloscope.App.Themes;
using Attribute = Terminal.Gui.Attribute;
using ThemeManager = Opcilloscope.App.Themes.ThemeManager;

namespace Opcilloscope.App.Dialogs;

/// <summary>
/// Help dialog displaying all keyboard shortcuts in a formatted layout.
/// </summary>
public class HelpDialog : Dialog
{
    public HelpDialog()
    {
        Title = " opcilloscope - Help ";
        Width = 64;
        Height = 32;

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

        contentView.Text = @"
NAVIGATION
  Tab              Move between panels
  Arrow Keys       Navigate within panel
  Space            Expand/collapse tree node

MONITORING
  Enter            Subscribe to selected node
  Delete           Unsubscribe from item
  W                Write value to node

RECORDING & SCOPE
  Space            Toggle recording selection (in variables)
  Ctrl+R           Start/stop recording
  Ctrl+G           Open Scope with selected variables

SCOPE VIEW CONTROLS
  Space            Pause/resume plotting
  +/-              Adjust vertical scale
  R                Reset to auto-scale

APPLICATION
  F5               Refresh address space
  F10              Open menu
  Ctrl+O           Open configuration
  Ctrl+S           Save configuration
  Ctrl+Q           Quit

TIPS
  - Only Variable nodes can be subscribed
  - Select up to 5 variables for Scope/Recording
  - Press Space on variables to toggle selection
";

        // OK button
        var okButton = new Button
        {
            Text = "OK",
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1),
            IsDefault = true,
            ColorScheme = theme.ButtonColorScheme
        };
        okButton.Accepting += (_, _) => RequestStop();

        Add(contentView);
        Add(okButton);

        // Subscribe to theme changes
        ThemeManager.ThemeChanged += OnThemeChanged;
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
