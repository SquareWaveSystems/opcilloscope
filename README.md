# opcilloscope

**The lightweight OPC UA client that gets out of your way.**

Browse, monitor, and subscribe to industrial automation data — right from your terminal. No bloated GUI, no complex setup, no license fees.

[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)
[![Terminal.Gui](https://img.shields.io/badge/UI-Terminal.Gui-blue)](https://github.com/gui-cs/Terminal.Gui)

---

## Why opcilloscope?

| Traditional OPC Clients | opcilloscope |
|------------------------|--------------|
| Heavy desktop apps with complex licensing | Single portable binary, MIT licensed |
| Minutes to install and configure | `curl | bash` and you're running |
| Resource-hungry GUIs | ~50MB RAM, runs anywhere |
| Windows-only | Windows, Linux, macOS (x64 & ARM) |
| Click-heavy workflows | Keyboard-driven, terminal-native |

**Built for automation engineers who live in the terminal.**

---

## See It In Action

### Connect & Browse

Connect to any OPC UA server and lazily browse the address space. No loading the entire tree upfront — just expand what you need.

### Real-Time Monitoring

Select variables with `Enter` to subscribe. Values update in real-time via OPC UA subscriptions — not polling. Remove with `Delete`.

### Inspect Node Details

Full attribute visibility: Description, DataType, AccessLevel, ValueRank. See exactly what you're working with.

### Light & Dark Themes

Easy on the eyes in any environment. Toggle themes to match your terminal or preference.

### Export to CSV

Capture a snapshot of your monitored values for documentation, analysis, or handoff.

### Save & Load Configurations

Save your connection and subscriptions to a config file. Load it next time to pick up exactly where you left off — perfect for recurring commissioning tasks.

---

## Quick Start

### One-Line Install

**Linux / macOS:**
```bash
curl -fsSL https://raw.githubusercontent.com/BrettKinny/opcilloscope/main/install.sh | bash
```

**Windows (PowerShell):**
```powershell
irm https://raw.githubusercontent.com/BrettKinny/opcilloscope/main/install.ps1 | iex
```

### Or Download a Release

Grab the latest from [GitHub Releases](https://github.com/BrettKinny/opcilloscope/releases):

| Platform | File |
|----------|------|
| Windows x64 | `opcilloscope-win-x64.zip` |
| Windows ARM64 | `opcilloscope-win-arm64.zip` |
| Linux x64 | `opcilloscope-linux-x64.tar.gz` |
| Linux ARM64 | `opcilloscope-linux-arm64.tar.gz` |
| macOS Intel | `opcilloscope-osx-x64.tar.gz` |
| macOS Apple Silicon | `opcilloscope-osx-arm64.tar.gz` |

### Build From Source

```bash
git clone https://github.com/BrettKinny/opcilloscope.git
cd opcilloscope
dotnet build
dotnet run
```

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

---

## Keyboard Shortcuts

opcilloscope is designed for keyboard-first workflows:

| Key | Action |
|-----|--------|
| `Ctrl+O` | Connect to server |
| `Enter` | Subscribe to selected node |
| `Delete` | Unsubscribe |
| `F5` | Refresh address space |
| `F1` | Help |
| `F10` | Menu |
| `Tab` | Switch panels |
| `Ctrl+Q` | Quit |

---

## Command Line Usage

```bash
# Launch with a saved configuration
opcilloscope config.cfg
opcilloscope --config production.json

# Show help
opcilloscope --help
```

---

## UI Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ File  Connection  View  Theme  Help                                         │
├───────────────────────────────┬─────────────────────────────────────────────┤
│ Address Space                 │ Monitored Variables                         │
│                               │                                             │
│ ▾ Root                        │ Name           │ Value    │ Time   │ Status │
│   ▾ Objects                   │────────────────┼──────────┼────────┼────────│
│     ▸ Server                  │ Temperature    │ 47.3     │ 12:34  │ Good   │
│     ▾ MyDevice                │ Pressure       │ 2.41     │ 12:34  │ Good   │
│       ├─ Temperature [Double] │ FlowRate       │ 124.7    │ 12:34  │ Good   │
│       ├─ Pressure [Double]    │ MotorRunning   │ True     │ 12:33  │ Good   │
│       └─ MotorRunning [Bool]  │                │          │        │        │
├───────────────────────────────┴─────────────────────────────────────────────┤
│ Node Details                                                                │
│ DisplayName: Temperature  │  DataType: Double  │  AccessLevel: Read/Write   │
├─────────────────────────────────────────────────────────────────────────────┤
│ Log                                                                         │
│ [12:30:15] Connected to opc.tcp://localhost:4840                            │
│ [12:30:16] Subscribed to Temperature (ns=2;s=MyDevice.Temperature)          │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Use Cases

- **Commissioning**: Quickly verify PLC tags are publishing correctly
- **Troubleshooting**: Monitor live values during fault diagnosis
- **Integration Testing**: Validate OPC UA server configurations
- **Documentation**: Export snapshots for handover reports
- **Remote Monitoring**: Run on headless servers without GUI dependencies

---

## OPC UA Test Servers

Don't have an OPC UA server handy? Here are several options to get started:

### Public Servers (No Setup Required)

These servers are available on the internet for testing — just connect and go:

| Server | Endpoint URL | Notes |
|--------|--------------|-------|
| OPC UA Server | `opc.tcp://opcuaserver.com:48010` | Great for beginners, no authentication required |
| Eclipse Milo | `opc.tcp://milo.digitalpetri.com:62541/milo` | Supports secured & unsecured connections |
| N3uron Demo | `opc.tcp://89.117.59.81:4840` | Username: `certTest`, Password: `n3uron` |

> **Note:** For secured connections to Milo, upload your client certificate at [milo.digitalpetri.com](http://milo.digitalpetri.com).

### Built-in Test Server

opcilloscope includes an in-process OPC UA test server for development and testing:

```bash
# Run the standalone test server
dotnet run --project tools/Opcilloscope.TestServer

# Server starts at opc.tcp://localhost:4840
```

The test server provides simulation nodes (Counter, SineWave, RandomValue) and writable nodes for testing write operations.

### Run Your Own Server

**Option 1: Microsoft OPC PLC (Docker)**

Microsoft's OPC PLC container is a full-featured simulation server:

```bash
docker run -p 50000:50000 mcr.microsoft.com/iotedge/opc-plc:latest \
  --autoaccept --unsecuretransport
```

Connect to `opc.tcp://localhost:50000`.

**Option 2: Node-OPCUA (npm)**

Spin up a quick server with Node.js:

```bash
npm install -g node-opcua-samples
simple_server
```

Connect to `opc.tcp://localhost:26543`.

**Option 3: Desktop Simulators**

- [Prosys OPC UA Simulation Server](https://prosysopc.com/products/opc-ua-simulation-server/) — Free, feature-rich, Windows/Linux/macOS

---

## Project Structure

```
Opcilloscope/
├── Program.cs                   # Entry point
├── App/
│   ├── MainWindow.cs           # Layout orchestration
│   ├── Views/                  # UI panels
│   └── Dialogs/                # Connection, settings, etc.
├── OpcUa/
│   ├── OpcUaClientWrapper.cs   # Session management
│   ├── SubscriptionManager.cs  # Real-time subscriptions
│   └── Models/                 # ViewModels
└── Utilities/                  # Helpers
```

---

## Contributing

Contributions welcome! Please submit a Pull Request.

---

## License

MIT License — use it however you want.

---

<p align="center">
  <strong>Built by <a href="https://squarewavesystems.com">Square Wave Systems</a></strong><br>
  Modern automation software for mid-market manufacturers
</p>
