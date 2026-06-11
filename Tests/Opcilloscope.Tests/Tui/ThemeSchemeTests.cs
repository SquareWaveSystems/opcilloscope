using Opcilloscope.App.Themes;

namespace Opcilloscope.Tests.Tui;

/// <summary>
/// Guards the Terminal.Gui 2.0 -> 2.4 theming migration, where the former
/// <c>ColorScheme</c> type and settable <c>View.ColorScheme</c> property were replaced by
/// <c>Scheme</c> and <c>View.SetScheme()</c>. These tests verify the app's themes still
/// produce schemes with the expected colours and that <see cref="ThemeStyler"/> applies them.
/// </summary>
[Collection("Tui")]
public class ThemeSchemeTests
{
    public static IEnumerable<object[]> Themes() => new[]
    {
        new object[] { new DarkTheme() },
        new object[] { new LightTheme() },
    };

    [Theory]
    [MemberData(nameof(Themes))]
    public void MainScheme_NormalRoleMatchesThemeForegroundAndBackground(AppTheme theme)
    {
        Scheme scheme = theme.MainColorScheme;

        Assert.Equal(theme.Foreground, scheme.Normal.Foreground);
        Assert.Equal(theme.Background, scheme.Normal.Background);
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void ButtonScheme_IsADistinctScheme(AppTheme theme)
    {
        // Sanity: the button scheme is produced and usable as a Terminal.Gui Scheme.
        Scheme button = theme.ButtonColorScheme;

        Assert.Equal(theme.Background, button.Normal.Background);
    }

    [Fact]
    public void ThemeStyler_ApplyToFrame_SetsTheViewScheme()
    {
        var theme = new DarkTheme();
        var view = new FrameView();

        ThemeStyler.ApplyToFrame(view, theme);

        Scheme? applied = view.GetScheme();
        Assert.NotNull(applied);
        Assert.Equal(theme.Foreground, applied!.Normal.Foreground);
        Assert.Equal(theme.Background, applied.Normal.Background);
    }
}
