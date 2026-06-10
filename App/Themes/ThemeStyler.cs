using Terminal.Gui;

namespace Opcilloscope.App.Themes;

/// <summary>
/// Helper class to apply Terminal.Gui v2 styling based on the current theme.
/// Centralizes border, margin, padding, and color scheme application.
/// </summary>
public static class ThemeStyler
{
    /// <summary>
    /// Applies full theme styling to a view including borders, colors, and spacing.
    /// Note: Does NOT override BorderStyle - caller should set that explicitly to control
    /// whether a view uses emphasized (double-line) or secondary (single-line) borders.
    /// </summary>
    public static void ApplyTo(View view, AppTheme? theme = null)
    {
        theme ??= ThemeManager.Current;

        // Apply color scheme
        view.SetScheme(theme.MainColorScheme);

        // Note: BorderStyle is NOT set here - callers should set it explicitly
        // to control emphasized vs secondary border styling

        // NOTE: Terminal.Gui 2.4 made adornments (Border/Margin/Padding) non-View objects
        // without their own Scheme, so borders now render with the view's scheme. The former
        // per-border grey/focus colouring (BorderColorScheme) no longer applies here.

        // Apply margin and padding from theme
        if (view.Margin != null)
        {
            view.Margin.Thickness = theme.MarginThickness;
        }

        if (view.Padding != null)
        {
            view.Padding.Thickness = theme.PaddingThickness;
        }
    }

    /// <summary>
    /// Applies styling to a FrameView with themed borders.
    /// </summary>
    public static void ApplyToFrame(FrameView frame, AppTheme? theme = null)
    {
        theme ??= ThemeManager.Current;

        // Apply base styling
        ApplyTo(frame, theme);
    }

    /// <summary>
    /// Applies dialog-specific styling.
    /// </summary>
    public static void ApplyToDialog(Dialog dialog, AppTheme? theme = null)
    {
        theme ??= ThemeManager.Current;

        dialog.SetScheme(theme.DialogColorScheme);
        dialog.BorderStyle = theme.BorderLineStyle;
    }

    /// <summary>
    /// Applies styling to a button.
    /// </summary>
    public static void ApplyToButton(Button button, AppTheme? theme = null)
    {
        theme ??= ThemeManager.Current;
        button.SetScheme(theme.ButtonColorScheme);
    }

    /// <summary>
    /// Applies menu bar styling.
    /// </summary>
    public static void ApplyToMenuBar(MenuBar menuBar, AppTheme? theme = null)
    {
        theme ??= ThemeManager.Current;
        menuBar.SetScheme(theme.MenuColorScheme);
    }

    /// <summary>
    /// Applies status bar styling.
    /// </summary>
    public static void ApplyToStatusBar(StatusBar statusBar, AppTheme? theme = null)
    {
        theme ??= ThemeManager.Current;
        statusBar.SetScheme(theme.MenuColorScheme);
    }

    /// <summary>
    /// Creates a styled label with theme colors.
    /// </summary>
    public static Label CreateLabel(string text, AppTheme? theme = null)
    {
        theme ??= ThemeManager.Current;
        var label = new Label { Text = text };
        label.SetScheme(theme.MainColorScheme);
        return label;
    }

    /// <summary>
    /// Creates a styled TextField with theme colors.
    /// </summary>
    public static TextField CreateTextField(string text = "", AppTheme? theme = null)
    {
        theme ??= ThemeManager.Current;
        var field = new TextField { Text = text };
        field.SetScheme(theme.MainColorScheme);
        return field;
    }

    /// <summary>
    /// Creates a styled button with theme decorations.
    /// </summary>
    public static Button CreateButton(string text, AppTheme? theme = null)
    {
        theme ??= ThemeManager.Current;
        var button = new Button { Text = text };
        button.SetScheme(theme.ButtonColorScheme);
        return button;
    }
}
