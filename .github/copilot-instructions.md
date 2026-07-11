# Opcilloscope - GitHub Copilot Instructions

## Project Overview
Opcilloscope is a terminal-based OPC UA client/monitor application built with:
- **.NET 10**
- **Terminal.Gui v2** for the UI
- **OPC Foundation Client SDK** (`OPCFoundation.NetStandard.Opc.Ua.Client`)

## Naming Convention

The project name is **opcilloscope** (lowercase "o") in all contexts except where .NET naming conventions require PascalCase:

| Context | Name | Example |
|---------|------|---------|
| User-facing text, CLI, URLs | `opcilloscope` | `opcilloscope --help` |
| C# namespaces, classes, projects | `Opcilloscope` | `namespace Opcilloscope.App` |
| File/folder names (code) | `Opcilloscope` | `Opcilloscope.csproj` |
| Config/data directories | `opcilloscope` | Use the platform locations below |
| Release artifacts | `opcilloscope` | `opcilloscope-linux-x64.tar.gz` |

Platform directories:
- Linux configuration: `${XDG_CONFIG_HOME:-$HOME/.config}/opcilloscope/`
- Linux application data: `${XDG_DATA_HOME:-$HOME/.local/share}/opcilloscope/`
- macOS configuration and application data: `~/Library/Application Support/opcilloscope/`
- Windows configuration: `%APPDATA%\opcilloscope\`; certificates: `%LOCALAPPDATA%\opcilloscope\pki\`

## Build Commands
```bash
dotnet build Opcilloscope.sln                 # Build app and tests
dotnet run --project Opcilloscope.csproj      # Run application
dotnet test Opcilloscope.sln                  # Run unit/integration suite
dotnet test Tests/Opcilloscope.E2ETests/Opcilloscope.E2ETests.csproj  # Linux real-PTY E2E
```

## Project Architecture

### Directory Structure
- `App/` - UI components (MainWindow, Views, Dialogs)
- `OpcUa/` - OPC UA client logic (Session wrapper, Browser, SubscriptionManager)
- `Utilities/` - Helper classes (Logger, UiThread)
- `Tests/Opcilloscope.Tests/` - Cross-platform xUnit tests with the in-process OPC UA test server
- `Tests/Opcilloscope.E2ETests/` - Linux-only published-binary PTY tests; intentionally outside `Opcilloscope.sln`

### Key Classes
- **MainWindow.cs** - Main UI layout with panels
- **OpcUaClientWrapper.cs** - OPC Foundation Session wrapper
- **NodeBrowser.cs** - Address space navigation
- **SubscriptionManager.cs** - OPC UA subscriptions and monitored-item notifications (not the OPC UA PubSub transport model)
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

// Thread marshalling through the repository helper
UiThread.Run(() => {
    // UI updates here
});
```

### OPC Foundation SDK Patterns

#### Endpoint Discovery
`DiscoveryClient.Create` and `Session.Create` are obsolete in the current SDK.
Existing wrapper call sites use narrowly scoped `CS0618` pragmas because the
replacement factories need additional telemetry setup. Prefer the repository
wrapper; do not introduce an unsuppressed call or a project-wide suppression.

```csharp
// DiscoveryClient.Create requires EndpointConfiguration, not ApplicationConfiguration
var endpointConfig = EndpointConfiguration.Create(config);
#pragma warning disable CS0618 // Existing wrapper exception: async factory needs telemetry setup
using var client = DiscoveryClient.Create(uri, endpointConfig);
#pragma warning restore CS0618
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
#pragma warning disable CS0618 // Existing wrapper exception: async factory needs telemetry setup
var session = await Session.Create(
    config,
    endpoint,
    false,
    "SessionName",
    60000,
    new UserIdentity(new AnonymousIdentityToken()),
    null
);
#pragma warning restore CS0618
```

#### Subscription with MonitoredItems
```csharp
// Create subscription
var subscription = new Subscription(session.DefaultSubscription) {
    PublishingInterval = 1000,
    PublishingEnabled = true
};
session.AddSubscription(subscription);
await subscription.CreateAsync();

// Add monitored item
var monitoredItem = new MonitoredItem(subscription.DefaultItem) {
    StartNodeId = nodeId,
    AttributeId = Attributes.Value,
    SamplingInterval = 500
};
monitoredItem.Notification += OnNotification;
subscription.AddItem(monitoredItem);
await subscription.ApplyChangesAsync();
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

⚠️ **Critical:** OPC Foundation callbacks arrive on background threads. Always use the repository's `UiThread.Run` helper for UI updates; the legacy static `Application` API is obsolete:

```csharp
monitoredItem.Notification += (item, e) => {
    UiThread.Run(() => {
        // Safe to update UI here
        label.Text = newValue;
    });
};
```

## NuGet Packages

Required packages:
- `OPCFoundation.NetStandard.Opc.Ua.Client` - Client session and subscription
- `OPCFoundation.NetStandard.Opc.Ua.Core` - Core types
- `OPCFoundation.NetStandard.Opc.Ua.Configuration` - Application configuration
- `OPCFoundation.NetStandard.Opc.Ua.Security.Certificates` - Certificate management
- `OPCFoundation.NetStandard.Opc.Ua.Server` - Server implementation (tests only)

## Common Pitfalls

1. **Tests fail with Xunit errors in main project** - Ensure `Tests/**` is excluded in Opcilloscope.csproj
2. **UI thread exceptions** - Always use `UiThread.Run()` for UI updates from background threads
3. **Ambiguous NodeBrowser reference** - OPC Foundation has its own `Browser` class; use fully qualified names
4. **Certificate validation errors** - Fix or trust the server certificate using the path reported by the connection log, or bypass validation with `--insecure` for development only

## Security Notes

An automatic/omitted or partial security profile requires a `SignAndEncrypt`
endpoint and selects the strongest matching candidate. Explicit
`SecurityMode=Sign` opts into signed-but-unencrypted traffic. Explicit
anonymous `SecurityMode=None` opts into unsecured plaintext; username
credentials never permit `None`.

Certificates that fail validation are rejected by default.
`opcilloscope --insecure` may be used for a development run only; it bypasses
certificate validation and never enables plaintext transport. Do not weaken
`SecurityConfiguration` in production code.

## Naming Conventions

- Use descriptive names for OPC UA nodes and variables
- Follow C# naming conventions (PascalCase for classes/methods, camelCase for parameters)
- Prefix UI callbacks with `On` (e.g., `OnConnectClicked`, `OnNotification`)
