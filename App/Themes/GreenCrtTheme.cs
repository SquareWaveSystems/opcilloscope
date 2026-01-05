using Terminal.Gui;

namespace OpcScope.App.Themes;

/// <summary>
/// Classic green phosphor CRT theme inspired by IBM terminals and early computing.
/// The quintessential "hacker" terminal aesthetic, also seen in The Matrix.
/// Primary: #00ff41 (vibrant green), Secondary: #006600 (dim green)
/// </summary>
public class GreenCrtTheme : RetroTheme
{
    public override string Name => "Green CRT";
    public override string Description => "Classic green phosphor terminal (IBM style)";

    public override Color Background => Color.Black;

    // Green phosphor color range
    public override Color Foreground => new(0, 255, 65);        // Bright green phosphor
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
