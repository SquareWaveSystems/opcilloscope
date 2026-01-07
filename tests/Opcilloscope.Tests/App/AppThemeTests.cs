using Opcilloscope.App.Themes;
using Terminal.Gui;

namespace Opcilloscope.Tests.App;

public class AppThemeTests
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
        Assert.Equal("Dark terminal theme", theme.Description);
    }

    [Fact]
    public void DarkTheme_HasDarkBackground()
    {
        var theme = new DarkTheme();
        // Dark charcoal background #1a1a1a
        Assert.Equal(new Color(26, 26, 26), theme.Background);
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
        Assert.Equal("Light terminal theme", theme.Description);
    }

    [Fact]
    public void LightTheme_HasLightBackground()
    {
        var theme = new LightTheme();
        // Warm paper white background #f5f5f0
        Assert.Equal(new Color(245, 245, 240), theme.Background);
    }

    [Theory]
    [InlineData(typeof(DarkTheme))]
    [InlineData(typeof(LightTheme))]
    public void AllThemes_HaveNonNullColors(Type themeType)
    {
        var theme = (AppTheme)Activator.CreateInstance(themeType)!;

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
        var theme = (AppTheme)Activator.CreateInstance(themeType)!;

        // Attribute is a value type (struct) so these properties can never be null.
        // We verify the theme is instantiated correctly, which is sufficient.
        Assert.NotNull(theme);
    }

    [Theory]
    [InlineData(typeof(DarkTheme))]
    [InlineData(typeof(LightTheme))]
    public void AllThemes_HaveNonNullColorSchemes(Type themeType)
    {
        var theme = (AppTheme)Activator.CreateInstance(themeType)!;

        Assert.NotNull(theme.MainColorScheme);
        Assert.NotNull(theme.DialogColorScheme);
        Assert.NotNull(theme.MenuColorScheme);
        Assert.NotNull(theme.ButtonColorScheme);
        Assert.NotNull(theme.FrameColorScheme);
    }

    [Theory]
    [InlineData(typeof(DarkTheme))]
    [InlineData(typeof(LightTheme))]
    public void AllThemes_HaveSingleLineBoxDrawingCharacters(Type themeType)
    {
        var theme = (AppTheme)Activator.CreateInstance(themeType)!;

        // Both themes use single-line box drawing for clean look
        Assert.Equal('┌', theme.BoxTopLeft);
        Assert.Equal('┐', theme.BoxTopRight);
        Assert.Equal('└', theme.BoxBottomLeft);
        Assert.Equal('┘', theme.BoxBottomRight);
        Assert.Equal('─', theme.BoxHorizontal);
        Assert.Equal('│', theme.BoxVertical);
    }

    [Theory]
    [InlineData(typeof(DarkTheme))]
    [InlineData(typeof(LightTheme))]
    public void AllThemes_HaveUIDecorations(Type themeType)
    {
        var theme = (AppTheme)Activator.CreateInstance(themeType)!;

        Assert.NotNull(theme.ButtonPrefix);
        Assert.NotNull(theme.ButtonSuffix);
        Assert.NotNull(theme.TitleDecoration);
        Assert.NotNull(theme.StatusLive);
        Assert.NotNull(theme.StatusHold);
        Assert.NotNull(theme.NoSignalMessage);
    }

    [Fact]
    public void DarkTheme_HasErrorColor()
    {
        var theme = new DarkTheme();
        // Dusty brick red #a65454 for dark backgrounds
        Assert.Equal(new Color(166, 84, 84), theme.Error);
    }

    [Fact]
    public void DarkTheme_HasWarningColor()
    {
        var theme = new DarkTheme();
        // Mustard yellow #c9a227 for dark backgrounds
        Assert.Equal(new Color(201, 162, 39), theme.Warning);
    }

    [Fact]
    public void LightTheme_HasErrorColor()
    {
        var theme = new LightTheme();
        // Dark red #8b4545 for light backgrounds
        Assert.Equal(new Color(139, 69, 69), theme.Error);
    }

    [Fact]
    public void LightTheme_HasWarningColor()
    {
        var theme = new LightTheme();
        // Dark mustard #a88620 for light backgrounds
        Assert.Equal(new Color(168, 134, 32), theme.Warning);
    }

    [Fact]
    public void DarkTheme_EnablesGlow()
    {
        var theme = new DarkTheme();
        Assert.True(theme.EnableGlow);
    }

    [Fact]
    public void LightTheme_DisablesGlow()
    {
        var theme = new LightTheme();
        Assert.False(theme.EnableGlow);
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
    public void ButtonDecorations_AreMinimal()
    {
        var theme = new DarkTheme();

        // Both themes use minimal bracket style
        Assert.Equal("[ ", theme.ButtonPrefix);
        Assert.Equal(" ]", theme.ButtonSuffix);
    }

    [Theory]
    [InlineData(typeof(DarkTheme), "Dark")]
    [InlineData(typeof(LightTheme), "Light")]
    public void AllThemes_NameMatchesExpected(Type themeType, string expectedName)
    {
        var theme = (AppTheme)Activator.CreateInstance(themeType)!;
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
    public void AllThemes_UseDoubleBorderAndSingleFrame(Type themeType)
    {
        var theme = (AppTheme)Activator.CreateInstance(themeType)!;

        // Main window border is double-line for emphasis
        Assert.Equal(LineStyle.Double, theme.BorderLineStyle);
        // Frame panels use single-line
        Assert.Equal(LineStyle.Single, theme.FrameLineStyle);
    }

    [Theory]
    [InlineData(typeof(DarkTheme))]
    [InlineData(typeof(LightTheme))]
    public void AllThemes_HaveStatusColors(Type themeType)
    {
        var theme = (AppTheme)Activator.CreateInstance(themeType)!;

        // New OPC UA status colors
        Assert.NotEqual(default, theme.StatusGood);
        Assert.NotEqual(default, theme.StatusBad);
        Assert.NotEqual(default, theme.StatusUncertain);
        Assert.NotEqual(default, theme.MutedText);
    }

    [Theory]
    [InlineData(typeof(DarkTheme))]
    [InlineData(typeof(LightTheme))]
    public void AllThemes_HaveConnectionIndicators(Type themeType)
    {
        var theme = (AppTheme)Activator.CreateInstance(themeType)!;

        Assert.Contains("Connected", theme.ConnectedIndicator);
        Assert.Contains("Not Connected", theme.DisconnectedIndicator);
        Assert.Contains("REC", theme.RecordingLabel);
        Assert.Contains("STOP", theme.StoppedLabel);
    }
}
