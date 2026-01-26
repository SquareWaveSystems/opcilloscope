using Opcilloscope.App.Themes;

namespace Opcilloscope.Tests.App;

public class ThemeManagerTests
{
    [Fact]
    public void ThemeManager_Current_ReturnsDefaultTheme()
    {
        // Act
        var theme = ThemeManager.Current;

        // Assert
        Assert.NotNull(theme);
        Assert.IsType<DarkTheme>(theme);
    }

    [Fact]
    public void ThemeManager_AvailableThemes_ContainsAllThemes()
    {
        // Act
        var themes = ThemeManager.AvailableThemes;

        // Assert
        Assert.Equal(2, themes.Count);
        Assert.Contains(themes, t => t is DarkTheme);
        Assert.Contains(themes, t => t is LightTheme);
    }

    [Fact]
    public void ThemeManager_SetTheme_ChangesCurrentTheme()
    {
        // Arrange
        var lightTheme = new LightTheme();

        // Act
        ThemeManager.SetTheme(lightTheme);

        // Assert
        Assert.IsType<LightTheme>(ThemeManager.Current);

        // Cleanup - restore default
        ThemeManager.SetTheme(new DarkTheme());
    }

    [Fact]
    public void ThemeManager_SetTheme_FiresThemeChangedEvent()
    {
        // Arrange
        AppTheme? changedTheme = null;
        Action<AppTheme> handler = theme => changedTheme = theme;
        ThemeManager.ThemeChanged += handler;
        var lightTheme = new LightTheme();

        try
        {
            // Act
            ThemeManager.SetTheme(lightTheme);

            // Assert
            Assert.NotNull(changedTheme);
            Assert.IsType<LightTheme>(changedTheme);
        }
        finally
        {
            // Cleanup
            ThemeManager.ThemeChanged -= handler;
            ThemeManager.SetTheme(new DarkTheme());
        }
    }

    [Fact]
    public void ThemeManager_SetTheme_WithNull_DoesNothing()
    {
        // Arrange
        var currentTheme = ThemeManager.Current;

        // Act
        ThemeManager.SetTheme((AppTheme)null!);

        // Assert
        Assert.Same(currentTheme, ThemeManager.Current);
    }

    [Fact]
    public void ThemeManager_SetThemeByName_ChangesTheme()
    {
        // Act
        ThemeManager.SetTheme("Light");

        // Assert
        Assert.IsType<LightTheme>(ThemeManager.Current);

        // Cleanup
        ThemeManager.SetTheme(new DarkTheme());
    }

    [Fact]
    public void ThemeManager_SetThemeByName_IgnoresCase()
    {
        // Act
        ThemeManager.SetTheme("light");

        // Assert
        Assert.IsType<LightTheme>(ThemeManager.Current);

        // Cleanup
        ThemeManager.SetTheme(new DarkTheme());
    }

    [Fact]
    public void ThemeManager_SetThemeByName_WithInvalidName_DoesNothing()
    {
        // Arrange
        var currentTheme = ThemeManager.Current;

        // Act
        ThemeManager.SetTheme("NonExistent Theme");

        // Assert
        Assert.Same(currentTheme, ThemeManager.Current);
    }

    [Fact]
    public void ThemeManager_GetThemeNames_ReturnsAllNames()
    {
        // Act
        var names = ThemeManager.GetThemeNames();

        // Assert
        Assert.Equal(2, names.Length);
        Assert.Contains("Dark", names);
        Assert.Contains("Light", names);
    }

    [Fact]
    public void ThemeManager_GetCurrentThemeIndex_ReturnsCorrectIndex()
    {
        // Arrange
        ThemeManager.SetTheme("Light");

        // Act
        var index = ThemeManager.GetCurrentThemeIndex();

        // Assert
        Assert.Equal(1, index); // Light is at index 1

        // Cleanup
        ThemeManager.SetTheme(new DarkTheme());
    }

    [Fact]
    public void ThemeManager_SetThemeByIndex_ChangesTheme()
    {
        // Act
        ThemeManager.SetThemeByIndex(1); // Light

        // Assert
        Assert.IsType<LightTheme>(ThemeManager.Current);

        // Cleanup
        ThemeManager.SetThemeByIndex(0);
    }

    [Fact]
    public void ThemeManager_SetThemeByIndex_WithInvalidIndex_DoesNothing()
    {
        // Arrange
        var currentTheme = ThemeManager.Current;

        // Act
        ThemeManager.SetThemeByIndex(-1);
        Assert.Same(currentTheme, ThemeManager.Current);

        ThemeManager.SetThemeByIndex(100);
        Assert.Same(currentTheme, ThemeManager.Current);
    }

    [Fact]
    public async Task ThemeManager_ConcurrentAccess_IsThreadSafe()
    {
        // Arrange
        var themes = new AppTheme[]
        {
            new DarkTheme(),
            new LightTheme()
        };
        var exceptions = new List<Exception>();
        var tasks = new List<Task>();

        // Act - Multiple threads reading and writing concurrently
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 100; j++)
                    {
                        // Read current theme
                        var _ = ThemeManager.Current;

                        // Set a theme
                        ThemeManager.SetTheme(themes[j % themes.Length]);

                        // Read again
                        _ = ThemeManager.Current;
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);

        // Cleanup
        ThemeManager.SetTheme(new DarkTheme());
    }

    [Fact]
    public void ThemeManager_ThemeChangedEvent_IsCapturedCorrectly()
    {
        // This test verifies that the event handler receives the correct theme
        // even when called outside the lock (testing the race condition fix)

        // Arrange
        var receivedThemes = new List<AppTheme>();
        var lockObj = new object();
        Action<AppTheme> handler = theme =>
        {
            lock (lockObj)
            {
                receivedThemes.Add(theme);
            }
        };
        ThemeManager.ThemeChanged += handler;

        try
        {
            // Act - Rapidly change themes
            var themes = new AppTheme[]
            {
                new DarkTheme(),
                new LightTheme()
            };

            foreach (var theme in themes)
            {
                ThemeManager.SetTheme(theme);
            }

            // Assert - All events should have been received
            Assert.Equal(2, receivedThemes.Count);
            for (int i = 0; i < themes.Length; i++)
            {
                Assert.Same(themes[i], receivedThemes[i]);
            }
        }
        finally
        {
            // Cleanup
            ThemeManager.ThemeChanged -= handler;
            ThemeManager.SetTheme(new DarkTheme());
        }
    }
}
