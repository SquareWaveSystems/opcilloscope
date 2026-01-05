# OpcScope Project Guide

## Overview
OpcScope is a terminal-based OPC UA client/monitor application built with .NET 8, Terminal.Gui v2, and LibUA (nauful-LibUA-core).

## Build & Run

```bash
# Build
dotnet build

# Run
dotnet run

# Run tests
dotnet test

# Or use the test script
./scripts/run-tests.sh --all
```

## Project Structure

```
OpcScope/
├── App/
│   ├── MainWindow.cs          # Main UI layout with panels
│   ├── Views/                  # UI view components
│   │   ├── AddressSpaceView.cs # TreeView for OPC UA nodes
│   │   ├── MonitoredItemsView.cs # TableView for subscribed items
│   │   ├── NodeDetailsView.cs  # Node attribute display
│   │   └── LogView.cs          # Application log display
│   └── Dialogs/                # Modal dialogs
│       ├── ConnectDialog.cs
│       ├── WriteValueDialog.cs
│       └── SettingsDialog.cs
├── OpcUa/
│   ├── OpcUaClientWrapper.cs   # LibUA client wrapper
│   ├── NodeBrowser.cs          # Address space navigation
│   ├── SubscriptionManager.cs  # Polling-based value monitoring
│   └── Models/
│       ├── BrowsedNode.cs      # Address space node model
│       └── MonitoredNode.cs    # Monitored item model
├── Utilities/
│   ├── Logger.cs               # In-app logging
│   └── UiThread.cs             # Thread marshalling for UI
├── tests/                      # Unit tests (xunit)
├── test-server/                # Node-OPCUA test server
├── packages/                   # Local NuGet packages
└── scripts/
    ├── download-packages.sh    # Offline package download
    └── run-tests.sh            # Test runner
```

## Key Technical Details

### Terminal.Gui v2 API
- Use `Shortcut` instead of `StatusItem` for status bar items
- Use `Button.Accepting` event instead of `Button.Accept`
- Use `Height = n` instead of `Dim.Sized(n)`
- Use `SetNeedsLayout()` or `Update()` instead of `SetNeedsDisplay()`
- `ListView.SetSource()` requires `ObservableCollection<T>`
- Use `Application.Invoke()` for thread marshalling (no MainLoop)

### LibUA API
- Use `NodeIdNetType` instead of `NodeIdType`
- `StatusCode` is a struct, not an enum - cast to/from uint
- `ExpandedNodeId` needs manual conversion to `NodeId`:
  ```csharp
  new NodeId(expandedNodeId.NamespaceIndex, expandedNodeId.NumericIdentifier)
  ```
- LibUA doesn't have a working Publish mechanism - use polling via Read instead

### NuGet Configuration
The project uses a local package source (`./packages/`) due to network restrictions. To add new packages:
1. Download .nupkg files to the packages directory
2. Run `dotnet restore`

### Test Server
Start the Node-OPCUA test server for integration testing:
```bash
cd test-server
npm start
```
Server runs at `opc.tcp://localhost:4840/UA/OpcScopeTest`

Available test nodes:
- `Simulation/Counter` - Int32, increments every second
- `Simulation/RandomValue` - Double, random 0-100
- `Simulation/SineWave` - Double, oscillating value
- `Simulation/WritableString` - String, writable
- `Simulation/WritableNumber` - Int32, writable
- `StaticData/*` - Read-only static values

## Common Issues

1. **NuGet restore fails**: Use local package source or `scripts/download-packages.sh`
2. **Tests fail with Xunit errors in main project**: Ensure `tests/**` is excluded in OpcScope.csproj
3. **UI thread exceptions**: Always use `Application.Invoke()` for UI updates from background threads
