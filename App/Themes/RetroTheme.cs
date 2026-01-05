using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace OpcScope.App.Themes;

/// <summary>
/// Base theme class for terminal display aesthetics.
/// Inspired by classic computing displays and industrial control systems.
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

    // === Cached Attribute objects to avoid allocations ===
    private Attribute? _normalAttr;
    private Attribute? _brightAttr;
    private Attribute? _dimAttr;
    private Attribute? _accentAttr;
    private Attribute? _accentBrightAttr;
    private Attribute? _borderAttr;
    private Attribute? _gridAttr;
    private Attribute? _statusActiveAttr;
    private Attribute? _statusInactiveAttr;
    private Attribute? _errorAttr;
    private Attribute? _warningAttr;
    private Attribute? _glowAttr;

    // === Derived Attributes (for direct drawing) ===
    public Attribute NormalAttr => _normalAttr ??= new(Foreground, Background);
    public Attribute BrightAttr => _brightAttr ??= new(ForegroundBright, Background);
    public Attribute DimAttr => _dimAttr ??= new(ForegroundDim, Background);
    public Attribute AccentAttr => _accentAttr ??= new(Accent, Background);
    public Attribute AccentBrightAttr => _accentBrightAttr ??= new(AccentBright, Background);
    public Attribute BorderAttr => _borderAttr ??= new(Border, Background);
    public Attribute GridAttr => _gridAttr ??= new(Grid, Background);
    public Attribute StatusActiveAttr => _statusActiveAttr ??= new(StatusActive, Background);
    public Attribute StatusInactiveAttr => _statusInactiveAttr ??= new(StatusInactive, Background);
    public Attribute ErrorAttr => _errorAttr ??= new(Error, Background);
    public Attribute WarningAttr => _warningAttr ??= new(Warning, Background);

    // Highlight effect for active elements
    public Attribute GlowAttr => _glowAttr ??= new(Color.White, Background);

    // === Cached Color Schemes for Terminal.Gui Widgets ===
    private ColorScheme? _mainColorScheme;
    private ColorScheme? _dialogColorScheme;
    private ColorScheme? _menuColorScheme;
    private ColorScheme? _buttonColorScheme;
    private ColorScheme? _frameColorScheme;

    public virtual ColorScheme MainColorScheme => _mainColorScheme ??= new()
    {
        Normal = NormalAttr,
        Focus = BrightAttr,
        HotNormal = AccentAttr,
        HotFocus = AccentBrightAttr,
        Disabled = new Attribute(StatusInactive, Background)
    };

    public virtual ColorScheme DialogColorScheme => _dialogColorScheme ??= new()
    {
        Normal = DimAttr,
        Focus = BrightAttr,
        HotNormal = AccentAttr,
        HotFocus = AccentBrightAttr,
        Disabled = new Attribute(StatusInactive, Background)
    };

    public virtual ColorScheme MenuColorScheme => _menuColorScheme ??= new()
    {
        Normal = NormalAttr,
        Focus = new Attribute(Background, Foreground),
        HotNormal = AccentAttr,
        HotFocus = new Attribute(Background, AccentBright),
        Disabled = new Attribute(StatusInactive, Background)
    };

    public virtual ColorScheme ButtonColorScheme => _buttonColorScheme ??= new()
    {
        Normal = BorderAttr,
        Focus = BrightAttr,
        HotNormal = AccentAttr,
        HotFocus = AccentBrightAttr,
        Disabled = new Attribute(StatusInactive, Background)
    };

    public virtual ColorScheme FrameColorScheme => _frameColorScheme ??= new()
    {
        Normal = BorderAttr,
        Focus = BrightAttr,
        HotNormal = AccentAttr,
        HotFocus = AccentBrightAttr,
        Disabled = new Attribute(StatusInactive, Background)
    };

    // === Box Drawing Characters ===
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
    public virtual char TickHorizontalBottom => '╧';
    public virtual char TickVerticalRight => '╢';
    public virtual char BoxLeftT => '╠';
    public virtual char BoxRightT => '╣';

    // === UI Element Decorations ===
    public virtual string ButtonPrefix => "◄ ";
    public virtual string ButtonSuffix => " ►";
    public virtual string TitleDecoration => "═══";
    public virtual string StatusLive => "● LIVE";
    public virtual string StatusHold => "○ HOLD";
    public virtual string NoSignalMessage => "▶ NO SIGNAL ◀";
}
