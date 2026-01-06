# OpcScope Project Guide

## Overview
OpcScope is a terminal-based OPC UA client/monitor application built with .NET 10, Terminal.Gui v2, and OPC Foundation Client SDK (`OPCFoundation.NetStandard.Opc.Ua.Client`).

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

### DiscoveryClient API
The `DiscoveryClient.Create` method requires `EndpointConfiguration`, not `ApplicationConfiguration`:

```csharp
// Correct usage - create EndpointConfiguration first
var endpointConfig = EndpointConfiguration.Create(config);
using var client = DiscoveryClient.Create(uri, endpointConfig);
var endpoints = await client.GetEndpointsAsync(null);

// Valid DiscoveryClient.Create overloads:
// - DiscoveryClient.Create(Uri discoveryUrl)
// - DiscoveryClient.Create(Uri discoveryUrl, EndpointConfiguration configuration)
// - DiscoveryClient.Create(ApplicationConfiguration application, Uri discoveryUrl)
```

### ApplicationInstance API (Server)
Use the async API variants for OPC UA server setup:

```csharp
// Configuration validation
await config.ValidateAsync(ApplicationType.Server);

// ApplicationInstance creation - use constructor with config
_application = new ApplicationInstance(config, null);

// Certificate check - use async variant
var hasAppCertificate = await _application.CheckApplicationInstanceCertificatesAsync(silent: true);

// Server start/stop - use async variants
await _application.StartAsync(_server);
await _server.StopAsync();
```

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
The project uses a dual-source NuGet configuration:
- **nuget.org** (primary) - Works in CI and normal network environments
- **Local packages** (fallback) - Used when nuget.org is inaccessible due to proxy issues

To add new packages:
1. Add the package reference to the .csproj file
2. Run `dotnet restore` (uses nuget.org)
3. If restore fails due to proxy issues, download the .nupkg to `./packages/` and restore again

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

## Technical Notes

### Thread Safety
OPC Foundation callbacks arrive on background threads. All UI updates are marshalled to the UI thread via `Application.Invoke()`.

### Lazy Loading
The address space tree uses lazy loading - child nodes are only fetched when a parent is expanded, preventing memory issues with large address spaces.

### OPC UA Subscriptions
Uses proper OPC UA Publish/Subscribe with `MonitoredItem.Notification` events - values are pushed by the server, no polling required.

### Error Handling
- Connection errors display in the log panel without crashing
- Automatic reconnection with exponential backoff (1s, 2s, 4s, 8s)
- Graceful handling of bad node IDs and access denied errors

## Common Issues

1. **NuGet restore fails**: Use local package source or `scripts/download-packages.sh`
2. **Tests fail with Xunit errors in main project**: Ensure `tests/**` is excluded in OpcScope.csproj
3. **UI thread exceptions**: Always use `Application.Invoke()` for UI updates from background threads
4. **Ambiguous NodeBrowser reference**: OPC Foundation has its own `Browser` class - use fully qualified names if needed
5. **Certificate validation errors**: Set `AutoAcceptUntrustedCertificates = true` in SecurityConfiguration for development
