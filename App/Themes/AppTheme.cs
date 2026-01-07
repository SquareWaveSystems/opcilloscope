using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace OpcScope.App.Themes;

/// <summary>
/// Base theme class for terminal display aesthetics.
/// Inspired by classic computing displays and industrial control systems.
/// Leverages Terminal.Gui v2 features: LineStyle, Border/Margin/Padding, ColorSchemes.
/// </summary>
public abstract class AppTheme
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

    // === OPC UA Status Colors ===
    public abstract Color StatusGood { get; }       // Good status - muted green
    public abstract Color StatusBad { get; }        // Bad status - dusty red
    public abstract Color StatusUncertain { get; }  // Uncertain status - amber

    // === Muted Text ===
    public abstract Color MutedText { get; }        // For timestamps, attribution, secondary info

    // === Terminal.Gui v2 LineStyle for borders ===
    /// <summary>
    /// The LineStyle used for view borders (Single, Double, Rounded, etc.)
    /// </summary>
    public virtual LineStyle BorderLineStyle => LineStyle.Double;

    /// <summary>
    /// The LineStyle used for frames/panels (can differ from main borders)
    /// </summary>
    public virtual LineStyle FrameLineStyle => LineStyle.Double;

    /// <summary>
    /// LineStyle for emphasized panels (main window, monitored variables) - typically double-line
    /// </summary>
    public virtual LineStyle EmphasizedBorderStyle => LineStyle.Double;

    /// <summary>
    /// LineStyle for secondary panels (log, node details, address space) - typically single-line
    /// </summary>
    public virtual LineStyle SecondaryBorderStyle => LineStyle.Single;

    /// <summary>
    /// Whether to use the SuperView's LineCanvas for auto-joining borders
    /// </summary>
    public virtual bool UseLineCanvas => true;

    /// <summary>
    /// Margin thickness around views (outside border)
    /// </summary>
    public virtual Thickness MarginThickness => new(0);

    /// <summary>
    /// Padding thickness inside views (inside border)
    /// </summary>
    public virtual Thickness PaddingThickness => new(0);

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
    private Attribute? _statusGoodAttr;
    private Attribute? _statusBadAttr;
    private Attribute? _statusUncertainAttr;
    private Attribute? _mutedTextAttr;

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

    // OPC UA Status Attributes
    public Attribute StatusGoodAttr => _statusGoodAttr ??= new(StatusGood, Background);
    public Attribute StatusBadAttr => _statusBadAttr ??= new(StatusBad, Background);
    public Attribute StatusUncertainAttr => _statusUncertainAttr ??= new(StatusUncertain, Background);
    public Attribute MutedTextAttr => _mutedTextAttr ??= new(MutedText, Background);

    // Whether to enable the glow effect on the leading edge of the plot
    public virtual bool EnableGlow => true;

    // === Cached Color Schemes for Terminal.Gui Widgets ===
    private ColorScheme? _mainColorScheme;
    private ColorScheme? _dialogColorScheme;
    private ColorScheme? _menuColorScheme;
    private ColorScheme? _buttonColorScheme;
    private ColorScheme? _frameColorScheme;
    private ColorScheme? _borderColorScheme;
    private ColorScheme? _focusedBorderColorScheme;

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

    /// <summary>
    /// Color scheme for structural borders - uses grey for border lines,
    /// but accent color for titles (HotNormal is used for title text).
    /// </summary>
    public virtual ColorScheme BorderColorScheme => _borderColorScheme ??= new()
    {
        Normal = BorderAttr,
        Focus = BorderAttr,
        HotNormal = AccentAttr,  // Title text uses accent color
        HotFocus = AccentAttr,
        Disabled = BorderAttr
    };

    /// <summary>
    /// Color scheme for focused view borders - uses accent color to highlight
    /// which panel currently has keyboard focus.
    /// </summary>
    public virtual ColorScheme FocusedBorderColorScheme => _focusedBorderColorScheme ??= new()
    {
        Normal = AccentAttr,
        Focus = AccentAttr,
        HotNormal = AccentBrightAttr,  // Title text uses bright accent
        HotFocus = AccentBrightAttr,
        Disabled = BorderAttr
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

    // === Connection Status Indicators ===
    public virtual string ConnectedIndicator => "● Connected";
    public virtual string DisconnectedIndicator => "○ Not Connected";
    public virtual string ConnectingIndicator => "Connecting";

    // === Recording Indicators ===
    public virtual string RecordingLabel => "◉ REC";
    public virtual string StoppedLabel => "○ STOP";

    // === OPC UA Status Icons for table display ===
    public virtual string StatusGoodIcon => "●";
    public virtual string StatusUncertainIcon => "▲";
    public virtual string StatusBadIcon => "✕";
}
