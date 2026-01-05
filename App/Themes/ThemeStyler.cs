using Terminal.Gui;

namespace OpcScope.App.Themes;

/// <summary>
/// Helper class to apply Terminal.Gui v2 styling based on the current theme.
/// Centralizes border, margin, padding, and color scheme application.
/// </summary>
public static class ThemeStyler
{
    /// <summary>
    /// Applies full theme styling to a view including borders, colors, and spacing.
    /// </summary>
    public static void ApplyTo(View view, RetroTheme? theme = null)
    {
        theme ??= ThemeManager.Current;

        // Apply color scheme
        view.ColorScheme = theme.MainColorScheme;

        // Apply border style using Terminal.Gui v2 BorderStyle property
        view.BorderStyle = theme.FrameLineStyle;

        // Configure border colors
        if (view.Border != null)
        {
            view.Border.ColorScheme = theme.FrameColorScheme;
        }

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
    public static void ApplyToFrame(FrameView frame, RetroTheme? theme = null)
    {
        theme ??= ThemeManager.Current;

        // Apply base styling
        ApplyTo(frame, theme);
    }

    /// <summary>
    /// Applies dialog-specific styling.
    /// </summary>
    public static void ApplyToDialog(Dialog dialog, RetroTheme? theme = null)
    {
        theme ??= ThemeManager.Current;

        dialog.ColorScheme = theme.DialogColorScheme;
        dialog.BorderStyle = theme.BorderLineStyle;

        if (dialog.Border != null)
        {
            dialog.Border.ColorScheme = theme.FrameColorScheme;
        }
    }

    /// <summary>
    /// Applies styling to a button.
    /// </summary>
    public static void ApplyToButton(Button button, RetroTheme? theme = null)
    {
        theme ??= ThemeManager.Current;
        button.ColorScheme = theme.ButtonColorScheme;
    }

    /// <summary>
    /// Applies menu bar styling.
    /// </summary>
    public static void ApplyToMenuBar(MenuBar menuBar, RetroTheme? theme = null)
    {
        theme ??= ThemeManager.Current;
        menuBar.ColorScheme = theme.MenuColorScheme;
    }

    /// <summary>
    /// Applies status bar styling.
    /// </summary>
    public static void ApplyToStatusBar(StatusBar statusBar, RetroTheme? theme = null)
    {
        theme ??= ThemeManager.Current;
        statusBar.ColorScheme = theme.MenuColorScheme;
    }

    /// <summary>
    /// Creates a styled label with theme colors.
    /// </summary>
    public static Label CreateLabel(string text, RetroTheme? theme = null)
    {
        theme ??= ThemeManager.Current;
        return new Label
        {
            Text = text,
            ColorScheme = theme.MainColorScheme
        };
    }

    /// <summary>
    /// Creates a styled TextField with theme colors.
    /// </summary>
    public static TextField CreateTextField(string text = "", RetroTheme? theme = null)
    {
        theme ??= ThemeManager.Current;
        return new TextField
        {
            Text = text,
            ColorScheme = theme.MainColorScheme
        };
    }

    /// <summary>
    /// Creates a styled button with theme decorations.
    /// </summary>
    public static Button CreateButton(string text, RetroTheme? theme = null)
    {
        theme ??= ThemeManager.Current;
        return new Button
        {
            Text = text,
            ColorScheme = theme.ButtonColorScheme
        };
    }
}
