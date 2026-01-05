using Terminal.Gui;

namespace OpcScope.App.Themes;

/// <summary>
/// Blue monochrome terminal theme.
/// Inspired by aerospace displays and scientific instruments.
/// </summary>
public class BlueTheme : RetroTheme
{
    public override string Name => "Blue";
    public override string Description => "Blue monochrome terminal";

    public override Color Background => Color.Black;

    // Blue color range (cool blue tones)
    public override Color Foreground => new(100, 180, 255);     // Bright blue
    public override Color ForegroundBright => new(150, 200, 255);
    public override Color ForegroundDim => new(50, 100, 150);   // Dim blue
    public override Color Accent => new(0, 200, 255);           // Cyan accent
    public override Color AccentBright => Color.White;
    public override Color Border => new(80, 140, 200);          // Medium blue for borders
    public override Color Grid => new(30, 60, 90);              // Very dim blue for grid
    public override Color StatusActive => new(0, 255, 128);     // Green-cyan for active
    public override Color StatusInactive => Color.DarkGray;
    public override Color Error => new(255, 100, 100);          // Red
    public override Color Warning => new(255, 200, 100);        // Amber
}
