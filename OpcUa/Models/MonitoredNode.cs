using Opc.Ua;

namespace Opcilloscope.OpcUa.Models;

/// <summary>
/// View model for monitored variables displayed in the table.
/// </summary>
public class MonitoredNode
{
    public uint ClientHandle { get; init; }
    public uint MonitoredItemId { get; set; }
    public NodeId NodeId { get; init; } = ObjectIds.RootFolder;
    public string DisplayName { get; init; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
    public uint StatusCode { get; set; }
    public bool IsGood => StatusCode == 0; // StatusCode.Good = 0
    public bool IsUncertain => (StatusCode & 0x40000000) != 0;
    public bool IsBad => (StatusCode & 0x80000000) != 0;
    public DateTime LastChangeTime { get; set; } = DateTime.MinValue;
    public bool RecentlyChanged => (DateTime.Now - LastChangeTime).TotalMilliseconds < 500;

    /// <summary>
    /// OPC UA AccessLevel attribute - bit flags for read/write permissions.
    /// </summary>
    public byte AccessLevel { get; set; } = AccessLevels.CurrentRead;

    /// <summary>
    /// The built-in data type of the node value.
    /// </summary>
    public BuiltInType DataType { get; set; } = BuiltInType.String;

    /// <summary>
    /// Human-readable data type name for display.
    /// </summary>
    public string DataTypeName { get; set; } = "String";

    /// <summary>
    /// Whether the node supports reading (has CurrentRead in AccessLevel).
    /// </summary>
    public bool IsReadable => (AccessLevel & AccessLevels.CurrentRead) != 0;

    /// <summary>
    /// Whether the node supports writing (has CurrentWrite in AccessLevel).
    /// </summary>
    public bool IsWritable => (AccessLevel & AccessLevels.CurrentWrite) != 0;

    /// <summary>
    /// Access string for display: "R", "W", "RW", or "-".
    /// </summary>
    public string AccessString =>
        IsReadable && IsWritable ? "RW" :
        IsReadable ? "R" :
        IsWritable ? "W" : "-";
    /// <summary>
    /// Indicates if this node is selected for display in the Scope view.
    /// </summary>
    public bool IsSelectedForScope { get; set; }

    public string StatusString
    {
        get
        {
            if (StatusCode == 0) return "Good";
            if ((StatusCode & 0x80000000) != 0) return $"Bad (0x{StatusCode:X8})";
            if ((StatusCode & 0x40000000) != 0) return $"Uncertain (0x{StatusCode:X8})";
            return $"0x{StatusCode:X8}";
        }
    }

    public string TimestampString => Timestamp?.ToString("HH:mm:ss") ?? "-";
}
