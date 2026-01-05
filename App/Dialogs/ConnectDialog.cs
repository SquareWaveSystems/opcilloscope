using Terminal.Gui;

namespace OpcScope.App.Dialogs;

/// <summary>
/// Dialog for entering OPC UA server connection details.
/// </summary>
public class ConnectDialog : Dialog
{
    private readonly TextField _endpointField;
    private bool _confirmed;

    public string EndpointUrl => _endpointField.Text?.ToString() ?? string.Empty;
    public bool Confirmed => _confirmed;

    public ConnectDialog(string? lastEndpoint = null)
    {
        Title = "Connect to OPC UA Server";
        Width = 60;
        Height = 10;

        var endpointLabel = new Label
        {
            X = 1,
            Y = 1,
            Text = "Endpoint URL:"
        };

        _endpointField = new TextField
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill(1),
            Text = lastEndpoint ?? "opc.tcp://localhost:4840"
        };

        var hintLabel = new Label
        {
            X = 1,
            Y = 4,
            Text = "Example: opc.tcp://192.168.1.50:4840",
            ColorScheme = new ColorScheme
            {
                Normal = new Attribute(Color.Gray, Color.Black)
            }
        };

        var connectButton = new Button
        {
            X = Pos.Center() - 10,
            Y = 6,
            Text = "Connect",
            IsDefault = true
        };

        connectButton.Accept += (s, e) =>
        {
            if (ValidateEndpoint())
            {
                _confirmed = true;
                Application.RequestStop();
            }
        };

        var cancelButton = new Button
        {
            X = Pos.Center() + 2,
            Y = 6,
            Text = "Cancel"
        };

        cancelButton.Accept += (s, e) =>
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
