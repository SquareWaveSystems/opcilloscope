# OpcScope

A lightweight terminal-based OPC UA client for browsing, monitoring, and subscribing to industrial automation data. Built with .NET 10, Terminal.Gui, and OPC Foundation UA-.NETStandard.

![Terminal UI](https://img.shields.io/badge/UI-Terminal.Gui-blue)
![.NET 10](https://img.shields.io/badge/.NET-10.0-purple)
![License](https://img.shields.io/badge/License-MIT-green)

## Features

- **Connection Management**: Connect to OPC UA servers via endpoint URL with anonymous authentication
- **Address Space Browser**: TreeView with lazy-loading for efficient navigation of large address spaces
- **Live Monitoring**: Real-time value updates via OPC UA subscriptions (not polling)
- **Subscribe/Unsubscribe**: Add variable nodes to monitor with Enter key, remove with Delete key
- **Event Log**: Color-coded log panel showing connection events, errors, and subscription changes
- **Node Details Panel**: View full node attributes (Description, AccessLevel, ValueRank, DataType)
- **Export to CSV**: Export current monitored values snapshot
- **Settings Dialog**: Configure publishing interval (100-10000ms)

## UI Layout

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ File   View   Connection   Help                                    [Status] │
├───────────────────────────────┬─────────────────────────────────────────────┤
│ Address Space                 │ Monitored Items                             │
│ ─────────────────────────────│──────────────────────────────────────────────│
│ ▾ Root                        │ Name           │ Value    │ Time   │ Status │
│   ▾ Objects                   │────────────────┼──────────┼────────┼────────│
│     ▸ Server                  │ Temperature    │ 47.3     │ 12:34  │ Good   │
│     ▾ MyDevice                │ Pressure       │ 2.41     │ 12:34  │ Good   │
│       ├─ Temperature [Double] │ FlowRate       │ 124.7    │ 12:34  │ Good   │
│       ├─ Pressure [Double]    │ MotorRunning   │ True     │ 12:33  │ Good   │
│       ├─ FlowRate [Double]    │                │          │        │        │
│       └─ MotorRunning [Bool]  │                │          │        │        │
│                               │                │          │        │        │
├───────────────────────────────┴─────────────────────────────────────────────┤
│ Node Details                                                                │
│ NodeId: ns=2;s=Temperature  DataType: Double  AccessLevel: CurrentRead      │
├─────────────────────────────────────────────────────────────────────────────┤
│ Log                                                                         │
│ [12:30:01] Connected to opc.tcp://localhost:4840                            │
│ [12:30:02] Subscription created (ID: 1, Interval: 1000ms)                   │
│ [12:30:15] Subscribed to ns=2;s=Temperature                                 │
└─────────────────────────────────────────────────────────────────────────────┘
 F1 Help │ F5 Refresh │ Enter Subscribe │ Del Unsubscribe │ F10 Menu
```

## Installation

### Quick Install (Recommended)

**Linux / macOS:**
```bash
curl -fsSL https://raw.githubusercontent.com/BrettKinny/OpcScope/main/install.sh | bash
```

**Windows (PowerShell):**
```powershell
irm https://raw.githubusercontent.com/BrettKinny/OpcScope/main/install.ps1 | iex
```

### Download Binary

Download the latest release for your platform from [GitHub Releases](https://github.com/BrettKinny/OpcScope/releases):

| Platform | Download |
|----------|----------|
| Windows x64 | `opcscope-win-x64.zip` |
| Windows ARM64 | `opcscope-win-arm64.zip` |
| Linux x64 | `opcscope-linux-x64.tar.gz` |
| Linux ARM64 | `opcscope-linux-arm64.tar.gz` |
| macOS x64 (Intel) | `opcscope-osx-x64.tar.gz` |
| macOS ARM64 (Apple Silicon) | `opcscope-osx-arm64.tar.gz` |

### Build from Source
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```bash
# Clone the repository
git clone https://github.com/BrettKinny/OpcScope.git
cd OpcScope

# Restore packages and build
dotnet restore
dotnet build

# Run the application
dotnet run
```

### NuGet Package Restore

The project uses a dual-source NuGet configuration that works in both CI and restricted network environments:

- **nuget.org** (primary) - Used by default when network access is available
- **Local packages** (fallback) - Used when nuget.org is inaccessible

**If `dotnet restore` fails due to proxy/network issues:**

```bash
# Download packages using curl (handles proxies better than .NET HttpClient)
./scripts/download-packages.sh

# Then restore will use the local packages as fallback
dotnet restore
```

This setup ensures builds work in GitHub Actions CI (uses nuget.org) and in restricted environments like corporate proxies (uses local packages).

### Command Line Options

```bash
# Auto-connect to a server on startup
dotnet run -- --connect opc.tcp://localhost:4840

# Or simply pass the endpoint URL
dotnet run -- opc.tcp://192.168.1.50:4840
```

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| F1 | Show help |
| F5 | Refresh address space tree |
| F10 | Open menu bar |
| Enter | Subscribe to selected node |
| Delete | Unsubscribe from selected item |
| Ctrl+O | Connect to server |
| Ctrl+Q | Quit application |
| Tab | Move between panels |
| Arrow Keys | Navigate within panel |
| Space | Expand/collapse tree node |

## Project Structure

```
OpcScope/
├── OpcScope.csproj              # Project file with dependencies
├── Program.cs                   # Entry point
├── App/
│   ├── MainWindow.cs           # Main window layout and orchestration
│   ├── Views/
│   │   ├── AddressSpaceView.cs # TreeView for browsing
│   │   ├── MonitoredItemsView.cs # TableView for subscriptions
│   │   ├── NodeDetailsView.cs  # Attribute display panel
│   │   └── LogView.cs          # Event log ListView
│   └── Dialogs/
│       ├── ConnectDialog.cs    # Server connection form
│       ├── WriteValueDialog.cs # Write to node (P1)
│       └── SettingsDialog.cs   # Subscription settings
├── OpcUa/
│   ├── OpcUaClientWrapper.cs   # Wrapper around OPC Foundation Session
│   ├── NodeBrowser.cs          # Address space navigation logic
│   ├── SubscriptionManager.cs  # OPC UA Subscription with server-pushed notifications
│   └── Models/
│       ├── MonitoredNode.cs    # ViewModel for monitored items
│       └── BrowsedNode.cs      # ViewModel for tree nodes
└── Utilities/
    ├── UiThread.cs             # Thread marshalling helpers
    └── Logger.cs               # Simple in-app logger
```

## Dependencies

- **Terminal.Gui v2.x** - Cross-platform terminal UI framework
- **OPC Foundation UA-.NETStandard** - Official OPC UA stack with full subscription support

## Testing

For testing, you can use:

1. **Prosys OPC UA Simulation Server** (free): https://prosysopc.com/products/opc-ua-simulation-server/
2. **Node-OPCUA sample server**: https://github.com/node-opcua/node-opcua

## License

MIT License - see LICENSE file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
