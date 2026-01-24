using Terminal.Gui;

namespace Opcilloscope.App.Keybindings;

/// <summary>
/// Represents a single keybinding with its context, key, action, and metadata.
/// Inspired by lazygit's keybinding system.
/// </summary>
public sealed class Keybinding
{
    /// <summary>
    /// The context in which this keybinding is active.
    /// </summary>
    public KeybindingContext Context { get; }

    /// <summary>
    /// The key that triggers this keybinding.
    /// </summary>
    public Key Key { get; }

    /// <summary>
    /// Short label for the keybinding (e.g., "Subscribe", "Delete").
    /// Used in status bar and quick help.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Longer description of what the keybinding does.
    /// Used in the full help dialog.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// The action to execute when the keybinding is triggered.
    /// </summary>
    public Action Handler { get; }

    /// <summary>
    /// Whether this keybinding should be shown in the status bar.
    /// </summary>
    public bool ShowInStatusBar { get; }

    /// <summary>
    /// Priority for status bar display (lower = shown first).
    /// </summary>
    public int StatusBarPriority { get; }

    /// <summary>
    /// Category for grouping in help display.
    /// </summary>
    public string Category { get; }

    public Keybinding(
        KeybindingContext context,
        Key key,
        string label,
        string description,
        Action handler,
        bool showInStatusBar = true,
        int statusBarPriority = 100,
        string category = "General")
    {
        Context = context;
        Key = key;
        Label = label;
        Description = description;
        Handler = handler;
        ShowInStatusBar = showInStatusBar;
        StatusBarPriority = statusBarPriority;
        Category = category;
    }

    /// <summary>
    /// Gets a formatted string representation of the key for display.
    /// </summary>
    public string KeyDisplay => FormatKey(Key);

    /// <summary>
    /// Formats a Terminal.Gui Key for human-readable display.
    /// </summary>
    private static string FormatKey(Key key)
    {
        var keyCode = key.KeyCode;

        // Handle function keys
        if (keyCode >= KeyCode.F1 && keyCode <= KeyCode.F12)
        {
            return keyCode.ToString();
        }

        // Handle special keys
        return keyCode switch
        {
            KeyCode.Enter => "Enter",
            KeyCode.Space => "Space",
            KeyCode.Tab => "Tab",
            KeyCode.Backspace => "Backspace",
            KeyCode.Delete => "Delete",
            KeyCode.Esc => "Esc",
            KeyCode.Home => "Home",
            KeyCode.End => "End",
            KeyCode.PageUp => "PgUp",
            KeyCode.PageDown => "PgDn",
            KeyCode.CursorUp => "↑",
            KeyCode.CursorDown => "↓",
            KeyCode.CursorLeft => "←",
            KeyCode.CursorRight => "→",
            _ => FormatKeyWithModifiers(key)
        };
    }

    /// <summary>
    /// Formats a key with its modifiers (Ctrl, Shift, Alt).
    /// </summary>
    private static string FormatKeyWithModifiers(Key key)
    {
        var parts = new List<string>();

        if (key.KeyCode.HasFlag(KeyCode.CtrlMask))
            parts.Add("Ctrl");
        if (key.KeyCode.HasFlag(KeyCode.ShiftMask))
            parts.Add("Shift");
        if (key.KeyCode.HasFlag(KeyCode.AltMask))
            parts.Add("Alt");

        // Get the base key without modifiers
        var baseKey = key.KeyCode & ~KeyCode.CtrlMask & ~KeyCode.ShiftMask & ~KeyCode.AltMask;

        // Format the base key character
        var keyChar = ((char)baseKey).ToString().ToUpperInvariant();
        parts.Add(keyChar);

        return string.Join("+", parts);
    }

    /// <summary>
    /// Checks if this keybinding matches the given key event.
    /// </summary>
    public bool Matches(Key eventKey)
    {
        return eventKey == Key;
    }
}
