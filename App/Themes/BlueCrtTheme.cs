using Terminal.Gui;

namespace OpcScope.App.Themes;

/// <summary>
/// Blue phosphor CRT theme inspired by aerospace and military displays.
/// This color was common in radar systems, oscilloscopes, and
/// early graphics terminals. Evokes a scientific/military aesthetic.
/// </summary>
public class BlueCrtTheme : RetroTheme
{
    public override string Name => "Blue CRT";
    public override string Description => "Aerospace/military phosphor display";

    public override Color Background => Color.Black;

    // Blue phosphor color range (cool blue tones)
    public override Color Foreground => new(100, 180, 255);     // Bright blue phosphor
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
