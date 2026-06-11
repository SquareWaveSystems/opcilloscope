using Opc.Ua;

namespace Opcilloscope.OpcUa;

/// <summary>
/// Shared lookup for OPC UA built-in data type names and BuiltInType resolution.
/// </summary>
internal static class DataTypeResolver
{
    // Built-in OPC UA data types: the numeric NodeId identifier in namespace 0
    // matches the BuiltInType enum value (Boolean=1 ... DiagnosticInfo=25).
    private static readonly Dictionary<uint, string> BuiltInNames = new()
    {
        { 1, "Boolean" },
        { 2, "SByte" },
        { 3, "Byte" },
        { 4, "Int16" },
        { 5, "UInt16" },
        { 6, "Int32" },
        { 7, "UInt32" },
        { 8, "Int64" },
        { 9, "UInt64" },
        { 10, "Float" },
        { 11, "Double" },
        { 12, "String" },
        { 13, "DateTime" },
        { 14, "Guid" },
        { 15, "ByteString" },
        { 16, "XmlElement" },
        { 17, "NodeId" },
        { 18, "ExpandedNodeId" },
        { 19, "StatusCode" },
        { 20, "QualifiedName" },
        { 21, "LocalizedText" },
        { 22, "ExtensionObject" },
        { 23, "DataValue" },
        { 24, "Variant" },
        { 25, "DiagnosticInfo" },
    };

    /// <summary>
    /// Gets the display name of a built-in data type, or null if the NodeId
    /// does not refer to a built-in type.
    /// </summary>
    public static bool TryGetBuiltInName(NodeId dataTypeNodeId, out string? name)
    {
        if (dataTypeNodeId.NamespaceIndex == 0
            && dataTypeNodeId.IdType == IdType.Numeric
            && dataTypeNodeId.Identifier is uint id
            && BuiltInNames.TryGetValue(id, out name))
        {
            return true;
        }

        name = null;
        return false;
    }

    /// <summary>
    /// Resolves a data type NodeId to a BuiltInType (for value conversion) and a
    /// display name. Only primitive types (Boolean through ByteString) map to a
    /// concrete BuiltInType; everything else is treated as Variant so string
    /// input is passed through unchanged when writing values.
    /// </summary>
    public static (BuiltInType Type, string Name) Resolve(NodeId dataTypeNodeId)
    {
        if (TryGetBuiltInName(dataTypeNodeId, out var name)
            && dataTypeNodeId.Identifier is uint id)
        {
            var type = id <= (uint)BuiltInType.ByteString ? (BuiltInType)id : BuiltInType.Variant;
            return (type, name!);
        }

        return (BuiltInType.Variant, dataTypeNodeId.ToString());
    }
}
