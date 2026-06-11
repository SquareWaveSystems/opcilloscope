using Terminal.Gui;

namespace Opcilloscope.App.Themes;

/// <summary>
/// Theme that inherits the terminal's own ANSI color palette.
/// Uses only the 16 named ANSI colors and enables Force16Colors so the
/// driver emits standard SGR color codes (30-37/90-97) instead of 24-bit
/// RGB - the terminal then renders them with its configured color scheme.
/// </summary>
public class TerminalTheme : AppTheme
{
    public override string Name => "Terminal";
    public override string Description => "Inherits the terminal's ANSI color palette";

    // Output only the 16 ANSI colors so the terminal applies its own palette
    public override bool UseTerminalColors => true;

    // Main window uses double-line for emphasis, frames use single
    public override LineStyle BorderLineStyle => LineStyle.Double;
    public override LineStyle FrameLineStyle => LineStyle.Single;
    public override LineStyle EmphasizedBorderStyle => LineStyle.Double;
    public override LineStyle SecondaryBorderStyle => LineStyle.Single;

    // All colors are constructed from ColorName16 enum values, so
    // GetClosestNamedColor16 resolves them to the exact ANSI index
    // (no nearest-color approximation)

    public override Color Background => new(ColorName16.Black);          // ANSI 0

    public override Color Foreground => new(ColorName16.Gray);           // ANSI 7
    public override Color ForegroundBright => new(ColorName16.White);    // ANSI 15
    public override Color ForegroundDim => new(ColorName16.DarkGray);    // ANSI 8

    // Yellow is the closest ANSI color to opcilloscope's signature amber
    public override Color Accent => new(ColorName16.Yellow);             // ANSI 3
    public override Color AccentBright => new(ColorName16.BrightYellow); // ANSI 11

    public override Color Border => new(ColorName16.DarkGray);
    public override Color Grid => new(ColorName16.DarkGray);

    public override Color StatusActive => new(ColorName16.Green);        // ANSI 2
    public override Color StatusInactive => new(ColorName16.DarkGray);
    public override Color Error => new(ColorName16.Red);                 // ANSI 1
    public override Color Warning => new(ColorName16.Yellow);

    // OPC UA Status Colors
    public override Color StatusGood => new(ColorName16.Green);
    public override Color StatusBad => new(ColorName16.Red);
    public override Color StatusUncertain => new(ColorName16.Yellow);

    // Muted text for timestamps, attribution
    public override Color MutedText => new(ColorName16.DarkGray);

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

    // Minimal decorations matching the other themes
    public override string ButtonPrefix => "[ ";
    public override string ButtonSuffix => " ]";
    public override string TitleDecoration => "───";
    public override string StatusLive => "◆ LIVE";
    public override string StatusHold => "◇ HOLD";
    public override string NoSignalMessage => "· NO SIGNAL ·";

    // Glow uses White which maps cleanly to ANSI 15
    public override bool EnableGlow => true;

    // Override color schemes - focus highlight uses a DarkGray band since
    // panel-background shades are not available in the 16-color palette
    private Scheme? _mainColorScheme;
    private Scheme? _menuColorScheme;

    public override Scheme MainColorScheme => _mainColorScheme ??= new()
    {
        Normal = NormalAttr,
        Focus = new Attribute(ForegroundBright, new Color(ColorName16.DarkGray)),
        HotNormal = AccentAttr,
        HotFocus = new Attribute(AccentBright, new Color(ColorName16.DarkGray)),
        Disabled = new Attribute(StatusInactive, Background)
    };

    public override Scheme MenuColorScheme => _menuColorScheme ??= new()
    {
        Normal = NormalAttr,
        Focus = new Attribute(Background, Foreground),  // Inverted for menu focus
        HotNormal = AccentAttr,
        HotFocus = new Attribute(Background, Accent),
        Disabled = new Attribute(StatusInactive, Background)
    };
}
