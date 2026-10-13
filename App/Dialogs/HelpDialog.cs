using Terminal.Gui;
using Opcilloscope.Utilities;
using Opcilloscope.App.Keybindings;
using Opcilloscope.App.Themes;
using ThemeManager = Opcilloscope.App.Themes.ThemeManager;

namespace Opcilloscope.App.Dialogs;

/// <summary>
/// Help dialog displaying all keyboard shortcuts in a formatted layout.
/// Help text is auto-generated from the KeybindingManager.
/// </summary>
public class HelpDialog : Dialog
{
    private readonly KeybindingManager _keybindingManager;

    public HelpDialog(KeybindingManager keybindingManager)
    {
        _keybindingManager = keybindingManager ?? throw new ArgumentNullException(nameof(keybindingManager));

        Title = " opcilloscope - Help ";
        Width = 64;
        Height = Dim.Fill(2);

        var theme = ThemeManager.Current;

        // Apply theme styling with emphasized border (double-line)
        SetScheme(theme.MainColorScheme);
        BorderStyle = theme.EmphasizedBorderStyle;

        // Create content view with the help text
        var contentView = new ScrollingTextView
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
            Height = Dim.Fill(2),
            Text = GenerateHelpFromBindings(keybindingManager),
        }.WithScheme(new Scheme
        {
            Normal = new Attribute(theme.Foreground, theme.Background),
            Focus = new Attribute(theme.Foreground, theme.Background),
            HotNormal = new Attribute(theme.Foreground, theme.Background),
            HotFocus = new Attribute(theme.Foreground, theme.Background),
            Disabled = new Attribute(theme.MutedText, theme.Background)
        });

        // OK button - highlighted with accent color (default action)
        var defaultButtonScheme = new Scheme
        {
            Normal = new Attribute(theme.Accent, theme.Background),
            Focus = new Attribute(theme.AccentBright, theme.Background),
            HotNormal = new Attribute(theme.Accent, theme.Background),
            HotFocus = new Attribute(theme.AccentBright, theme.Background),
            Disabled = new Attribute(theme.MutedText, theme.Background)
        };

        var okButton = new Button
        {
            Text = "OK",
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1),
            IsDefault = true,
        }.WithScheme(defaultButtonScheme);
        okButton.Accepting += (_, _) => RequestStop();

        Add(contentView);
        Add(okButton);

        okButton.SetFocus();

        // Subscribe to theme changes
        ThemeManager.ThemeChanged += OnThemeChanged;
    }

    /// <summary>
    /// Generates help text from the KeybindingManager.
    /// </summary>
    private static string GenerateHelpFromBindings(KeybindingManager manager)
    {
        var lines = new List<string> { "" };

        foreach (var group in manager.GetAllBindingsGroupedByCategory())
        {
            lines.Add(group.Key.ToUpperInvariant());

            // Some actions register both a lower- and upper-case key variant
            // (e.g. r/R) that render identically; only the status-bar variant
            // carries ShowInStatusBar. Ordering by priority encounters that
            // variant first, so deduping by rendered row keeps the right one.
            var seen = new HashSet<string>();
            foreach (var binding in group.OrderBy(b => b.StatusBarPriority))
            {
                var rowKey = $"{binding.KeyDisplay}\0{binding.Description}";
                if (!seen.Add(rowKey))
                {
                    continue;
                }

                var keyDisplay = binding.KeyDisplay.PadRight(16);
                lines.Add($"  {keyDisplay}{binding.Description}");
            }

            lines.Add("");
        }

        // Add tips section
        lines.Add("TIPS");
        lines.Add("  - Press ? at any time to show this help");
        lines.Add("  - Only Variable nodes can be subscribed");
        lines.Add("  - Select up to 5 variables for Scope/Recording");
        lines.Add("  - Status bar shows context-specific shortcuts");
        lines.Add("  - Publishing Interval (Connect dialog) controls how often");
        lines.Add("    the server sends data and affects Scope");
        lines.Add("    and CSV data resolution");

        return string.Join("\n", lines);
    }

    private void OnThemeChanged(AppTheme theme)
    {
        UiThread.Run(() =>
        {
            SetScheme(theme.MainColorScheme);
            BorderStyle = theme.EmphasizedBorderStyle;
            SetNeedsLayout();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Read-only, word-wrapped text that scrolls vertically, built on a plain View
    /// (content size + built-in vertical scrollbar) instead of the obsolete TextView.
    /// </summary>
    private sealed class ScrollingTextView : View
    {
        private int _wrappedWidth = -1;

        public ScrollingTextView()
        {
            CanFocus = true;
            TextFormatter.WordWrap = true;
            ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar;

            AddCommand(Command.ScrollUp, () => ScrollVertical(-1));
            AddCommand(Command.ScrollDown, () => ScrollVertical(1));
            AddCommand(Command.PageUp, () => ScrollVertical(-Viewport.Height));
            AddCommand(Command.PageDown, () => ScrollVertical(Viewport.Height));
            AddCommand(Command.Start, () => ScrollVertical(-Viewport.Y));
            AddCommand(Command.End, () => ScrollVertical(GetContentSize().Height));

            KeyBindings.Add(Key.CursorUp, Command.ScrollUp);
            KeyBindings.Add(Key.CursorDown, Command.ScrollDown);
            KeyBindings.Add(Key.PageUp, Command.PageUp);
            KeyBindings.Add(Key.PageDown, Command.PageDown);
            KeyBindings.Add(Key.Home, Command.Start);
            KeyBindings.Add(Key.End, Command.End);

            ViewportChanged += (_, _) => UpdateContentSize();
            TextChanged += (_, _) =>
            {
                _wrappedWidth = -1;
                UpdateContentSize();
            };
        }

        // Content height is the number of wrapped lines at the current width,
        // so the scrollbar range follows the dialog size.
        private void UpdateContentSize()
        {
            var width = Viewport.Width;
            if (width <= 0 || width == _wrappedWidth)
            {
                return;
            }

            _wrappedWidth = width;
            var lines = TextFormatter.Format(Text, width, justify: false, wordWrap: true);
            SetContentSize(new System.Drawing.Size(width, lines.Count));
        }
    }
}
