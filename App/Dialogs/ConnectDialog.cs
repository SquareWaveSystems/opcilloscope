using Terminal.Gui;
using OpcScope.App.Themes;
using AppThemeManager = OpcScope.App.Themes.ThemeManager;

namespace OpcScope.App.Dialogs;

/// <summary>
/// Dialog for entering OPC UA server connection details.
/// Uses Terminal.Gui v2 styling with theme support.
/// </summary>
public class ConnectDialog : Dialog
{
    private readonly TextField _endpointField;
    private bool _confirmed;

    public string EndpointUrl => _endpointField.Text ?? string.Empty;
    public bool Confirmed => _confirmed;

    public ConnectDialog(string? lastEndpoint = null)
    {
        var theme = AppThemeManager.Current;

        Title = " Connect to Server ";
        Width = 60;
        Height = 9;

        // Apply theme styling - double-line border for emphasis with grey border color
        ColorScheme = theme.DialogColorScheme;
        BorderStyle = LineStyle.Double;
        if (Border != null)
        {
            Border.ColorScheme = theme.BorderColorScheme;
        }

        var endpointLabel = new Label
        {
            X = 1,
            Y = 1,
            Text = "Endpoint URL:"
        };

        string defaultText = lastEndpoint ?? "opc.tcp://localhost:4840";

        _endpointField = new TextField
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill(1),
            Text = defaultText
        };

        var hintLabel = new Label
        {
            X = 1,
            Y = 3,
            Text = "Example: opc.tcp://server:4840/path",
            ColorScheme = theme.MainColorScheme
        };

        // Default button highlighted with amber
        var defaultButtonScheme = new ColorScheme
        {
            Normal = new Terminal.Gui.Attribute(theme.Accent, theme.Background),
            Focus = new Terminal.Gui.Attribute(theme.AccentBright, theme.Background),
            HotNormal = new Terminal.Gui.Attribute(theme.Accent, theme.Background),
            HotFocus = new Terminal.Gui.Attribute(theme.AccentBright, theme.Background),
            Disabled = new Terminal.Gui.Attribute(theme.MutedText, theme.Background)
        };

        var connectButton = new Button
        {
            X = Pos.Center() - 10,
            Y = 5,
            Text = $"{theme.ButtonPrefix}Connect{theme.ButtonSuffix}",
            IsDefault = true,
            ColorScheme = defaultButtonScheme
        };

        connectButton.Accepting += (_, _) =>
        {
            if (ValidateEndpoint())
            {
                _confirmed = true;
                Application.RequestStop();
            }
        };

        var cancelButton = new Button
        {
            X = Pos.Center() + 4,
            Y = 5,
            Text = $"{theme.ButtonPrefix}Cancel{theme.ButtonSuffix}",
            ColorScheme = theme.ButtonColorScheme
        };

        cancelButton.Accepting += (_, _) =>
        {
            _confirmed = false;
            Application.RequestStop();
        };

        Add(endpointLabel, _endpointField, hintLabel, connectButton, cancelButton);

        _endpointField.SetFocus();
    }

    private bool ValidateEndpoint()
    {
        var url = EndpointUrl.Trim();

        if (string.IsNullOrEmpty(url))
        {
            MessageBox.ErrorQuery("Error", "Please enter an endpoint URL", "OK");
            return false;
        }

        if (!url.StartsWith("opc.tcp://", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.ErrorQuery("Error", "Endpoint must start with 'opc.tcp://'", "OK");
            return false;
        }

        try
        {
            var uri = new Uri(url);
            if (string.IsNullOrEmpty(uri.Host))
            {
                MessageBox.ErrorQuery("Error", "Invalid host in endpoint URL", "OK");
                return false;
            }
        }
        catch
        {
            MessageBox.ErrorQuery("Error", "Invalid endpoint URL format", "OK");
            return false;
        }

        return true;
    }
}
