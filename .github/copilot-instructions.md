# OpcScope - GitHub Copilot Instructions

## Project Overview
OpcScope is a terminal-based OPC UA client/monitor application built with:
- **.NET 10**
- **Terminal.Gui v2** for the UI
- **OPC Foundation Client SDK** (`OPCFoundation.NetStandard.Opc.Ua.Client`)

## Build Commands
```bash
dotnet build      # Build project
dotnet run        # Run application
dotnet test       # Run tests
```

## Project Architecture

### Directory Structure
- `App/` - UI components (MainWindow, Views, Dialogs)
- `OpcUa/` - OPC UA client logic (Session wrapper, Browser, SubscriptionManager)
- `Utilities/` - Helper classes (Logger, UiThread)
- `tests/` - xUnit tests with in-process OPC UA test server

### Key Classes
- **MainWindow.cs** - Main UI layout with panels
- **OpcUaClientWrapper.cs** - OPC Foundation Session wrapper
- **NodeBrowser.cs** - Address space navigation
- **SubscriptionManager.cs** - OPC UA Subscription management with Publish/Subscribe
- **TestServer.cs** - In-process OPC UA server for testing

## Coding Guidelines

### Terminal.Gui v2 API
When working with Terminal.Gui v2, use these patterns:

```csharp
// Status bar items
new Shortcut() { ... }  // NOT StatusItem

// Button events
button.Accepting += OnAccept;  // NOT Button.Accept

// Layout
Height = 10  // NOT Dim.Sized(10)

// Refresh
SetNeedsLayout()  // OR Update(), NOT SetNeedsDisplay()

// ListView
ObservableCollection<T>  // Required for ListView.SetSource()

// Thread marshalling
Application.Invoke(() => {
    // UI updates here
});
```

### OPC Foundation SDK Patterns

#### Endpoint Discovery
```csharp
// DiscoveryClient.Create requires EndpointConfiguration, not ApplicationConfiguration
var endpointConfig = EndpointConfiguration.Create(config);
using var client = DiscoveryClient.Create(uri, endpointConfig);
var endpoints = await client.GetEndpointsAsync(null);

// Valid DiscoveryClient.Create overloads:
// - DiscoveryClient.Create(Uri discoveryUrl)
// - DiscoveryClient.Create(Uri discoveryUrl, EndpointConfiguration configuration)
// - DiscoveryClient.Create(ApplicationConfiguration application, Uri discoveryUrl)
```

#### Server Setup (Async API)
```csharp
// Use async API variants for server setup
await config.ValidateAsync(ApplicationType.Server);
_application = new ApplicationInstance(config, null);
var hasAppCertificate = await _application.CheckApplicationInstanceCertificatesAsync(silent: true);
await _application.StartAsync(_server);
await _server.StopAsync();
```

#### Session Creation
```csharp
var session = await Session.Create(
    config,
    endpoint,
    false,
    "SessionName",
    60000,
    new UserIdentity(new AnonymousIdentityToken()),
    null
);
```

#### Subscription with MonitoredItems
```csharp
// Create subscription
var subscription = new Subscription(session.DefaultSubscription) {
    PublishingInterval = 1000,
    PublishingEnabled = true
};
session.AddSubscription(subscription);
subscription.Create();

// Add monitored item
var monitoredItem = new MonitoredItem(subscription.DefaultItem) {
    StartNodeId = nodeId,
    AttributeId = Attributes.Value,
    SamplingInterval = 500
};
monitoredItem.Notification += OnNotification;
subscription.AddItem(monitoredItem);
subscription.ApplyChanges();
```

#### NodeId Usage
```csharp
// Constructor patterns
new NodeId(uint identifier)
new NodeId(ushort namespaceIndex, uint identifier)

// Root folder
ObjectIds.RootFolder  // ns=0;i=84

// ExpandedNodeId conversion
ExpandedNodeId.ToNodeId(expandedNodeId, session.NamespaceUris)
```

#### Status Codes
```csharp
StatusCode.Code  // Returns uint value
StatusCodes.Good
StatusCodes.BadUnexpectedError
```

#### Attributes
```csharp
Attributes.Value
Attributes.DataType
Attributes.AccessLevel
```

### Testing Patterns

#### Integration Test Base Class
```csharp
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
```

#### Test Server Fixture (Collection)
```csharp
[Collection("TestServer")]
public class OtherTests
{
    private readonly TestServerFixture _fixture;
    public OtherTests(TestServerFixture fixture) => _fixture = fixture;
}
```

### Available Test Nodes

**Simulation folder** (updates every second):
- `Counter` - Int32
- `RandomValue` - Double (0-100)
- `SineWave` - Double
- `WritableString` - String (writable)
- `ToggleBoolean` - Boolean (writable)
- `WritableNumber` - Int32 (writable)

**StaticData folder** (read-only):
- `ServerName` - String
- `StartTime` - DateTime
- `Version` - String
- `ArrayOfInts` - Int32[]

## Thread Safety

⚠️ **Critical:** OPC Foundation callbacks arrive on background threads. Always use `Application.Invoke()` for UI updates:

```csharp
monitoredItem.Notification += (item, e) => {
    Application.Invoke(() => {
        // Safe to update UI here
        label.Text = newValue;
    });
};
```

## NuGet Configuration

The project uses a **dual-source NuGet configuration** that works in both CI and restricted network environments:

**How it works:**
- `NuGet.Config` lists **nuget.org first** (primary) and **local packages second** (fallback)
- GitHub Actions CI uses nuget.org directly (network available)
- Restricted environments (corporate proxies) fall back to local packages

**If `dotnet restore` fails due to proxy/network issues:**
```bash
# Download packages via curl (handles proxies better than .NET HttpClient)
./scripts/download-packages.sh

# Then restore using local packages
dotnet restore
```

**To add new packages:**
1. Add package reference to `.csproj`
2. Run `dotnet restore` (uses nuget.org if available)
3. If proxy blocks nuget.org, download `.nupkg` to `./packages/` directory

### Required Packages
- `OPCFoundation.NetStandard.Opc.Ua.Client` - Client session and subscription
- `OPCFoundation.NetStandard.Opc.Ua.Core` - Core types
- `OPCFoundation.NetStandard.Opc.Ua.Configuration` - Application configuration
- `OPCFoundation.NetStandard.Opc.Ua.Security.Certificates` - Certificate management
- `OPCFoundation.NetStandard.Opc.Ua.Server` - Server implementation (tests only)

## Common Pitfalls

1. **NuGet restore fails** - Run `./scripts/download-packages.sh` to download packages via curl, then restore
2. **Tests fail with Xunit errors in main project** - Ensure `tests/**` is excluded in OpcScope.csproj
3. **UI thread exceptions** - Always use `Application.Invoke()` for UI updates from background threads
4. **Ambiguous NodeBrowser reference** - OPC Foundation has its own `Browser` class; use fully qualified names
5. **Certificate validation errors** - Set `AutoAcceptUntrustedCertificates = true` in SecurityConfiguration for development

## Security Notes

For development environments:
```csharp
config.SecurityConfiguration.AutoAcceptUntrustedCertificates = true;
```

## Naming Conventions

- Use descriptive names for OPC UA nodes and variables
- Follow C# naming conventions (PascalCase for classes/methods, camelCase for parameters)
- Prefix UI callbacks with `On` (e.g., `OnConnectClicked`, `OnNotification`)
