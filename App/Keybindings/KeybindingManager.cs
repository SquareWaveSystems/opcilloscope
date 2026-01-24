using Terminal.Gui;

namespace Opcilloscope.App.Keybindings;

/// <summary>
/// Manages keybindings with context-aware resolution, inspired by lazygit.
///
/// Key features:
/// - Context-based keybindings (different keys per view/context)
/// - Hierarchical resolution (context-specific > global)
/// - Auto-generation of help text
/// - Status bar integration
/// </summary>
public sealed class KeybindingManager
{
    private readonly Dictionary<KeybindingContext, List<Keybinding>> _bindings = new();
    private KeybindingContext _currentContext = KeybindingContext.Global;

    /// <summary>
    /// Event fired when a keybinding is executed.
    /// </summary>
    public event Action<Keybinding>? KeybindingExecuted;

    /// <summary>
    /// Gets or sets the current active context.
    /// </summary>
    public KeybindingContext CurrentContext
    {
        get => _currentContext;
        set => _currentContext = value;
    }

    /// <summary>
    /// Creates a new KeybindingManager instance.
    /// </summary>
    public KeybindingManager()
    {
        // Initialize dictionaries for all contexts
        foreach (KeybindingContext context in Enum.GetValues<KeybindingContext>())
        {
            _bindings[context] = new List<Keybinding>();
        }
    }

    /// <summary>
    /// Registers a keybinding for a specific context.
    /// </summary>
    public KeybindingManager Register(
        KeybindingContext context,
        Key key,
        string label,
        string description,
        Action handler,
        bool showInStatusBar = true,
        int statusBarPriority = 100,
        string category = "General")
    {
        var keybinding = new Keybinding(
            context, key, label, description, handler,
            showInStatusBar, statusBarPriority, category);

        _bindings[context].Add(keybinding);
        return this;
    }

    /// <summary>
    /// Registers a global keybinding (available in all contexts).
    /// </summary>
    public KeybindingManager RegisterGlobal(
        Key key,
        string label,
        string description,
        Action handler,
        bool showInStatusBar = true,
        int statusBarPriority = 100,
        string category = "Application")
    {
        return Register(KeybindingContext.Global, key, label, description, handler,
            showInStatusBar, statusBarPriority, category);
    }

    /// <summary>
    /// Tries to handle a key event using the current context's keybindings.
    /// Resolution order: current context > global.
    /// </summary>
    /// <returns>True if the key was handled, false otherwise.</returns>
    public bool TryHandle(Key key)
    {
        // First, check context-specific bindings
        if (_currentContext != KeybindingContext.Global)
        {
            var contextBinding = FindBinding(_currentContext, key);
            if (contextBinding != null)
            {
                Execute(contextBinding);
                return true;
            }
        }

        // Fall back to global bindings
        var globalBinding = FindBinding(KeybindingContext.Global, key);
        if (globalBinding != null)
        {
            Execute(globalBinding);
            return true;
        }

        return false;
    }

    private Keybinding? FindBinding(KeybindingContext context, Key key)
    {
        return _bindings[context].FirstOrDefault(b => b.Matches(key));
    }

    private void Execute(Keybinding binding)
    {
        binding.Handler();
        KeybindingExecuted?.Invoke(binding);
    }

    /// <summary>
    /// Gets all keybindings for the current context, including globals.
    /// Ordered by status bar priority.
    /// </summary>
    public IEnumerable<Keybinding> GetActiveBindings()
    {
        var contextBindings = _currentContext != KeybindingContext.Global
            ? _bindings[_currentContext]
            : Enumerable.Empty<Keybinding>();

        var globalBindings = _bindings[KeybindingContext.Global];

        return contextBindings
            .Concat(globalBindings)
            .OrderBy(b => b.StatusBarPriority);
    }

    /// <summary>
    /// Gets keybindings that should be shown in the status bar for the current context.
    /// </summary>
    public IEnumerable<Keybinding> GetStatusBarBindings()
    {
        return GetActiveBindings()
            .Where(b => b.ShowInStatusBar)
            .Take(6); // Limit to avoid overflow
    }

    /// <summary>
    /// Gets all keybindings for a specific context.
    /// </summary>
    public IEnumerable<Keybinding> GetBindingsForContext(KeybindingContext context)
    {
        return _bindings[context].AsReadOnly();
    }

    /// <summary>
    /// Gets all registered keybindings grouped by category.
    /// </summary>
    public IEnumerable<IGrouping<string, Keybinding>> GetAllBindingsGroupedByCategory()
    {
        return _bindings.Values
            .SelectMany(list => list)
            .GroupBy(b => b.Category)
            .OrderBy(g => GetCategoryOrder(g.Key));
    }

    /// <summary>
    /// Gets all registered keybindings grouped by context.
    /// </summary>
    public IEnumerable<IGrouping<KeybindingContext, Keybinding>> GetAllBindingsGroupedByContext()
    {
        return _bindings
            .Where(kvp => kvp.Value.Count > 0)
            .SelectMany(kvp => kvp.Value.Select(b => (Context: kvp.Key, Binding: b)))
            .GroupBy(x => x.Context, x => x.Binding)
            .OrderBy(g => GetContextOrder(g.Key));
    }

    /// <summary>
    /// Generates formatted help text for the current context.
    /// </summary>
    public string GenerateContextHelp()
    {
        var lines = new List<string>();
        var contextName = GetContextDisplayName(_currentContext);

        lines.Add($"[{contextName}]");
        lines.Add("");

        foreach (var binding in GetActiveBindings())
        {
            var keyDisplay = binding.KeyDisplay.PadRight(16);
            lines.Add($"  {keyDisplay}{binding.Description}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Generates complete help text for all keybindings.
    /// </summary>
    public string GenerateFullHelp()
    {
        var lines = new List<string>();

        foreach (var group in GetAllBindingsGroupedByCategory())
        {
            lines.Add(group.Key.ToUpperInvariant());

            foreach (var binding in group.OrderBy(b => b.StatusBarPriority))
            {
                var keyDisplay = binding.KeyDisplay.PadRight(16);
                lines.Add($"  {keyDisplay}{binding.Description}");
            }

            lines.Add("");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Gets a human-readable name for a context.
    /// </summary>
    public static string GetContextDisplayName(KeybindingContext context)
    {
        return context switch
        {
            KeybindingContext.Global => "Global",
            KeybindingContext.AddressSpace => "Address Space",
            KeybindingContext.MonitoredVariables => "Monitored Variables",
            KeybindingContext.Scope => "Scope View",
            KeybindingContext.TrendPlot => "Trend Plot",
            KeybindingContext.Dialog => "Dialog",
            _ => context.ToString()
        };
    }

    private static int GetCategoryOrder(string category)
    {
        return category switch
        {
            "Navigation" => 0,
            "Address Space" => 1,
            "Monitored Variables" => 2,
            "Scope View" => 3,
            "Application" => 4,
            _ => 99
        };
    }

    private static int GetContextOrder(KeybindingContext context)
    {
        return context switch
        {
            KeybindingContext.Global => 0,
            KeybindingContext.AddressSpace => 1,
            KeybindingContext.MonitoredVariables => 2,
            KeybindingContext.Scope => 3,
            KeybindingContext.TrendPlot => 4,
            KeybindingContext.Dialog => 5,
            _ => 99
        };
    }
}
