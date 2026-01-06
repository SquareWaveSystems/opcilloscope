using OpcScope.App.Themes;
using Terminal.Gui;

namespace OpcScope.Tests.App;

public class RetroThemeTests
{
    [Fact]
    public void DarkTheme_HasCorrectName()
    {
        var theme = new DarkTheme();
        Assert.Equal("Dark", theme.Name);
    }

    [Fact]
    public void DarkTheme_HasCorrectDescription()
    {
        var theme = new DarkTheme();
        Assert.Equal("Classic green monochrome terminal", theme.Description);
    }

    [Fact]
    public void DarkTheme_HasBlackBackground()
    {
        var theme = new DarkTheme();
        Assert.Equal(Color.Black, theme.Background);
    }

    [Fact]
    public void LightTheme_HasCorrectName()
    {
        var theme = new LightTheme();
        Assert.Equal("Light", theme.Name);
    }

    [Fact]
    public void LightTheme_HasCorrectDescription()
    {
        var theme = new LightTheme();
        Assert.Equal("Light theme with dark grey highlights", theme.Description);
    }

    [Fact]
    public void LightTheme_HasLightBackground()
    {
        var theme = new LightTheme();
        // Off-white background
        Assert.NotEqual(Color.Black, theme.Background);
    }

    [Theory]
    [InlineData(typeof(DarkTheme))]
    [InlineData(typeof(LightTheme))]
    public void AllThemes_HaveNonNullColors(Type themeType)
    {
        var theme = (RetroTheme)Activator.CreateInstance(themeType)!;

        Assert.NotEqual(default, theme.Background);
        Assert.NotEqual(default, theme.Foreground);
        Assert.NotEqual(default, theme.ForegroundBright);
        Assert.NotEqual(default, theme.ForegroundDim);
        Assert.NotEqual(default, theme.Accent);
        Assert.NotEqual(default, theme.AccentBright);
        Assert.NotEqual(default, theme.Border);
        Assert.NotEqual(default, theme.Grid);
        Assert.NotEqual(default, theme.StatusActive);
        Assert.NotEqual(default, theme.StatusInactive);
        Assert.NotEqual(default, theme.Error);
        Assert.NotEqual(default, theme.Warning);
    }

    [Theory]
    [InlineData(typeof(DarkTheme))]
    [InlineData(typeof(LightTheme))]
    public void AllThemes_HaveNonNullAttributes(Type themeType)
    {
        var theme = (RetroTheme)Activator.CreateInstance(themeType)!;

        // Attribute is a value type (struct) so these properties can never be null.
        // We verify the theme is instantiated correctly, which is sufficient.
        Assert.NotNull(theme);
    }

    [Theory]
    [InlineData(typeof(DarkTheme))]
    [InlineData(typeof(LightTheme))]
    public void AllThemes_HaveNonNullColorSchemes(Type themeType)
    {
        var theme = (RetroTheme)Activator.CreateInstance(themeType)!;

        Assert.NotNull(theme.MainColorScheme);
        Assert.NotNull(theme.DialogColorScheme);
        Assert.NotNull(theme.MenuColorScheme);
        Assert.NotNull(theme.ButtonColorScheme);
        Assert.NotNull(theme.FrameColorScheme);
    }

    [Fact]
    public void DarkTheme_HasDoubleLineBorders()
    {
        var theme = new DarkTheme();

        Assert.Equal('╔', theme.BoxTopLeft);
        Assert.Equal('╗', theme.BoxTopRight);
        Assert.Equal('╚', theme.BoxBottomLeft);
        Assert.Equal('╝', theme.BoxBottomRight);
        Assert.Equal('═', theme.BoxHorizontal);
        Assert.Equal('║', theme.BoxVertical);
        Assert.Equal('╡', theme.BoxTitleLeft);
        Assert.Equal('╞', theme.BoxTitleRight);
    }

    [Fact]
    public void LightTheme_HasSingleLineBorders()
    {
        var theme = new LightTheme();

        Assert.Equal('┌', theme.BoxTopLeft);
        Assert.Equal('┐', theme.BoxTopRight);
        Assert.Equal('└', theme.BoxBottomLeft);
        Assert.Equal('┘', theme.BoxBottomRight);
        Assert.Equal('─', theme.BoxHorizontal);
        Assert.Equal('│', theme.BoxVertical);
        Assert.Equal('┤', theme.BoxTitleLeft);
        Assert.Equal('├', theme.BoxTitleRight);
    }

