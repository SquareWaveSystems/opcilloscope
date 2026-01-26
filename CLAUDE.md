# Opcilloscope Project Guide

## Overview
Opcilloscope is a terminal-based OPC UA client/monitor application built with .NET 10, Terminal.Gui v2, and OPC Foundation Client SDK (`OPCFoundation.NetStandard.Opc.Ua.Client`). It provides real-time browsing, monitoring, and visualization of industrial automation data.

## Naming Convention

The project name is **opcilloscope** (lowercase "o") in all contexts except where .NET naming conventions require PascalCase:

| Context | Name | Example |
|---------|------|---------|
| User-facing text, CLI, URLs | `opcilloscope` | `opcilloscope --help` |
| C# namespaces, classes, projects | `Opcilloscope` | `namespace Opcilloscope.App` |
| File/folder names (code) | `Opcilloscope` | `Opcilloscope.csproj` |
| Config directories (all platforms) | `opcilloscope` | `~/.config/opcilloscope/` |
| Release artifacts | `opcilloscope` | `opcilloscope-linux-x64.tar.gz` |

## Environment Setup

### .NET SDK Installation
If the `dotnet` command is not available, install .NET 10 SDK using Microsoft's install script:

```bash
# Download and run the install script
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 10.0 --install-dir ~/.dotnet

# Add to PATH for the current session
export PATH="$HOME/.dotnet:$PATH"
```

## Build & Run

```bash
# Build
dotnet build

# Run (from repo root)
dotnet run

# Run with a configuration file
dotnet run -- config.cfg
dotnet run -- --config config.cfg

# Run tests
dotnet test
```

## Command-Line Interface

```
Usage: opcilloscope [options] [file]

Options:
  -f, --config <file>   Load configuration file (.cfg or .json)
  -h, --help            Show help message

Examples:
  opcilloscope                           Start with empty configuration
  opcilloscope production.cfg                Load configuration file
  opcilloscope --config config.json      Load configuration file
```

## Project Structure

