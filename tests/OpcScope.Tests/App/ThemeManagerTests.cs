using OpcScope.App.Themes;

namespace OpcScope.Tests.App;

public class ThemeManagerTests
{
    [Fact]
    public void ThemeManager_Current_ReturnsDefaultTheme()
    {
        // Act
        var theme = ThemeManager.Current;

        // Assert
        Assert.NotNull(theme);
        Assert.IsType<AmberCrtTheme>(theme);
    }

    [Fact]
    public void ThemeManager_AvailableThemes_ContainsAllThemes()
    {
        // Act
        var themes = ThemeManager.AvailableThemes;

        // Assert
        Assert.Equal(5, themes.Count);
        Assert.Contains(themes, t => t is AmberCrtTheme);
        Assert.Contains(themes, t => t is GreenCrtTheme);
        Assert.Contains(themes, t => t is BlueCrtTheme);
        Assert.Contains(themes, t => t is LcdSurveillanceTheme);
        Assert.Contains(themes, t => t is SquareWaveTheme);
    }

    [Fact]
    public void ThemeManager_SetTheme_ChangesCurrentTheme()
    {
        // Arrange
        var greenTheme = new GreenCrtTheme();

        // Act
        ThemeManager.SetTheme(greenTheme);

        // Assert
        Assert.IsType<GreenCrtTheme>(ThemeManager.Current);

        // Cleanup - restore default
        ThemeManager.SetTheme(new AmberCrtTheme());
    }

    [Fact]
    public void ThemeManager_SetTheme_FiresThemeChangedEvent()
    {
        // Arrange
        RetroTheme? changedTheme = null;
        Action<RetroTheme> handler = theme => changedTheme = theme;
        ThemeManager.ThemeChanged += handler;
        var blueTheme = new BlueCrtTheme();

        try
        {
            // Act
            ThemeManager.SetTheme(blueTheme);

            // Assert
            Assert.NotNull(changedTheme);
            Assert.IsType<BlueCrtTheme>(changedTheme);
        }
        finally
        {
            // Cleanup
            ThemeManager.ThemeChanged -= handler;
            ThemeManager.SetTheme(new AmberCrtTheme());
        }
    }

    [Fact]
    public void ThemeManager_SetTheme_WithNull_DoesNothing()
    {
        // Arrange
        var currentTheme = ThemeManager.Current;

        // Act
        ThemeManager.SetTheme((RetroTheme)null!);

        // Assert
        Assert.Same(currentTheme, ThemeManager.Current);
    }

    [Fact]
    public void ThemeManager_SetThemeByName_ChangesTheme()
    {
        // Act
        ThemeManager.SetTheme("Green CRT");

        // Assert
        Assert.IsType<GreenCrtTheme>(ThemeManager.Current);

        // Cleanup
        ThemeManager.SetTheme(new AmberCrtTheme());
    }

    [Fact]
    public void ThemeManager_SetThemeByName_IgnoresCase()
    {
        // Act
        ThemeManager.SetTheme("green crt");

        // Assert
        Assert.IsType<GreenCrtTheme>(ThemeManager.Current);

        // Cleanup
        ThemeManager.SetTheme(new AmberCrtTheme());
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
        Assert.Equal(5, names.Length);
        Assert.Contains("Amber CRT", names);
        Assert.Contains("Green CRT", names);
        Assert.Contains("Blue CRT", names);
        Assert.Contains("LCD Surveillance", names);
        Assert.Contains("SquareWave", names);
    }

    [Fact]
    public void ThemeManager_GetCurrentThemeIndex_ReturnsCorrectIndex()
    {
        // Arrange
        ThemeManager.SetTheme("Blue CRT");

        // Act
        var index = ThemeManager.GetCurrentThemeIndex();

        // Assert
        Assert.Equal(2, index); // Blue CRT is at index 2

        // Cleanup
        ThemeManager.SetTheme(new AmberCrtTheme());
    }

    [Fact]
    public void ThemeManager_SetThemeByIndex_ChangesTheme()
    {
        // Act
        ThemeManager.SetThemeByIndex(3); // LCD Surveillance

        // Assert
        Assert.IsType<LcdSurveillanceTheme>(ThemeManager.Current);

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
    public void ThemeManager_ConcurrentAccess_IsThreadSafe()
    {
        // Arrange
        var themes = new RetroTheme[] 
        { 
            new AmberCrtTheme(), 
            new GreenCrtTheme(), 
            new BlueCrtTheme() 
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

        Task.WaitAll(tasks.ToArray());

        // Assert
        Assert.Empty(exceptions);

        // Cleanup
        ThemeManager.SetTheme(new AmberCrtTheme());
    }

    [Fact]
    public void ThemeManager_ThemeChangedEvent_IsCapturedCorrectly()
    {
        // This test verifies that the event handler receives the correct theme
        // even when called outside the lock (testing the race condition fix)
        
        // Arrange
        var receivedThemes = new List<RetroTheme>();
        var lockObj = new object();
        Action<RetroTheme> handler = theme =>
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
            var themes = new RetroTheme[] 
            { 
                new AmberCrtTheme(), 
                new GreenCrtTheme(), 
                new BlueCrtTheme(),
                new LcdSurveillanceTheme(),
                new SquareWaveTheme()
            };

            foreach (var theme in themes)
            {
                ThemeManager.SetTheme(theme);
            }

            // Assert - All events should have been received
            Assert.Equal(5, receivedThemes.Count);
            for (int i = 0; i < themes.Length; i++)
            {
                Assert.Same(themes[i], receivedThemes[i]);
            }
        }
        finally
        {
            // Cleanup
            ThemeManager.ThemeChanged -= handler;
            ThemeManager.SetTheme(new AmberCrtTheme());
        }
    }
}
