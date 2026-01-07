using Opc.Ua;

namespace Opcilloscope.OpcUa.Models;

/// <summary>
/// View model for nodes displayed in the address space tree.
/// </summary>
public class BrowsedNode
{
    public NodeId NodeId { get; init; } = ObjectIds.RootFolder;
    public string BrowseName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public NodeClass NodeClass { get; init; } = NodeClass.Unspecified;
    public NodeId? DataType { get; init; }
    public string? DataTypeName { get; set; }
    public bool HasChildren { get; set; } = true; // Assume true until proven otherwise
    public bool ChildrenLoaded { get; set; } = false;
    public List<BrowsedNode> Children { get; } = new();
    public BrowsedNode? Parent { get; set; }

    public string NodeClassIcon => NodeClass switch
    {
        NodeClass.Object => "[O]",
        NodeClass.Variable => "[V]",
        NodeClass.Method => "[M]",
        NodeClass.ObjectType => "[OT]",
        NodeClass.VariableType => "[VT]",
        NodeClass.ReferenceType => "[RT]",
        NodeClass.DataType => "[DT]",
        NodeClass.View => "[Vw]",
        _ => "[?]"
    };

    public override string ToString()
    {
        var suffix = NodeClass == NodeClass.Variable && !string.IsNullOrEmpty(DataTypeName)
            ? $" [{DataTypeName}]"
            : string.Empty;
        return $"{NodeClassIcon} {DisplayName}{suffix}";
    }
}
