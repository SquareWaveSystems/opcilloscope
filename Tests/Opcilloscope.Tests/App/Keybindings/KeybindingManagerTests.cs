using Opcilloscope.App.Keybindings;
using Terminal.Gui;

namespace Opcilloscope.Tests.App.Keybindings;

public class KeybindingManagerTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_InitializesAllContexts()
    {
        // Act
        var manager = new KeybindingManager();

        // Assert - all contexts should be initialized with empty lists
        foreach (KeybindingContext context in Enum.GetValues<KeybindingContext>())
        {
            var bindings = manager.GetBindingsForContext(context);
            Assert.NotNull(bindings);
            Assert.Empty(bindings);
        }
    }

    [Fact]
    public void Constructor_DefaultContextIsGlobal()
    {
        // Act
        var manager = new KeybindingManager();

        // Assert
        Assert.Equal(KeybindingContext.Global, manager.CurrentContext);
    }

    #endregion

    #region Registration Tests

    [Fact]
    public void Register_AddsKeybindingToCorrectContext()
    {
        // Arrange
        var manager = new KeybindingManager();
        var executed = false;

        // Act
        manager.Register(
            KeybindingContext.AddressSpace,
            Key.Enter,
            "Subscribe",
            "Subscribe to node",
            () => executed = true);

        // Assert
        var bindings = manager.GetBindingsForContext(KeybindingContext.AddressSpace);
        Assert.Single(bindings);
        Assert.Equal(Key.Enter, bindings[0].Key);
        Assert.Equal("Subscribe", bindings[0].Label);
    }

    [Fact]
    public void RegisterGlobal_AddsKeybindingToGlobalContext()
    {
        // Arrange
        var manager = new KeybindingManager();

        // Act
        manager.RegisterGlobal(
            Key.F1,
            "Help",
            "Show help",
            () => { });

        // Assert
        var bindings = manager.GetBindingsForContext(KeybindingContext.Global);
        Assert.Single(bindings);
        Assert.Equal(Key.F1, bindings[0].Key);
    }

    [Fact]
    public void Register_WithNullHandler_ThrowsArgumentNullException()
    {
        // Arrange
        var manager = new KeybindingManager();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            manager.Register(
                KeybindingContext.Global,
                Key.Enter,
                "Test",
                "Test description",
                null!));
    }

    [Fact]
    public void Register_ReturnsManagerForFluentChaining()
    {
        // Arrange
        var manager = new KeybindingManager();

        // Act
        var result = manager.Register(
            KeybindingContext.Global,
            Key.F1,
            "Help",
            "Help description",
            () => { });

        // Assert
        Assert.Same(manager, result);
    }

    [Fact]
    public void Register_DuplicateKey_ReplacesExistingBinding()
    {
        // Arrange
        var manager = new KeybindingManager();
        var firstExecuted = false;
        var secondExecuted = false;

        // Act - Register same key twice
        manager.Register(
            KeybindingContext.AddressSpace,
            Key.Enter,
            "First",
            "First binding",
            () => firstExecuted = true);

        manager.Register(
            KeybindingContext.AddressSpace,
            Key.Enter,
            "Second",
            "Second binding",
            () => secondExecuted = true);

        // Assert - Only one binding should exist with the second handler
        var bindings = manager.GetBindingsForContext(KeybindingContext.AddressSpace);
        Assert.Single(bindings);
        Assert.Equal("Second", bindings[0].Label);

        // Execute and verify second handler is called
        manager.CurrentContext = KeybindingContext.AddressSpace;
        manager.TryHandle(Key.Enter);
        Assert.False(firstExecuted);
        Assert.True(secondExecuted);
    }

    [Fact]
    public void Register_SameKeyDifferentContexts_BothRegistered()
    {
        // Arrange
        var manager = new KeybindingManager();

        // Act
        manager.Register(KeybindingContext.AddressSpace, Key.Enter, "Subscribe", "Subscribe", () => { });
        manager.Register(KeybindingContext.MonitoredVariables, Key.Enter, "Edit", "Edit value", () => { });

        // Assert
        Assert.Single(manager.GetBindingsForContext(KeybindingContext.AddressSpace));
        Assert.Single(manager.GetBindingsForContext(KeybindingContext.MonitoredVariables));
    }

    #endregion

    #region Context Switching Tests

    [Fact]
    public void CurrentContext_CanBeSet()
    {
        // Arrange
        var manager = new KeybindingManager();

        // Act
        manager.CurrentContext = KeybindingContext.AddressSpace;

        // Assert
        Assert.Equal(KeybindingContext.AddressSpace, manager.CurrentContext);
    }

    [Fact]
    public void TryHandle_ContextSpecificBindingTakesPrecedence()
    {
        // Arrange
        var manager = new KeybindingManager();
        var globalExecuted = false;
        var contextExecuted = false;

        manager.RegisterGlobal(Key.Enter, "Global", "Global action", () => globalExecuted = true);
        manager.Register(KeybindingContext.AddressSpace, Key.Enter, "Context", "Context action", () => contextExecuted = true);

        manager.CurrentContext = KeybindingContext.AddressSpace;

        // Act
        var handled = manager.TryHandle(Key.Enter);

        // Assert
        Assert.True(handled);
        Assert.False(globalExecuted);
        Assert.True(contextExecuted);
    }

    [Fact]
    public void TryHandle_FallsBackToGlobalWhenNoContextBinding()
    {
        // Arrange
        var manager = new KeybindingManager();
        var globalExecuted = false;

        manager.RegisterGlobal(Key.F1, "Help", "Show help", () => globalExecuted = true);
        manager.CurrentContext = KeybindingContext.AddressSpace;

        // Act
        var handled = manager.TryHandle(Key.F1);

        // Assert
        Assert.True(handled);
        Assert.True(globalExecuted);
    }

    [Fact]
    public void TryHandle_ReturnsFalseWhenNoMatchingBinding()
    {
        // Arrange
        var manager = new KeybindingManager();
        manager.RegisterGlobal(Key.F1, "Help", "Help", () => { });

        // Act
        var handled = manager.TryHandle(Key.F2);

        // Assert
        Assert.False(handled);
    }

    [Fact]
    public void TryHandle_GlobalContextOnlyChecksGlobal()
    {
        // Arrange
        var manager = new KeybindingManager();
        var executed = false;

        manager.RegisterGlobal(Key.F1, "Help", "Help", () => executed = true);
        manager.CurrentContext = KeybindingContext.Global;

        // Act
        var handled = manager.TryHandle(Key.F1);

        // Assert
        Assert.True(handled);
        Assert.True(executed);
    }

    #endregion

    #region GetActiveBindings Tests

    [Fact]
    public void GetActiveBindings_IncludesContextAndGlobalBindings()
    {
        // Arrange
        var manager = new KeybindingManager();
        manager.RegisterGlobal(Key.F1, "Help", "Help", () => { });
        manager.Register(KeybindingContext.AddressSpace, Key.Enter, "Subscribe", "Subscribe", () => { });

        manager.CurrentContext = KeybindingContext.AddressSpace;

        // Act
        var bindings = manager.GetActiveBindings().ToList();

        // Assert
        Assert.Equal(2, bindings.Count);
    }

    [Fact]
    public void GetActiveBindings_OrderedByStatusBarPriority()
    {
        // Arrange
        var manager = new KeybindingManager();
        manager.RegisterGlobal(Key.F1, "Low", "Low priority", () => { }, statusBarPriority: 100);
        manager.RegisterGlobal(Key.F2, "High", "High priority", () => { }, statusBarPriority: 1);
        manager.RegisterGlobal(Key.F3, "Medium", "Medium priority", () => { }, statusBarPriority: 50);

        // Act
        var bindings = manager.GetActiveBindings().ToList();

        // Assert
        Assert.Equal("High", bindings[0].Label);
        Assert.Equal("Medium", bindings[1].Label);
        Assert.Equal("Low", bindings[2].Label);
    }

    [Fact]
    public void GetActiveBindings_WhenGlobalContext_ReturnsOnlyGlobals()
    {
        // Arrange
        var manager = new KeybindingManager();
        manager.RegisterGlobal(Key.F1, "Help", "Help", () => { });
        manager.Register(KeybindingContext.AddressSpace, Key.Enter, "Subscribe", "Subscribe", () => { });

        manager.CurrentContext = KeybindingContext.Global;

        // Act
        var bindings = manager.GetActiveBindings().ToList();

        // Assert
        Assert.Single(bindings);
        Assert.Equal("Help", bindings[0].Label);
    }

    #endregion

    #region GetStatusBarBindings Tests

    [Fact]
    public void GetStatusBarBindings_FiltersNonStatusBarBindings()
    {
        // Arrange
        var manager = new KeybindingManager();
        manager.RegisterGlobal(Key.F1, "Visible", "Visible", () => { }, showInStatusBar: true);
        manager.RegisterGlobal(Key.F2, "Hidden", "Hidden", () => { }, showInStatusBar: false);

        // Act
        var bindings = manager.GetStatusBarBindings().ToList();

        // Assert
        Assert.Single(bindings);
        Assert.Equal("Visible", bindings[0].Label);
    }

    [Fact]
    public void GetStatusBarBindings_LimitedToMaxStatusBarShortcuts()
    {
        // Arrange
        var manager = new KeybindingManager();
        var keys = new[] { Key.D0, Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9 };
        for (int i = 0; i < 10; i++)
        {
            manager.RegisterGlobal(
                keys[i].WithCtrl,
                $"Binding{i}",
                $"Binding {i}",
                () => { },
                showInStatusBar: true,
                statusBarPriority: i);
        }

        // Act
        var bindings = manager.GetStatusBarBindings().ToList();

        // Assert
        Assert.Equal(KeybindingManager.MaxStatusBarShortcuts, bindings.Count);
    }

    [Fact]
    public void MaxStatusBarShortcuts_IsSix()
    {
        // Assert
        Assert.Equal(6, KeybindingManager.MaxStatusBarShortcuts);
    }

    #endregion

    #region GetBindingsForContext Tests

    [Fact]
    public void GetBindingsForContext_ReturnsReadOnlyCollection()
    {
        // Arrange
        var manager = new KeybindingManager();
        manager.Register(KeybindingContext.AddressSpace, Key.Enter, "Test", "Test", () => { });

        // Act
        var bindings = manager.GetBindingsForContext(KeybindingContext.AddressSpace);

        // Assert
        Assert.IsAssignableFrom<IReadOnlyList<Keybinding>>(bindings);
    }

    [Fact]
    public void GetBindingsForContext_ReturnsOnlySpecifiedContext()
    {
        // Arrange
        var manager = new KeybindingManager();
        manager.Register(KeybindingContext.AddressSpace, Key.Enter, "AddressSpace", "AddressSpace", () => { });
        manager.Register(KeybindingContext.MonitoredVariables, Key.Delete, "MonitoredVars", "MonitoredVars", () => { });

        // Act
        var addressSpaceBindings = manager.GetBindingsForContext(KeybindingContext.AddressSpace);
        var monitoredVarsBindings = manager.GetBindingsForContext(KeybindingContext.MonitoredVariables);

        // Assert
        Assert.Single(addressSpaceBindings);
        Assert.Equal("AddressSpace", addressSpaceBindings[0].Label);
        Assert.Single(monitoredVarsBindings);
        Assert.Equal("MonitoredVars", monitoredVarsBindings[0].Label);
    }

    #endregion

    #region Event Tests

    [Fact]
    public void KeybindingExecuted_FiredWhenKeybindingHandled()
    {
        // Arrange
        var manager = new KeybindingManager();
        Keybinding? executedBinding = null;

        manager.RegisterGlobal(Key.F1, "Help", "Help", () => { });
        manager.KeybindingExecuted += binding => executedBinding = binding;

        // Act
        manager.TryHandle(Key.F1);

        // Assert
        Assert.NotNull(executedBinding);
        Assert.Equal("Help", executedBinding.Label);
    }

    [Fact]
    public void KeybindingExecuted_NotFiredWhenNoMatch()
    {
        // Arrange
        var manager = new KeybindingManager();
        var eventFired = false;

        manager.RegisterGlobal(Key.F1, "Help", "Help", () => { });
        manager.KeybindingExecuted += _ => eventFired = true;

        // Act
        manager.TryHandle(Key.F2);

        // Assert
        Assert.False(eventFired);
    }

    #endregion

    #region Help Generation Tests

    [Fact]
    public void GenerateContextHelp_IncludesContextName()
    {
        // Arrange
        var manager = new KeybindingManager();
        manager.CurrentContext = KeybindingContext.AddressSpace;

        // Act
        var help = manager.GenerateContextHelp();

        // Assert
        Assert.Contains("[Address Space]", help);
    }

    [Fact]
    public void GenerateFullHelp_GroupsByCategory()
    {
        // Arrange
        var manager = new KeybindingManager();
        manager.RegisterGlobal(Key.F1, "Help", "Help", () => { }, category: "Application");
        manager.Register(KeybindingContext.AddressSpace, Key.Enter, "Subscribe", "Subscribe", () => { }, category: "Navigation");

        // Act
        var help = manager.GenerateFullHelp();

        // Assert
        Assert.Contains("APPLICATION", help);
        Assert.Contains("NAVIGATION", help);
    }

    [Fact]
    public void GetContextDisplayName_ReturnsHumanReadableNames()
    {
        // Assert
        Assert.Equal("Global", KeybindingManager.GetContextDisplayName(KeybindingContext.Global));
        Assert.Equal("Address Space", KeybindingManager.GetContextDisplayName(KeybindingContext.AddressSpace));
        Assert.Equal("Monitored Variables", KeybindingManager.GetContextDisplayName(KeybindingContext.MonitoredVariables));
        Assert.Equal("Scope View", KeybindingManager.GetContextDisplayName(KeybindingContext.Scope));
        Assert.Equal("Trend Plot", KeybindingManager.GetContextDisplayName(KeybindingContext.TrendPlot));
        Assert.Equal("Dialog", KeybindingManager.GetContextDisplayName(KeybindingContext.Dialog));
    }

    #endregion

    #region GetAllBindingsGroupedByCategory Tests

    [Fact]
    public void GetAllBindingsGroupedByCategory_GroupsCorrectly()
    {
        // Arrange
        var manager = new KeybindingManager();
        manager.RegisterGlobal(Key.F1, "Help1", "Help 1", () => { }, category: "Application");
        manager.RegisterGlobal(Key.F2, "Help2", "Help 2", () => { }, category: "Application");
        manager.Register(KeybindingContext.AddressSpace, Key.Enter, "Sub", "Subscribe", () => { }, category: "Navigation");

        // Act
        var groups = manager.GetAllBindingsGroupedByCategory().ToList();

        // Assert
        Assert.Equal(2, groups.Count);

        var navigationGroup = groups.First(g => g.Key == "Navigation");
        var applicationGroup = groups.First(g => g.Key == "Application");

        Assert.Single(navigationGroup);
        Assert.Equal(2, applicationGroup.Count());
    }

    #endregion

    #region GetAllBindingsGroupedByContext Tests

    [Fact]
    public void GetAllBindingsGroupedByContext_GroupsCorrectly()
    {
        // Arrange
        var manager = new KeybindingManager();
        manager.RegisterGlobal(Key.F1, "Help", "Help", () => { });
        manager.Register(KeybindingContext.AddressSpace, Key.Enter, "Sub", "Subscribe", () => { });
        manager.Register(KeybindingContext.AddressSpace, Key.F5, "Refresh", "Refresh", () => { });

        // Act
        var groups = manager.GetAllBindingsGroupedByContext().ToList();

        // Assert
        Assert.Equal(2, groups.Count);

        var globalGroup = groups.First(g => g.Key == KeybindingContext.Global);
        var addressSpaceGroup = groups.First(g => g.Key == KeybindingContext.AddressSpace);

        Assert.Single(globalGroup);
        Assert.Equal(2, addressSpaceGroup.Count());
    }

    [Fact]
    public void GetAllBindingsGroupedByContext_ExcludesEmptyContexts()
    {
        // Arrange
        var manager = new KeybindingManager();
        manager.RegisterGlobal(Key.F1, "Help", "Help", () => { });

        // Act
        var groups = manager.GetAllBindingsGroupedByContext().ToList();

        // Assert
        Assert.Single(groups);
        Assert.Equal(KeybindingContext.Global, groups[0].Key);
    }

    #endregion
}
