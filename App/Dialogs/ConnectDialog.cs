using Terminal.Gui;
using Opcilloscope.App.Themes;
using Opcilloscope.OpcUa;
using System.Collections.ObjectModel;
using AppThemeManager = Opcilloscope.App.Themes.ThemeManager;

namespace Opcilloscope.App.Dialogs;

/// <summary>
/// Dialog for entering OPC UA server connection details.
/// Uses Terminal.Gui v2 styling with theme support.
/// Hardcodes 'opc.tcp://' prefix and handles pasted addresses intelligently.
/// </summary>
public class ConnectDialog : Dialog
{
    private const string ProtocolPrefix = "opc.tcp://";
    private readonly TextField _endpointField;
    private readonly NumericUpDown<int> _publishIntervalField;
    private readonly RadioGroup _authTypeRadio;
    private readonly Label _usernameLabel;
    private readonly TextField _usernameField;
    private readonly Label _passwordLabel;
    private readonly TextField _passwordField;
    private readonly Button _connectButton;
    private readonly Button _cancelButton;
    private bool _confirmed;
    private bool _isProcessingTextChange;

    public string EndpointUrl => ProtocolPrefix + (_endpointField.Text?.Trim() ?? string.Empty);
    public bool Confirmed => _confirmed;
    public int PublishingInterval => _publishIntervalField.Value;
    public AuthenticationType SelectedAuthType =>
        _authTypeRadio.SelectedItem == 1 ? AuthenticationType.UserName : AuthenticationType.Anonymous;
    public string? Username => SelectedAuthType == AuthenticationType.UserName
        ? _usernameField.Text?.Trim() : null;
    public string? Password => SelectedAuthType == AuthenticationType.UserName
        ? _passwordField.Text : null;

    public ConnectDialog(
        string? initialEndpoint = null,
        int publishingInterval = 250,
        AuthenticationType authType = AuthenticationType.Anonymous,
        string? username = null)
    {
        var theme = AppThemeManager.Current;

        Title = " Connect to Server ";
        Width = 60;
        Height = 18;

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
            Text = "Server Address:"
        };

        // Fixed protocol prefix label
        var protocolLabel = new Label
        {
            X = 1,
            Y = 2,
            Text = ProtocolPrefix,
            ColorScheme = theme.MainColorScheme
        };

        _endpointField = new TextField
        {
            X = 1 + ProtocolPrefix.Length,
            Y = 2,
            Width = Dim.Fill(1),
            Text = StripProtocolPrefix(initialEndpoint ?? string.Empty)
        };

        // Handle pasted addresses by stripping protocol prefixes
        _endpointField.TextChanged += OnTextChanged;

        var intervalLabel = new Label
        {
            X = 1,
            Y = 4,
            Text = "Publishing Interval (ms):"
        };

        _publishIntervalField = new NumericUpDown<int>
        {
            X = 1,
            Y = 5,
            Width = 20,
            Value = publishingInterval,
            Increment = 100
        };

        var intervalHintLabel = new Label
        {
            X = 1,
            Y = 6,
            Text = "How often the server sends data updates (100-10000)",
            ColorScheme = theme.MainColorScheme
        };

        // Authentication section
        var authLabel = new Label
        {
            X = 1,
            Y = 8,
            Text = "Authentication:"
        };

        _authTypeRadio = new RadioGroup
        {
            X = 1,
            Y = 9,
            RadioLabels = ["Anonymous", "Username/Password"],
            Orientation = Orientation.Horizontal,
            SelectedItem = authType == AuthenticationType.UserName ? 1 : 0
        };

        _usernameLabel = new Label
        {
            X = 1,
            Y = 11,
            Text = "Username:",
            Visible = authType == AuthenticationType.UserName
        };

        _usernameField = new TextField
        {
            X = 12,
            Y = 11,
            Width = Dim.Fill(1),
            Text = username ?? string.Empty,
            Visible = authType == AuthenticationType.UserName
        };

        _passwordLabel = new Label
        {
            X = 1,
            Y = 12,
            Text = "Password:",
            Visible = authType == AuthenticationType.UserName
        };

