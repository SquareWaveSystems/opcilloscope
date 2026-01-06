using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace OpcScope.App.Themes;

/// <summary>
/// Light theme with retro CRT aesthetic.
/// Warm paper white with dark text for comfortable viewing.
/// </summary>
public class LightTheme : RetroTheme
{
    public override string Name => "Light";
    public override string Description => "Retro CRT light theme";

    // Main window uses double-line for emphasis, frames use single
    public override LineStyle BorderLineStyle => LineStyle.Double;
    public override LineStyle FrameLineStyle => LineStyle.Single;
    public override LineStyle EmphasizedBorderStyle => LineStyle.Double;
    public override LineStyle SecondaryBorderStyle => LineStyle.Single;

    // Background per spec: #f5f5f0 warm paper white
    public override Color Background => new(245, 245, 240);

    // Text per spec: #1a1a1a dark
    public override Color Foreground => new(26, 26, 26);
    public override Color ForegroundBright => new(10, 10, 10);
    public override Color ForegroundDim => new(136, 136, 136);   // #888888 muted grey

    // Accent per spec: #c4692b deeper amber for contrast on light
    public override Color Accent => new(196, 105, 43);
    public override Color AccentBright => new(220, 120, 50);

    // Border: darker grey for better cross-terminal consistency
    // (lighter greys like #c0c0c0 get mapped to yellow in some terminals)
    public override Color Border => new(128, 128, 128);
    public override Color Grid => new(200, 200, 195);

    // Status colors - adjusted for light background contrast
    public override Color StatusActive => new(74, 122, 74);      // #4a7a4a darker green
    public override Color StatusInactive => new(136, 136, 136);  // #888888 grey
    public override Color Error => new(139, 69, 69);             // #8b4545 dark red
    public override Color Warning => new(168, 134, 32);          // #a88620 dark mustard

    // OPC UA Status Colors per spec (adjusted for light background)
    public override Color StatusGood => new(74, 122, 74);        // #4a7a4a darker green
    public override Color StatusBad => new(139, 69, 69);         // #8b4545 dark red
    public override Color StatusUncertain => new(181, 101, 29);  // #b5651d amber

    // Muted text for timestamps, attribution
    public override Color MutedText => new(136, 136, 136);       // #888888

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

    // Override color schemes for light display with amber highlights
    private ColorScheme? _mainColorScheme;
    private ColorScheme? _menuColorScheme;

    public override ColorScheme MainColorScheme => _mainColorScheme ??= new()
    {
        Normal = NormalAttr,
        Focus = new Attribute(Background, new Color(234, 234, 229)),  // #eaeae5 panel background for focus
        HotNormal = AccentAttr,
        HotFocus = new Attribute(Background, Accent),
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
