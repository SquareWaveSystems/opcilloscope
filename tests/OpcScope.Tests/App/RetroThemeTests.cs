using OpcScope.App.Themes;
using Terminal.Gui;

namespace OpcScope.Tests.App;

public class RetroThemeTests
{
    [Fact]
    public void AmberTheme_HasCorrectName()
    {
        var theme = new AmberTheme();
        Assert.Equal("Amber", theme.Name);
    }

    [Fact]
    public void AmberTheme_HasCorrectDescription()
    {
        var theme = new AmberTheme();
        Assert.Equal("Classic amber monochrome terminal", theme.Description);
    }

    [Fact]
    public void AmberTheme_HasBlackBackground()
    {
        var theme = new AmberTheme();
        Assert.Equal(Color.Black, theme.Background);
    }

    [Fact]
    public void GreenTheme_HasCorrectName()
    {
        var theme = new GreenTheme();
        Assert.Equal("Green", theme.Name);
    }

    [Fact]
    public void GreenTheme_HasCorrectDescription()
    {
        var theme = new GreenTheme();
        Assert.Equal("Classic green monochrome terminal", theme.Description);
    }

    [Fact]
    public void GreenTheme_HasBlackBackground()
    {
        var theme = new GreenTheme();
        Assert.Equal(Color.Black, theme.Background);
    }

    [Fact]
    public void BlueTheme_HasCorrectName()
    {
        var theme = new BlueTheme();
        Assert.Equal("Blue", theme.Name);
    }

    [Fact]
    public void BlueTheme_HasBlackBackground()
    {
        var theme = new BlueTheme();
        Assert.Equal(Color.Black, theme.Background);
    }

    [Fact]
    public void GreyTheme_HasCorrectName()
    {
        var theme = new GreyTheme();
        Assert.Equal("Grey", theme.Name);
    }

    [Fact]
    public void GreyTheme_HasBlackBackground()
    {
        var theme = new GreyTheme();
        Assert.Equal(Color.Black, theme.Background);
    }

    [Theory]
    [InlineData(typeof(AmberTheme))]
    [InlineData(typeof(GreenTheme))]
    [InlineData(typeof(BlueTheme))]
    [InlineData(typeof(GreyTheme))]
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
    [InlineData(typeof(AmberTheme))]
    [InlineData(typeof(GreenTheme))]
    [InlineData(typeof(BlueTheme))]
    [InlineData(typeof(GreyTheme))]
    public void AllThemes_HaveNonNullAttributes(Type themeType)
    {
        var theme = (RetroTheme)Activator.CreateInstance(themeType)!;

        Assert.NotNull(theme.NormalAttr);
        Assert.NotNull(theme.BrightAttr);
        Assert.NotNull(theme.DimAttr);
        Assert.NotNull(theme.AccentAttr);
        Assert.NotNull(theme.AccentBrightAttr);
        Assert.NotNull(theme.BorderAttr);
        Assert.NotNull(theme.GridAttr);
        Assert.NotNull(theme.StatusActiveAttr);
        Assert.NotNull(theme.StatusInactiveAttr);
        Assert.NotNull(theme.ErrorAttr);
        Assert.NotNull(theme.WarningAttr);
        Assert.NotNull(theme.GlowAttr);
    }

    [Theory]
    [InlineData(typeof(AmberTheme))]
    [InlineData(typeof(GreenTheme))]
    [InlineData(typeof(BlueTheme))]
    [InlineData(typeof(GreyTheme))]
    public void AllThemes_HaveNonNullColorSchemes(Type themeType)
    {
        var theme = (RetroTheme)Activator.CreateInstance(themeType)!;

        Assert.NotNull(theme.MainColorScheme);
        Assert.NotNull(theme.DialogColorScheme);
        Assert.NotNull(theme.MenuColorScheme);
        Assert.NotNull(theme.ButtonColorScheme);
        Assert.NotNull(theme.FrameColorScheme);
    }

    [Theory]
    [InlineData(typeof(AmberTheme))]
    [InlineData(typeof(GreenTheme))]
    [InlineData(typeof(BlueTheme))]
    [InlineData(typeof(GreyTheme))]
    public void AllThemes_HaveBoxDrawingCharacters(Type themeType)
    {
        var theme = (RetroTheme)Activator.CreateInstance(themeType)!;

        Assert.Equal('╔', theme.BoxTopLeft);
        Assert.Equal('╗', theme.BoxTopRight);
        Assert.Equal('╚', theme.BoxBottomLeft);
        Assert.Equal('╝', theme.BoxBottomRight);
        Assert.Equal('═', theme.BoxHorizontal);
        Assert.Equal('║', theme.BoxVertical);
        Assert.Equal('╡', theme.BoxTitleLeft);
        Assert.Equal('╞', theme.BoxTitleRight);
        Assert.Equal('╤', theme.TickHorizontal);
        Assert.Equal('╟', theme.TickVertical);
        Assert.Equal('╧', theme.TickHorizontalBottom);
        Assert.Equal('╢', theme.TickVerticalRight);
        Assert.Equal('╠', theme.BoxLeftT);
        Assert.Equal('╣', theme.BoxRightT);
    }

