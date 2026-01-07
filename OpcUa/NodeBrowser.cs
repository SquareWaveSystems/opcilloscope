using Opc.Ua;
using Opcilloscope.OpcUa.Models;
using Opcilloscope.Utilities;

namespace Opcilloscope.OpcUa;

/// <summary>
/// Address space navigation logic with lazy loading.
/// </summary>
public class NodeBrowser
{
    private readonly OpcUaClientWrapper _client;
    private readonly Logger _logger;
    private readonly Dictionary<string, string> _dataTypeCache = new();

    // Common data type NodeIds
    private static readonly Dictionary<uint, string> BuiltInDataTypes = new()
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

    public NodeBrowser(OpcUaClientWrapper client, Logger logger)
    {
        _client = client;
        _logger = logger;
    }

    public BrowsedNode GetRootNode()
    {
        return new BrowsedNode
        {
            NodeId = ObjectIds.RootFolder, // ns=0;i=84
            BrowseName = "Root",
            DisplayName = "Root",
            NodeClass = NodeClass.Object,
            HasChildren = true
        };
    }

    public async Task<List<BrowsedNode>> GetChildrenAsync(BrowsedNode parent)
    {
        if (!_client.IsConnected)
            return new List<BrowsedNode>();

        try
        {
            var refs = await _client.BrowseAsync(parent.NodeId);
            var children = new List<BrowsedNode>();

            foreach (var r in refs)
            {
                // Convert ExpandedNodeId to NodeId
                var targetNodeId = ExpandedNodeId.ToNodeId(r.NodeId, _client.Session?.NamespaceUris);
                if (targetNodeId == null)
                    continue;

                // Get TypeDefinition NodeId
                NodeId? typeDefNodeId = null;
                if (r.TypeDefinition != null && !r.TypeDefinition.IsNull)
                {
                    typeDefNodeId = ExpandedNodeId.ToNodeId(r.TypeDefinition, _client.Session?.NamespaceUris);
                }

                var child = new BrowsedNode
                {
                    NodeId = targetNodeId,
                    BrowseName = r.BrowseName?.Name ?? string.Empty,
                    DisplayName = r.DisplayName?.Text ?? r.BrowseName?.Name ?? "Unknown",
                    NodeClass = r.NodeClass,
                    DataType = typeDefNodeId,
                    Parent = parent
                };

                // For variables, try to get the data type
                if (r.NodeClass == NodeClass.Variable)
                {
                    child.DataTypeName = await GetDataTypeNameAsync(targetNodeId);
                }

                // Check if node has children by doing a quick browse
                child.HasChildren = await HasChildrenAsync(targetNodeId);

                children.Add(child);
            }

            parent.ChildrenLoaded = true;
            parent.Children.Clear();
            parent.Children.AddRange(children);

            return children;
        }
        catch (Exception ex)
        {
            _logger.Error($"Browse failed for {parent.NodeId}: {ex.Message}");
            return new List<BrowsedNode>();
        }
    }

    private async Task<bool> HasChildrenAsync(NodeId nodeId)
    {
        try
        {
            var refs = await _client.BrowseAsync(nodeId);
            return refs.Count > 0;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Node may not be browsable - this is expected for some node types
            _logger.Warning($"Could not check children for {nodeId}: {ex.Message}");
            return false;
        }
    }

