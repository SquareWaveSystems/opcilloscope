namespace OpcScope.App.Themes;

/// <summary>
/// Manages application themes and provides global access to the current theme.
/// </summary>
public static class ThemeManager
{
    private static AppTheme _currentTheme = new DarkTheme();
    private static readonly object _lock = new();

    /// <summary>
    /// Available themes in the application.
    /// </summary>
    public static IReadOnlyList<AppTheme> AvailableThemes { get; } = new AppTheme[]
    {
        new DarkTheme(),
        new LightTheme()
    };

    /// <summary>
    /// Gets the currently active theme.
    /// </summary>
    public static AppTheme Current
    {
        get
        {
            lock (_lock)
            {
                return _currentTheme;
            }
        }
    }

    /// <summary>
    /// Event fired when the theme changes.
    /// </summary>
    public static event Action<AppTheme>? ThemeChanged;

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
    public static void SetTheme(AppTheme theme)
    {
        if (theme == null) return;

        AppTheme themeToUse;
        Action<AppTheme>? handlers;

        lock (_lock)
        {
            _currentTheme = theme;
            themeToUse = _currentTheme;
            handlers = ThemeChanged;
        }

        handlers?.Invoke(themeToUse);
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
        lock (_lock)
        {
            for (int i = 0; i < AvailableThemes.Count; i++)
            {
                if (AvailableThemes[i].Name == _currentTheme.Name)
                    return i;
            }
            return 0;
        }
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
