namespace Opcilloscope.App.Keybindings;

/// <summary>
/// Defines the contexts in which keybindings can be active.
/// Inspired by lazygit's context-based keybinding system.
/// </summary>
public enum KeybindingContext
{
    /// <summary>
    /// Global keybindings available in all contexts.
    /// </summary>
    Global,

    /// <summary>
    /// Keybindings active when AddressSpaceView has focus.
    /// </summary>
    AddressSpace,

    /// <summary>
    /// Keybindings active when MonitoredVariablesView has focus.
    /// </summary>
    MonitoredVariables,

    /// <summary>
    /// Keybindings active when ScopeView is displayed.
    /// </summary>
    Scope,

    /// <summary>
    /// Keybindings active when TrendPlotView is displayed.
    /// </summary>
    TrendPlot,

    /// <summary>
    /// Keybindings active in dialogs.
    /// </summary>
    Dialog
}
