using Terminal.Gui;
using Opcilloscope.App.Themes;
using ThemeManager = Opcilloscope.App.Themes.ThemeManager;

namespace Opcilloscope.App.Dialogs;

/// <summary>
/// Dialog for configuring subscription settings.
/// Uses Terminal.Gui v2 styling with theme support.
/// </summary>
public class SettingsDialog : Dialog
{
    private readonly NumericUpDown<int> _publishIntervalField;
    private bool _confirmed;

    public bool Confirmed => _confirmed;
    public int PublishingInterval { get; private set; }

    public SettingsDialog(int currentInterval)
    {
        var theme = ThemeManager.Current;

        Title = " Settings ";
        Width = 45;
        Height = 10;
        PublishingInterval = currentInterval;

        // Apply theme styling - double-line border for emphasis with grey border color
        ColorScheme = theme.DialogColorScheme;
        BorderStyle = LineStyle.Double;
        if (Border != null)
        {
            Border.ColorScheme = theme.BorderColorScheme;
        }

        var intervalLabel = new Label
        {
            X = 1,
            Y = 1,
            Text = "Publishing Interval (ms):"
        };

        _publishIntervalField = new NumericUpDown<int>
        {
            X = 1,
            Y = 2,
            Width = 20,
            Value = currentInterval,
            Increment = 100
        };

        var hintLabel = new Label
        {
            X = 1,
            Y = 4,
            Text = "Range: 100 - 10000 ms (use +/- or type)",
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

        var applyButton = new Button
        {
            X = Pos.Center() - 10,
            Y = 6,
            Text = $"{theme.ButtonPrefix}Apply{theme.ButtonSuffix}",
            IsDefault = true,
            ColorScheme = defaultButtonScheme
        };

        applyButton.Accepting += (_, _) =>
        {
            if (ValidateSettings())
            {
                _confirmed = true;
                Application.RequestStop();
            }
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

        Add(intervalLabel, _publishIntervalField, hintLabel, applyButton, cancelButton);

        _publishIntervalField.SetFocus();
    }

    private bool ValidateSettings()
    {
        var interval = _publishIntervalField.Value;

        if (interval < 100 || interval > 10000)
        {
            MessageBox.ErrorQuery("Error", "Interval must be between 100 and 10000 ms", "OK");
            return false;
        }

        PublishingInterval = interval;
        return true;
    }
}
