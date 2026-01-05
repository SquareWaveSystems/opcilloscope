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
│   └── OpcScope.Tests/
│       ├── Infrastructure/     # In-process OPC UA test server
│       │   ├── TestServer.cs   # Server with ApplicationConfiguration
│       │   ├── TestNodeManager.cs # Custom NodeManager with test nodes
│       │   └── TestServerFixture.cs # xUnit fixture with IAsyncLifetime
│       └── Integration/        # Integration tests
├── test-server/                # Node-OPCUA test server (legacy)
├── packages/                   # Local NuGet packages
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
- `OPCFoundation.NetStandard.Opc.Ua.Client` - Client session and subscription
- `OPCFoundation.NetStandard.Opc.Ua.Core` - Core types and utilities
- `OPCFoundation.NetStandard.Opc.Ua.Configuration` - Application configuration
- `OPCFoundation.NetStandard.Opc.Ua.Security.Certificates` - Certificate management
- `OPCFoundation.NetStandard.Opc.Ua.Server` - Server implementation (tests only)

### In-Process Test Server (Recommended)
The project includes an in-process OPC Foundation test server for integration testing without external dependencies.

```csharp
// Using xUnit IClassFixture for test class
public class MyTests : IntegrationTestBase
{
    public MyTests(TestServerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CanReadValue()
    {
        var nodeId = new NodeId("Counter", (ushort)GetNamespaceIndex());
        var value = await Client!.ReadValueAsync(nodeId);
        Assert.NotNull(value);
    }
}

// Using collection fixture for shared server across classes
[Collection("TestServer")]
public class OtherTests
{
    private readonly TestServerFixture _fixture;
    public OtherTests(TestServerFixture fixture) => _fixture = fixture;
}
```

Key classes:
- `TestServer` - Server with start/stop methods and ApplicationConfiguration
- `TestNodeManager` - Custom NodeManager exposing test nodes in `urn:opcscope:testserver` namespace
- `TestServerFixture` - xUnit fixture implementing `IAsyncLifetime`
- `IntegrationTestBase` - Base class with auto-connected client

### Test Server Nodes
Available test nodes:

**Simulation folder** (values update every second):
- `Counter` - Int32, increments every second
- `RandomValue` - Double, random 0-100
- `SineWave` - Double, oscillating value
- `WritableString` - String, writable
- `ToggleBoolean` - Boolean, writable
- `WritableNumber` - Int32, writable

**StaticData folder** (read-only):
- `ServerName` - String ("OpcScope Test Server")
- `StartTime` - DateTime
- `Version` - String ("1.0.0")
- `ArrayOfInts` - Int32[] ([1, 2, 3, 4, 5])

## Common Issues

1. **NuGet restore fails**: Use local package source or `scripts/download-packages.sh`
2. **Tests fail with Xunit errors in main project**: Ensure `tests/**` is excluded in OpcScope.csproj
3. **UI thread exceptions**: Always use `Application.Invoke()` for UI updates from background threads
4. **Ambiguous NodeBrowser reference**: OPC Foundation has its own `Browser` class - use fully qualified names if needed
5. **Certificate validation errors**: Set `AutoAcceptUntrustedCertificates = true` in SecurityConfiguration for development
