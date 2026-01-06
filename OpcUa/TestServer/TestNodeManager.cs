using Opc.Ua;
using Opc.Ua.Server;

namespace OpcScope.OpcUa.TestServer;

/// <summary>
/// Custom NodeManager that exposes test nodes for demonstration and testing.
/// Provides simulation nodes with changing values and writable nodes.
/// </summary>
internal class TestNodeManager : CustomNodeManager2
{
    public const string NamespaceUri = "urn:opcscope:testserver";

    // Simulation node values (mutable for simulation)
    private int _counterValue;
    private double _randomValue;
    private double _sineValue;
    private string _writableString = "Hello OpcScope";
    private bool _toggleBoolean;
    private int _writableNumber = 42;

    // Node references for simulation updates
    private BaseDataVariableState<int>? _counterNode;
    private BaseDataVariableState<double>? _randomNode;
    private BaseDataVariableState<double>? _sineNode;

    // Timer for simulation
    private Timer? _simulationTimer;
    private int _tick;
    private readonly Random _random = new();

    public TestNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        : base(server, configuration, NamespaceUri)
    {
        SystemContext.NodeIdFactory = this;
    }

    protected override NodeStateCollection LoadPredefinedNodes(ISystemContext context)
    {
        return new NodeStateCollection();
    }

    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        lock (Lock)
        {
            base.CreateAddressSpace(externalReferences);

            // Get the Objects folder
            var objectsFolder = FindPredefinedNode(
                ExpandedNodeId.ToNodeId(ObjectIds.ObjectsFolder, Server.NamespaceUris),
                typeof(FolderState)) as FolderState;

            if (objectsFolder == null)
            {
                return;
            }

            // Create Simulation folder
            var simulationFolder = CreateFolder(objectsFolder, "Simulation", "Simulation");
            CreateSimulationNodes(simulationFolder);

            // Create StaticData folder
            var staticDataFolder = CreateFolder(objectsFolder, "StaticData", "StaticData");
            CreateStaticDataNodes(staticDataFolder);

            // Add references to the Objects folder
            AddPredefinedNode(SystemContext, simulationFolder);
            AddPredefinedNode(SystemContext, staticDataFolder);

            // Start simulation timer
            StartSimulation();
        }
    }

    private void CreateSimulationNodes(FolderState folder)
    {
        // Counter - Int32, changes every second
        _counterNode = CreateVariable<int>(folder, "Counter", "Counter", DataTypeIds.Int32, ValueRanks.Scalar);
        _counterNode.Value = _counterValue;
        _counterNode.AccessLevel = AccessLevels.CurrentRead;
        _counterNode.UserAccessLevel = AccessLevels.CurrentRead;

        // RandomValue - Double, random 0-100
        _randomNode = CreateVariable<double>(folder, "RandomValue", "RandomValue", DataTypeIds.Double, ValueRanks.Scalar);
        _randomNode.Value = _randomValue;
        _randomNode.AccessLevel = AccessLevels.CurrentRead;
        _randomNode.UserAccessLevel = AccessLevels.CurrentRead;

        // SineWave - Double, oscillating value
        _sineNode = CreateVariable<double>(folder, "SineWave", "SineWave", DataTypeIds.Double, ValueRanks.Scalar);
        _sineNode.Value = _sineValue;
        _sineNode.AccessLevel = AccessLevels.CurrentRead;
        _sineNode.UserAccessLevel = AccessLevels.CurrentRead;

        // WritableString - String, writable
        var writableString = CreateVariable(folder, "WritableString", "WritableString", DataTypeIds.String, ValueRanks.Scalar);
        writableString.Value = _writableString;
        writableString.AccessLevel = AccessLevels.CurrentReadOrWrite;
        writableString.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
        writableString.OnSimpleWriteValue = OnWriteWritableString;

        // ToggleBoolean - Boolean, writable
        var toggleBoolean = CreateVariable(folder, "ToggleBoolean", "ToggleBoolean", DataTypeIds.Boolean, ValueRanks.Scalar);
        toggleBoolean.Value = _toggleBoolean;
        toggleBoolean.AccessLevel = AccessLevels.CurrentReadOrWrite;
        toggleBoolean.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
        toggleBoolean.OnSimpleWriteValue = OnWriteToggleBoolean;

        // WritableNumber - Int32, writable
        var writableNumber = CreateVariable(folder, "WritableNumber", "WritableNumber", DataTypeIds.Int32, ValueRanks.Scalar);
        writableNumber.Value = _writableNumber;
        writableNumber.AccessLevel = AccessLevels.CurrentReadOrWrite;
        writableNumber.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
        writableNumber.OnSimpleWriteValue = OnWriteWritableNumber;
    }

    private void CreateStaticDataNodes(FolderState folder)
    {
        // ServerName - String, read-only
        var serverName = CreateVariable(folder, "ServerName", "ServerName", DataTypeIds.String, ValueRanks.Scalar);
        serverName.Value = "OpcScope Test Server";
        serverName.AccessLevel = AccessLevels.CurrentRead;
        serverName.UserAccessLevel = AccessLevels.CurrentRead;

        // StartTime - DateTime, read-only
        var startTime = CreateVariable(folder, "StartTime", "StartTime", DataTypeIds.DateTime, ValueRanks.Scalar);
        startTime.Value = DateTime.UtcNow;
        startTime.AccessLevel = AccessLevels.CurrentRead;
        startTime.UserAccessLevel = AccessLevels.CurrentRead;

        // Version - String, read-only
        var version = CreateVariable(folder, "Version", "Version", DataTypeIds.String, ValueRanks.Scalar);
        version.Value = "1.0.0";
        version.AccessLevel = AccessLevels.CurrentRead;
        version.UserAccessLevel = AccessLevels.CurrentRead;

        // ArrayOfInts - Int32[], read-only
        var arrayOfInts = CreateVariable(folder, "ArrayOfInts", "ArrayOfInts", DataTypeIds.Int32, ValueRanks.OneDimension);
        arrayOfInts.Value = new int[] { 1, 2, 3, 4, 5 };
        arrayOfInts.AccessLevel = AccessLevels.CurrentRead;
        arrayOfInts.UserAccessLevel = AccessLevels.CurrentRead;
    }

    private FolderState CreateFolder(NodeState parent, string path, string name)
    {
        var folder = new FolderState(parent)
        {
            SymbolicName = name,
            ReferenceTypeId = ReferenceTypeIds.Organizes,
            TypeDefinitionId = ObjectTypeIds.FolderType,
            NodeId = new NodeId(path, NamespaceIndex),
            BrowseName = new QualifiedName(name, NamespaceIndex),
            DisplayName = new LocalizedText("en", name),
            WriteMask = AttributeWriteMask.None,
            UserWriteMask = AttributeWriteMask.None,
            EventNotifier = EventNotifiers.None
        };

        parent?.AddChild(folder);

        return folder;
    }

    private BaseDataVariableState<T> CreateVariable<T>(
        NodeState parent,
        string path,
        string name,
        NodeId dataType,
        int valueRank)
    {
        var variable = new BaseDataVariableState<T>(parent);
        InitializeVariable(variable, path, name, dataType, valueRank);
        parent?.AddChild(variable);
        return variable;
    }

    // Simplified overload for non-generic use
    private BaseDataVariableState CreateVariable(
        NodeState parent,
        string path,
        string name,
        NodeId dataType,
        int valueRank)
    {
        var variable = new BaseDataVariableState(parent);
        InitializeVariable(variable, path, name, dataType, valueRank);
        parent?.AddChild(variable);
        return variable;
    }

    private void InitializeVariable(
        BaseVariableState variable,
        string path,
        string name,
        NodeId dataType,
        int valueRank)
    {
        variable.SymbolicName = name;
        variable.ReferenceTypeId = ReferenceTypeIds.Organizes;
        variable.TypeDefinitionId = VariableTypeIds.BaseDataVariableType;
        variable.NodeId = new NodeId(path, NamespaceIndex);
        variable.BrowseName = new QualifiedName(name, NamespaceIndex);
        variable.DisplayName = new LocalizedText("en", name);
        variable.WriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description;
        variable.UserWriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description;
        variable.DataType = dataType;
        variable.ValueRank = valueRank;
        variable.AccessLevel = AccessLevels.CurrentReadOrWrite;
        variable.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
        variable.Historizing = false;
        variable.StatusCode = StatusCodes.Good;
        variable.Timestamp = DateTime.UtcNow;

        if (valueRank == ValueRanks.OneDimension)
        {
            variable.ArrayDimensions = new ReadOnlyList<uint>(new List<uint> { 0 });
        }
    }

    private void StartSimulation()
    {
        _simulationTimer = new Timer(OnSimulationTick, null, 1000, 1000);
    }

    private void OnSimulationTick(object? state)
    {
        try
        {
            lock (Lock)
            {
                _tick++;
                _counterValue++;
                _randomValue = _random.NextDouble() * 100;
                _sineValue = Math.Sin(_tick * 0.1) * 50 + 50;

                if (_counterNode != null)
                {
                    _counterNode.Value = _counterValue;
                    _counterNode.Timestamp = DateTime.UtcNow;
                    _counterNode.ClearChangeMasks(SystemContext, false);
                }

                if (_randomNode != null)
                {
                    _randomNode.Value = _randomValue;
                    _randomNode.Timestamp = DateTime.UtcNow;
                    _randomNode.ClearChangeMasks(SystemContext, false);
                }

                if (_sineNode != null)
                {
                    _sineNode.Value = _sineValue;
                    _sineNode.Timestamp = DateTime.UtcNow;
                    _sineNode.ClearChangeMasks(SystemContext, false);
                }
            }
        }
        catch (Exception ex)
        {
            Utils.Trace("Exception in TestNodeManager.OnSimulationTick: {0}", ex);
        }
    }

    private ServiceResult OnWriteWritableString(
        ISystemContext context,
        NodeState node,
        ref object value)
    {
        if (value is not string stringValue)
        {
            return StatusCodes.BadTypeMismatch;
        }
        _writableString = stringValue;
        return ServiceResult.Good;
    }

    private ServiceResult OnWriteToggleBoolean(
        ISystemContext context,
        NodeState node,
        ref object value)
    {
        if (value is not bool boolValue)
        {
            return StatusCodes.BadTypeMismatch;
        }
        _toggleBoolean = boolValue;
        return ServiceResult.Good;
    }

    private ServiceResult OnWriteWritableNumber(
        ISystemContext context,
        NodeState node,
        ref object value)
    {
        if (value is not int intValue)
        {
            return StatusCodes.BadTypeMismatch;
        }
        _writableNumber = intValue;
        return ServiceResult.Good;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _simulationTimer?.Dispose();
            _simulationTimer = null;
        }

        base.Dispose(disposing);
    }
}
