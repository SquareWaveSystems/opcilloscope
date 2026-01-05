const { OPCUAServer, Variant, DataType, StatusCodes } = require("node-opcua");

(async () => {
  const server = new OPCUAServer({
    port: 4840,
    resourcePath: "/UA/TestServer",
    buildInfo: {
      productName: "OpcScope Test Server",
      buildNumber: "1.0.0",
      buildDate: new Date(),
    },
  });

  await server.initialize();

  const addressSpace = server.engine.addressSpace;
  const namespace = addressSpace.getOwnNamespace();
  const objectsFolder = addressSpace.rootFolder.objects;

  // Create folder structure
  const simulationFolder = namespace.addFolder(objectsFolder, {
    browseName: "Simulation",
  });

  const staticDataFolder = namespace.addFolder(objectsFolder, {
    browseName: "StaticData",
  });

  const writableFolder = namespace.addFolder(objectsFolder, {
    browseName: "Writable",
  });

  const dataTypesFolder = namespace.addFolder(objectsFolder, {
    browseName: "DataTypes",
  });

  // =====================
  // Simulation Variables
  // =====================

  // Counter - increments every second
  let counter = 0;
  namespace.addVariable({
    componentOf: simulationFolder,
    browseName: "Counter",
    dataType: "Int32",
    value: {
      get: () => new Variant({ dataType: DataType.Int32, value: counter }),
    },
  });

  // Random value - changes every second
  let randomValue = Math.random() * 100;
  namespace.addVariable({
    componentOf: simulationFolder,
    browseName: "RandomValue",
    dataType: "Double",
    value: {
      get: () => new Variant({ dataType: DataType.Double, value: randomValue }),
    },
  });

  // Sine wave - oscillates between -1 and 1
  let sineAngle = 0;
  namespace.addVariable({
    componentOf: simulationFolder,
    browseName: "SineWave",
    dataType: "Double",
    value: {
      get: () =>
        new Variant({ dataType: DataType.Double, value: Math.sin(sineAngle) }),
    },
  });

  // Cosine wave - oscillates between -1 and 1
  namespace.addVariable({
    componentOf: simulationFolder,
    browseName: "CosineWave",
    dataType: "Double",
    value: {
      get: () =>
        new Variant({ dataType: DataType.Double, value: Math.cos(sineAngle) }),
    },
  });

  // Sawtooth wave - goes from 0 to 100 and resets
  let sawtoothValue = 0;
  namespace.addVariable({
    componentOf: simulationFolder,
    browseName: "SawtoothWave",
    dataType: "Double",
    value: {
      get: () =>
        new Variant({ dataType: DataType.Double, value: sawtoothValue }),
    },
  });

  // Triangle wave
  let triangleValue = 0;
  let triangleDirection = 1;
  namespace.addVariable({
    componentOf: simulationFolder,
    browseName: "TriangleWave",
    dataType: "Double",
    value: {
      get: () =>
        new Variant({ dataType: DataType.Double, value: triangleValue }),
    },
  });

  // Timestamp - current server time
  namespace.addVariable({
    componentOf: simulationFolder,
    browseName: "CurrentTime",
    dataType: "DateTime",
    value: {
      get: () => new Variant({ dataType: DataType.DateTime, value: new Date() }),
    },
  });

  // =====================
  // Static Data Variables
  // =====================

  namespace.addVariable({
    componentOf: staticDataFolder,
    browseName: "ServerName",
    dataType: "String",
    value: {
      get: () =>
        new Variant({
          dataType: DataType.String,
          value: "OpcScope Test Server",
        }),
    },
  });

  const startTime = new Date();
  namespace.addVariable({
    componentOf: staticDataFolder,
    browseName: "StartTime",
    dataType: "DateTime",
    value: {
      get: () => new Variant({ dataType: DataType.DateTime, value: startTime }),
    },
  });

  namespace.addVariable({
    componentOf: staticDataFolder,
    browseName: "Version",
    dataType: "String",
    value: {
      get: () => new Variant({ dataType: DataType.String, value: "1.0.0" }),
    },
  });

  namespace.addVariable({
    componentOf: staticDataFolder,
    browseName: "PiValue",
    dataType: "Double",
    value: {
      get: () => new Variant({ dataType: DataType.Double, value: Math.PI }),
    },
  });

  // =====================
  // Writable Variables
  // =====================

  let writableString = "Hello OpcScope";
  namespace.addVariable({
    componentOf: writableFolder,
    browseName: "WritableString",
    dataType: "String",
    value: {
      get: () =>
        new Variant({ dataType: DataType.String, value: writableString }),
      set: (variant) => {
        writableString = variant.value;
        return StatusCodes.Good;
      },
    },
  });

  let writableNumber = 42;
  namespace.addVariable({
    componentOf: writableFolder,
    browseName: "WritableNumber",
    dataType: "Int32",
    value: {
      get: () =>
        new Variant({ dataType: DataType.Int32, value: writableNumber }),
      set: (variant) => {
        writableNumber = variant.value;
        return StatusCodes.Good;
      },
    },
  });

  let writableDouble = 3.14159;
  namespace.addVariable({
    componentOf: writableFolder,
    browseName: "WritableDouble",
    dataType: "Double",
    value: {
      get: () =>
        new Variant({ dataType: DataType.Double, value: writableDouble }),
      set: (variant) => {
        writableDouble = variant.value;
        return StatusCodes.Good;
      },
    },
  });

  let toggleBoolean = false;
  namespace.addVariable({
    componentOf: writableFolder,
    browseName: "ToggleBoolean",
    dataType: "Boolean",
    value: {
      get: () =>
        new Variant({ dataType: DataType.Boolean, value: toggleBoolean }),
      set: (variant) => {
        toggleBoolean = variant.value;
        return StatusCodes.Good;
      },
    },
  });

  // Setpoint that affects simulation
  let setpoint = 50;
  namespace.addVariable({
    componentOf: writableFolder,
    browseName: "Setpoint",
    dataType: "Double",
    value: {
      get: () => new Variant({ dataType: DataType.Double, value: setpoint }),
      set: (variant) => {
        setpoint = variant.value;
        return StatusCodes.Good;
      },
    },
  });

  // Process value that follows setpoint with some lag
  let processValue = 50;
  namespace.addVariable({
    componentOf: simulationFolder,
    browseName: "ProcessValue",
    dataType: "Double",
    value: {
      get: () =>
        new Variant({ dataType: DataType.Double, value: processValue }),
    },
  });

  // =====================
  // Data Type Examples
  // =====================

  namespace.addVariable({
    componentOf: dataTypesFolder,
    browseName: "ByteValue",
    dataType: "Byte",
    value: {
      get: () => new Variant({ dataType: DataType.Byte, value: 255 }),
    },
  });

  namespace.addVariable({
    componentOf: dataTypesFolder,
    browseName: "Int16Value",
    dataType: "Int16",
    value: {
      get: () => new Variant({ dataType: DataType.Int16, value: -32768 }),
    },
  });

  namespace.addVariable({
    componentOf: dataTypesFolder,
    browseName: "UInt16Value",
    dataType: "UInt16",
    value: {
      get: () => new Variant({ dataType: DataType.UInt16, value: 65535 }),
    },
  });

  namespace.addVariable({
    componentOf: dataTypesFolder,
    browseName: "Int32Value",
    dataType: "Int32",
    value: {
      get: () => new Variant({ dataType: DataType.Int32, value: -2147483648 }),
    },
  });

  namespace.addVariable({
    componentOf: dataTypesFolder,
    browseName: "UInt32Value",
    dataType: "UInt32",
    value: {
      get: () => new Variant({ dataType: DataType.UInt32, value: 4294967295 }),
    },
  });

  namespace.addVariable({
    componentOf: dataTypesFolder,
    browseName: "Int64Value",
    dataType: "Int64",
    value: {
      get: () => new Variant({ dataType: DataType.Int64, value: [0, 1] }),
    },
  });

  namespace.addVariable({
    componentOf: dataTypesFolder,
    browseName: "FloatValue",
    dataType: "Float",
    value: {
      get: () => new Variant({ dataType: DataType.Float, value: 3.14 }),
    },
  });

  namespace.addVariable({
    componentOf: dataTypesFolder,
    browseName: "DoubleValue",
    dataType: "Double",
    value: {
      get: () =>
        new Variant({ dataType: DataType.Double, value: 3.141592653589793 }),
    },
  });

  namespace.addVariable({
    componentOf: dataTypesFolder,
    browseName: "BooleanTrue",
    dataType: "Boolean",
    value: {
      get: () => new Variant({ dataType: DataType.Boolean, value: true }),
    },
  });

  namespace.addVariable({
    componentOf: dataTypesFolder,
    browseName: "BooleanFalse",
    dataType: "Boolean",
    value: {
      get: () => new Variant({ dataType: DataType.Boolean, value: false }),
    },
  });

  // =====================
  // Update simulation values
  // =====================

  setInterval(() => {
    counter++;
    randomValue = Math.random() * 100;
    sineAngle += 0.1;

    // Sawtooth wave
    sawtoothValue += 2;
    if (sawtoothValue >= 100) sawtoothValue = 0;

    // Triangle wave
    triangleValue += triangleDirection * 2;
    if (triangleValue >= 100) triangleDirection = -1;
    if (triangleValue <= 0) triangleDirection = 1;

    // Process value follows setpoint with lag
    processValue += (setpoint - processValue) * 0.1;
  }, 1000);

  await server.start();

  console.log("========================================");
  console.log("OpcScope Test Server Started");
  console.log("========================================");
  console.log("Endpoint URL:", server.getEndpointUrl());
  console.log("");
  console.log("Available nodes:");
  console.log("");
  console.log("Simulation/");
  console.log("  - Counter (Int32) - increments every second");
  console.log("  - RandomValue (Double) - random 0-100");
  console.log("  - SineWave (Double) - oscillates -1 to 1");
  console.log("  - CosineWave (Double) - oscillates -1 to 1");
  console.log("  - SawtoothWave (Double) - 0 to 100 repeating");
  console.log("  - TriangleWave (Double) - 0 to 100 and back");
  console.log("  - CurrentTime (DateTime) - server time");
  console.log("  - ProcessValue (Double) - follows Setpoint");
  console.log("");
  console.log("StaticData/");
  console.log("  - ServerName (String)");
  console.log("  - StartTime (DateTime)");
  console.log("  - Version (String)");
  console.log("  - PiValue (Double)");
  console.log("");
  console.log("Writable/");
  console.log("  - WritableString (String)");
  console.log("  - WritableNumber (Int32)");
  console.log("  - WritableDouble (Double)");
  console.log("  - ToggleBoolean (Boolean)");
  console.log("  - Setpoint (Double) - affects ProcessValue");
  console.log("");
  console.log("DataTypes/");
  console.log("  - ByteValue, Int16Value, UInt16Value");
  console.log("  - Int32Value, UInt32Value, Int64Value");
  console.log("  - FloatValue, DoubleValue");
  console.log("  - BooleanTrue, BooleanFalse");
  console.log("");
  console.log("Press Ctrl+C to stop");
})();
