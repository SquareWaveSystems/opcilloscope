using Terminal.Gui;

namespace OpcScope.App.Themes;

/// <summary>
/// Grey monochrome terminal theme.
/// Classic high-contrast display for readability.
/// </summary>
public class GreyTheme : RetroTheme
{
    public override string Name => "Grey";
    public override string Description => "Grey monochrome terminal";

    public override Color Background => Color.Black;

    // Grey color range
    public override Color Foreground => new(180, 180, 180);     // Light grey
    public override Color ForegroundBright => Color.White;
    public override Color ForegroundDim => new(100, 100, 100);  // Medium grey
    public override Color Accent => new(140, 140, 140);         // Subtle grey accent
    public override Color AccentBright => Color.White;
    public override Color Border => new(120, 120, 120);         // Medium grey for borders
    public override Color Grid => new(50, 50, 50);              // Very dim grey for grid
    public override Color StatusActive => Color.BrightGreen;
    public override Color StatusInactive => Color.DarkGray;
    public override Color Error => new(255, 100, 100);
    public override Color Warning => new(255, 200, 100);
}
