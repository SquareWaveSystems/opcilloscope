using Terminal.Gui;
using Opcilloscope.App.Themes;
using AppThemeManager = Opcilloscope.App.Themes.ThemeManager;

namespace Opcilloscope.App.Dialogs;

/// <summary>
/// Minimal dialog for prompting a password when loading a configuration file
/// that specifies username/password authentication.
/// </summary>
public class PasswordPromptDialog : Dialog
{
    private readonly TextField _passwordField;
    private bool _confirmed;

    public string? Password => _passwordField.Text;
    public bool Confirmed => _confirmed;

    public PasswordPromptDialog(string username, string endpoint)
    {
        var theme = AppThemeManager.Current;

        Title = " Authentication Required ";
        Width = 55;
        Height = 10;

        ColorScheme = theme.DialogColorScheme;
        BorderStyle = LineStyle.Double;
        if (Border != null)
        {
            Border.ColorScheme = theme.BorderColorScheme;
        }

        var promptLabel = new Label
        {
            X = 1,
            Y = 1,
            Text = $"Enter password for '{username}' on:",
        };

        var endpointLabel = new Label
        {
            X = 1,
            Y = 2,
            Text = endpoint.Length > 50 ? endpoint[..47] + "..." : endpoint,
            ColorScheme = theme.MainColorScheme
        };

        _passwordField = new TextField
        {
            X = 1,
            Y = 4,
            Width = Dim.Fill(1),
            Secret = true
        };

        var defaultButtonScheme = new ColorScheme
        {
            Normal = new Terminal.Gui.Attribute(theme.Accent, theme.Background),
            Focus = new Terminal.Gui.Attribute(theme.AccentBright, theme.Background),
            HotNormal = new Terminal.Gui.Attribute(theme.Accent, theme.Background),
            HotFocus = new Terminal.Gui.Attribute(theme.AccentBright, theme.Background),
            Disabled = new Terminal.Gui.Attribute(theme.MutedText, theme.Background)
        };

        var okButton = new Button
        {
            X = Pos.Center() - 8,
            Y = 6,
            Text = $"{theme.ButtonPrefix}OK{theme.ButtonSuffix}",
            IsDefault = true,
            ColorScheme = defaultButtonScheme
        };

        okButton.Accepting += (_, _) =>
        {
            _confirmed = true;
            Application.RequestStop();
        };

        var cancelButton = new Button
        {
            X = Pos.Center() + 4,
            Y = 6,
            Text = $"{theme.ButtonPrefix}Cancel{theme.ButtonSuffix}",
            ColorScheme = theme.ButtonColorScheme
        };

        cancelButton.Accepting += (_, _) =>
        {
            _confirmed = false;
            Application.RequestStop();
        };

        Add(promptLabel, endpointLabel, _passwordField, okButton, cancelButton);

        _passwordField.SetFocus();
    }
}
