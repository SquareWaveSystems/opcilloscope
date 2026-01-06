using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace OpcScope.App.Themes;

/// <summary>
/// Dark theme for industrial terminal displays.
/// </summary>
public class DarkTheme : AppTheme
{
    public override string Name => "Dark";
    public override string Description => "Dark terminal theme";

    // Main window uses double-line for emphasis, frames use single
    public override LineStyle BorderLineStyle => LineStyle.Double;
    public override LineStyle FrameLineStyle => LineStyle.Single;
    public override LineStyle EmphasizedBorderStyle => LineStyle.Double;
    public override LineStyle SecondaryBorderStyle => LineStyle.Single;

    // Background per spec: #1a1a1a charcoal
    public override Color Background => new(26, 26, 26);

    // Text per spec: #d4d4c8 warm off-white with slight amber tint
    public override Color Foreground => new(212, 212, 200);
    public override Color ForegroundBright => new(240, 240, 230);
    public override Color ForegroundDim => new(128, 128, 128);    // #808080 secondary text

    // Accent per spec: #cc7832 dull CRT amber/orange - the signature color
    public override Color Accent => new(204, 120, 50);
    public override Color AccentBright => new(230, 140, 60);

    // Border per spec: #404040 understated
    public override Color Border => new(64, 64, 64);
    public override Color Grid => new(50, 50, 50);

    // Status colors - visible on dark background
    public override Color StatusActive => new(93, 138, 93);       // #5d8a5d muted sage green
    public override Color StatusInactive => new(110, 110, 110);   // #6e6e6e grey
    public override Color Error => new(166, 84, 84);              // #a65454 dusty brick red
    public override Color Warning => new(201, 162, 39);           // #c9a227 mustard yellow

    // OPC UA Status Colors per spec
    public override Color StatusGood => new(93, 138, 93);         // #5d8a5d muted sage green
    public override Color StatusBad => new(166, 84, 84);          // #a65454 dusty brick red
    public override Color StatusUncertain => new(204, 120, 50);   // #cc7832 amber (same as accent)

    // Muted text for timestamps, attribution
    public override Color MutedText => new(128, 128, 128);        // #808080

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

    // Override color schemes for dark display with amber highlights
    private ColorScheme? _mainColorScheme;
    private ColorScheme? _menuColorScheme;

    public override ColorScheme MainColorScheme => _mainColorScheme ??= new()
    {
        Normal = NormalAttr,
        Focus = new Attribute(ForegroundBright, new Color(45, 45, 45)),  // #2d2d2d panel background
        HotNormal = AccentAttr,
        HotFocus = new Attribute(AccentBright, new Color(45, 45, 45)),
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
