using Opcilloscope.App.Keybindings;
using Terminal.Gui;

namespace Opcilloscope.Tests.App.Keybindings;

public class KeybindingTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var executed = false;
        Action handler = () => executed = true;

        // Act
        var binding = new Keybinding(
            KeybindingContext.AddressSpace,
            Key.Enter,
            "Subscribe",
            "Subscribe to the selected node",
            handler,
            showInStatusBar: true,
            statusBarPriority: 50,
            category: "Navigation");

        // Assert
        Assert.Equal(KeybindingContext.AddressSpace, binding.Context);
        Assert.Equal(Key.Enter, binding.Key);
        Assert.Equal("Subscribe", binding.Label);
        Assert.Equal("Subscribe to the selected node", binding.Description);
        Assert.True(binding.ShowInStatusBar);
        Assert.Equal(50, binding.StatusBarPriority);
        Assert.Equal("Navigation", binding.Category);

        // Verify handler
        binding.Handler();
        Assert.True(executed);
    }

    [Fact]
    public void Constructor_DefaultValues()
    {
        // Act
        var binding = new Keybinding(
            KeybindingContext.Global,
            Key.F1,
            "Help",
            "Show help",
            () => { });

        // Assert
        Assert.True(binding.ShowInStatusBar);
        Assert.Equal(100, binding.StatusBarPriority);
        Assert.Equal("General", binding.Category);
    }

    #endregion

    #region Matches Tests

    [Fact]
    public void Matches_SameKey_ReturnsTrue()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            Key.F1,
            "Help",
            "Help",
            () => { });

        // Act & Assert
        Assert.True(binding.Matches(Key.F1));
    }

    [Fact]
    public void Matches_DifferentKey_ReturnsFalse()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            Key.F1,
            "Help",
            "Help",
            () => { });

        // Act & Assert
        Assert.False(binding.Matches(Key.F2));
    }

    [Fact]
    public void Matches_CharacterKey_MatchesExactly()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            (Key)'w',
            "Write",
            "Write value",
            () => { });

        // Act & Assert
        Assert.True(binding.Matches((Key)'w'));
        Assert.False(binding.Matches((Key)'W')); // Case sensitive
    }

    [Fact]
    public void Matches_ModifierKey_RequiresExactModifiers()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            Key.S.WithCtrl,
            "Save",
            "Save file",
            () => { });

        // Act & Assert
        Assert.True(binding.Matches(Key.S.WithCtrl));
        Assert.False(binding.Matches(Key.S)); // Without Ctrl
        Assert.False(binding.Matches(Key.S.WithAlt)); // Different modifier
    }

    [Fact]
    public void Matches_MultipleModifiers_RequiresAllModifiers()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            Key.S.WithCtrl.WithShift,
            "SaveAs",
            "Save file as",
            () => { });

        // Act & Assert
        Assert.True(binding.Matches(Key.S.WithCtrl.WithShift));
        Assert.False(binding.Matches(Key.S.WithCtrl)); // Missing Shift
        Assert.False(binding.Matches(Key.S.WithShift)); // Missing Ctrl
    }

    [Fact]
    public void Matches_Enter_MatchesEnterKey()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.AddressSpace,
            Key.Enter,
            "Subscribe",
            "Subscribe",
            () => { });

        // Act & Assert
        Assert.True(binding.Matches(Key.Enter));
    }

    [Fact]
    public void Matches_Space_MatchesSpaceKey()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.MonitoredVariables,
            Key.Space,
            "Select",
            "Toggle selection",
            () => { });

        // Act & Assert
        Assert.True(binding.Matches(Key.Space));
    }

    [Fact]
    public void Matches_Delete_MatchesDeleteKey()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.MonitoredVariables,
            Key.Delete,
            "Unsub",
            "Unsubscribe",
            () => { });

        // Act & Assert
        Assert.True(binding.Matches(Key.Delete));
    }

    [Fact]
    public void Matches_Tab_MatchesTabKey()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            Key.Tab,
            "Switch",
            "Switch panes",
            () => { });

        // Act & Assert
        Assert.True(binding.Matches(Key.Tab));
    }

    #endregion

    #region KeyDisplay / FormatKey Tests

    [Theory]
    [InlineData(KeyCode.F1, "F1")]
    [InlineData(KeyCode.F2, "F2")]
    [InlineData(KeyCode.F5, "F5")]
    [InlineData(KeyCode.F10, "F10")]
    [InlineData(KeyCode.F12, "F12")]
    public void KeyDisplay_FunctionKeys_FormatsCorrectly(KeyCode keyCode, string expected)
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            new Key(keyCode),
            "Test",
            "Test",
            () => { });

        // Act & Assert
        Assert.Equal(expected, binding.KeyDisplay);
    }

    [Fact]
    public void KeyDisplay_Enter_FormatsAsEnter()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            Key.Enter,
            "Test",
            "Test",
            () => { });

        // Act & Assert
        Assert.Equal("Enter", binding.KeyDisplay);
    }

    [Fact]
    public void KeyDisplay_Space_FormatsAsSpace()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            Key.Space,
            "Test",
            "Test",
            () => { });

        // Act & Assert
        Assert.Equal("Space", binding.KeyDisplay);
    }

    [Fact]
    public void KeyDisplay_Tab_FormatsAsTab()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            Key.Tab,
            "Test",
            "Test",
            () => { });

        // Act & Assert
        Assert.Equal("Tab", binding.KeyDisplay);
    }

    [Fact]
    public void KeyDisplay_Delete_FormatsAsDelete()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            Key.Delete,
            "Test",
            "Test",
            () => { });

        // Act & Assert
        Assert.Equal("Delete", binding.KeyDisplay);
    }

    [Fact]
    public void KeyDisplay_Escape_FormatsAsEsc()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            Key.Esc,
            "Test",
            "Test",
            () => { });

        // Act & Assert
        Assert.Equal("Esc", binding.KeyDisplay);
    }

    [Fact]
    public void KeyDisplay_Backspace_FormatsAsBackspace()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            Key.Backspace,
            "Test",
            "Test",
            () => { });

        // Act & Assert
        Assert.Equal("Backspace", binding.KeyDisplay);
    }

    [Fact]
    public void KeyDisplay_ArrowKeys_FormatsWithUnicodeSymbols()
    {
        // Arrange & Assert
        Assert.Equal("↑", CreateBinding(Key.CursorUp).KeyDisplay);
        Assert.Equal("↓", CreateBinding(Key.CursorDown).KeyDisplay);
        Assert.Equal("←", CreateBinding(Key.CursorLeft).KeyDisplay);
        Assert.Equal("→", CreateBinding(Key.CursorRight).KeyDisplay);
    }

    [Fact]
    public void KeyDisplay_HomeEnd_FormatsCorrectly()
    {
        // Assert
        Assert.Equal("Home", CreateBinding(Key.Home).KeyDisplay);
        Assert.Equal("End", CreateBinding(Key.End).KeyDisplay);
    }

    [Fact]
    public void KeyDisplay_PageUpDown_FormatsAsPgUpPgDn()
    {
        // Assert
        Assert.Equal("PgUp", CreateBinding(Key.PageUp).KeyDisplay);
        Assert.Equal("PgDn", CreateBinding(Key.PageDown).KeyDisplay);
    }

    [Fact]
    public void KeyDisplay_CtrlModifier_FormatsWithCtrlPrefix()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            Key.S.WithCtrl,
            "Save",
            "Save",
            () => { });

        // Act & Assert
        Assert.Equal("Ctrl+S", binding.KeyDisplay);
    }

    [Fact]
    public void KeyDisplay_ShiftModifier_FormatsWithShiftPrefix()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            Key.S.WithShift,
            "Test",
            "Test",
            () => { });

        // Act & Assert
        Assert.Equal("Shift+S", binding.KeyDisplay);
    }

    [Fact]
    public void KeyDisplay_AltModifier_FormatsWithAltPrefix()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            Key.S.WithAlt,
            "Test",
            "Test",
            () => { });

        // Act & Assert
        Assert.Equal("Alt+S", binding.KeyDisplay);
    }

    [Fact]
    public void KeyDisplay_CtrlShiftModifiers_FormatsWithBothPrefixes()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            Key.S.WithCtrl.WithShift,
            "SaveAs",
            "Save As",
            () => { });

        // Act & Assert
        Assert.Equal("Ctrl+Shift+S", binding.KeyDisplay);
    }

    [Fact]
    public void KeyDisplay_AllModifiers_FormatsWithAllPrefixes()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            Key.S.WithCtrl.WithShift.WithAlt,
            "Test",
            "Test",
            () => { });

        // Act & Assert
        Assert.Equal("Ctrl+Shift+Alt+S", binding.KeyDisplay);
    }

    [Fact]
    public void KeyDisplay_CharacterKey_FormatsAsUppercase()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            (Key)'w',
            "Write",
            "Write",
            () => { });

        // Act & Assert
        Assert.Equal("W", binding.KeyDisplay);
    }

    [Fact]
    public void KeyDisplay_PlusKey_FormatsCorrectly()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Scope,
            (Key)'+',
            "Zoom+",
            "Zoom in",
            () => { });

        // Act & Assert
        Assert.Equal("+", binding.KeyDisplay);
    }

    [Fact]
    public void KeyDisplay_MinusKey_FormatsCorrectly()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Scope,
            (Key)'-',
            "Zoom-",
            "Zoom out",
            () => { });

        // Act & Assert
        Assert.Equal("-", binding.KeyDisplay);
    }

    [Fact]
    public void KeyDisplay_QuestionMark_FormatsCorrectly()
    {
        // Arrange
        var binding = new Keybinding(
            KeybindingContext.Global,
            (Key)'?',
            "Help",
            "Quick help",
            () => { });

        // Act & Assert
        Assert.Equal("?", binding.KeyDisplay);
    }

    #endregion

    #region Helper Methods

    private static Keybinding CreateBinding(Key key)
    {
        return new Keybinding(
            KeybindingContext.Global,
            key,
            "Test",
            "Test description",
            () => { });
    }

    #endregion
}
