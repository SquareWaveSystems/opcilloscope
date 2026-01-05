using Terminal.Gui;

namespace OpcScope.App.Themes;

/// <summary>
/// Severance-inspired theme with clinical, minimal aesthetic.
/// Clean teal accents on dark background with single-line borders.
/// Inspired by the Lumon Industries computer interfaces.
/// Uses Terminal.Gui v2 LineStyle.Single for clean borders.
/// </summary>
public class SeveranceTheme : RetroTheme
{
    public override string Name => "Severance";
    public override string Description => "Clinical minimal (Lumon)";

    // Use Terminal.Gui v2 single-line borders for clean look
    public override LineStyle BorderLineStyle => LineStyle.Single;
    public override LineStyle FrameLineStyle => LineStyle.Single;

    // Deep black background for stark contrast
    public override Color Background => Color.Black;

    // Cool grey-white foreground
    public override Color Foreground => new(200, 205, 210);      // Cool grey
    public override Color ForegroundBright => new(240, 245, 250); // Near-white
    public override Color ForegroundDim => new(90, 95, 100);     // Muted grey

    // Signature Lumon teal accent
    public override Color Accent => new(0, 180, 180);            // Teal
    public override Color AccentBright => new(0, 220, 220);      // Bright teal

    // Clean minimal borders
    public override Color Border => new(60, 65, 70);             // Subtle dark grey
    public override Color Grid => new(30, 32, 35);               // Very subtle grid

    // Status colors - muted, clinical
    public override Color StatusActive => new(0, 200, 150);      // Teal-green
    public override Color StatusInactive => new(80, 85, 90);     // Grey
    public override Color Error => new(200, 80, 80);             // Muted red
    public override Color Warning => new(200, 180, 80);          // Muted amber

    // Single-line box drawing for clean, minimal look
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

    // Minimal, sparse decorations
    public override string ButtonPrefix => "[ ";
    public override string ButtonSuffix => " ]";
    public override string TitleDecoration => "───";
    public override string StatusLive => "◆ LIVE";
    public override string StatusHold => "◇ HOLD";
    public override string NoSignalMessage => "· NO SIGNAL ·";

    // Disable glow for cleaner look
    public override bool EnableGlow => false;
}
