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
        frame.ColorScheme = theme.MainColorScheme;

        // Note: BorderStyle is NOT set here - callers should set it explicitly
        // to control emphasized vs secondary border styling

        // Configure border colors - use BorderColorScheme for consistent grey borders
        // that don't change to amber/yellow when focused (avoids terminal inconsistencies)
        if (frame.Border != null)
        {
            frame.Border.ColorScheme = theme.BorderColorScheme;
        }

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

        dialog.ColorScheme = theme.DialogColorScheme;
        dialog.BorderStyle = theme.BorderLineStyle;

        if (dialog.Border != null)
        {
            dialog.Border.ColorScheme = theme.BorderColorScheme;
        }
    }

    /// <summary>
    /// Applies menu bar styling.
    /// </summary>
    public static void ApplyToMenuBar(MenuBar menuBar, AppTheme? theme = null)
    {
        theme ??= ThemeManager.Current;
        menuBar.ColorScheme = theme.MenuColorScheme;
    }

    /// <summary>
    /// Creates the accent-colored scheme used to highlight a dialog's default button.
    /// </summary>
    public static ColorScheme CreateAccentButtonScheme(AppTheme? theme = null)
    {
        theme ??= ThemeManager.Current;
        return new ColorScheme
        {
            Normal = new Terminal.Gui.Attribute(theme.Accent, theme.Background),
            Focus = new Terminal.Gui.Attribute(theme.AccentBright, theme.Background),
            HotNormal = new Terminal.Gui.Attribute(theme.Accent, theme.Background),
            HotFocus = new Terminal.Gui.Attribute(theme.AccentBright, theme.Background),
            Disabled = new Terminal.Gui.Attribute(theme.MutedText, theme.Background)
        };
    }

    /// <summary>
    /// Creates the flat menu/status bar scheme. Unlike MenuColorScheme this keeps the
    /// theme background on focus, avoiding inverted highlight flashes on the bars.
    /// </summary>
    public static ColorScheme CreateFlatBarScheme(AppTheme? theme = null)
    {
        theme ??= ThemeManager.Current;
        return new ColorScheme
        {
            Normal = new Terminal.Gui.Attribute(theme.Foreground, theme.Background),
            Focus = new Terminal.Gui.Attribute(theme.ForegroundBright, theme.Background),
            HotNormal = new Terminal.Gui.Attribute(theme.Accent, theme.Background),
            HotFocus = new Terminal.Gui.Attribute(theme.AccentBright, theme.Background),
            Disabled = new Terminal.Gui.Attribute(theme.MutedText, theme.Background)
        };
    }
}
