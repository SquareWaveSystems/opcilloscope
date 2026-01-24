using Terminal.Gui;

namespace Opcilloscope.App.Keybindings;

/// <summary>
/// Configures the default keybindings for opcilloscope.
/// Inspired by lazygit's keybinding organization.
/// </summary>
public static class DefaultKeybindings
{
    /// <summary>
    /// Callback interface for keybinding actions.
    /// </summary>
    public interface IKeybindingActions
    {
        // Navigation
        void SwitchPane();
        void ShowHelp();
        void ShowQuickHelp();
        void OpenMenu();

        // Address Space
        void SubscribeSelected();
        void RefreshTree();

        // Monitored Variables
        void UnsubscribeSelected();
        void ToggleScopeSelection();
        void WriteToSelected();
        void ShowTrendPlot();
        void OpenScope();

        // Application
        void OpenConfig();
        void SaveConfig();
        void SaveConfigAs();
        void ToggleRecording();
        void Connect();
        void Disconnect();
        void Quit();
    }

    /// <summary>
    /// Registers all default keybindings with the provided manager.
    /// </summary>
    public static void Configure(KeybindingManager manager, IKeybindingActions actions)
    {
        ConfigureGlobalBindings(manager, actions);
        ConfigureAddressSpaceBindings(manager, actions);
        ConfigureMonitoredVariablesBindings(manager, actions);
        ConfigureScopeBindings(manager);
    }

    private static void ConfigureGlobalBindings(KeybindingManager manager, IKeybindingActions actions)
    {
        // Navigation
        manager.RegisterGlobal(
            Key.Tab,
            "Switch",
            "Switch between panes",
            actions.SwitchPane,
            showInStatusBar: true,
            statusBarPriority: 10,
            category: "Navigation");

        manager.RegisterGlobal(
            Key.F1,
            "Help",
            "Show help",
            actions.ShowHelp,
            showInStatusBar: true,
            statusBarPriority: 1,
            category: "Application");

        manager.RegisterGlobal(
            (Key)'?',
            "?",
            "Show context-sensitive quick help",
            actions.ShowQuickHelp,
            showInStatusBar: false,
            statusBarPriority: 2,
            category: "Application");

        manager.RegisterGlobal(
            Key.F10,
            "Menu",
            "Open menu",
            actions.OpenMenu,
            showInStatusBar: true,
            statusBarPriority: 99,
            category: "Application");

        // File operations
        manager.RegisterGlobal(
            Key.O.WithCtrl,
            "Open",
            "Open configuration file",
            actions.OpenConfig,
            showInStatusBar: false,
            statusBarPriority: 50,
            category: "Application");

        manager.RegisterGlobal(
            Key.S.WithCtrl,
            "Save",
            "Save configuration",
            actions.SaveConfig,
            showInStatusBar: false,
            statusBarPriority: 51,
            category: "Application");

        manager.RegisterGlobal(
            Key.S.WithCtrl.WithShift,
            "SaveAs",
            "Save configuration as...",
            actions.SaveConfigAs,
            showInStatusBar: false,
            statusBarPriority: 52,
            category: "Application");

        manager.RegisterGlobal(
            Key.R.WithCtrl,
            "Record",
            "Toggle CSV recording",
            actions.ToggleRecording,
            showInStatusBar: false,
            statusBarPriority: 53,
            category: "Application");

        manager.RegisterGlobal(
            Key.Q.WithCtrl,
            "Quit",
            "Quit application",
            actions.Quit,
            showInStatusBar: false,
            statusBarPriority: 100,
            category: "Application");
    }

    private static void ConfigureAddressSpaceBindings(KeybindingManager manager, IKeybindingActions actions)
    {
        manager.Register(
            KeybindingContext.AddressSpace,
            Key.Enter,
            "Subscribe",
            "Subscribe to selected node",
            actions.SubscribeSelected,
            showInStatusBar: true,
            statusBarPriority: 20,
            category: "Address Space");

        manager.Register(
            KeybindingContext.AddressSpace,
            Key.F5,
            "Refresh",
            "Refresh address space tree",
            actions.RefreshTree,
            showInStatusBar: true,
            statusBarPriority: 30,
            category: "Address Space");
    }

