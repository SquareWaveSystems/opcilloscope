# opcilloscope

**A lightweight, cross-platform OPC UA client for the terminal heads.**

Browse, monitor, and subscribe to industrial automation data right from your terminal. No bloated GUI, no complex setup, no license fees.

[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)
[![Terminal.Gui](https://img.shields.io/badge/UI-Terminal.Gui-blue)](https://github.com/gui-cs/Terminal.Gui)

---

## Why opcilloscope?

Cause it was fun to build, but also...

| Traditional OPC Clients | opcilloscope |
|------------------------|--------------|
| Heavy desktop apps, potentially with licensing | Single portable binary, MIT licensed |
| Minutes to install | `curl | bash` and you're running |
| Resource-hungry GUIs | ~50MB RAM, runs anywhere |
| Windows-only | Windows, Linux, macOS (x64 & ARM) |
| Click-heavy workflows | Keyboard driven, mouse support |

### Use Cases

- **Commissioning**: Quickly verify PLC tags are publishing correctly
- **Troubleshooting**: Monitor live values during fault diagnosis
- **Integration Testing**: Validate OPC UA server configurations
- **Documentation**: Export snapshots for handover reports

---

## Features

- **Browse** — Lazily explore the OPC UA address space. Expand only what you need.
- **Monitor** — Subscribe to variables with `Enter`. Real-time updates via OPC UA subscriptions, not polling.
- **Inspect** — Full node attributes: Description, DataType, AccessLevel, ValueRank.
- **Scope** — Real-time multi-signal oscilloscope view (up to 5 signals).
- **Record** — Export monitored values to CSV for analysis or documentation.
- **Configure** — Save/load connection and subscription configs for recurring tasks.
- **Themes** — Dark and light themes to match your environment.

---

## Quickstart (User)

**Linux / macOS:**
```bash
curl -fsSL https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/install.sh | bash
```

**Windows (PowerShell):**
```powershell
irm https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/install.ps1 | iex
```

Or grab a binary from [GitHub Releases](https://github.com/SquareWaveSystems/opcilloscope/releases).

Then connect to any OPC UA server:
```bash
opcilloscope
```

### Uninstall

**Linux / macOS:**
```bash
curl -fsSL https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/uninstall.sh | bash
```

Or manually:
```bash
rm ~/.local/bin/opcilloscope
rm -rf ~/.config/opcilloscope/  # optional: remove config files
```

**Windows (PowerShell):**
```powershell
Remove-Item "$env:LOCALAPPDATA\Opcilloscope" -Recurse -Force
```

If you installed to a custom directory (`OPCILLOSCOPE_INSTALL_DIR`), replace the paths above with your custom install location.

---

## Quickstart (Developer)

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/SquareWaveSystems/opcilloscope.git
cd opcilloscope
dotnet build
dotnet run
```

Start the built-in test server (Counter, SineWave, RandomValue, writable nodes):
```bash
dotnet run --project Tests/Opcilloscope.TestServer
# Starts at opc.tcp://localhost:4840
```

Run tests:
```bash
dotnet test
```

---

## OPC UA Test Servers

**Built-in test server** (Counter, SineWave, RandomValue, writable nodes):
```bash
dotnet run --project Tests/Opcilloscope.TestServer
# Starts at opc.tcp://localhost:4840
```

**Public servers** (no setup required):

| Server | Endpoint URL |
|--------|--------------|
| OPC UA Server | `opc.tcp://opcuaserver.com:48010` |
| Eclipse Milo | `opc.tcp://milo.digitalpetri.com:62541/milo` |

**Docker** ([Microsoft OPC PLC](https://mcr.microsoft.com/iotedge/opc-plc)):
```bash
docker run -p 50000:50000 mcr.microsoft.com/iotedge/opc-plc:latest \
  --autoaccept --unsecuretransport
# Connect to opc.tcp://localhost:50000
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
