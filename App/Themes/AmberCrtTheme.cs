using Terminal.Gui;

namespace OpcScope.App.Themes;

/// <summary>
/// Classic amber phosphor CRT theme inspired by 1970s-80s terminals.
/// Evokes the aesthetic of Alien (1979), industrial control systems,
/// and vintage computing equipment.
/// Primary: #ff6a00 (vibrant amber), Secondary: #994400 (dim amber)
/// </summary>
public class AmberCrtTheme : RetroTheme
{
    public override string Name => "Amber CRT";
    public override string Description => "Classic amber phosphor terminal (1970s-80s)";

    // Background is pure black for maximum contrast
    public override Color Background => Color.Black;

    // Amber phosphor color range (warm orange-yellow)
    public override Color Foreground => new(255, 170, 0);       // Bright amber
    public override Color ForegroundBright => Color.BrightYellow; // Peak intensity
    public override Color ForegroundDim => new(180, 100, 0);    // Dim amber
    public override Color Accent => new(255, 106, 0);           // Vibrant orange-amber
    public override Color AccentBright => Color.White;          // Peak glow
    public override Color Border => new(180, 100, 0);           // Medium amber for borders
    public override Color Grid => new(64, 40, 0);               // Very dim amber for grid
    public override Color StatusActive => Color.BrightGreen;    // Green for active status
    public override Color StatusInactive => Color.Gray;
    public override Color Error => Color.Red;
    public override Color Warning => Color.BrightYellow;
}