    private async Task<string?> GetDataTypeNameAsync(NodeId nodeId)
    {
        var key = nodeId.ToString();
        if (_dataTypeCache.TryGetValue(key, out var cached))
            return cached;

        try
        {
            var attrs = await _client.ReadAttributesAsync(nodeId, Attributes.DataType);
            if (attrs.Count > 0 && attrs[0].Value is NodeId dataTypeId)
            {
                string? name = null;

                // Check built-in types first
                if (dataTypeId.NamespaceIndex == 0 && dataTypeId.IdType == IdType.Numeric)
                {
                    var id = (uint)dataTypeId.Identifier;
                    if (BuiltInDataTypes.TryGetValue(id, out var builtIn))
                        name = builtIn;
                }

                // If not built-in, browse for the type name
                if (name == null)
                {
                    var typeAttrs = await _client.ReadAttributesAsync(dataTypeId, Attributes.DisplayName);
                    if (typeAttrs.Count > 0 && typeAttrs[0].Value is LocalizedText lt)
                        name = lt.Text;
                }

                if (name != null)
                {
                    _dataTypeCache[key] = name;
                    return name;
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Data type lookup failure is non-critical - node will still be displayed
            System.Diagnostics.Debug.WriteLine($"Data type lookup failed for {nodeId}: {ex.Message}");
        }

        return null;
    }

    public async Task<NodeAttributes?> GetNodeAttributesAsync(NodeId nodeId)
    {
        if (!_client.IsConnected)
            return null;

        try
        {
            var attrs = await _client.ReadAttributesAsync(
                nodeId,
                Attributes.NodeId,
                Attributes.NodeClass,
                Attributes.BrowseName,
                Attributes.DisplayName,
                Attributes.Description,
                Attributes.DataType,
                Attributes.ValueRank,
                Attributes.AccessLevel,
                Attributes.UserAccessLevel
            );

            // Check if the node exists by verifying the NodeId attribute read was successful
            if (attrs.Count == 0 || StatusCode.IsBad(attrs[0].StatusCode))
            {
                return null;
            }

            return new NodeAttributes
            {
                NodeId = nodeId,
                NodeClass = attrs.Count > 1 && attrs[1].Value is int nc ? (NodeClass)nc : NodeClass.Unspecified,
                BrowseName = attrs.Count > 2 && attrs[2].Value is QualifiedName qn ? qn.Name : null,
                DisplayName = attrs.Count > 3 && attrs[3].Value is LocalizedText lt ? lt.Text : null,
                Description = attrs.Count > 4 && attrs[4].Value is LocalizedText desc ? desc.Text : null,
                DataType = attrs.Count > 5 && attrs[5].Value is NodeId dt ? await GetDataTypeNameByIdAsync(dt) : null,
                ValueRank = attrs.Count > 6 && attrs[6].Value is int vr ? vr : null,
                AccessLevel = attrs.Count > 7 && attrs[7].Value is byte al ? al : null,
                UserAccessLevel = attrs.Count > 8 && attrs[8].Value is byte ual ? ual : null
            };
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to read attributes: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> GetDataTypeNameByIdAsync(NodeId dataTypeId)
    {
        if (dataTypeId.NamespaceIndex == 0 && dataTypeId.IdType == IdType.Numeric)
        {
            var id = (uint)dataTypeId.Identifier;
            if (BuiltInDataTypes.TryGetValue(id, out var name))
                return name;
        }

        try
        {
            var attrs = await _client.ReadAttributesAsync(dataTypeId, Attributes.DisplayName);
            if (attrs.Count > 0 && attrs[0].Value is LocalizedText lt)
                return lt.Text;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Fall back to NodeId string representation
            System.Diagnostics.Debug.WriteLine($"Could not resolve data type name for {dataTypeId}: {ex.Message}");
        }

        return dataTypeId.ToString();
    }
}

public class NodeAttributes
{
    public NodeId NodeId { get; init; } = ObjectIds.RootFolder;
    public NodeClass NodeClass { get; init; }
    public string? BrowseName { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? DataType { get; init; }
    public int? ValueRank { get; init; }
    public byte? AccessLevel { get; init; }
    public byte? UserAccessLevel { get; init; }

    public string AccessLevelString
    {
        get
        {
            if (AccessLevel == null) return "N/A";
            var parts = new List<string>();
            if ((AccessLevel & 0x01) != 0) parts.Add("Read");
            if ((AccessLevel & 0x02) != 0) parts.Add("Write");
            if ((AccessLevel & 0x04) != 0) parts.Add("HistoryRead");
            if ((AccessLevel & 0x08) != 0) parts.Add("HistoryWrite");
            return parts.Count > 0 ? string.Join(", ", parts) : "None";
        }
    }
}