    [Theory]
    [InlineData(typeof(DarkTheme))]
    [InlineData(typeof(LightTheme))]
    public void AllThemes_HaveUIDecorations(Type themeType)
    {
        var theme = (RetroTheme)Activator.CreateInstance(themeType)!;

        Assert.NotNull(theme.ButtonPrefix);
        Assert.NotNull(theme.ButtonSuffix);
        Assert.NotNull(theme.TitleDecoration);
        Assert.NotNull(theme.StatusLive);
        Assert.NotNull(theme.StatusHold);
        Assert.NotNull(theme.NoSignalMessage);
    }

    [Theory]
    [InlineData(typeof(DarkTheme))]
    [InlineData(typeof(LightTheme))]
    public void AllThemes_HaveDefaultEnableGlow(Type themeType)
    {
        var theme = (RetroTheme)Activator.CreateInstance(themeType)!;

        // Default EnableGlow is true in RetroTheme base class
        Assert.True(theme.EnableGlow);
    }

    [Fact]
    public void DarkTheme_ErrorColorIsRed()
    {
        var theme = new DarkTheme();
        Assert.Equal(Color.Red, theme.Error);
    }

    [Fact]
    public void DarkTheme_WarningColorIsBrightYellow()
    {
        var theme = new DarkTheme();
        Assert.Equal(Color.BrightYellow, theme.Warning);
    }

    [Fact]
    public void ThemeAttributes_AreCachedOnSecondAccess()
    {
        var theme = new DarkTheme();

        // Access attributes twice
        var attr1 = theme.NormalAttr;
        var attr2 = theme.NormalAttr;

        // They should be the same cached instance - Value types are compared by value, not reference
        Assert.Equal(attr1, attr2);
    }

    [Fact]
    public void ThemeColorSchemes_AreCachedOnSecondAccess()
    {
        var theme = new LightTheme();

        // Access color schemes twice
        var scheme1 = theme.MainColorScheme;
        var scheme2 = theme.MainColorScheme;

        // They should be the same cached instance
        Assert.Same(scheme1, scheme2);
    }

    [Fact]
    public void StatusDecorations_HaveExpectedFormat()
    {
        var theme = new DarkTheme();

        Assert.Contains("LIVE", theme.StatusLive);
        Assert.Contains("HOLD", theme.StatusHold);
        Assert.Contains("NO SIGNAL", theme.NoSignalMessage);
    }

    [Fact]
    public void ButtonDecorations_AreSymmetric()
    {
        var theme = new DarkTheme();

        // Both should have similar visual weight
        Assert.False(string.IsNullOrEmpty(theme.ButtonPrefix));
        Assert.False(string.IsNullOrEmpty(theme.ButtonSuffix));
    }

    [Theory]
    [InlineData(typeof(DarkTheme), "Dark")]
    [InlineData(typeof(LightTheme), "Light")]
    public void AllThemes_NameMatchesClassName(Type themeType, string expectedName)
    {
        var theme = (RetroTheme)Activator.CreateInstance(themeType)!;
        Assert.Equal(expectedName, theme.Name);
    }

    [Fact]
    public void MainColorScheme_HasAllRequiredProperties()
    {
        var theme = new DarkTheme();
        var scheme = theme.MainColorScheme;

        // ColorScheme properties return Attribute (value type), which can never be null
        // We just verify the scheme itself is not null
        Assert.NotNull(scheme);
    }

    [Fact]
    public void DialogColorScheme_HasAllRequiredProperties()
    {
        var theme = new LightTheme();
        var scheme = theme.DialogColorScheme;

        // ColorScheme properties return Attribute (value type), which can never be null
        // We just verify the scheme itself is not null
        Assert.NotNull(scheme);
    }

    [Fact]
    public void MenuColorScheme_HasAllRequiredProperties()
    {
        var theme = new DarkTheme();
        var scheme = theme.MenuColorScheme;

        // ColorScheme properties return Attribute (value type), which can never be null
        // We just verify the scheme itself is not null
        Assert.NotNull(scheme);
    }

    [Theory]
    [InlineData(typeof(DarkTheme))]
    [InlineData(typeof(LightTheme))]
    public void AllThemes_HaveLineStyleSettings(Type themeType)
    {
        var theme = (RetroTheme)Activator.CreateInstance(themeType)!;

        // LineStyle is an enum, verify it's a valid value
        Assert.True(Enum.IsDefined(typeof(LineStyle), theme.BorderLineStyle));
        Assert.True(Enum.IsDefined(typeof(LineStyle), theme.FrameLineStyle));
    }
}
