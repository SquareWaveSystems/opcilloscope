using Terminal.Gui;

namespace Opcilloscope.App.Themes;

/// <summary>
/// Helper class to apply Terminal.Gui v2 styling based on the current theme.
/// Centralizes border, margin, padding, and color scheme application.
/// </summary>
public static class ThemeStyler
{
    /// <summary>
    /// Applies full theme styling to a frame including borders, colors, and spacing.
    /// Note: Does NOT override BorderStyle - caller should set that explicitly to control
    /// whether a view uses emphasized (double-line) or secondary (single-line) borders.
    /// </summary>
    public static void ApplyToFrame(FrameView frame, AppTheme? theme = null)
    {
        theme ??= ThemeManager.Current;

        // Apply color scheme
        frame.SetScheme(theme.MainColorScheme);

        // Note: BorderStyle is NOT set here - callers should set it explicitly
        // to control emphasized vs secondary border styling

        // NOTE: Terminal.Gui 2.4 made adornments (Border/Margin/Padding) non-View objects
        // without their own Scheme, so borders now render with the view's scheme. The former
        // per-border grey/focus colouring (BorderColorScheme) no longer applies here.

        // Apply margin and padding from theme
        if (frame.Margin != null)
        {
            frame.Margin.Thickness = theme.MarginThickness;
        }

        if (frame.Padding != null)
        {
            frame.Padding.Thickness = theme.PaddingThickness;
        }
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
    /// Applies menu bar styling.
    /// </summary>
    public static void ApplyToMenuBar(MenuBar menuBar, AppTheme? theme = null)
    {
        theme ??= ThemeManager.Current;
        menuBar.SetScheme(theme.MenuColorScheme);
    }

    /// <summary>
    /// Creates the accent-colored scheme used to highlight a dialog's default button.
    /// </summary>
    public static Scheme CreateAccentButtonScheme(AppTheme? theme = null)
    {
        theme ??= ThemeManager.Current;
        return new Scheme
        {
            Normal = new Attribute(theme.Accent, theme.Background),
            Focus = new Attribute(theme.AccentBright, theme.Background),
            HotNormal = new Attribute(theme.Accent, theme.Background),
            HotFocus = new Attribute(theme.AccentBright, theme.Background),
            Disabled = new Attribute(theme.MutedText, theme.Background)
        };
    }

    /// <summary>
    /// Creates the flat menu/status bar scheme. Unlike MenuColorScheme this keeps the
    /// theme background on focus, avoiding inverted highlight flashes on the bars.
    /// </summary>
    public static Scheme CreateFlatBarScheme(AppTheme? theme = null)
    {
        theme ??= ThemeManager.Current;
        return new Scheme
        {
            Normal = new Attribute(theme.Foreground, theme.Background),
            Focus = new Attribute(theme.ForegroundBright, theme.Background),
            HotNormal = new Attribute(theme.Accent, theme.Background),
            HotFocus = new Attribute(theme.AccentBright, theme.Background),
            Disabled = new Attribute(theme.MutedText, theme.Background)
        };
    }
}
