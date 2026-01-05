# OpcScope Project Guide

## Overview
OpcScope is a terminal-based OPC UA client/monitor application built with .NET 8, Terminal.Gui v2, and OPC Foundation Client SDK (`OPCFoundation.NetStandard.Opc.Ua.Client`).

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
│   ├── OpcUaClientWrapper.cs   # OPC Foundation Session wrapper
│   ├── NodeBrowser.cs          # Address space navigation
│   ├── SubscriptionManager.cs  # OPC UA Subscription with Publish/Subscribe
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

### OPC Foundation SDK API
- Uses `Opc.Ua.Client.Session` for connection management
- Uses proper OPC UA Subscriptions with `Subscription` and `MonitoredItem` classes
- MonitoredItem notifications are pushed by the server (not polling)
- `NodeId` constructor: `new NodeId(uint identifier)` or `new NodeId(ushort namespaceIndex, uint identifier)`
- Use `ExpandedNodeId.ToNodeId(expandedNodeId, session.NamespaceUris)` for conversion
- Use `ObjectIds.RootFolder` for the root node (ns=0;i=84)
- `StatusCode.Code` returns the uint value; check with `StatusCodes.Good`, `StatusCodes.BadUnexpectedError`, etc.
- Use `Attributes.Value`, `Attributes.DataType`, etc. for attribute IDs
- Certificate validation: Set `AutoAcceptUntrustedCertificates = true` for development

### Key OPC Foundation Classes
```csharp
// Session creation
var session = await Session.Create(config, endpoint, false, "SessionName", 60000, new UserIdentity(new AnonymousIdentityToken()), null);

// Subscription creation
var subscription = new Subscription(session.DefaultSubscription) {
    PublishingInterval = 1000,
    PublishingEnabled = true
};
session.AddSubscription(subscription);
subscription.Create();

// MonitoredItem creation
var monitoredItem = new MonitoredItem(subscription.DefaultItem) {
    StartNodeId = nodeId,
    AttributeId = Attributes.Value,
    SamplingInterval = 500
};
monitoredItem.Notification += OnNotification;
subscription.AddItem(monitoredItem);
subscription.ApplyChanges();
```

### NuGet Configuration
The project uses a local package source (`./packages/`) due to network restrictions. To add new packages:
1. Download .nupkg files to the packages directory
2. Run `dotnet restore`

Required OPC Foundation packages:
- `OPCFoundation.NetStandard.Opc.Ua.Client`
- `OPCFoundation.NetStandard.Opc.Ua.Core`
- `OPCFoundation.NetStandard.Opc.Ua.Configuration`
- `OPCFoundation.NetStandard.Opc.Ua.Security.Certificates`

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
4. **Ambiguous NodeBrowser reference**: OPC Foundation has its own `Browser` class - use fully qualified names if needed
5. **Certificate validation errors**: Set `AutoAcceptUntrustedCertificates = true` in SecurityConfiguration for development

## Why OPC Foundation over LibUA

The project was refactored from LibUA to OPC Foundation because:
- LibUA lacks proper Publish/Subscribe mechanism (required polling workaround)
- OPC Foundation is the official, certified stack
- Full subscription support with server-pushed notifications
- Better documentation and community support
- More complete OPC UA feature coverage
