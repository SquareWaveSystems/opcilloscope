using Terminal.Gui;

namespace OpcScope.App.Themes;

/// <summary>
/// Modern terminal theme inspired by Square Wave Systems' web design.
/// Dark mode with high contrast, command-line interface aesthetics,
/// and technical/hacker culture styling.
/// Based on: https://squarewavesystems.github.io/
/// </summary>
public class SquareWaveTheme : RetroTheme
{
    public override string Name => "SquareWave";
    public override string Description => "Modern dark terminal (Square Wave Systems)";

    // Pure black background for maximum contrast
    public override Color Background => Color.Black;

    // High-contrast white/gray text
    public override Color Foreground => new(200, 200, 200);     // Light gray
    public override Color ForegroundBright => Color.White;
    public override Color ForegroundDim => new(128, 128, 128);  // Medium gray
    public override Color Accent => new(0, 200, 255);           // Cyan accent (technical)
    public override Color AccentBright => new(100, 255, 255);   // Bright cyan
    public override Color Border => new(80, 80, 80);            // Subtle gray borders
    public override Color Grid => new(40, 40, 40);              // Very subtle grid
    public override Color StatusActive => Color.BrightGreen;    // Green for "connected"
    public override Color StatusInactive => Color.DarkGray;
    public override Color Error => new(255, 80, 80);            // Bright red
    public override Color Warning => new(255, 200, 0);          // Amber warning

    // Modern terminal doesn't need CRT effects
    public override bool EnableScanlines => false;
    public override bool EnableGlow => true;

    // Cleaner button style
    public override string ButtonPrefix => "[ ";
    public override string ButtonSuffix => " ]";
    public override string TitleDecoration => "───";
    public override string StatusLive => "● CONNECTED";
    public override string StatusHold => "○ IDLE";
    public override string NoSignalMessage => "[ NO SIGNAL ]";
}
