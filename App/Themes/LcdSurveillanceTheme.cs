using Terminal.Gui;

namespace OpcScope.App.Themes;

/// <summary>
/// LCD surveillance display theme inspired by industrial security equipment.
/// Muted greenish tones on a dark background, evoking 1980s-90s
/// LCD displays found in security systems and industrial controllers.
/// Based on the "Surveillance Device" from retro-futuristic-ui-design.
/// Screen gradient: #4a5a3a to #354525 (muted greenish tones)
/// </summary>
public class LcdSurveillanceTheme : RetroTheme
{
    public override string Name => "LCD Surveillance";
    public override string Description => "Industrial surveillance LCD display";

    // Dark greenish-gray background (like old LCDs)
    public override Color Background => new(26, 32, 20);

    // Muted green LCD colors
    public override Color Foreground => new(140, 160, 110);     // LCD active segments
    public override Color ForegroundBright => new(180, 200, 150);
    public override Color ForegroundDim => new(90, 110, 70);
    public override Color Accent => new(100, 140, 80);          // Highlighted segments
    public override Color AccentBright => new(160, 200, 120);
    public override Color Border => new(74, 90, 58);            // Frame color
    public override Color Grid => new(40, 50, 32);              // Very subtle grid
    public override Color StatusActive => new(120, 180, 80);    // Bright LCD green
    public override Color StatusInactive => new(60, 70, 50);
    public override Color Error => new(180, 80, 60);            // Muted red
    public override Color Warning => new(180, 160, 60);         // Muted yellow

    // LCD doesn't have scanlines in the same way
    public override bool EnableScanlines => false;

    // LCD has more subtle glow
    public override bool EnableGlow => false;
}
