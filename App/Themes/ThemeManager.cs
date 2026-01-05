namespace OpcScope.App.Themes;

/// <summary>
/// Manages application themes and provides global access to the current theme.
/// Themes are inspired by retro-futuristic "Cassette Futurism" aesthetics.
/// </summary>
public static class ThemeManager
{
    private static RetroTheme _currentTheme = new AmberCrtTheme();

    /// <summary>
    /// Available themes in the application.
    /// </summary>
    public static IReadOnlyList<RetroTheme> AvailableThemes { get; } = new RetroTheme[]
    {
        new AmberCrtTheme(),
        new GreenCrtTheme(),
        new BlueCrtTheme(),
        new LcdSurveillanceTheme(),
        new SquareWaveTheme()
    };

    /// <summary>
    /// Gets the currently active theme.
    /// </summary>
    public static RetroTheme Current => _currentTheme;

    /// <summary>
    /// Event fired when the theme changes.
    /// </summary>
    public static event Action<RetroTheme>? ThemeChanged;

    /// <summary>
    /// Sets the current theme by name.
    /// </summary>
    public static void SetTheme(string themeName)
    {
        var theme = AvailableThemes.FirstOrDefault(t =>
            t.Name.Equals(themeName, StringComparison.OrdinalIgnoreCase));

        if (theme != null)
        {
            SetTheme(theme);
        }
    }

    /// <summary>
    /// Sets the current theme.
    /// </summary>
    public static void SetTheme(RetroTheme theme)
    {
        if (theme == null) return;

        _currentTheme = theme;
        ThemeChanged?.Invoke(theme);
    }

    /// <summary>
    /// Gets theme names for display in UI.
    /// </summary>
    public static string[] GetThemeNames()
    {
        return AvailableThemes.Select(t => t.Name).ToArray();
    }

    /// <summary>
    /// Gets the index of the current theme in the AvailableThemes list.
    /// </summary>
    public static int GetCurrentThemeIndex()
    {
        for (int i = 0; i < AvailableThemes.Count; i++)
        {
            if (AvailableThemes[i].Name == _currentTheme.Name)
                return i;
        }
        return 0;
    }

    /// <summary>
    /// Sets theme by index in the AvailableThemes list.
    /// </summary>
    public static void SetThemeByIndex(int index)
    {
        if (index >= 0 && index < AvailableThemes.Count)
        {
            SetTheme(AvailableThemes[index]);
        }
    }
}
