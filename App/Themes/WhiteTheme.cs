using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace OpcScope.App.Themes;

/// <summary>
/// Inverted light theme inspired by Severance/Lumon corporate aesthetic.
/// White background with dark text - sterile, institutional feel.
/// Uses single-line box drawing characters for a clean corporate appearance.
/// </summary>
public class WhiteTheme : RetroTheme
{
    public override string Name => "White";
    public override string Description => "Light corporate (Outie)";

    // Use single-line borders for clean corporate look
    public override LineStyle BorderLineStyle => LineStyle.Single;
    public override LineStyle FrameLineStyle => LineStyle.Single;

    // Off-white background - slightly warm for reduced harshness
    public override Color Background => new(245, 245, 242);

    // Dark text for high contrast
    public override Color Foreground => new(40, 45, 50);         // Near-black
    public override Color ForegroundBright => new(20, 25, 30);   // Pure dark
    public override Color ForegroundDim => new(120, 125, 130);   // Mid grey

    // Subtle teal accent - Lumon corporate color
    public override Color Accent => new(0, 140, 140);            // Darker teal
    public override Color AccentBright => new(0, 100, 100);      // Even darker when bright

    // Light borders that don't compete
    public override Color Border => new(180, 182, 185);          // Light grey
    public override Color Grid => new(220, 222, 225);            // Very light grid

    // Status colors - visible on light background
    public override Color StatusActive => new(0, 150, 100);      // Dark teal-green
    public override Color StatusInactive => new(160, 165, 170);  // Grey
    public override Color Error => new(180, 60, 60);             // Dark red
    public override Color Warning => new(180, 140, 40);          // Dark amber

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

    // Minimal decorations
    public override string ButtonPrefix => "[ ";
    public override string ButtonSuffix => " ]";
    public override string TitleDecoration => "───";
    public override string StatusLive => "◆ LIVE";
    public override string StatusHold => "◇ HOLD";
    public override string NoSignalMessage => "· NO SIGNAL ·";

    // Disable glow - doesn't work well on light backgrounds
    public override bool EnableGlow => false;

    // Override color schemes for inverted display
    private ColorScheme? _mainColorScheme;
    private ColorScheme? _menuColorScheme;

    public override ColorScheme MainColorScheme => _mainColorScheme ??= new()
    {
        Normal = NormalAttr,
        Focus = new Attribute(Background, Foreground),  // Inverted for visibility
        HotNormal = AccentAttr,
        HotFocus = new Attribute(Background, Accent),
        Disabled = new Attribute(StatusInactive, Background)
    };

    public override ColorScheme MenuColorScheme => _menuColorScheme ??= new()
    {
        Normal = NormalAttr,
        Focus = new Attribute(Background, ForegroundBright),  // Inverted
        HotNormal = AccentAttr,
        HotFocus = new Attribute(Background, Accent),
        Disabled = new Attribute(StatusInactive, Background)
    };
}