        _passwordField = new TextField
        {
            X = 12,
            Y = 12,
            Width = Dim.Fill(1),
            Secret = true,
            Visible = authType == AuthenticationType.UserName
        };

        _authTypeRadio.SelectedItemChanged += (_, _) =>
        {
            var showCredentials = _authTypeRadio.SelectedItem == 1;
            _usernameLabel.Visible = showCredentials;
            _usernameField.Visible = showCredentials;
            _passwordLabel.Visible = showCredentials;
            _passwordField.Visible = showCredentials;
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

        _connectButton = new Button
        {
            X = Pos.Center() - 10,
            Y = 14,
            Text = $"{theme.ButtonPrefix}Connect{theme.ButtonSuffix}",
            IsDefault = true,
            ColorScheme = defaultButtonScheme
        };

        _connectButton.Accepting += (_, _) =>
        {
            if (ValidateInput())
            {
                _confirmed = true;
                Application.RequestStop();
            }
        };

        _cancelButton = new Button
        {
            X = Pos.Center() + 4,
            Y = 14,
            Text = $"{theme.ButtonPrefix}Cancel{theme.ButtonSuffix}",
            ColorScheme = theme.ButtonColorScheme
        };

        _cancelButton.Accepting += (_, _) =>
        {
            _confirmed = false;
            Application.RequestStop();
        };

        Add(endpointLabel, protocolLabel, _endpointField,
            intervalLabel, _publishIntervalField, intervalHintLabel,
            authLabel, _authTypeRadio,
            _usernameLabel, _usernameField, _passwordLabel, _passwordField,
            _connectButton, _cancelButton);

        _endpointField.SetFocus();
    }

    private bool ValidateInput()
    {
        var serverAddress = _endpointField.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(serverAddress))
        {
            MessageBox.ErrorQuery("Error", "Please enter a server address", "OK");
            return false;
        }

        try
        {
            var uri = new Uri(EndpointUrl);
            if (string.IsNullOrEmpty(uri.Host))
            {
                MessageBox.ErrorQuery("Error", "Invalid host in server address", "OK");
                return false;
            }
        }
        catch
        {
            MessageBox.ErrorQuery("Error", "Invalid server address format", "OK");
            return false;
        }

        var interval = _publishIntervalField.Value;
        if (interval < 100 || interval > 10000)
        {
            MessageBox.ErrorQuery("Error", "Publishing interval must be between 100 and 10000 ms", "OK");
            return false;
        }

        if (SelectedAuthType == AuthenticationType.UserName)
        {
            var username = _usernameField.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(username))
            {
                MessageBox.ErrorQuery("Error", "Please enter a username", "OK");
                _usernameField.SetFocus();
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Handles text changes to strip protocol prefixes from pasted addresses.
    /// Supports opc.tcp://, http://, https://, and handles double-prefixing.
    /// </summary>
    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (_isProcessingTextChange)
            return;

        var text = _endpointField.Text ?? string.Empty;
        var cleaned = StripProtocolPrefix(text);

        if (cleaned != text)
        {
            _isProcessingTextChange = true;
            try
            {
                _endpointField.Text = cleaned;
                // Move cursor to end
                _endpointField.CursorPosition = cleaned.Length;
            }
            finally
            {
                _isProcessingTextChange = false;
            }
        }
    }

    /// <summary>
    /// Strips common protocol prefixes from the input string.
    /// Handles cases like "opc.tcp://localhost:4840" → "localhost:4840"
    /// and double-prefixes like "opc.tcp://opc.tcp://localhost" → "localhost"
    /// </summary>
    private static string StripProtocolPrefix(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = input;
        var changed = true;

        // Keep stripping prefixes until no more are found (handles double-paste scenarios)
        while (changed)
        {
            changed = false;
            var lower = result.ToLowerInvariant();

            if (lower.StartsWith("opc.tcp://"))
            {
                result = result.Substring(10);
                changed = true;
            }
            else if (lower.StartsWith("http://"))
            {
                result = result.Substring(7);
                changed = true;
            }
            else if (lower.StartsWith("https://"))
            {
                result = result.Substring(8);
                changed = true;
            }
            else if (lower.StartsWith("opc.https://"))
            {
                result = result.Substring(12);
                changed = true;
            }
        }

        return result;
    }
}
