using Terminal.Gui;
using Opcilloscope.App.Keybindings;
using Opcilloscope.App.Themes;
using Attribute = Terminal.Gui.Attribute;
using ThemeManager = Opcilloscope.App.Themes.ThemeManager;

namespace Opcilloscope.App.Dialogs;

/// <summary>
/// Quick context-sensitive help overlay showing keybindings for the current context.
/// Inspired by lazygit's ? keybindings menu.
/// </summary>
public class QuickHelpDialog : Dialog
{
    private readonly KeybindingManager _keybindingManager;

    public QuickHelpDialog(KeybindingManager keybindingManager)
    {
        _keybindingManager = keybindingManager;

        var contextName = KeybindingManager.GetContextDisplayName(keybindingManager.CurrentContext);
        Title = $" {contextName} - Keybindings ";

        // Calculate size based on content
        var bindings = keybindingManager.GetActiveBindings().ToList();

        int maxKeyWidth;
        // Handle empty bindings case
        if (bindings.Count == 0)
        {
            maxKeyWidth = 8; // Default width for "No keybindings"
            Width = 44;
            Height = 8;
        }
        else
        {
            maxKeyWidth = bindings.Max(b => b.KeyDisplay.Length);
            var maxDescWidth = bindings.Max(b => b.Description.Length);
            var contentWidth = Math.Max(maxKeyWidth + maxDescWidth + 6, 40);

            Width = Math.Min(contentWidth + 4, 60);
            Height = Math.Min(bindings.Count + 6, 24);
        }

        var theme = ThemeManager.Current;

        // Apply theme styling
        ColorScheme = theme.MainColorScheme;
        BorderStyle = theme.EmphasizedBorderStyle;

        // Create content with keybindings
        var content = GenerateHelpContent(bindings, maxKeyWidth);

        var textView = new TextView
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
            Height = Dim.Fill(2),
            ReadOnly = true,
            WordWrap = false,
            Text = content,
            ColorScheme = new ColorScheme
            {
                Normal = new Attribute(theme.Foreground, theme.Background),
                Focus = new Attribute(theme.Foreground, theme.Background),
                HotNormal = new Attribute(theme.Foreground, theme.Background),
                HotFocus = new Attribute(theme.Foreground, theme.Background),
                Disabled = new Attribute(theme.MutedText, theme.Background)
            }
        };

        // Close on any key
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == KeyCode.Esc || e.KeyCode == (KeyCode)'?' || e.KeyCode == KeyCode.Enter)
            {
                RequestStop();
                e.Handled = true;
            }
        };

        var closeButton = new Button
        {
            Text = "Close",
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1),
            IsDefault = true,
            ColorScheme = theme.ButtonColorScheme
        };
        closeButton.Accepting += (_, _) => RequestStop();

        Add(textView);
        Add(closeButton);
    }

    private static string GenerateHelpContent(List<Keybinding> bindings, int keyWidth)
    {
        var lines = new List<string>();

        if (bindings.Count == 0)
        {
            lines.Add("  No keybindings available for this context.");
        }
        else
        {
            // Group by category
            var groups = bindings.GroupBy(b => b.Category);

            foreach (var group in groups)
            {
                foreach (var binding in group)
                {
                    var key = binding.KeyDisplay.PadRight(keyWidth + 2);
                    lines.Add($"  {key}{binding.Description}");
                }
            }
        }

        // Add footer hint
        lines.Add("");
        lines.Add("  Press ? or Esc to close");

        return string.Join("\n", lines);
    }
}
