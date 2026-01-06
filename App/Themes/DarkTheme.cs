using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace OpcScope.App.Themes;

/// <summary>
/// Dark theme with clean, minimal appearance.
/// Neutral dark background with light text and tasteful color accents.
/// </summary>
public class DarkTheme : RetroTheme
{
    public override string Name => "Dark";
    public override string Description => "Dark theme";

    // Use single-line borders for clean look
    public override LineStyle BorderLineStyle => LineStyle.Single;
    public override LineStyle FrameLineStyle => LineStyle.Single;

    // Neutral dark background - slightly warm charcoal
    public override Color Background => new(30, 32, 36);

    // Light text for high contrast
    public override Color Foreground => new(220, 222, 225);      // Off-white
    public override Color ForegroundBright => new(245, 246, 248); // Near-white
    public override Color ForegroundDim => new(130, 135, 140);    // Mid grey

    // Teal accent for highlights
    public override Color Accent => new(80, 180, 160);            // Soft teal
    public override Color AccentBright => new(100, 210, 185);     // Brighter teal

    // Subtle borders that don't compete
    public override Color Border => new(70, 75, 82);              // Dark grey
    public override Color Grid => new(50, 54, 60);                // Very dark grid

    // Status colors - visible on dark background
    public override Color StatusActive => new(80, 200, 140);      // Soft green
    public override Color StatusInactive => new(100, 105, 115);   // Grey
    public override Color Error => new(230, 90, 90);              // Soft red
    public override Color Warning => new(230, 180, 80);           // Warm amber

    // Single-line box drawing for clean look
    public override char BoxTopLeft => '┌';
    public override char BoxTopRight => '┐';
    public override char BoxBottomLeft => '└';
    public override char BoxBottomRight => '┘';
    public override char BoxHorizontal => '─';
    public override char BoxVertical => '│';
    public override char BoxTitleLeft => '┤';
    public override char BoxTitleRight => '├';
    public override char TickHorizontal => '┬';
    public override char TickVertical => '├';
    public override char TickHorizontalBottom => '┴';
    public override char TickVerticalRight => '┤';
    public override char BoxLeftT => '├';
    public override char BoxRightT => '┤';

    // Minimal decorations matching light theme
    public override string ButtonPrefix => "[ ";
    public override string ButtonSuffix => " ]";
    public override string TitleDecoration => "───";
    public override string StatusLive => "◆ LIVE";
    public override string StatusHold => "◇ HOLD";
    public override string NoSignalMessage => "· NO SIGNAL ·";

    // Enable subtle glow for dark backgrounds
    public override bool EnableGlow => true;

    // Override color schemes for dark display with teal highlights
    private ColorScheme? _mainColorScheme;
    private ColorScheme? _menuColorScheme;

    public override ColorScheme MainColorScheme => _mainColorScheme ??= new()
    {
        Normal = NormalAttr,
        Focus = new Attribute(ForegroundBright, new Color(55, 60, 68)),  // Slightly lighter background for focus
        HotNormal = AccentAttr,
        HotFocus = new Attribute(AccentBright, new Color(55, 60, 68)),
        Disabled = new Attribute(StatusInactive, Background)
    };

    public override ColorScheme MenuColorScheme => _menuColorScheme ??= new()
    {
        Normal = NormalAttr,
        Focus = new Attribute(Background, Foreground),  // Inverted for menu focus
        HotNormal = AccentAttr,
        HotFocus = new Attribute(Background, Accent),
        Disabled = new Attribute(StatusInactive, Background)
    };
}