```
Opcilloscope/
├── Opcilloscope.csproj                 # Main application project
├── Opcilloscope.sln                    # Solution file
├── Program.cs                      # Application entry point with CLI argument parsing
├── CLAUDE.md                       # This file - AI assistant guide
│
├── App/
│   ├── MainWindow.cs               # Main UI layout, menu bar, status bar, event orchestration
│   ├── FocusManager.cs             # Keyboard focus navigation between panes
│   ├── Views/
│   │   ├── AddressSpaceView.cs     # TreeView for OPC UA address space navigation
│   │   ├── MonitoredVariablesView.cs # TableView for subscribed variables with selection
│   │   ├── NodeDetailsView.cs      # Node attribute display panel
│   │   ├── LogView.cs              # Application log display
│   │   ├── ScopeView.cs            # Real-time multi-signal oscilloscope view
│   │   └── TrendPlotView.cs        # Single-signal trend plot view
│   ├── Dialogs/
│   │   ├── ConnectDialog.cs        # Server connection dialog
│   │   ├── WriteValueDialog.cs     # Write value to node dialog
│   │   ├── SettingsDialog.cs       # Application settings dialog
│   │   ├── ScopeDialog.cs          # Multi-signal scope dialog (up to 5 signals)
│   │   ├── TrendPlotDialog.cs      # Single-signal trend plot dialog
│   │   ├── SaveConfigDialog.cs     # Save configuration file dialog
│   │   ├── SaveRecordingDialog.cs  # Save CSV recording dialog
│   │   ├── HelpDialog.cs           # Full help/documentation dialog
│   │   └── QuickHelpDialog.cs      # Quick keyboard shortcuts reference
│   ├── Keybindings/
│   │   ├── Keybinding.cs           # Keybinding model (key, action, context)
│   │   ├── KeybindingContext.cs    # Context enum (Global, AddressSpace, etc.)
│   │   ├── KeybindingManager.cs    # Keybinding registration and lookup
│   │   └── DefaultKeybindings.cs   # Default keybinding definitions
│   └── Themes/
│       ├── AppTheme.cs             # Abstract base theme class
│       ├── DarkTheme.cs            # Dark theme implementation
│       ├── LightTheme.cs           # Light theme implementation
│       ├── ThemeManager.cs         # Global theme state and switching
│       └── ThemeStyler.cs          # Theme application helper
│
├── Configuration/
│   ├── ConfigurationService.cs     # Load/save .cfg configuration files
│   ├── RecentFilesManager.cs       # Recently opened files tracking
│   ├── OpcilloscopeJsonContext.cs  # Source-generated JSON serialization context
│   └── Models/
│       └── OpcilloscopeConfig.cs   # Configuration data models (ServerConfig, SubscriptionSettings, etc.)
│
├── OpcUa/
│   ├── OpcUaClientWrapper.cs       # OPC Foundation Session wrapper with connection management
│   ├── ConnectionManager.cs        # Connection lifecycle orchestration (connect/disconnect/reconnect)
│   ├── NodeBrowser.cs              # Address space navigation and browsing
│   ├── SubscriptionManager.cs      # OPC UA Subscription with MonitoredVariables
│   └── Models/
│       ├── BrowsedNode.cs          # Address space node model for tree view
│       └── MonitoredNode.cs        # Monitored variable model with value tracking
│
├── Utilities/
│   ├── Logger.cs                   # In-app logging service
│   ├── UiThread.cs                 # Thread marshalling for UI updates
│   ├── CsvRecordingManager.cs      # Background CSV recording of monitored values
│   ├── OpcValueConverter.cs        # OPC UA value type conversion utilities
│   ├── TaskExtensions.cs           # Async task helper extensions (FireAndForget)
│   ├── ConnectionIdentifier.cs     # Parse connection strings and server identifiers
│   └── NodeAttributeFormatter.cs   # Format OPC UA node attributes for display
│
├── Tests/
│   ├── Opcilloscope.TestServer/        # In-process OPC UA test server library
│   │   ├── Opcilloscope.TestServer.csproj
│   │   ├── Program.cs              # Standalone test server entry point
│   │   ├── TestServer.cs           # Server with ApplicationConfiguration
│   │   └── TestNodeManager.cs      # Custom NodeManager with test nodes
│   │
│   └── Opcilloscope.Tests/             # Unit and integration tests (xUnit)
│       ├── Opcilloscope.Tests.csproj
│       ├── Infrastructure/
│       │   └── TestServerFixture.cs # xUnit fixture with IAsyncLifetime
│       ├── Integration/
│       │   ├── OpcUaIntegrationTests.cs
│       │   ├── ConnectionManagerIntegrationTests.cs
│       │   ├── ErrorHandlingIntegrationTests.cs
│       │   ├── NodeBrowserIntegrationTests.cs
│       │   └── SubscriptionManagerIntegrationTests.cs
│       ├── App/
│       │   ├── ThemeManagerTests.cs
│       │   ├── AppThemeTests.cs
│       │   └── Keybindings/
│       │       ├── KeybindingTests.cs
│       │       └── KeybindingManagerTests.cs
│       ├── Configuration/
│       │   └── ConfigurationServiceTests.cs
│       ├── OpcUa/
│       │   ├── SubscriptionManagerTests.cs
│       │   ├── NodeAttributesTests.cs
│       │   └── Models/
│       │       ├── BrowsedNodeTests.cs
│       │       └── MonitoredNodeTests.cs
│       └── Utilities/
│           ├── LoggerTests.cs
│           ├── CsvRecordingManagerTests.cs
│           ├── OpcValueConverterTests.cs
│           └── ConnectionIdentifierTests.cs
│
├── docs/
│   ├── MARKETING_DESCRIPTION.md    # Product marketing copy
│   ├── UAT-CHECKLIST.md            # User acceptance testing guide
│   └── plan.md                     # Development plan
│
└── .github/workflows/
    ├── ci.yml                      # Build and test on push/PR
    ├── release.yml                 # Release automation
    ├── claude.yml                  # Claude AI integration
    └── claude-code-review.yml      # Claude code review automation
```

## Key Features

### Configuration Files (.cfg)
Opcilloscope uses JSON-based configuration files with the `.cfg` extension:

```json
{
  "version": "1.0",
  "server": {
    "endpointUrl": "opc.tcp://localhost:4840",
    "securityMode": "None"
  },
  "settings": {
    "publishingIntervalMs": 1000,
    "samplingIntervalMs": 500
  },
  "monitoredNodes": [
    {
      "nodeId": "ns=2;s=Counter",
      "displayName": "Counter",
      "enabled": true
    }
  ],
  "metadata": {
    "name": "My Config",
    "description": "Production server monitoring",
    "createdAt": "2026-01-06T00:00:00Z",
    "lastModified": "2026-01-06T00:00:00Z"
  }
}
```

### Theme System
Two built-in themes with consistent styling:
- **DarkTheme** (default): Dark background, high contrast for terminal use
- **LightTheme**: Light background for bright environments

Toggle themes via View menu or programmatically:
```csharp
ThemeManager.SetTheme("Light");
ThemeManager.SetThemeByIndex(0); // 0 = Dark, 1 = Light
```

### Scope View (Multi-Signal Oscilloscope)
Real-time visualization of up to 5 signals simultaneously:
- Time-based X-axis (elapsed seconds)
- Auto-scaling Y-axis with manual override (+/- keys)
- Pause/resume with Space key
- Distinct colors per signal (Green, Cyan, Yellow, Magenta, White)

### CSV Recording
Record monitored variable values to CSV files:
- Background queue-based writing (non-blocking)
- ISO 8601 timestamps with millisecond precision
- CSV format: `Timestamp,DisplayName,NodeId,Value,Status`

## Key Technical Details

