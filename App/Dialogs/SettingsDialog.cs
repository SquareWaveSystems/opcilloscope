using Terminal.Gui;

namespace OpcScope.App.Dialogs;

/// <summary>
/// Dialog for configuring subscription settings.
/// </summary>
public class SettingsDialog : Dialog
{
    private readonly TextField _publishIntervalField;
    private bool _confirmed;

    public bool Confirmed => _confirmed;
    public int PublishingInterval { get; private set; }

    public SettingsDialog(int currentInterval)
    {
        Title = "Subscription Settings";
        Width = 45;
        Height = 10;
        PublishingInterval = currentInterval;

        var intervalLabel = new Label
        {
            X = 1,
            Y = 1,
            Text = "Publishing Interval (ms):"
        };

        _publishIntervalField = new TextField
        {
            X = 1,
            Y = 2,
            Width = 15,
            Text = currentInterval.ToString()
        };

        var hintLabel = new Label
        {
            X = 1,
            Y = 4,
            Text = "Range: 100 - 10000 ms"
        };

        var applyButton = new Button
        {
            X = Pos.Center() - 10,
            Y = 6,
            Text = "Apply",
            IsDefault = true
        };

        applyButton.Accepting += (s, e) =>
        {
            if (ValidateSettings())
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

        cancelButton.Accepting += (s, e) =>
        {
            _confirmed = false;
            Application.RequestStop();
        };

        Add(intervalLabel, _publishIntervalField, hintLabel, applyButton, cancelButton);

        _publishIntervalField.SetFocus();
    }

    private bool ValidateSettings()
    {
        var text = _publishIntervalField.Text?.Trim() ?? "";

        if (!int.TryParse(text, out var interval))
        {
            MessageBox.ErrorQuery("Error", "Please enter a valid number", "OK");
            return false;
        }

        if (interval < 100 || interval > 10000)
        {
            MessageBox.ErrorQuery("Error", "Interval must be between 100 and 10000 ms", "OK");
            return false;
        }

        PublishingInterval = interval;
        return true;
    }
}