    private static void ConfigureMonitoredVariablesBindings(KeybindingManager manager, IKeybindingActions actions)
    {
        manager.Register(
            KeybindingContext.MonitoredVariables,
            Key.Delete,
            "Unsub",
            "Unsubscribe from selected variable",
            actions.UnsubscribeSelected,
            showInStatusBar: true,
            statusBarPriority: 20,
            category: "Monitored Variables");

        manager.Register(
            KeybindingContext.MonitoredVariables,
            Key.Space,
            "Sel",
            "Toggle selection for Scope/Recording",
            actions.ToggleScopeSelection,
            showInStatusBar: true,
            statusBarPriority: 25,
            category: "Monitored Variables");

        manager.Register(
            KeybindingContext.MonitoredVariables,
            (Key)'w',
            "Write",
            "Write value to selected node",
            actions.WriteToSelected,
            showInStatusBar: true,
            statusBarPriority: 30,
            category: "Monitored Variables");

        manager.Register(
            KeybindingContext.MonitoredVariables,
            (Key)'W',
            "Write",
            "Write value to selected node",
            actions.WriteToSelected,
            showInStatusBar: false,
            statusBarPriority: 31,
            category: "Monitored Variables");

        manager.Register(
            KeybindingContext.MonitoredVariables,
            (Key)'t',
            "Trend",
            "Show trend plot for selected variable",
            actions.ShowTrendPlot,
            showInStatusBar: true,
            statusBarPriority: 40,
            category: "Monitored Variables");

        manager.Register(
            KeybindingContext.MonitoredVariables,
            (Key)'T',
            "Trend",
            "Show trend plot for selected variable",
            actions.ShowTrendPlot,
            showInStatusBar: false,
            statusBarPriority: 41,
            category: "Monitored Variables");

        manager.Register(
            KeybindingContext.MonitoredVariables,
            (Key)'s',
            "Scope",
            "Open Scope with selected variables",
            actions.OpenScope,
            showInStatusBar: true,
            statusBarPriority: 50,
            category: "Monitored Variables");

        manager.Register(
            KeybindingContext.MonitoredVariables,
            (Key)'S',
            "Scope",
            "Open Scope with selected variables",
            actions.OpenScope,
            showInStatusBar: false,
            statusBarPriority: 51,
            category: "Monitored Variables");
    }

    private static void ConfigureScopeBindings(KeybindingManager manager)
    {
        // NOTE: Scope keybindings are handled directly by ScopeView's KeyDown handler
        // because ScopeView is a modal dialog that captures its own key events.
        // These registrations exist solely for:
        //   1. Help text generation (F1 and ? dialogs)
        //   2. Status bar display when Scope context is active
        // The empty handlers () => { } are intentional - they are never invoked.
        // If you need to change Scope keybindings, update both here AND in ScopeView.

        manager.Register(
            KeybindingContext.Scope,
            Key.Space,
            "Pause",
            "Pause/resume plotting",
            () => { }, // Documentation only - handled by ScopeView
            showInStatusBar: true,
            statusBarPriority: 10,
            category: "Scope View");

        manager.Register(
            KeybindingContext.Scope,
            (Key)'+',
            "Zoom+",
            "Zoom in (increase scale)",
            () => { }, // Documentation only - handled by ScopeView
            showInStatusBar: true,
            statusBarPriority: 20,
            category: "Scope View");

        manager.Register(
            KeybindingContext.Scope,
            (Key)'-',
            "Zoom-",
            "Zoom out (decrease scale)",
            () => { }, // Documentation only - handled by ScopeView
            showInStatusBar: true,
            statusBarPriority: 21,
            category: "Scope View");

        manager.Register(
            KeybindingContext.Scope,
            (Key)'r',
            "Reset",
            "Reset to auto-scale",
            () => { }, // Documentation only - handled by ScopeView
            showInStatusBar: true,
            statusBarPriority: 30,
            category: "Scope View");
    }
}
