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
| Heavy desktop apps | Single self-contained executable |
| Minutes to install | `curl \| bash` and you're running |
| Resource-hungry GUIs | Terminal-native interface |
| Windows-only | Windows, Linux, macOS (x64 & ARM64) |
| Click-heavy workflows | Keyboard-driven, mouse support |

**Use cases:** commissioning (verify PLC tags), troubleshooting (live values during fault diagnosis), integration testing (validate OPC UA server configs), recording (export to CSV for reports).

## Features

- **Browse** — Lazily explore the OPC UA address space. Expand only what you need.
- **Monitor** — Subscribe to variables with `Enter`. Receive OPC UA monitored-item notifications instead of polling values.
- **Inspect** — Full node attributes: Description, DataType, AccessLevel, ValueRank.
- **Scope** — Real-time multi-signal oscilloscope (up to 5 signals, 30 s sliding window).
- **Record** — Export selected monitored variables to CSV. Notifications are queued with full-precision, locale-independent values (ISO 8601 UTC timestamps, `.` decimal separator, arrays as semicolon-joined elements). A bounded queue protects the UI if storage falls behind, and any dropped records are reported when recording stops.
- **Configure** — Save/load connection and subscription configs (`.cfg` JSON files).
- **Themes** — Dark (default), light, and terminal (inherits your terminal's ANSI colour scheme).

<p align="center">
  <img src="docs/pr165/main-dark.png" alt="Dark theme" width="49%">
  <img src="docs/pr165/main-light.png" alt="Light theme" width="49%">
</p>

<p align="center">
  <img src="docs/opcilloscope-demo2.webp" alt="opcilloscope scope view with live sine wave" width="600"><br>
  <em>Scope view — a soothing sine wave</em>
</p>

<details>
<summary><strong>How signal sampling works</strong></summary>

opcilloscope does **not** repeatedly read each value. It creates OPC UA subscriptions and monitored items, then processes the data-change notifications delivered by the server. This is distinct from the OPC UA PubSub transport model.

```
OPC UA Server
  │  samples monitored items (250 ms requested by default)
  │  delivers subscription notifications (250 ms publishing interval by default)
  ▼
opcilloscope receives data-change notifications
  ├─→ Scope View     — retains up to 2,000 numeric samples per signal while open
  └─→ CSV Recording  — queues selected-variable notifications for background writes
```

Data capture and screen rendering are decoupled:

| What | Rate | Details |
|------|------|---------|
| Server sampling | 250 ms requested | Configurable through a saved configuration; the server may revise the interval |
| Subscription publishing | 250 ms by default | Adjustable from 100 ms to 10 s in the connect dialog |
| Scope redraw | 10 FPS (100 ms) | Renders whatever samples arrived since last frame |
| CSV recording | Every accepted notification for selected variables | Uses a 10,000-record bounded queue and flushes every 10 records |

The scope view starts with a sliding **30-second window** (zoomable from 5 s to 300 s). It draws with Unicode braille subcells, so display resolution depends on the terminal's dimensions.

</details>

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Tab` | Cycle between panes |
| `Enter` | Subscribe to selected node |
| `F5` | Refresh address space tree |
| `Delete` | Unsubscribe from selected variable |
| `Space` | Toggle selection / pause scope |
| `S` | Open scope with selected variables |
| `W` | Write value to node |
| `R` | Toggle CSV recording (selected monitored variables) |
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

Release archives include the self-contained executable, the project license, and notices for bundled third-party components. The installers preserve those notices in an app-owned license directory.

The installers require the matching release checksum and refuse an unverified download. On Windows, the default install directory is added to your user `PATH`; a custom `OPCILLOSCOPE_INSTALL_DIR` is treated as shared and leaves `PATH` unchanged.

> **macOS note:** the binaries are unsigned, so archives downloaded with a browser are
> quarantined by Gatekeeper. Either use the curl installer above, or clear the
> quarantine attribute after extracting: `xattr -d com.apple.quarantine <binary>`.

Then run:
```bash
opcilloscope
```

An automatic/omitted or partial security profile requires a `SignAndEncrypt`
endpoint and selects the strongest matching candidate offered by the server.
Explicit `securityMode: "Sign"` opts into signed-but-unencrypted traffic.
Explicit anonymous `securityMode: "None"` opts into unsecured plaintext;
username authentication never permits `None`.

Server certificates that fail validation are rejected by default. For a
development server, `opcilloscope --insecure` disables server certificate
validation for that run; it does not enable plaintext transport. Do not use
this option in production. The connection log reports the trusted-certificate
store path when validation fails.

<details>
<summary>Uninstall</summary>

**Linux / macOS:**
```bash
curl -fsSL https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/uninstall.sh | bash
```

Or manually on Linux:
```bash
rm ~/.local/bin/opcilloscope
rm -rf "${XDG_DATA_HOME:-$HOME/.local/share}/opcilloscope/licenses"
rm -rf "${XDG_CONFIG_HOME:-$HOME/.config}/opcilloscope"  # optional: configs and recent files
rm -rf "${XDG_DATA_HOME:-$HOME/.local/share}/opcilloscope/pki"  # optional: certificates
```

Or manually on macOS:
```bash
rm ~/.local/bin/opcilloscope
rm -rf "$HOME/Library/Application Support/opcilloscope/licenses"
rm -rf "$HOME/Library/Application Support/opcilloscope/configs"  # optional
rm -f "$HOME/Library/Application Support/opcilloscope/recent-files.json"  # optional
rm -rf "$HOME/Library/Application Support/opcilloscope/pki"  # optional: certificates
```

**Windows (PowerShell):**
```powershell
irm https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/uninstall.ps1 | iex
```

Or manually:
```powershell
$installDir = "$env:LOCALAPPDATA\Programs\opcilloscope"
Remove-Item "$installDir\opcilloscope.exe" -Force
Remove-Item "$installDir\opcilloscope-licenses" -Recurse -Force
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
$pathEntries = @($userPath -split ";" | Where-Object { $_.Trim().TrimEnd('\') -ine $installDir.TrimEnd('\') })
[Environment]::SetEnvironmentVariable("Path", ($pathEntries -join ";"), "User")
Remove-Item "$env:APPDATA\opcilloscope" -Recurse -Force             # optional: configs and recent files
Remove-Item "$env:LOCALAPPDATA\opcilloscope\pki" -Recurse -Force    # optional: certificates
```

If you set `OPCILLOSCOPE_INSTALL_DIR`, replace only the executable path above on
Linux/macOS; license notices remain in the platform data directory shown above.
On Windows, replace both `$installDir` executable/license paths with the custom
directory and skip the `PATH`-removal lines; the installer never adds a custom
directory to `PATH`. Configuration and certificate locations do not move. The
uninstall scripts remove only files owned by opcilloscope rather than recursively
deleting a shared custom install directory.

</details>

## Quickstart (Developer)

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/SquareWaveSystems/opcilloscope.git
cd opcilloscope
dotnet build Opcilloscope.sln
dotnet run --project Opcilloscope.csproj
```

Run the cross-platform unit, integration, and component suite:
```bash
dotnet test Opcilloscope.sln
```

On Linux, also exercise a freshly published binary through the real PTY E2E
harness:

```bash
dotnet test Tests/Opcilloscope.E2ETests/Opcilloscope.E2ETests.csproj
```

See [docs/TESTING.md](docs/TESTING.md) for test layers and exact-artifact usage.

## OPC UA Test Servers

**Built-in test server** (Counter, SineWave, RandomValue, writable nodes):
```bash
dotnet run --project Tests/Opcilloscope.TestServer
# Starts at opc.tcp://localhost:4840/UA/OpcilloscopeTest
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

Contributions welcome! Please submit an issue or a pull request, and see [CONTRIBUTING.md](CONTRIBUTING.md) for development and review guidance.

## License

The opcilloscope **source code** is MIT-licensed — see [LICENSE](LICENSE).

Official **binary releases** are self-contained builds that bundle the
[OPC Foundation UA .NET Standard](https://github.com/OPCFoundation/UA-.NETStandard)
stack and other third-party components. The bundled stack version
(1.5.378.156) is distributed by the OPC Foundation under its MIT license;
earlier versions of that stack were dual-licensed GPL-2.0/RCL. See
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for the full list of bundled
components and their licenses.

<p align="center">
  <strong>Built by <a href="https://squarewavesystems.com">Square Wave Systems</a></strong><br>
  <em>Inspired by <a href="https://github.com/jesseduffield/lazygit">lazygit</a></em>
</p>
