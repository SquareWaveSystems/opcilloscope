using Terminal.Gui;
using OpcScope.App.Themes;
using System.Collections.ObjectModel;

namespace OpcScope.App.Dialogs;

/// <summary>
/// Dialog for entering OPC UA server connection details.
/// Uses Terminal.Gui v2 styling with theme support.
/// Features ComboBox with endpoint history for quick access to recent servers.
/// </summary>
public class ConnectDialog : Dialog
{
    private static readonly List<string> _endpointHistory = new()
    {
        "opc.tcp://localhost:4840",
        "opc.tcp://localhost:48010",
        "opc.tcp://192.168.1.1:4840"
    };
    private static readonly object _historyLock = new();

    private readonly ComboBox _endpointComboBox;
    private bool _confirmed;

    public string EndpointUrl => _endpointComboBox.Text ?? string.Empty;
    public bool Confirmed => _confirmed;

    /// <summary>
    /// Adds an endpoint URL to the history, moving it to the top if it already exists,
    /// and maintaining only the most recent 10 endpoints.
    /// </summary>
    /// <param name="endpoint">The OPC UA endpoint URL to add to the history.</param>
    public static void AddToHistory(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return;

        lock (_historyLock)
        {
            // Remove if exists (to move to top)
            _endpointHistory.Remove(endpoint);
            // Insert at top
            _endpointHistory.Insert(0, endpoint);
            // Keep only last 10
            while (_endpointHistory.Count > 10)
                _endpointHistory.RemoveAt(_endpointHistory.Count - 1);
        }
    }

    public ConnectDialog(string? lastEndpoint = null)
    {
        var theme = ThemeManager.Current;

        Title = " Connect to Server ";
        Width = 60;
        Height = 11;

        // Apply theme styling
        ColorScheme = theme.DialogColorScheme;
        BorderStyle = theme.BorderLineStyle;

        // Move last endpoint to top of history if provided
        if (!string.IsNullOrEmpty(lastEndpoint))
        {
            AddToHistory(lastEndpoint);
        }

        var endpointLabel = new Label
        {
            X = 1,
            Y = 1,
            Text = "Endpoint URL (select or type):"
        };

        // Get default text with thread safety
        string defaultText;
        List<string> historyCopy;
        lock (_historyLock)
        {
            defaultText = lastEndpoint ?? _endpointHistory.FirstOrDefault() ?? "opc.tcp://localhost:4840";
            historyCopy = new List<string>(_endpointHistory);
        }

        var historySource = new ObservableCollection<string>(historyCopy);

        _endpointComboBox = new ComboBox
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill(1),
            Height = 4,
            Text = defaultText
        };
        _endpointComboBox.SetSource(historySource);

        var hintLabel = new Label
        {
            X = 1,
            Y = 5,
            Text = "Use ↓ to see recent endpoints",
            ColorScheme = theme.MainColorScheme
        };

        var connectButton = new Button
        {
            X = Pos.Center() - 10,
            Y = 7,
            Text = $"{theme.ButtonPrefix}Connect{theme.ButtonSuffix}",
            IsDefault = true,
            ColorScheme = theme.ButtonColorScheme
        };

        connectButton.Accepting += (_, _) =>
        {
            if (ValidateEndpoint())
            {
                AddToHistory(EndpointUrl);
                _confirmed = true;
                Application.RequestStop();
            }
        };

        var cancelButton = new Button
        {
            X = Pos.Center() + 4,
            Y = 7,
            Text = $"{theme.ButtonPrefix}Cancel{theme.ButtonSuffix}",
            ColorScheme = theme.ButtonColorScheme
        };

        cancelButton.Accepting += (_, _) =>
        {
            _confirmed = false;
            Application.RequestStop();
        };

        Add(endpointLabel, _endpointComboBox, hintLabel, connectButton, cancelButton);

        _endpointComboBox.SetFocus();
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
