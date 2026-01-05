using Terminal.Gui;

namespace OpcScope.App.Themes;

/// <summary>
/// Classic amber monochrome terminal theme.
/// Evokes the warm glow of vintage computing equipment and industrial displays.
/// </summary>
public class AmberTheme : RetroTheme
{
    public override string Name => "Amber";
    public override string Description => "Classic amber monochrome terminal";

    public override Color Background => Color.Black;

    // Amber color range (warm orange-yellow)
    public override Color Foreground => new(255, 170, 0);       // Bright amber
    public override Color ForegroundBright => Color.BrightYellow;
    public override Color ForegroundDim => new(180, 100, 0);    // Dim amber
    public override Color Accent => new(255, 106, 0);           // Vibrant orange-amber
    public override Color AccentBright => Color.White;
    public override Color Border => new(180, 100, 0);           // Medium amber for borders
    public override Color Grid => new(64, 40, 0);               // Very dim amber for grid
    public override Color StatusActive => Color.BrightGreen;
    public override Color StatusInactive => Color.Gray;
    public override Color Error => Color.Red;
    public override Color Warning => Color.BrightYellow;
}
