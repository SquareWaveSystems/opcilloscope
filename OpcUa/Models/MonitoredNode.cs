using Opc.Ua;

namespace Opcilloscope.OpcUa.Models;

/// <summary>
/// View model for monitored variables displayed in the table.
/// </summary>
public class MonitoredNode
{
    private long _connectionGeneration;

    public uint ClientHandle { get; init; }
    /// <summary>
    /// Connection lifecycle generation currently owning this monitored node.
    /// Retained nodes advance with a successful reconnect; client handles may be
    /// reused by a later generation.
    /// </summary>
    public long ConnectionGeneration
    {
        get => Volatile.Read(ref _connectionGeneration);
        internal set => Volatile.Write(ref _connectionGeneration, value);
    }
    public NodeId NodeId { get; init; } = ObjectIds.RootFolder;
    public string DisplayName { get; init; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Full-precision, culture-invariant representation of the last value,
    /// captured at the same point the display <see cref="Value"/> is set
    /// (see <c>SubscriptionManager.FormatRawValue</c>). Used for CSV recording
    /// so exported data is lossless and locale-independent, while
    /// <see cref="Value"/> remains a culture-aware display string ("F2" for
    /// floating point). Arrays are serialized as semicolon-joined elements.
    /// </summary>
    public string RawValue { get; set; } = string.Empty;

    public DateTime? Timestamp { get; set; }
    public uint StatusCode { get; set; }
    // OPC UA status severity lives in the top two bits: 00 = Good, 01 = Uncertain,
    // 10 = Bad. Good codes with info bits set (e.g. GoodClamped 0x00300000) are
    // still Good, so testing for == 0 would misclassify them.
    public bool IsGood => (StatusCode & 0xC0000000) == 0;
    public bool IsUncertain => (StatusCode & 0xC0000000) == 0x40000000;
    public bool IsBad => (StatusCode & 0x80000000) != 0;
    public DateTime LastChangeTime { get; set; } = DateTime.MinValue;
    public bool RecentlyChanged => (DateTime.Now - LastChangeTime).TotalMilliseconds < 500;

    /// <summary>
    /// OPC UA AccessLevel attribute - bit flags for read/write permissions.
    /// </summary>
    public byte AccessLevel { get; set; } = AccessLevels.CurrentRead;

    /// <summary>
    /// Effective access for the connected user. This, rather than the node's
    /// general AccessLevel, controls whether write affordances are shown.
    /// </summary>
    public byte UserAccessLevel { get; set; } = AccessLevels.CurrentRead;

    /// <summary>
    /// OPC UA ValueRank. -1 is scalar; zero or greater is an array shape that
    /// the current scalar write dialog does not support.
    /// </summary>
    public int ValueRank { get; set; } = ValueRanks.Any;

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
    public bool IsReadable => (UserAccessLevel & AccessLevels.CurrentRead) != 0;

    /// <summary>
    /// Whether the node supports writing (has CurrentWrite in AccessLevel).
    /// </summary>
    public bool IsWritable => (UserAccessLevel & AccessLevels.CurrentWrite) != 0;

    public bool IsScalar => ValueRank == ValueRanks.Scalar;

    public bool CanWrite => IsWritable && IsScalar;

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

    /// <summary>
    /// True when the current value is UI-only connection state rather than a
    /// value delivered/read from the OPC UA server. Synthetic values must not
    /// be written to CSV recordings.
    /// </summary>
    public bool IsSyntheticValue { get; set; }

    public string StatusString
    {
        get
        {
            if (StatusCode == 0) return "Good";
            if (IsBad) return $"Bad (0x{StatusCode:X8})";
            if (IsUncertain) return $"Uncertain (0x{StatusCode:X8})";
            return $"Good (0x{StatusCode:X8})";
        }
    }

    public string TimestampString => Timestamp is { } timestamp
        ? ToLocalDisplayTime(timestamp).ToString("HH:mm:ss")
        : "-";

    private static DateTime ToLocalDisplayTime(DateTime timestamp)
    {
        // OPC UA source timestamps are UTC. Some SDK/server paths surface them
        // with Kind=Unspecified, so attach the protocol-defined kind before
        // converting rather than interpreting them as local wall-clock time.
        if (timestamp.Kind == DateTimeKind.Unspecified)
        {
            timestamp = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
        }

        return timestamp.ToLocalTime();
    }
}
