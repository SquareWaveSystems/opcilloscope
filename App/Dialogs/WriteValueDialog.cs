using Terminal.Gui;
using Opc.Ua;
using OpcScope.App.Themes;
using OpcScope.Utilities;
using AppThemeManager = OpcScope.App.Themes.ThemeManager;

namespace OpcScope.App.Dialogs;

/// <summary>
/// Dialog for writing a value to an OPC UA node.
/// Features real-time validation, NodeId display, and theme support.
/// </summary>
public class WriteValueDialog : Dialog
{
    private readonly TextField _valueField;
    private readonly Label _errorLabel;
    private readonly BuiltInType _dataType;
    private bool _confirmed;
    private object? _parsedValue;

    public bool Confirmed => _confirmed;
    public object? ParsedValue => _parsedValue;

    public WriteValueDialog(NodeId nodeId, string nodeName, BuiltInType dataType, string dataTypeName, string? currentValue)
    {
        var theme = AppThemeManager.Current;
        _dataType = dataType;

        Title = " Write Value ";
        Width = 60;
        Height = 14;

        // Apply theme styling
        ColorScheme = theme.DialogColorScheme;
        BorderStyle = theme.BorderLineStyle;

        // Node information section (read-only)
        var nodeIdLabel = new Label
        {
            X = 1,
            Y = 1,
            Text = "NodeId:"
        };

        var nodeIdValue = new Label
        {
            X = Pos.Right(nodeIdLabel) + 1,
            Y = Pos.Top(nodeIdLabel),
            Width = Dim.Fill()! - 1,
            Text = nodeId.ToString()
        };

        var nameLabel = new Label
        {
            X = 1,
            Y = Pos.Bottom(nodeIdLabel),
            Text = "Name:"
        };

        var nameValue = new Label
        {
            X = Pos.Right(nameLabel) + 3,
            Y = Pos.Top(nameLabel),
            Width = Dim.Fill()! - 1,
            Text = nodeName
        };

        var typeLabel = new Label
        {
            X = 1,
            Y = Pos.Bottom(nameLabel),
            Text = "Type:"
        };

        var typeValue = new Label
        {
            X = Pos.Right(typeLabel) + 3,
            Y = Pos.Top(typeLabel),
            Text = dataTypeName
        };

        var currentLabel = new Label
        {
            X = 1,
            Y = Pos.Bottom(typeLabel),
            Text = "Current:"
        };

        var currentValueLabel = new Label
        {
            X = Pos.Right(currentLabel) + 1,
            Y = Pos.Top(currentLabel),
            Width = Dim.Fill()! - 1,
            Text = currentValue ?? "(null)"
        };

        // Input section
        var newValueLabel = new Label
        {
            X = 1,
            Y = Pos.Bottom(currentLabel) + 1,
            Text = $"New Value ({OpcValueConverter.GetInputHint(dataType)}):"
        };

        _valueField = new TextField
        {
            X = 1,
            Y = Pos.Bottom(newValueLabel),
            Width = Dim.Fill()! - 1,
            Text = currentValue ?? ""
        };

        // Error display label
        _errorLabel = new Label
        {
            X = 1,
            Y = Pos.Bottom(_valueField),
            Width = Dim.Fill()! - 1,
            Text = "",
            ColorScheme = new ColorScheme
            {
                Normal = new Terminal.Gui.Attribute(Color.Red, theme.Background)
            }
        };

        // Real-time validation on text change
        _valueField.TextChanged += (_, _) => ValidateInput();

        // Buttons using Dialog's AddButton pattern
        var writeButton = new Button
        {
            Text = $"{theme.ButtonPrefix}Write{theme.ButtonSuffix}",
            IsDefault = true,
            ColorScheme = theme.ButtonColorScheme
        };

        var cancelButton = new Button
        {
            Text = $"{theme.ButtonPrefix}Cancel{theme.ButtonSuffix}",
            ColorScheme = theme.ButtonColorScheme
        };

        writeButton.Accepting += (_, _) =>
        {
            if (ValidateAndParse())
            {
                _confirmed = true;
                Application.RequestStop();
            }
        };

        cancelButton.Accepting += (_, _) =>
        {
            _confirmed = false;
            Application.RequestStop();
        };

        // Add all controls
        Add(nodeIdLabel, nodeIdValue);
        Add(nameLabel, nameValue);
        Add(typeLabel, typeValue);
        Add(currentLabel, currentValueLabel);
        Add(newValueLabel, _valueField);
        Add(_errorLabel);
        AddButton(cancelButton);
        AddButton(writeButton);

        _valueField.SetFocus();

        // Initial validation
        ValidateInput();
    }

    private void ValidateInput()
    {
        var text = _valueField.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(text))
        {
            _errorLabel.Text = "";
            return;
        }

        var (success, _, error) = OpcValueConverter.TryConvert(text, _dataType);

        if (!success)
        {
            _errorLabel.Text = error ?? "Invalid value";
        }
        else
        {
            _errorLabel.Text = "";
        }
    }

    private bool ValidateAndParse()
    {
        var text = _valueField.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(text))
        {
            _errorLabel.Text = "Value cannot be empty";
            return false;
        }

        // Check if write is supported for this data type
        if (!OpcValueConverter.IsWriteSupported(_dataType))
        {
            MessageBox.ErrorQuery("Write Error", $"Write not supported for data type: {_dataType}", "OK");
            return false;
        }

        var (success, value, error) = OpcValueConverter.TryConvert(text, _dataType);

        if (!success)
        {
            _errorLabel.Text = error ?? "Invalid value";
            return false;
        }

        _parsedValue = value;
        return true;
    }
}
