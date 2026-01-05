# OpcScope Test Server

A Node-OPCUA test server for OpcScope development and testing.

## Setup

```bash
cd test-server
npm install
```

## Run

```bash
npm start
```

The server will start on `opc.tcp://localhost:4840/UA/TestServer`.

## Available Nodes

### Simulation/ (values update every second)
- `Counter` (Int32) - increments every second
- `RandomValue` (Double) - random value 0-100
- `SineWave` (Double) - oscillates between -1 and 1
- `CosineWave` (Double) - oscillates between -1 and 1
- `SawtoothWave` (Double) - goes from 0 to 100 and resets
- `TriangleWave` (Double) - goes from 0 to 100 and back
- `CurrentTime` (DateTime) - current server time
- `ProcessValue` (Double) - follows Setpoint with lag

### StaticData/ (read-only)
- `ServerName` (String) - "OpcScope Test Server"
- `StartTime` (DateTime) - when server started
- `Version` (String) - "1.0.0"
- `PiValue` (Double) - Pi constant

### Writable/
- `WritableString` (String)
- `WritableNumber` (Int32)
- `WritableDouble` (Double)
- `ToggleBoolean` (Boolean)
- `Setpoint` (Double) - affects ProcessValue in Simulation

### DataTypes/ (various OPC UA data types)
- `ByteValue`, `Int16Value`, `UInt16Value`
- `Int32Value`, `UInt32Value`, `Int64Value`
- `FloatValue`, `DoubleValue`
- `BooleanTrue`, `BooleanFalse`
