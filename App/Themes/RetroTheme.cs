using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace OpcScope.App.Themes;

/// <summary>
/// Retro-futuristic theme inspired by 1970s-80s industrial control displays
/// and cassette futurism aesthetics (Alien, Blade Runner, Signalis).
/// Based on: https://github.com/Imetomi/retro-futuristic-ui-design
///           https://squarewavesystems.github.io/
/// </summary>
public abstract class RetroTheme
{
    // === Theme Identity ===
    public abstract string Name { get; }
    public abstract string Description { get; }

    // === Base Colors ===
    public abstract Color Background { get; }
    public abstract Color Foreground { get; }
    public abstract Color ForegroundBright { get; }
    public abstract Color ForegroundDim { get; }
    public abstract Color Accent { get; }
    public abstract Color AccentBright { get; }
    public abstract Color Border { get; }
    public abstract Color Grid { get; }
    public abstract Color StatusActive { get; }
    public abstract Color StatusInactive { get; }
    public abstract Color Error { get; }
    public abstract Color Warning { get; }

    // === Derived Attributes (for direct drawing) ===
    public Attribute NormalAttr => new(Foreground, Background);
    public Attribute BrightAttr => new(ForegroundBright, Background);
    public Attribute DimAttr => new(ForegroundDim, Background);
    public Attribute AccentAttr => new(Accent, Background);
    public Attribute AccentBrightAttr => new(AccentBright, Background);
    public Attribute BorderAttr => new(Border, Background);
    public Attribute GridAttr => new(Grid, Background);
    public Attribute StatusActiveAttr => new(StatusActive, Background);
    public Attribute StatusInactiveAttr => new(StatusInactive, Background);
    public Attribute ErrorAttr => new(Error, Background);
    public Attribute WarningAttr => new(Warning, Background);

    // Glow/highlight effect for active elements
    public Attribute GlowAttr => new(Color.White, Background);

    // Scanline effect (subtle darkening)
    public virtual Attribute ScanlineAttr => new(new Color(10, 10, 10), Background);

    // === Color Schemes for Terminal.Gui Widgets ===
    public virtual ColorScheme MainColorScheme => new()
    {
        Normal = NormalAttr,
        Focus = BrightAttr,
        HotNormal = AccentAttr,
        HotFocus = AccentBrightAttr,
        Disabled = new Attribute(StatusInactive, Background)
    };

    public virtual ColorScheme DialogColorScheme => new()
    {
        Normal = DimAttr,
        Focus = BrightAttr,
        HotNormal = AccentAttr,
        HotFocus = AccentBrightAttr,
        Disabled = new Attribute(StatusInactive, Background)
    };

    public virtual ColorScheme MenuColorScheme => new()
    {
        Normal = NormalAttr,
        Focus = new Attribute(Background, Foreground),
        HotNormal = AccentAttr,
        HotFocus = new Attribute(Background, AccentBright),
        Disabled = new Attribute(StatusInactive, Background)
    };

    public virtual ColorScheme ButtonColorScheme => new()
    {
        Normal = BorderAttr,
        Focus = BrightAttr,
        HotNormal = AccentAttr,
        HotFocus = AccentBrightAttr,
        Disabled = new Attribute(StatusInactive, Background)
    };

    public virtual ColorScheme FrameColorScheme => new()
    {
        Normal = BorderAttr,
        Focus = BrightAttr,
        HotNormal = AccentAttr,
        HotFocus = AccentBrightAttr,
        Disabled = new Attribute(StatusInactive, Background)
    };

    // === CRT Effect Settings ===
    public virtual bool EnableScanlines => true;
    public virtual bool EnableGlow => true;

    // === Box Drawing Characters (industrial style) ===
    public virtual char BoxTopLeft => '╔';
    public virtual char BoxTopRight => '╗';
    public virtual char BoxBottomLeft => '╚';
    public virtual char BoxBottomRight => '╝';
    public virtual char BoxHorizontal => '═';
    public virtual char BoxVertical => '║';
    public virtual char BoxTitleLeft => '╡';
    public virtual char BoxTitleRight => '╞';
    public virtual char TickHorizontal => '╤';
    public virtual char TickVertical => '╟';

    // === UI Element Decorations ===
    public virtual string ButtonPrefix => "◄ ";
    public virtual string ButtonSuffix => " ►";
    public virtual string TitleDecoration => "═══";
    public virtual string StatusLive => "● LIVE";
    public virtual string StatusHold => "○ HOLD";
    public virtual string NoSignalMessage => "▶ NO SIGNAL ◀";
}
