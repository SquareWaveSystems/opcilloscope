/**
 * OPC UA Test Server for OpcScope
 * Provides a sample address space for testing the OpcScope client.
 */

const {
    OPCUAServer,
    Variant,
    DataType,
    StatusCodes
} = require("node-opcua");

let running = true;

async function startServer() {
    console.log("Starting OPC UA Test Server...");

    const server = new OPCUAServer({
        port: 4840,
        resourcePath: "/UA/OpcScopeTest",
        buildInfo: {
            productName: "OpcScope Test Server",
            buildNumber: "1.0.0",
            buildDate: new Date()
        }
    });

    await server.initialize();

    const addressSpace = server.engine.addressSpace;
    const namespace = addressSpace.getOwnNamespace();

    // Create simulation folder
    const simulationFolder = namespace.addFolder("ObjectsFolder", {
        browseName: "Simulation"
    });

    // Counter variable (increments every second)
    let counterValue = 0;
    namespace.addVariable({
        componentOf: simulationFolder,
        browseName: "Counter",
        dataType: "Int32",
        value: {
            get: () => new Variant({ dataType: DataType.Int32, value: counterValue })
        }
    });

    // Random value variable (random double)
    let randomValue = Math.random() * 100;
    namespace.addVariable({
        componentOf: simulationFolder,
        browseName: "RandomValue",
        dataType: "Double",
        value: {
            get: () => new Variant({ dataType: DataType.Double, value: randomValue })
        }
    });

    // Sine wave variable
    let sineValue = 0;
    namespace.addVariable({
        componentOf: simulationFolder,
        browseName: "SineWave",
        dataType: "Double",
        value: {
            get: () => new Variant({ dataType: DataType.Double, value: sineValue })
        }
    });

    // Writable variable
    let writableValue = "Hello OpcScope";
    namespace.addVariable({
        componentOf: simulationFolder,
        browseName: "WritableString",
        dataType: "String",
        value: {
            get: () => new Variant({ dataType: DataType.String, value: writableValue }),
            set: (variant) => {
                writableValue = variant.value;
                console.log(`WritableString updated to: ${writableValue}`);
                return StatusCodes.Good;
            }
        }
    });

    // Boolean toggle variable
    let boolValue = false;
    namespace.addVariable({
        componentOf: simulationFolder,
        browseName: "ToggleBoolean",
        dataType: "Boolean",
        value: {
            get: () => new Variant({ dataType: DataType.Boolean, value: boolValue }),
            set: (variant) => {
                boolValue = variant.value;
                console.log(`ToggleBoolean updated to: ${boolValue}`);
                return StatusCodes.Good;
            }
        }
    });

    // Writable numeric variable
    let numericValue = 42;
    namespace.addVariable({
        componentOf: simulationFolder,
        browseName: "WritableNumber",
        dataType: "Int32",
        value: {
            get: () => new Variant({ dataType: DataType.Int32, value: numericValue }),
            set: (variant) => {
                numericValue = variant.value;
                console.log(`WritableNumber updated to: ${numericValue}`);
                return StatusCodes.Good;
            }
        }
    });

    // Static variables for testing
    const staticFolder = namespace.addFolder("ObjectsFolder", {
        browseName: "StaticData"
    });

    namespace.addVariable({
        componentOf: staticFolder,
        browseName: "ServerName",
        dataType: "String",
        value: new Variant({ dataType: DataType.String, value: "OpcScope Test Server" })
    });

    namespace.addVariable({
        componentOf: staticFolder,
        browseName: "StartTime",
        dataType: "DateTime",
        value: new Variant({ dataType: DataType.DateTime, value: new Date() })
    });

    namespace.addVariable({
        componentOf: staticFolder,
        browseName: "Version",
        dataType: "String",
        value: new Variant({ dataType: DataType.String, value: "1.0.0" })
    });

    // Array variable
    namespace.addVariable({
        componentOf: staticFolder,
        browseName: "ArrayOfInts",
        dataType: "Int32",
        valueRank: 1,
        arrayDimensions: [5],
        value: new Variant({
            dataType: DataType.Int32,
            arrayType: 1, // Array
            value: [1, 2, 3, 4, 5]
        })
    });

    // Start the server
    await server.start();

    const endpointUrl = server.endpoints[0].endpointDescriptions()[0].endpointUrl;
    console.log("Server is running at:", endpointUrl);
    console.log("\nAvailable nodes:");
    console.log("  - Simulation/Counter (Int32, changes every second)");
    console.log("  - Simulation/RandomValue (Double, changes every second)");
    console.log("  - Simulation/SineWave (Double, changes every second)");
    console.log("  - Simulation/WritableString (String, writable)");
    console.log("  - Simulation/ToggleBoolean (Boolean, writable)");
    console.log("  - Simulation/WritableNumber (Int32, writable)");
    console.log("  - StaticData/ServerName (String, read-only)");
    console.log("  - StaticData/StartTime (DateTime, read-only)");
    console.log("  - StaticData/Version (String, read-only)");
    console.log("  - StaticData/ArrayOfInts (Int32[], read-only)");
    console.log("\nPress Ctrl+C to stop the server.");

    // Update simulation values every second
    let tick = 0;
    const updateInterval = setInterval(() => {
        if (!running) return;
        tick++;
        counterValue++;
        randomValue = Math.random() * 100;
        sineValue = Math.sin(tick * 0.1) * 50 + 50;
    }, 1000);

    // Handle graceful shutdown
    process.on("SIGINT", async () => {
        console.log("\nShutting down server...");
        running = false;
        clearInterval(updateInterval);
        await server.shutdown();
        console.log("Server stopped.");
        process.exit(0);
    });

    process.on("SIGTERM", async () => {
        console.log("\nShutting down server...");
        running = false;
        clearInterval(updateInterval);
        await server.shutdown();
        console.log("Server stopped.");
        process.exit(0);
    });

    return server;
}

startServer().catch((err) => {
    console.error("Error starting server:", err);
    process.exit(1);
});