    [Theory]
    [InlineData(typeof(AmberTheme))]
    [InlineData(typeof(GreenTheme))]
    [InlineData(typeof(BlueTheme))]
    [InlineData(typeof(GreyTheme))]
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
    [InlineData(typeof(AmberTheme))]
    [InlineData(typeof(GreenTheme))]
    [InlineData(typeof(BlueTheme))]
    [InlineData(typeof(GreyTheme))]
    public void AllThemes_HaveDefaultEnableGlow(Type themeType)
    {
        var theme = (RetroTheme)Activator.CreateInstance(themeType)!;

        // Default EnableGlow is true in RetroTheme base class
        Assert.True(theme.EnableGlow);
    }

    [Fact]
    public void AmberTheme_ErrorColorIsRed()
    {
        var theme = new AmberTheme();
        Assert.Equal(Color.Red, theme.Error);
    }

    [Fact]
    public void AmberTheme_WarningColorIsBrightYellow()
    {
        var theme = new AmberTheme();
        Assert.Equal(Color.BrightYellow, theme.Warning);
    }

    [Fact]
    public void GreenTheme_ErrorColorIsRed()
    {
        var theme = new GreenTheme();
        Assert.Equal(Color.Red, theme.Error);
    }

    [Fact]
    public void GreenTheme_WarningColorIsBrightYellow()
    {
        var theme = new GreenTheme();
        Assert.Equal(Color.BrightYellow, theme.Warning);
    }

    [Fact]
    public void ThemeAttributes_AreCachedOnSecondAccess()
    {
        var theme = new AmberTheme();

        // Access attributes twice
        var attr1 = theme.NormalAttr;
        var attr2 = theme.NormalAttr;

        // They should be the same cached instance
        Assert.Same(attr1, attr2);
    }

    [Fact]
    public void ThemeColorSchemes_AreCachedOnSecondAccess()
    {
        var theme = new GreenTheme();

        // Access color schemes twice
        var scheme1 = theme.MainColorScheme;
        var scheme2 = theme.MainColorScheme;

        // They should be the same cached instance
        Assert.Same(scheme1, scheme2);
    }

    [Fact]
    public void StatusDecorations_HaveExpectedFormat()
    {
        var theme = new AmberTheme();

        Assert.Contains("LIVE", theme.StatusLive);
        Assert.Contains("HOLD", theme.StatusHold);
        Assert.Contains("NO SIGNAL", theme.NoSignalMessage);
    }

    [Fact]
    public void ButtonDecorations_AreSymmetric()
    {
        var theme = new AmberTheme();

        // Both should have similar visual weight
        Assert.False(string.IsNullOrEmpty(theme.ButtonPrefix));
        Assert.False(string.IsNullOrEmpty(theme.ButtonSuffix));
    }

    [Theory]
    [InlineData(typeof(AmberTheme), "Amber")]
    [InlineData(typeof(GreenTheme), "Green")]
    [InlineData(typeof(BlueTheme), "Blue")]
    [InlineData(typeof(GreyTheme), "Grey")]
    public void AllThemes_NameMatchesClassName(Type themeType, string expectedName)
    {
        var theme = (RetroTheme)Activator.CreateInstance(themeType)!;
        Assert.Equal(expectedName, theme.Name);
    }

    [Fact]
    public void MainColorScheme_HasAllRequiredProperties()
    {
        var theme = new AmberTheme();
        var scheme = theme.MainColorScheme;

        Assert.NotNull(scheme.Normal);
        Assert.NotNull(scheme.Focus);
        Assert.NotNull(scheme.HotNormal);
        Assert.NotNull(scheme.HotFocus);
        Assert.NotNull(scheme.Disabled);
    }

    [Fact]
    public void DialogColorScheme_HasAllRequiredProperties()
    {
        var theme = new BlueTheme();
        var scheme = theme.DialogColorScheme;

        Assert.NotNull(scheme.Normal);
        Assert.NotNull(scheme.Focus);
        Assert.NotNull(scheme.HotNormal);
        Assert.NotNull(scheme.HotFocus);
        Assert.NotNull(scheme.Disabled);
    }

    [Fact]
    public void MenuColorScheme_HasAllRequiredProperties()
    {
        var theme = new GreyTheme();
        var scheme = theme.MenuColorScheme;

        Assert.NotNull(scheme.Normal);
        Assert.NotNull(scheme.Focus);
        Assert.NotNull(scheme.HotNormal);
        Assert.NotNull(scheme.HotFocus);
        Assert.NotNull(scheme.Disabled);
    }
}
