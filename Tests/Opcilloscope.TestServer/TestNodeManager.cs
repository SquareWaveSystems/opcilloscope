using Opc.Ua;
using Opc.Ua.Server;

namespace Opcilloscope.TestServer;

/// <summary>
/// Custom NodeManager that exposes test nodes for demonstration and testing.
/// Provides simulation nodes with changing values and writable nodes.
/// </summary>
public class TestNodeManager : CustomNodeManager2
{
    public const string NamespaceUri = "urn:opcilloscope:testserver";

    private int _counterValue;
    private double _randomValue;
    private double _sineValue;
    private double _triangleValue;
    private double _squareValue;
    private double _sawtoothValue;
    private string _writableString = "Hello Opcilloscope";
    private bool _toggleBoolean;
    private int _writableNumber = 42;

    private BaseDataVariableState<int>? _counterNode;
    private BaseDataVariableState<double>? _randomNode;
    private BaseDataVariableState<double>? _sineNode;
    private BaseDataVariableState<double>? _sineFrequencyNode;
    private BaseDataVariableState<double>? _triangleNode;
    private BaseDataVariableState<double>? _triangleFrequencyNode;
    private BaseDataVariableState<double>? _squareNode;
    private BaseDataVariableState<double>? _squareFrequencyNode;
    private BaseDataVariableState<double>? _squareDutyCycleNode;
    private BaseDataVariableState<double>? _sawtoothNode;
    private BaseDataVariableState<double>? _sawtoothFrequencyNode;

    private Timer? _simulationTimer;
    private int _tick;
    private double _sineFrequency = 0.1;
    private double _triangleFrequency = 0.1;
    private double _squareFrequency = 0.1;
    private double _squareDutyCycle = 0.5;
    private double _sawtoothFrequency = 0.1;
    private readonly Random _random = new();

