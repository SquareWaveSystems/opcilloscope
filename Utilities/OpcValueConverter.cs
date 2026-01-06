using Opc.Ua;

namespace OpcScope.Utilities;

/// <summary>
/// Utility class for converting string input to OPC UA data types.
/// </summary>
public static class OpcValueConverter
{
    /// <summary>
    /// Attempts to convert a string input to the specified OPC UA BuiltInType.
    /// </summary>
    /// <param name="input">The string input to convert.</param>
    /// <param name="dataType">The target BuiltInType.</param>
    /// <returns>A tuple containing success status, the converted value (if successful), and an error message (if failed).</returns>
    /// <remarks>
    /// Empty strings are rejected for all types except String and Variant.
    /// </remarks>
    public static (bool Success, object? Value, string? Error) TryConvert(string input, BuiltInType dataType)
    {
        if (string.IsNullOrEmpty(input) && dataType is not BuiltInType.String and not BuiltInType.Variant)
        {
            return (false, null, "Value cannot be empty");
        }

        return dataType switch
        {
            BuiltInType.Boolean => TryParseBoolean(input),
            BuiltInType.SByte => TryParse<sbyte>(input, sbyte.TryParse, "Enter a number from -128 to 127"),
            BuiltInType.Byte => TryParse<byte>(input, byte.TryParse, "Enter a number from 0 to 255"),
            BuiltInType.Int16 => TryParse<short>(input, short.TryParse, "Enter a number from -32768 to 32767"),
            BuiltInType.UInt16 => TryParse<ushort>(input, ushort.TryParse, "Enter a number from 0 to 65535"),
            BuiltInType.Int32 => TryParse<int>(input, int.TryParse, "Enter a valid 32-bit integer"),
            BuiltInType.UInt32 => TryParse<uint>(input, uint.TryParse, "Enter a valid unsigned 32-bit integer"),
            BuiltInType.Int64 => TryParse<long>(input, long.TryParse, "Enter a valid 64-bit integer"),
            BuiltInType.UInt64 => TryParse<ulong>(input, ulong.TryParse, "Enter a valid unsigned 64-bit integer"),
            BuiltInType.Float => TryParseFloat(input),
            BuiltInType.Double => TryParseDouble(input),
            BuiltInType.String => (true, input ?? "", null),
            BuiltInType.DateTime => TryParseDateTime(input),
            BuiltInType.Guid => TryParseGuid(input),
            BuiltInType.Variant => (true, new Variant(input ?? ""), null),
            _ => (false, null, $"Unsupported data type: {dataType}")
        };
    }

    private static (bool, object?, string?) TryParseBoolean(string input)
    {
        var normalized = input.Trim().ToLowerInvariant();
        return normalized switch
        {
            "true" or "1" or "yes" or "on" => (true, true, null),
            "false" or "0" or "no" or "off" => (true, false, null),
            _ => (false, null, "Enter true/false, 1/0, yes/no, or on/off")
        };
    }

    private delegate bool TryParseDelegate<T>(string s, out T result);

    private static (bool, object?, string?) TryParse<T>(string input, TryParseDelegate<T> parser, string errorMessage)
        where T : struct
    {
        if (parser(input.Trim(), out var result))
        {
            return (true, result, null);
        }
        return (false, null, errorMessage);
    }

    /// <summary>
    /// Parses a Float value. Rejects NaN and Infinity values for safety.
    /// </summary>
    /// <remarks>
    /// Note: float.TryParse can successfully parse "NaN", "Infinity", and "-Infinity" 
    /// into their respective special floating-point values. These are explicitly rejected 
    /// to prevent unintended writes of special values to OPC UA nodes.
    /// </remarks>
    private static (bool, object?, string?) TryParseFloat(string input)
    {
        if (float.TryParse(input.Trim(), out var result))
        {
            if (float.IsNaN(result) || float.IsInfinity(result))
            {
                return (false, null, "Value out of range for Float");
            }
            return (true, result, null);
        }
        return (false, null, "Enter a valid decimal number");
    }

    /// <summary>
    /// Parses a Double value. Rejects NaN and Infinity values for safety.
    /// </summary>
    /// <remarks>
    /// Note: double.TryParse can successfully parse "NaN", "Infinity", and "-Infinity" 
    /// into their respective special floating-point values. These are explicitly rejected 
    /// to prevent unintended writes of special values to OPC UA nodes.
    /// </remarks>
    private static (bool, object?, string?) TryParseDouble(string input)
    {
        if (double.TryParse(input.Trim(), out var result))
        {
            if (double.IsNaN(result) || double.IsInfinity(result))
            {
                return (false, null, "Value out of range for Double");
            }
            return (true, result, null);
        }
        return (false, null, "Enter a valid decimal number");
    }

    private static (bool, object?, string?) TryParseDateTime(string input)
    {
        if (DateTime.TryParse(input.Trim(), out var result))
        {
            return (true, result, null);
        }
        return (false, null, "Enter a valid date/time (e.g., 2026-01-06 12:00:00)");
    }

    private static (bool, object?, string?) TryParseGuid(string input)
    {
        if (Guid.TryParse(input.Trim(), out var result))
        {
            return (true, result, null);
        }
        return (false, null, "Enter a valid GUID (e.g., 12345678-1234-1234-1234-123456789012)");
    }

    /// <summary>
    /// Gets a user-friendly description of acceptable input formats for a data type.
    /// </summary>
    public static string GetInputHint(BuiltInType dataType)
    {
        return dataType switch
        {
            BuiltInType.Boolean => "true/false, 1/0, yes/no",
            BuiltInType.SByte => "-128 to 127",
            BuiltInType.Byte => "0 to 255",
            BuiltInType.Int16 => "-32768 to 32767",
            BuiltInType.UInt16 => "0 to 65535",
            BuiltInType.Int32 => "Integer",
            BuiltInType.UInt32 => "Positive integer",
            BuiltInType.Int64 => "Large integer",
            BuiltInType.UInt64 => "Large positive integer",
            BuiltInType.Float => "Decimal number",
            BuiltInType.Double => "Decimal number",
            BuiltInType.String => "Text",
            BuiltInType.DateTime => "Date/time",
            BuiltInType.Guid => "GUID",
            _ => "Value"
        };
    }

    /// <summary>
    /// Checks if the data type is supported for writing.
    /// </summary>
    public static bool IsWriteSupported(BuiltInType dataType)
    {
        return dataType switch
        {
            BuiltInType.Boolean => true,
            BuiltInType.SByte => true,
            BuiltInType.Byte => true,
            BuiltInType.Int16 => true,
            BuiltInType.UInt16 => true,
            BuiltInType.Int32 => true,
            BuiltInType.UInt32 => true,
            BuiltInType.Int64 => true,
            BuiltInType.UInt64 => true,
            BuiltInType.Float => true,
            BuiltInType.Double => true,
            BuiltInType.String => true,
            BuiltInType.DateTime => true,
            BuiltInType.Guid => true,
            _ => false
        };
    }
}
