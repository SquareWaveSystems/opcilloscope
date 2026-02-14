# opcilloscope

**A lightweight, cross-platform OPC UA client for the terminal.**

Browse, monitor, and subscribe to industrial automation data right from your terminal. Keyboard-driven with mouse support. No bloated GUI, no complex setup, no license fees.

[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)
[![Terminal.Gui](https://img.shields.io/badge/UI-Terminal.Gui-blue)](https://github.com/gui-cs/Terminal.Gui)

<p align="center">
  <img src="docs/opcilloscope-hero.webp" alt="opcilloscope demo: install, connect, browse, subscribe, scope view" width="600">
</p>

## Why opcilloscope?

| Traditional OPC Clients | opcilloscope |
|------------------------|--------------|
| Heavy desktop apps | Single portable binary |
| Minutes to install | `curl \| bash` and you're running |
| Resource-hungry GUIs | ~40 MB RAM |
| Windows-only | Windows, Linux, macOS (x64 & ARM) |
| Click-heavy workflows | Keyboard-driven, mouse support |

**Use cases:** commissioning (verify PLC tags), troubleshooting (live values during fault diagnosis), integration testing (validate OPC UA server configs), recording (export to CSV for reports).

## Features

- **Browse** — Lazily explore the OPC UA address space. Expand only what you need.
- **Monitor** — Subscribe to variables with `Enter`. Real-time updates via OPC UA pub/sub, not polling.
- **Inspect** — Full node attributes: Description, DataType, AccessLevel, ValueRank.
- **Scope** — Real-time multi-signal oscilloscope (up to 5 signals, 30 s sliding window).
- **Trend Plot** — Single-signal trend view with auto-scaling.
- **Record** — Export monitored values to CSV. Zero data loss — every server-pushed sample is captured.
- **Configure** — Save/load connection and subscription configs (`.cfg` JSON files).
- **Themes** — Dark (default) and light.

<p align="center">
  <img src="docs/theme-dark.png" alt="Dark theme" width="49%">
  <img src="docs/theme-light.png" alt="Light theme" width="49%">
</p>

<p align="center">
  <img src="docs/opcilloscope-demo2.webp" alt="opcilloscope scope view with live sine wave" width="600"><br>
  <em>Scope view — a soothing sine wave</em>
</p>

<details>
<summary><strong>How signal sampling works</strong></summary>

opcilloscope does **not** poll your OPC UA server. The server *pushes* value updates using OPC UA's built-in publish/subscribe mechanism.

```
OPC UA Server
  │  pushes values every 250ms (configurable 100ms–10s)
  ▼
opcilloscope receives value change events
  ├─→ Scope View     — stores every sample (up to 2,000 per signal)
  ├─→ Trend Plot     — stores last 200 samples in a ring buffer
  └─→ CSV Recording  — writes every sample to disk (zero data loss)
```

Data capture and screen rendering are decoupled:

| What | Rate | Details |
|------|------|---------|
| Server → Client updates | ~4 Hz (250 ms) | Default publishing + sampling interval, adjustable in connect dialog |
| Scope / Trend Plot redraw | 10 FPS (100 ms) | Renders whatever samples arrived since last frame |
| CSV recording | Every update | Captures 100% of server-pushed values, flushes every 10 records |

The scope view holds a sliding **30-second window** (zoomable 5 s – 300 s). Display resolution is limited by terminal width — each character cell is one data point.

</details>

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Tab` | Cycle between panes |
| `Enter` | Subscribe to selected node |
| `Delete` | Unsubscribe from selected variable |
| `Space` | Toggle selection / pause scope |
| `S` | Open scope with selected variables |
| `T` | Show trend plot |
| `W` | Write value to node |
| `+` / `-` | Zoom in / out (scope) |
| `Ctrl+O` / `Ctrl+S` | Open / save configuration |
| `Ctrl+R` | Toggle CSV recording |
| `?` | Help |

## Install

**Linux / macOS:**
```bash
curl -fsSL https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/install.sh | bash
```

**Windows (PowerShell):**
```powershell
irm https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/install.ps1 | iex
```

Or grab a binary from [GitHub Releases](https://github.com/SquareWaveSystems/opcilloscope/releases).

Then run:
```bash
opcilloscope
```

<details>
<summary>Linux dependency: ICU libraries</summary>

opcilloscope requires ICU libraries at runtime for globalization support.

```bash
# Debian 13 / Ubuntu 24.04+
sudo apt install libicu72       # or: sudo apt install libicu-dev

# Fedora / RHEL
sudo dnf install libicu
```

</details>

<details>
<summary>Uninstall</summary>

**Linux / macOS:**
```bash
curl -fsSL https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/uninstall.sh | bash
```

Or manually:
```bash
rm ~/.local/bin/opcilloscope
rm -rf ~/.config/opcilloscope/       # optional: remove config files
rm -rf ~/.local/share/opcilloscope/  # optional: remove OPC UA certificates
```

**Windows (PowerShell):**
```powershell
irm https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/uninstall.ps1 | iex
```

Or manually:
```powershell
Remove-Item "$env:LOCALAPPDATA\Opcilloscope" -Recurse -Force   # binary
Remove-Item "$env:LOCALAPPDATA\opcilloscope" -Recurse -Force   # OPC UA certificates
Remove-Item "$env:APPDATA\opcilloscope" -Recurse -Force        # config files
```

If you installed to a custom directory (`OPCILLOSCOPE_INSTALL_DIR`), replace the paths above with your custom install location.

</details>

## Quickstart (Developer)

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/SquareWaveSystems/opcilloscope.git
cd opcilloscope
dotnet build
dotnet run
```

Run tests:
```bash
dotnet test
```

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

**Docker** ([Microsoft OPC PLC](https://github.com/Azure-Samples/iot-edge-opc-plc)):
```bash
docker run -p 50000:50000 mcr.microsoft.com/iotedge/opc-plc:latest \
  --autoaccept --unsecuretransport
# Connect to opc.tcp://localhost:50000
```

## Contributing

Contributions welcome! Please submit an issue or a pull request.

## License

MIT — see [LICENSE](LICENSE).

<p align="center">
  <strong>Built by <a href="https://squarewavesystems.com">Square Wave Systems</a></strong><br>
  <em>Inspired by <a href="https://github.com/jesseduffield/lazygit">lazygit</a></em>
</p>