    /// <summary>
    /// Gets or sets the sine wave frequency factor.
    /// Default is 0.1. Higher values produce faster oscillation.
    /// </summary>
    public double SineFrequency
    {
        get => _sineFrequency;
        set
        {
            _sineFrequency = value;
            lock (Lock)
            {
                if (_sineFrequencyNode != null)
                {
                    _sineFrequencyNode.Value = value;
                    _sineFrequencyNode.Timestamp = DateTime.UtcNow;
                    _sineFrequencyNode.ClearChangeMasks(SystemContext, false);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the triangle wave frequency factor.
    /// Default is 0.1. Higher values produce faster oscillation.
    /// </summary>
    public double TriangleFrequency
    {
        get => _triangleFrequency;
        set
        {
            _triangleFrequency = value;
            lock (Lock)
            {
                if (_triangleFrequencyNode != null)
                {
                    _triangleFrequencyNode.Value = value;
                    _triangleFrequencyNode.Timestamp = DateTime.UtcNow;
                    _triangleFrequencyNode.ClearChangeMasks(SystemContext, false);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the square wave frequency factor.
    /// Default is 0.1. Higher values produce faster oscillation.
    /// </summary>
    public double SquareFrequency
    {
        get => _squareFrequency;
        set
        {
            _squareFrequency = value;
            lock (Lock)
            {
                if (_squareFrequencyNode != null)
                {
                    _squareFrequencyNode.Value = value;
                    _squareFrequencyNode.Timestamp = DateTime.UtcNow;
                    _squareFrequencyNode.ClearChangeMasks(SystemContext, false);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the square wave duty cycle (0.0 to 1.0).
    /// Default is 0.5 (50% duty cycle).
    /// </summary>
    public double SquareDutyCycle
    {
        get => _squareDutyCycle;
        set
        {
            _squareDutyCycle = Math.Clamp(value, 0.0, 1.0);
            lock (Lock)
            {
                if (_squareDutyCycleNode != null)
                {
                    _squareDutyCycleNode.Value = _squareDutyCycle;
                    _squareDutyCycleNode.Timestamp = DateTime.UtcNow;
                    _squareDutyCycleNode.ClearChangeMasks(SystemContext, false);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the sawtooth wave frequency factor.
    /// Default is 0.1. Higher values produce faster oscillation.
    /// </summary>
    public double SawtoothFrequency
    {
        get => _sawtoothFrequency;
        set
        {
            _sawtoothFrequency = value;
            lock (Lock)
            {
                if (_sawtoothFrequencyNode != null)
                {
                    _sawtoothFrequencyNode.Value = value;
                    _sawtoothFrequencyNode.Timestamp = DateTime.UtcNow;
                    _sawtoothFrequencyNode.ClearChangeMasks(SystemContext, false);
                }
            }
        }
    }

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

            // Get reference to ObjectsFolder - we'll add references to it
            var objectsFolderId = ObjectIds.ObjectsFolder;

            // Create Simulation folder
            var simulationFolder = CreateFolderUnderObjects(
                externalReferences, objectsFolderId, "Simulation", "Simulation");
            CreateSimulationNodes(simulationFolder);
            AddPredefinedNode(SystemContext, simulationFolder);

            // Create StaticData folder
            var staticDataFolder = CreateFolderUnderObjects(
                externalReferences, objectsFolderId, "StaticData", "StaticData");
            CreateStaticDataNodes(staticDataFolder);
            AddPredefinedNode(SystemContext, staticDataFolder);

            StartSimulation();
        }
    }

    private FolderState CreateFolderUnderObjects(
        IDictionary<NodeId, IList<IReference>> externalReferences,
        NodeId parentId,
        string path,
        string name)
    {
        var folder = new FolderState(null)
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

        // Add reference from Objects folder to our folder
        if (externalReferences != null)
        {
            if (!externalReferences.TryGetValue(parentId, out var references))
            {
                references = new List<IReference>();
                externalReferences[parentId] = references;
            }

            folder.AddReference(ReferenceTypeIds.Organizes, true, parentId);
            references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, folder.NodeId));
        }

        return folder;
    }

    private void CreateSimulationNodes(FolderState folder)
    {
        _counterNode = CreateVariable<int>(folder, "Counter", "Counter", DataTypeIds.Int32, ValueRanks.Scalar);
        _counterNode.Value = _counterValue;
        _counterNode.AccessLevel = AccessLevels.CurrentRead;
        _counterNode.UserAccessLevel = AccessLevels.CurrentRead;

        _randomNode = CreateVariable<double>(folder, "RandomValue", "RandomValue", DataTypeIds.Double, ValueRanks.Scalar);
        _randomNode.Value = _randomValue;
        _randomNode.AccessLevel = AccessLevels.CurrentRead;
        _randomNode.UserAccessLevel = AccessLevels.CurrentRead;

        _sineNode = CreateVariable<double>(folder, "SineWave", "SineWave", DataTypeIds.Double, ValueRanks.Scalar);
        _sineNode.Value = _sineValue;
        _sineNode.AccessLevel = AccessLevels.CurrentRead;
        _sineNode.UserAccessLevel = AccessLevels.CurrentRead;

        _sineFrequencyNode = CreateVariable<double>(folder, "SineFrequency", "SineFrequency", DataTypeIds.Double, ValueRanks.Scalar);
        _sineFrequencyNode.Value = _sineFrequency;
        _sineFrequencyNode.AccessLevel = AccessLevels.CurrentReadOrWrite;
        _sineFrequencyNode.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
        _sineFrequencyNode.OnSimpleWriteValue = OnWriteSineFrequency;

        _triangleNode = CreateVariable<double>(folder, "TriangleWave", "TriangleWave", DataTypeIds.Double, ValueRanks.Scalar);
        _triangleNode.Value = _triangleValue;
        _triangleNode.AccessLevel = AccessLevels.CurrentRead;
        _triangleNode.UserAccessLevel = AccessLevels.CurrentRead;

        _triangleFrequencyNode = CreateVariable<double>(folder, "TriangleFrequency", "TriangleFrequency", DataTypeIds.Double, ValueRanks.Scalar);
        _triangleFrequencyNode.Value = _triangleFrequency;
        _triangleFrequencyNode.AccessLevel = AccessLevels.CurrentReadOrWrite;
        _triangleFrequencyNode.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
        _triangleFrequencyNode.OnSimpleWriteValue = OnWriteTriangleFrequency;

        _squareNode = CreateVariable<double>(folder, "SquareWave", "SquareWave", DataTypeIds.Double, ValueRanks.Scalar);
        _squareNode.Value = _squareValue;
        _squareNode.AccessLevel = AccessLevels.CurrentRead;
        _squareNode.UserAccessLevel = AccessLevels.CurrentRead;

        _squareFrequencyNode = CreateVariable<double>(folder, "SquareFrequency", "SquareFrequency", DataTypeIds.Double, ValueRanks.Scalar);
        _squareFrequencyNode.Value = _squareFrequency;
        _squareFrequencyNode.AccessLevel = AccessLevels.CurrentReadOrWrite;
        _squareFrequencyNode.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
        _squareFrequencyNode.OnSimpleWriteValue = OnWriteSquareFrequency;

        _squareDutyCycleNode = CreateVariable<double>(folder, "SquareDutyCycle", "SquareDutyCycle", DataTypeIds.Double, ValueRanks.Scalar);
        _squareDutyCycleNode.Value = _squareDutyCycle;
        _squareDutyCycleNode.AccessLevel = AccessLevels.CurrentReadOrWrite;
        _squareDutyCycleNode.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
        _squareDutyCycleNode.OnSimpleWriteValue = OnWriteSquareDutyCycle;

        _sawtoothNode = CreateVariable<double>(folder, "SawtoothWave", "SawtoothWave", DataTypeIds.Double, ValueRanks.Scalar);
        _sawtoothNode.Value = _sawtoothValue;
        _sawtoothNode.AccessLevel = AccessLevels.CurrentRead;
        _sawtoothNode.UserAccessLevel = AccessLevels.CurrentRead;

        _sawtoothFrequencyNode = CreateVariable<double>(folder, "SawtoothFrequency", "SawtoothFrequency", DataTypeIds.Double, ValueRanks.Scalar);
        _sawtoothFrequencyNode.Value = _sawtoothFrequency;
        _sawtoothFrequencyNode.AccessLevel = AccessLevels.CurrentReadOrWrite;
        _sawtoothFrequencyNode.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
        _sawtoothFrequencyNode.OnSimpleWriteValue = OnWriteSawtoothFrequency;

        var writableString = CreateVariable(folder, "WritableString", "WritableString", DataTypeIds.String, ValueRanks.Scalar);
        writableString.Value = _writableString;
        writableString.AccessLevel = AccessLevels.CurrentReadOrWrite;
        writableString.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
        writableString.OnSimpleWriteValue = OnWriteWritableString;

        var toggleBoolean = CreateVariable(folder, "ToggleBoolean", "ToggleBoolean", DataTypeIds.Boolean, ValueRanks.Scalar);
        toggleBoolean.Value = _toggleBoolean;
        toggleBoolean.AccessLevel = AccessLevels.CurrentReadOrWrite;
        toggleBoolean.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
        toggleBoolean.OnSimpleWriteValue = OnWriteToggleBoolean;

        var writableNumber = CreateVariable(folder, "WritableNumber", "WritableNumber", DataTypeIds.Int32, ValueRanks.Scalar);
        writableNumber.Value = _writableNumber;
        writableNumber.AccessLevel = AccessLevels.CurrentReadOrWrite;
        writableNumber.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
        writableNumber.OnSimpleWriteValue = OnWriteWritableNumber;
    }

    private void CreateStaticDataNodes(FolderState folder)
    {
        var serverName = CreateVariable(folder, "ServerName", "ServerName", DataTypeIds.String, ValueRanks.Scalar);
        serverName.Value = "Opcilloscope Test Server";
        serverName.AccessLevel = AccessLevels.CurrentRead;
        serverName.UserAccessLevel = AccessLevels.CurrentRead;

        var startTime = CreateVariable(folder, "StartTime", "StartTime", DataTypeIds.DateTime, ValueRanks.Scalar);
        startTime.Value = DateTime.UtcNow;
        startTime.AccessLevel = AccessLevels.CurrentRead;
        startTime.UserAccessLevel = AccessLevels.CurrentRead;

        var version = CreateVariable(folder, "Version", "Version", DataTypeIds.String, ValueRanks.Scalar);
        version.Value = "1.0.0";
        version.AccessLevel = AccessLevels.CurrentRead;
        version.UserAccessLevel = AccessLevels.CurrentRead;

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
                _sineValue = Math.Sin(_tick * _sineFrequency) * 50 + 50;

                // Triangle wave: linear ramp up then down, range 0-100
                var trianglePhase = (_tick * _triangleFrequency) % (2 * Math.PI);
                _triangleValue = (2 * Math.Abs(trianglePhase / Math.PI - 1) - 1) * -50 + 50;

                // Square wave: high/low based on duty cycle, range 0-100
                var squarePhase = (_tick * _squareFrequency) % (2 * Math.PI);
                _squareValue = (squarePhase / (2 * Math.PI)) < _squareDutyCycle ? 100 : 0;

                // Sawtooth wave: linear ramp up then reset, range 0-100
                var sawtoothPhase = (_tick * _sawtoothFrequency) % (2 * Math.PI);
                _sawtoothValue = (sawtoothPhase / (2 * Math.PI)) * 100;

                var now = DateTime.UtcNow;

                if (_counterNode != null)
                {
                    _counterNode.Value = _counterValue;
                    _counterNode.Timestamp = now;
                    _counterNode.ClearChangeMasks(SystemContext, false);
                }

                if (_randomNode != null)
                {
                    _randomNode.Value = _randomValue;
                    _randomNode.Timestamp = now;
                    _randomNode.ClearChangeMasks(SystemContext, false);
                }

                if (_sineNode != null)
                {
                    _sineNode.Value = _sineValue;
                    _sineNode.Timestamp = now;
                    _sineNode.ClearChangeMasks(SystemContext, false);
                }

                if (_triangleNode != null)
                {
                    _triangleNode.Value = _triangleValue;
                    _triangleNode.Timestamp = now;
                    _triangleNode.ClearChangeMasks(SystemContext, false);
                }

                if (_squareNode != null)
                {
                    _squareNode.Value = _squareValue;
                    _squareNode.Timestamp = now;
                    _squareNode.ClearChangeMasks(SystemContext, false);
                }

                if (_sawtoothNode != null)
                {
                    _sawtoothNode.Value = _sawtoothValue;
                    _sawtoothNode.Timestamp = now;
                    _sawtoothNode.ClearChangeMasks(SystemContext, false);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exception in TestNodeManager.OnSimulationTick: {ex}");
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

    private ServiceResult OnWriteSineFrequency(
        ISystemContext context,
        NodeState node,
        ref object value)
    {
        if (value is not double doubleValue)
        {
            return StatusCodes.BadTypeMismatch;
        }
        _sineFrequency = doubleValue;
        return ServiceResult.Good;
    }

    private ServiceResult OnWriteTriangleFrequency(
        ISystemContext context,
        NodeState node,
        ref object value)
    {
        if (value is not double doubleValue)
        {
            return StatusCodes.BadTypeMismatch;
        }
        _triangleFrequency = doubleValue;
        return ServiceResult.Good;
    }

    private ServiceResult OnWriteSquareFrequency(
        ISystemContext context,
        NodeState node,
        ref object value)
    {
        if (value is not double doubleValue)
        {
            return StatusCodes.BadTypeMismatch;
        }
        _squareFrequency = doubleValue;
        return ServiceResult.Good;
    }

    private ServiceResult OnWriteSquareDutyCycle(
        ISystemContext context,
        NodeState node,
        ref object value)
    {
        if (value is not double doubleValue)
        {
            return StatusCodes.BadTypeMismatch;
        }
        _squareDutyCycle = Math.Clamp(doubleValue, 0.0, 1.0);
        return ServiceResult.Good;
    }

    private ServiceResult OnWriteSawtoothFrequency(
        ISystemContext context,
        NodeState node,
        ref object value)
    {
        if (value is not double doubleValue)
        {
            return StatusCodes.BadTypeMismatch;
        }
        _sawtoothFrequency = doubleValue;
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
