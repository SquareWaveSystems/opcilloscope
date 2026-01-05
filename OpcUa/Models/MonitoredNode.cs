using LibUA.Core;

namespace OpcScope.OpcUa.Models;

/// <summary>
/// View model for monitored items displayed in the table.
/// </summary>
public class MonitoredNode
{
    public uint ClientHandle { get; init; }
    public uint MonitoredItemId { get; set; }
    public NodeId NodeId { get; init; } = NodeId.Zero;
    public string DisplayName { get; init; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
    public uint StatusCode { get; set; }
    public bool IsGood => StatusCode == 0; // StatusCode.Good = 0
    public bool IsUncertain => (StatusCode & 0x40000000) != 0;
    public bool IsBad => (StatusCode & 0x80000000) != 0;
    public DateTime LastChangeTime { get; set; } = DateTime.MinValue;
    public bool RecentlyChanged => (DateTime.Now - LastChangeTime).TotalMilliseconds < 500;

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
