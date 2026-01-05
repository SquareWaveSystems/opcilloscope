using Terminal.Gui;

namespace OpcScope.App.Themes;

/// <summary>
/// Dark theme with classic green monochrome terminal aesthetic.
/// The quintessential terminal look from the golden age of computing.
/// </summary>
public class DarkTheme : RetroTheme
{
    public override string Name => "Dark";
    public override string Description => "Classic green monochrome terminal";

    public override Color Background => Color.Black;

    // Green phosphor color range
    public override Color Foreground => new(0, 255, 65);        // Bright green
    public override Color ForegroundBright => Color.BrightGreen;
    public override Color ForegroundDim => new(0, 128, 32);     // Dim green
    public override Color Accent => new(0, 255, 128);           // Cyan-green accent
    public override Color AccentBright => Color.White;
    public override Color Border => new(0, 180, 45);            // Medium green for borders
    public override Color Grid => new(0, 64, 16);               // Very dim green for grid
    public override Color StatusActive => new(0, 255, 65);      // Same as foreground
    public override Color StatusInactive => Color.DarkGray;
    public override Color Error => Color.Red;
    public override Color Warning => Color.BrightYellow;
}
