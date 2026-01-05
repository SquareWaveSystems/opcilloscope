using Terminal.Gui;
using OpcScope.App.Themes;

namespace OpcScope.App.Dialogs;

/// <summary>
/// Dialog for writing a value to an OPC UA node.
/// Uses Terminal.Gui v2 styling with theme support.
/// </summary>
public class WriteValueDialog : Dialog
{
    private readonly TextField _valueField;
    private readonly string _dataType;
    private bool _confirmed;
    private object? _parsedValue;

    public bool Confirmed => _confirmed;
    public object? ParsedValue => _parsedValue;

    public WriteValueDialog(string nodeName, string? dataType, string? currentValue)
    {
        var theme = ThemeManager.Current;

        Title = " Write Value ";
        Width = 50;
        Height = 12;
        _dataType = dataType ?? "String";

        // Apply theme styling
        ColorScheme = theme.DialogColorScheme;
        BorderStyle = theme.BorderLineStyle;

        var nodeLabel = new Label
        {
            X = 1,
            Y = 1,
            Text = $"Node: {nodeName}"
        };

        var typeLabel = new Label
        {
            X = 1,
            Y = 2,
            Text = $"Type: {_dataType}"
        };

        var currentLabel = new Label
        {
            X = 1,
            Y = 3,
            Text = $"Current: {currentValue ?? "N/A"}"
        };

        var valueLabel = new Label
        {
            X = 1,
            Y = 5,
            Text = "New Value:"
        };

        _valueField = new TextField
        {
            X = 1,
            Y = 6,
            Width = Dim.Fill(1),
            Text = currentValue ?? ""
        };

        var writeButton = new Button
        {
            X = Pos.Center() - 10,
            Y = 8,
            Text = $"{theme.ButtonPrefix}Write{theme.ButtonSuffix}",
            IsDefault = true,
            ColorScheme = theme.ButtonColorScheme
        };

        writeButton.Accepting += (_, _) =>
        {
            if (ValidateAndParse())
            {
                var result = MessageBox.Query(
                    "Confirm Write",
                    $"Write '{_valueField.Text}' to {nodeName}?",
                    "Yes", "No"
                );

                if (result == 0)
                {
                    _confirmed = true;
                    Application.RequestStop();
                }
            }
        };

        var cancelButton = new Button
        {
            X = Pos.Center() + 4,
            Y = 8,
            Text = $"{theme.ButtonPrefix}Cancel{theme.ButtonSuffix}",
            ColorScheme = theme.ButtonColorScheme
        };

        cancelButton.Accepting += (_, _) =>
        {
            _confirmed = false;
            Application.RequestStop();
        };

        Add(nodeLabel, typeLabel, currentLabel, valueLabel, _valueField, writeButton, cancelButton);

        _valueField.SetFocus();
    }

    private bool ValidateAndParse()
    {
        var text = _valueField.Text?.Trim() ?? "";

        try
        {
            _parsedValue = _dataType.ToLower() switch
            {
                "boolean" or "bool" => bool.Parse(text),
                "sbyte" => sbyte.Parse(text),
                "byte" => byte.Parse(text),
                "int16" => short.Parse(text),
                "uint16" => ushort.Parse(text),
                "int32" => int.Parse(text),
                "uint32" => uint.Parse(text),
                "int64" => long.Parse(text),
                "uint64" => ulong.Parse(text),
                "float" => float.Parse(text),
                "double" => double.Parse(text),
                "string" => text,
                "datetime" => DateTime.Parse(text),
                _ => text // Default to string
            };

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Parse Error", $"Cannot parse '{text}' as {_dataType}: {ex.Message}", "OK");
            return false;
        }
    }
}
