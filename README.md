# opcilloscope

**A lightweight OPC UA client for the terminal heads.**

Browse, monitor, and subscribe to industrial automation data right from your terminal. No bloated GUI, no complex setup, no license fees.

[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)
[![Terminal.Gui](https://img.shields.io/badge/UI-Terminal.Gui-blue)](https://github.com/gui-cs/Terminal.Gui)

---

## Why opcilloscope?

Cause it was fun to build, but also...

| Traditional OPC Clients | opcilloscope |
|------------------------|--------------|
| Heavy desktop apps with complex licensing | Single portable binary, MIT licensed |
| Seconds to install | `curl | bash` and you're running |
| Resource-hungry GUIs | ~50MB RAM, runs anywhere |
| Windows-only | Windows, Linux, macOS (x64 & ARM) |
| Click-heavy workflows | Keyboard-driven, mouse support, terminal-native |

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
curl -fsSL https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/install.sh | bash
```

**Windows (PowerShell):**
```powershell
irm https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/install.ps1 | iex
```

### Or Download a Release

Grab the latest from [GitHub Releases](https://github.com/SquareWaveSystems/opcilloscope/releases):

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
git clone https://github.com/SquareWaveSystems/opcilloscope.git
cd opcilloscope
dotnet build
dotnet run
```

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

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

## Use Cases

- **Commissioning**: Quickly verify PLC tags are publishing correctly
- **Troubleshooting**: Monitor live values during fault diagnosis
- **Integration Testing**: Validate OPC UA server configurations
- **Documentation**: Export snapshots for handover reports
- **Remote Monitoring**: Run on headless servers without GUI dependencies

---

## OPC UA Test Servers

Don't have an OPC UA server handy? Here are some options:

**Public servers** (no setup required):

| Server | Endpoint URL |
|--------|--------------|
| OPC UA Server | `opc.tcp://opcuaserver.com:48010` |
| Eclipse Milo | `opc.tcp://milo.digitalpetri.com:62541/milo` |

**Built-in test server:**

```bash
dotnet run --project Tests/Opcilloscope.TestServer
# Starts at opc.tcp://localhost:4840 with simulation nodes
```

**Run your own** with [Microsoft OPC PLC](https://mcr.microsoft.com/iotedge/opc-plc) (Docker):

```bash
docker run -p 50000:50000 mcr.microsoft.com/iotedge/opc-plc:latest \
  --autoaccept --unsecuretransport
# Connect to opc.tcp://localhost:50000
```

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
  Modern automation software
</p>
