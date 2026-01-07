using System.Text;
using Opc.Ua;

namespace Opcilloscope.Utilities;

/// <summary>
/// Formats OPC UA node attributes into human-readable text for clipboard copy.
/// </summary>
public static class NodeAttributeFormatter
{
    /// <summary>
    /// Formats a dictionary of node attributes into a structured, human-readable string.
    /// </summary>
    public static string Format(Dictionary<string, object?> attributes)
    {
        var sb = new StringBuilder();

        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine("         OPC UA Node Attributes");
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine();

        // Identity section (always present)
        sb.AppendLine("── Identity ──");
        AppendAttribute(sb, "NodeId", attributes);
        AppendAttribute(sb, "NodeClass", attributes);
        AppendAttribute(sb, "BrowseName", attributes);
        AppendAttribute(sb, "DisplayName", attributes);
        AppendAttribute(sb, "Description", attributes);
        sb.AppendLine();

        // Access section
        if (HasAnyKey(attributes, "WriteMask", "UserWriteMask", "AccessLevel", "UserAccessLevel", "AccessLevelEx", "EventNotifier"))
        {
            sb.AppendLine("── Access ──");
            AppendAttribute(sb, "WriteMask", attributes, FormatWriteMask);
            AppendAttribute(sb, "UserWriteMask", attributes, FormatWriteMask);
            AppendAttribute(sb, "AccessLevel", attributes, FormatAccessLevel);
            AppendAttribute(sb, "UserAccessLevel", attributes, FormatAccessLevel);
            AppendAttribute(sb, "AccessLevelEx", attributes, FormatAccessLevelEx);
            AppendAttribute(sb, "EventNotifier", attributes, FormatEventNotifier);
            sb.AppendLine();
        }

        // Value section (Variables only)
        if (HasAnyKey(attributes, "Value", "DataType", "ValueRank", "ArrayDimensions", "MinimumSamplingInterval", "Historizing"))
        {
            sb.AppendLine("── Value ──");
            AppendAttribute(sb, "Value", attributes, FormatValue);
            AppendAttribute(sb, "DataType", attributes);
            AppendAttribute(sb, "ValueRank", attributes, FormatValueRank);
            AppendAttribute(sb, "ArrayDimensions", attributes, FormatArrayDimensions);
            AppendAttribute(sb, "MinimumSamplingInterval", attributes, v => $"{v} ms");
            AppendAttribute(sb, "Historizing", attributes);
            sb.AppendLine();
        }

        // Type section (ObjectType, VariableType, DataType, ReferenceType)
        if (HasAnyKey(attributes, "IsAbstract", "Symmetric", "InverseName", "DataTypeDefinition", "ContainsNoLoops"))
        {
            sb.AppendLine("── Type ──");
            AppendAttribute(sb, "IsAbstract", attributes);
            AppendAttribute(sb, "Symmetric", attributes);
            AppendAttribute(sb, "InverseName", attributes);
            AppendAttribute(sb, "ContainsNoLoops", attributes);
            AppendAttribute(sb, "DataTypeDefinition", attributes);
            sb.AppendLine();
        }

        // Method section
        if (HasAnyKey(attributes, "Executable", "UserExecutable"))
        {
            sb.AppendLine("── Method ──");
            AppendAttribute(sb, "Executable", attributes);
            AppendAttribute(sb, "UserExecutable", attributes);
            sb.AppendLine();
        }

        // Permissions section (optional attributes)
        if (HasAnyKey(attributes, "RolePermissions", "UserRolePermissions", "AccessRestrictions"))
        {
            sb.AppendLine("── Permissions ──");
            AppendAttribute(sb, "RolePermissions", attributes);
            AppendAttribute(sb, "UserRolePermissions", attributes);
            AppendAttribute(sb, "AccessRestrictions", attributes, FormatAccessRestrictions);
            sb.AppendLine();
        }

        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine($"Copied: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        return sb.ToString();
    }

    private static bool HasAnyKey(Dictionary<string, object?> attributes, params string[] keys)
    {
        return keys.Any(k => attributes.ContainsKey(k) && attributes[k] != null);
    }

    private static void AppendAttribute(StringBuilder sb, string name, Dictionary<string, object?> attributes, Func<object, string>? formatter = null)
    {
        if (!attributes.TryGetValue(name, out var value) || value == null)
            return;

        var formattedValue = formatter != null ? formatter(value) : FormatDefault(value);
        sb.AppendLine($"{name,-24} {formattedValue}");
    }

    private static string FormatDefault(object value)
    {
        return value switch
        {
            LocalizedText lt => lt.Text ?? "(empty)",
            QualifiedName qn => qn.ToString(),
            NodeId nodeId => nodeId.ToString(),
            _ => value.ToString() ?? "(null)"
        };
    }

    private static string FormatValue(object value)
    {
        if (value is DataValue dv)
        {
            if (StatusCode.IsBad(dv.StatusCode))
                return $"(bad: 0x{dv.StatusCode.Code:X8})";
            value = dv.Value;
        }

        return value switch
        {
            null => "(null)",
            string s => $"\"{s}\"",
            byte[] bytes => $"[{bytes.Length} bytes]",
            Array arr => FormatArray(arr),
            _ => value.ToString() ?? "(null)"
        };
    }

    private static string FormatArray(Array arr)
    {
        if (arr.Length == 0)
            return "[]";
        if (arr.Length <= 5)
        {
            var elements = new List<string>();
            foreach (var item in arr)
                elements.Add(item?.ToString() ?? "null");
            return $"[{string.Join(", ", elements)}]";
        }
        return $"[{arr.Length} elements]";
    }

    private static string FormatAccessLevel(object value)
    {
        var level = Convert.ToByte(value);
        var flags = new List<string>();

        if ((level & 0x01) != 0) flags.Add("Read");
        if ((level & 0x02) != 0) flags.Add("Write");
        if ((level & 0x04) != 0) flags.Add("HistoryRead");
        if ((level & 0x08) != 0) flags.Add("HistoryWrite");
        if ((level & 0x10) != 0) flags.Add("SemanticChange");
        if ((level & 0x20) != 0) flags.Add("StatusWrite");
        if ((level & 0x40) != 0) flags.Add("TimestampWrite");

        var result = flags.Count > 0 ? string.Join(" | ", flags) : "None";
        return $"{result} (0x{level:X2})";
    }

    private static string FormatAccessLevelEx(object value)
    {
        var level = Convert.ToUInt32(value);
        var flags = new List<string>();

        // Lower byte is same as AccessLevel
        if ((level & 0x01) != 0) flags.Add("Read");
        if ((level & 0x02) != 0) flags.Add("Write");
        if ((level & 0x04) != 0) flags.Add("HistoryRead");
        if ((level & 0x08) != 0) flags.Add("HistoryWrite");
        if ((level & 0x10) != 0) flags.Add("SemanticChange");
        if ((level & 0x20) != 0) flags.Add("StatusWrite");
        if ((level & 0x40) != 0) flags.Add("TimestampWrite");

        // Extended flags
        if ((level & 0x100) != 0) flags.Add("NonatomicRead");
        if ((level & 0x200) != 0) flags.Add("NonatomicWrite");
        if ((level & 0x400) != 0) flags.Add("WriteFullArrayOnly");

        var result = flags.Count > 0 ? string.Join(" | ", flags) : "None";
        return $"{result} (0x{level:X8})";
    }

    private static string FormatWriteMask(object value)
    {
        var mask = Convert.ToUInt32(value);
        if (mask == 0)
            return "None (0)";

        var flags = new List<string>();

        if ((mask & 0x01) != 0) flags.Add("AccessLevel");
        if ((mask & 0x02) != 0) flags.Add("ArrayDimensions");
        if ((mask & 0x04) != 0) flags.Add("BrowseName");
        if ((mask & 0x08) != 0) flags.Add("ContainsNoLoops");
        if ((mask & 0x10) != 0) flags.Add("DataType");
        if ((mask & 0x20) != 0) flags.Add("Description");
        if ((mask & 0x40) != 0) flags.Add("DisplayName");
        if ((mask & 0x80) != 0) flags.Add("EventNotifier");
        if ((mask & 0x100) != 0) flags.Add("Executable");
        if ((mask & 0x200) != 0) flags.Add("Historizing");
        if ((mask & 0x400) != 0) flags.Add("InverseName");
        if ((mask & 0x800) != 0) flags.Add("IsAbstract");
        if ((mask & 0x1000) != 0) flags.Add("MinimumSamplingInterval");
        if ((mask & 0x2000) != 0) flags.Add("NodeClass");
        if ((mask & 0x4000) != 0) flags.Add("NodeId");
        if ((mask & 0x8000) != 0) flags.Add("Symmetric");
        if ((mask & 0x10000) != 0) flags.Add("UserAccessLevel");
        if ((mask & 0x20000) != 0) flags.Add("UserExecutable");
        if ((mask & 0x40000) != 0) flags.Add("UserWriteMask");
        if ((mask & 0x80000) != 0) flags.Add("ValueRank");
        if ((mask & 0x100000) != 0) flags.Add("WriteMask");
        if ((mask & 0x200000) != 0) flags.Add("ValueForVariableType");

        return $"{string.Join(" | ", flags)} (0x{mask:X})";
    }

    private static string FormatEventNotifier(object value)
    {
        var notifier = Convert.ToByte(value);
        var flags = new List<string>();

        if ((notifier & 0x01) != 0) flags.Add("SubscribeToEvents");
        if ((notifier & 0x04) != 0) flags.Add("HistoryRead");
        if ((notifier & 0x08) != 0) flags.Add("HistoryWrite");

        var result = flags.Count > 0 ? string.Join(" | ", flags) : "None";
        return $"{result} (0x{notifier:X2})";
    }

    private static string FormatValueRank(object value)
    {
        var rank = Convert.ToInt32(value);
        return rank switch
        {
            -3 => "ScalarOrOneDimension (-3)",
            -2 => "Any (-2)",
            -1 => "Scalar (-1)",
            0 => "OneOrMoreDimensions (0)",
            1 => "OneDimension (1)",
            _ => $"{rank}D Array ({rank})"
        };
    }

    private static string FormatArrayDimensions(object value)
    {
        if (value is uint[] dims)
        {
            if (dims.Length == 0)
                return "(none)";
            return $"[{string.Join(", ", dims)}]";
        }
        return value?.ToString() ?? "(none)";
    }

    private static string FormatAccessRestrictions(object value)
    {
        var restrictions = Convert.ToUInt16(value);
        var flags = new List<string>();

        if ((restrictions & 0x01) != 0) flags.Add("SigningRequired");
        if ((restrictions & 0x02) != 0) flags.Add("EncryptionRequired");
        if ((restrictions & 0x04) != 0) flags.Add("SessionRequired");
        if ((restrictions & 0x08) != 0) flags.Add("ApplyRestrictionsToBrowse");

        var result = flags.Count > 0 ? string.Join(" | ", flags) : "None";
        return $"{result} (0x{restrictions:X4})";
    }
}