### Terminal.Gui v2 API
- Use `Shortcut` instead of `StatusItem` for status bar items
- Use `Button.Accepting` event instead of `Button.Accept`
- Use `Height = n` instead of `Dim.Sized(n)`
- Use `SetNeedsLayout()` or `Update()` instead of `SetNeedsDisplay()`
- `ListView.SetSource()` requires `ObservableCollection<T>`
- Use `Application.Invoke()` for thread marshalling (no MainLoop)
- Use `Application.AddTimeout()` for periodic updates

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

### ConnectionManager Pattern
The `ConnectionManager` class orchestrates connection lifecycle:

```csharp
var connectionManager = new ConnectionManager(logger);

// Events
connectionManager.StateChanged += state => { /* Connecting, Connected, Disconnected, Reconnecting */ };
connectionManager.ValueChanged += node => { /* Handle value updates */ };
connectionManager.VariableAdded += node => { /* Handle new subscription */ };
connectionManager.VariableRemoved += handle => { /* Handle unsubscription */ };

// Operations
await connectionManager.ConnectAsync("opc.tcp://localhost:4840");
await connectionManager.SubscribeAsync(nodeId, displayName);
await connectionManager.UnsubscribeAsync(clientHandle);
await connectionManager.ReconnectAsync();
connectionManager.Disconnect();
```

### NuGet Packages
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
- `TestNodeManager` - Custom NodeManager exposing test nodes in `urn:opcilloscope:testserver` namespace
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
- `ServerName` - String ("Opcilloscope Test Server")
- `StartTime` - DateTime
- `Version` - String ("1.0.0")
- `ArrayOfInts` - Int32[] ([1, 2, 3, 4, 5])

## Architecture Patterns

### Thread Safety
OPC Foundation callbacks arrive on background threads. All UI updates are marshalled to the UI thread:

```csharp
// Using UiThread helper
UiThread.Run(() => _monitoredVariablesView.UpdateVariable(variable));

// Using Application.Invoke directly
Application.Invoke(() => SetNeedsLayout());
```

### Async Pattern with FireAndForget
For async operations from synchronous event handlers:

```csharp
// FireAndForget extension logs exceptions without blocking
_connectionManager.SubscribeAsync(nodeId, displayName).FireAndForget(_logger);
```

### Lazy Loading
The address space tree uses lazy loading - child nodes are only fetched when a parent is expanded, preventing memory issues with large address spaces.

### OPC UA Subscriptions
Uses proper OPC UA Publish/Subscribe with `MonitoredItem.Notification` events - values are pushed by the server, no polling required.

### Error Handling
- Connection errors display in the log panel without crashing
- Automatic reconnection with exponential backoff (1s, 2s, 4s, 8s)
- Graceful handling of bad node IDs and access denied errors
- CSV recording continues silently on individual write failures

## CI/CD Workflows

### CI Workflow (ci.yml)
Runs on push/PR to main:
- Checkout, setup .NET 10, restore, build (Release), test

### Release Workflow (release.yml)
Automates release builds and publishing.

## Common Issues

1. **`dotnet` command not found**: Install .NET SDK using the install script (see Environment Setup above)
2. **Tests fail with Xunit errors in main project**: Ensure `tests/**` is excluded in Opcilloscope.csproj
3. **UI thread exceptions**: Always use `Application.Invoke()` or `UiThread.Run()` for UI updates from background threads
4. **Ambiguous NodeBrowser reference**: OPC Foundation has its own `Browser` class - use fully qualified names if needed
5. **Certificate validation errors**: Set `AutoAcceptUntrustedCertificates = true` in SecurityConfiguration for development
6. **Integration tests fail with "Unexpected error starting application"**: The OPC UA test server requires specific environment permissions - unit tests will still pass
7. **Theme not applying correctly**: Ensure `ApplyTheme()` is called after all controls are created

## Keyboard Shortcuts

**Global:**
| Key | Action |
|-----|--------|
| ? | Show help |
| M | Open menu |
| Tab | Switch between panes |
| Ctrl+O | Open configuration |
| Ctrl+S | Save configuration |
| Ctrl+Shift+S | Save configuration as |
| Ctrl+R | Toggle recording (start/stop) |
| Ctrl+Q | Quit |

**Address Space:**
| Key | Action |
|-----|--------|
| Enter | Subscribe to selected node |
| R | Refresh address space tree |

**Monitored Variables:**
| Key | Action |
|-----|--------|
| Delete | Unsubscribe from selected variable |
| Space | Toggle selection (for Scope/Recording) |
| W | Write value to selected variable |
| T | Show trend plot |
| S | Open Scope with selected variables |

**Scope/Trend Plot View:**
| Key | Action |
|-----|--------|
| Space | Pause/resume plotting |
| +/= | Zoom in (increase scale) |
| - | Zoom out (decrease scale) |
| R | Reset to auto-scale |

## Rules
Always use Context7 MCP when I need library/API documentation, code generation, setup or configuration steps without me having to explicitly ask.
