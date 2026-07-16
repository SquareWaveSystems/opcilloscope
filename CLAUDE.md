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
| Config/data directories | `opcilloscope` | Use the platform locations below |
| Release artifacts | `opcilloscope` | `opcilloscope-linux-x64.tar.gz` |

Platform directories:
- Linux configuration: `${XDG_CONFIG_HOME:-$HOME/.config}/opcilloscope/`
- Linux application data (certificates and installed notices): `${XDG_DATA_HOME:-$HOME/.local/share}/opcilloscope/`
- macOS configuration and application data: `~/Library/Application Support/opcilloscope/`
- Windows configuration: `%APPDATA%\opcilloscope\`; certificates: `%LOCALAPPDATA%\opcilloscope\pki\`

## Environment Setup

### .NET SDK Installation
Install the exact .NET SDK version pinned by `global.json` using Microsoft's install script:

```bash
# Download and run the install script
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --version 10.0.109 --install-dir ~/.dotnet

# Add to PATH for the current session
export PATH="$HOME/.dotnet:$PATH"
```

## Build & Run

```bash
# Build
dotnet build Opcilloscope.sln

# Run (from repo root)
dotnet run --project Opcilloscope.csproj

# Run with a configuration file
dotnet run --project Opcilloscope.csproj -- config.cfg
dotnet run --project Opcilloscope.csproj -- --config config.cfg

# Run the cross-platform unit, integration, and component suite
dotnet test Opcilloscope.sln

# Linux only: publish and exercise the real TUI through a PTY
dotnet test Tests/Opcilloscope.E2ETests/Opcilloscope.E2ETests.csproj
```

## Command-Line Interface

```
Usage: opcilloscope [options] [file]

Options:
  -f, --config <file>   Load configuration file (.cfg, .opcilloscope, or .json)
  -c, --connect <url>   Connect directly to an OPC UA endpoint
      --insecure        Accept untrusted server certificates (development only)
  -V, --version         Show version information
  -h, --help            Show help message

Examples:
  opcilloscope                           Start with empty configuration
  opcilloscope production.cfg            Load configuration file
  opcilloscope --config config.json      Load configuration file
  opcilloscope --connect opc.tcp://localhost:4840
```

The Linux-only `Tests/Opcilloscope.E2ETests` project intentionally stays out
of `Opcilloscope.sln`; see [`docs/TESTING.md`](docs/TESTING.md) for all test
layers and exact-artifact usage.

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
│   │   ├── BrailleCanvas.cs        # High-resolution braille-character drawing canvas
│   │   ├── MonitoredVariablesView.cs # TableView for subscribed variables with selection
│   │   ├── NodeDetailsView.cs      # Node attribute display panel
│   │   ├── LogView.cs              # Application log display
│   │   └── ScopeView.cs            # Real-time multi-signal oscilloscope view
│   ├── Dialogs/
│   │   ├── ConnectDialog.cs        # Server connection dialog with publishing interval
│   │   ├── OpenConfigDialog.cs     # Open configuration file dialog with recent files
│   │   ├── PasswordPromptDialog.cs # Password prompt for authenticated connections
│   │   ├── WriteValueDialog.cs     # Write value to node dialog
│   │   ├── ScopeDialog.cs          # Multi-signal scope dialog (up to 5 signals)
│   │   ├── SaveConfigDialog.cs     # Save configuration file dialog
│   │   ├── SaveRecordingDialog.cs  # Save CSV recording dialog
│   │   └── HelpDialog.cs           # Full help/documentation dialog
│   ├── Keybindings/
│   │   ├── Keybinding.cs           # Keybinding model (key, action, context)
│   │   ├── KeybindingContext.cs    # Context enum (Global, AddressSpace, etc.)
│   │   ├── KeybindingManager.cs    # Keybinding registration and lookup
│   │   └── DefaultKeybindings.cs   # Default keybinding definitions
│   └── Themes/
│       ├── AppTheme.cs             # Abstract base theme class
│       ├── DarkTheme.cs            # Dark theme implementation
│       ├── LightTheme.cs           # Light theme implementation
│       ├── TerminalTheme.cs        # Theme inheriting the terminal's ANSI palette
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
│   ├── ConnectionCredentials.cs    # Username/password credentials for authenticated sessions
│   ├── ConnectionManager.cs        # Connection lifecycle orchestration (connect/disconnect/reconnect)
│   ├── DataTypeResolver.cs         # Built-in OPC UA data type name/BuiltInType lookup
│   ├── NodeBrowser.cs              # Address space navigation and browsing
│   ├── SubscriptionManager.cs      # OPC UA Subscription with MonitoredVariables
│   └── Models/
│       ├── BrowsedNode.cs          # Address space node model for tree view
│       └── MonitoredNode.cs        # Monitored variable model with value tracking
│
├── Utilities/
│   ├── Logger.cs                   # In-app logging service
│   ├── UiThread.cs                 # Thread marshalling for UI updates (via TerminalUi)
│   ├── TerminalUi.cs               # Instance-based IApplication access (timers, dialogs, message boxes, clipboard)
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
│       │   ├── TestModuleInitializer.cs # Test assembly initialization
│       │   └── TestServerFixture.cs # xUnit fixture with IAsyncLifetime
│       ├── Integration/
│       │   ├── OpcUaIntegrationTests.cs
│       │   ├── AuthenticationIntegrationTests.cs
│       │   ├── ConnectionManagerIntegrationTests.cs
│       │   ├── ErrorHandlingIntegrationTests.cs
│       │   ├── NodeBrowserIntegrationTests.cs
│       │   ├── ReconnectIntegrationTests.cs
│       │   ├── SubscriptionManagerIntegrationTests.cs
│       │   └── WriteIntegrationTests.cs
│       ├── App/
│       │   ├── ThemeManagerTests.cs
│       │   ├── AppThemeTests.cs
│       │   ├── FocusManagerTests.cs
│       │   ├── Views/
│       │   │   └── BrailleCanvasTests.cs
│       │   └── Keybindings/
│       │       ├── KeybindingTests.cs
│       │       └── KeybindingManagerTests.cs
│       ├── Configuration/
│       │   ├── ConfigurationServiceTests.cs
│       │   └── RecentFilesManagerTests.cs
│       ├── OpcUa/
│       │   ├── SubscriptionManagerTests.cs
│       │   ├── NodeAttributesTests.cs
│       │   └── Models/
│       │       ├── BrowsedNodeTests.cs
│       │       └── MonitoredNodeTests.cs
│       └── Utilities/
│           ├── LoggerTests.cs
│           ├── CsvRecordingManagerTests.cs
│           ├── NodeAttributeFormatterTests.cs
│           ├── OpcValueConverterTests.cs
│           └── ConnectionIdentifierTests.cs
│
├── docs/                           # Screenshots and promotional images
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
    "endpointUrl": "opc.tcp://localhost:4840"
  },
  "settings": {
    "publishingIntervalMs": 1000,
    "samplingIntervalMs": 500
  },
  "monitoredNodes": [
    {
      "nodeId": "ns=2;s=Counter",
      "namespaceUri": "urn:example:machine",
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

Newly saved non-standard nodes include `namespaceUri`. The URI is stable across
sessions; the numeric namespace index inside `nodeId` is retained for backward
compatibility but ignored when the URI is present.

An automatic/omitted or partial security profile requires a
`SignAndEncrypt` endpoint and selects the strongest matching candidate.
Explicit `securityMode: "Sign"` opts into signed-but-unencrypted traffic.
Explicit `securityMode: "None"` is the unsecured plaintext opt-in for an
anonymous connection; username authentication never permits `None`.

### Theme System
Three built-in themes with consistent styling:
- **DarkTheme** (default): Dark background, high contrast for terminal use
- **LightTheme**: Light background for bright environments
- **TerminalTheme**: Inherits the terminal's own ANSI color palette. Uses only the 16 named ANSI colors (`ColorName16`) and enables `TerminalUi.Driver.Force16Colors` so the driver emits standard SGR color codes instead of 24-bit RGB — the terminal renders them with its configured scheme. `ThemeManager.SetTheme` toggles `Force16Colors` automatically via `AppTheme.UseTerminalColors`.

Toggle themes via View menu (cycles Dark → Light → Terminal) or programmatically:
```csharp
ThemeManager.SetTheme("Terminal");
ThemeManager.SetThemeByIndex(0); // 0 = Dark, 1 = Light, 2 = Terminal
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

#### Instance-based application model (do NOT use the static `Application`)
Terminal.Gui 2.4 deprecated the legacy static `Application` object (`Application.Invoke`,
`AddTimeout`, `Run`, `RequestStop`, `Instance`, `Driver`, `KeyDown`, `Init`/`Shutdown`, the
static `Clipboard`, etc.). The whole static surface is `[Obsolete]` and will be removed in a
future release, and `TreatWarningsAsErrors` is on — so a static-`Application` call is a build
error, not a warning. The app uses the instance-based model (`Application.Create()` →
`IApplication`) instead:
- `Program.Main` owns the lifecycle: `Application.Create()` → `app.Init()` →
  `app.Run(mainWindow)` → `app.Dispose()` (Dispose replaces the obsolete `Shutdown`). It stores
  the instance in `TerminalUi.App`.
- **All UI code routes through the helpers in `Utilities/`, never the static `Application`:**
  - `UiThread.Run(...)` — marshal an action onto the UI thread (thread marshalling; no MainLoop)
  - `TerminalUi.AddTimeout(...)` / `RemoveTimeout(...)` — periodic/one-shot main-loop timers
  - `TerminalUi.RunModal(dialog)` / `RequestStop()` — open/close a modal dialog
  - `TerminalUi.Query(...)` / `ErrorQuery(...)` — message boxes (no need to pass the app instance)
  - `TerminalUi.TrySetClipboardData(...)` — OS clipboard
  - `TerminalUi.Driver`, `TopRunnableView`, `IsTopRunnable(...)`, `Add`/`RemoveKeyDownHandler(...)`
- The direct `IApplication` uses (`Create`/`Init`/`Run`/`Dispose`, keyboard, driver) are confined
  to `Program.cs`, `TerminalUi`, and `ThemeManager`. Add new helpers to `TerminalUi` rather than
  reaching for the static API. In headless unit tests `TerminalUi.App` is null: fire-and-forget
  helpers (Invoke, timers, clipboard) no-op and interactive ones (modal dialogs, message boxes) throw.

### OPC Foundation SDK API
- Uses `Opc.Ua.Client.Session` for connection management
- Uses proper OPC UA Subscriptions with `Subscription` and `MonitoredItem` classes
- MonitoredItem data-change notifications are delivered through OPC UA subscriptions (not repeated reads and not the OPC UA PubSub transport model)
- `NodeId` constructor: `new NodeId(uint identifier)` or `new NodeId(ushort namespaceIndex, uint identifier)`
- Use `ExpandedNodeId.ToNodeId(expandedNodeId, session.NamespaceUris)` for conversion
- Use `ObjectIds.RootFolder` for the root node (ns=0;i=84)
- `StatusCode.Code` returns the uint value; check with `StatusCodes.Good`, `StatusCodes.BadUnexpectedError`, etc.
- Use `Attributes.Value`, `Attributes.DataType`, etc. for attribute IDs
- An automatic/omitted or partial security profile requires `SignAndEncrypt` and selects the strongest matching endpoint. Explicit `SecurityMode=Sign` opts into signed-but-unencrypted traffic. Explicit anonymous `SecurityMode=None` opts into unsecured plaintext; username credentials never permit `None`.
- Certificate validation rejects untrusted certificates by default. The `--insecure` CLI option may be used for a development run only; it changes certificate trust, not transport security. Production certificates belong in the trusted-peer store reported in the connection log.

### DiscoveryClient API
The `DiscoveryClient.Create` method requires `EndpointConfiguration`, not `ApplicationConfiguration`:

`DiscoveryClient.Create` and `Session.Create` are obsolete in the current SDK.
The existing wrapper uses narrowly scoped `CS0618` pragmas because the async
factory replacements require additional telemetry setup. Do not copy these
calls into new code without the same documented justification, and never add
a project-wide suppression.

```csharp
// Correct usage - create EndpointConfiguration first
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
#pragma warning disable CS0618 // Existing wrapper exception: async factory needs telemetry setup
var session = await Session.Create(config, endpoint, false, "SessionName", 60000, new UserIdentity(new AnonymousIdentityToken()), null);
#pragma warning restore CS0618

// Subscription creation
var subscription = new Subscription(session.DefaultSubscription) {
    PublishingInterval = 1000,
    PublishingEnabled = true
};
session.AddSubscription(subscription);
await subscription.CreateAsync();

// MonitoredItem creation
var monitoredItem = new MonitoredItem(subscription.DefaultItem) {
    StartNodeId = nodeId,
    AttributeId = Attributes.Value,
    SamplingInterval = 500
};
monitoredItem.Notification += OnNotification;
subscription.AddItem(monitoredItem);
await subscription.ApplyChangesAsync();
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
await connectionManager.DisconnectAsync();
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
- `SineWave` - Double, sine oscillation (0-100)
- `SineFrequency` - Double, writable (controls SineWave frequency factor, default 0.1)
- `TriangleWave` - Double, triangle oscillation (0-100)
- `TriangleFrequency` - Double, writable (controls TriangleWave frequency factor, default 0.1)
- `SquareWave` - Double, square/pulse oscillation (0 or 100)
- `SquareFrequency` - Double, writable (controls SquareWave frequency factor, default 0.1)
- `SquareDutyCycle` - Double, writable (controls SquareWave duty cycle, 0.0-1.0, default 0.5)
- `SawtoothWave` - Double, sawtooth ramp (0-100)
- `SawtoothFrequency` - Double, writable (controls SawtoothWave frequency factor, default 0.1)
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
// Marshal onto the UI thread with the UiThread helper
UiThread.Run(() => _monitoredVariablesView.UpdateVariable(variable));
UiThread.Run(() => SetNeedsLayout());
```

Do not call the deprecated static `Application.Invoke()` directly — `UiThread.Run` wraps the
instance-based `IApplication.Invoke` (see the "Instance-based application model" note above).

### Async Pattern with FireAndForget
For async operations from synchronous event handlers:

```csharp
// FireAndForget extension logs exceptions without blocking
_connectionManager.SubscribeAsync(nodeId, displayName).FireAndForget(_logger);
```

### Lazy Loading
The address space tree uses lazy loading - child nodes are only fetched when a parent is expanded, preventing memory issues with large address spaces.

### OPC UA Subscriptions
Uses OPC UA client/server `Subscription` and `MonitoredItem` services with `MonitoredItem.Notification` events, so values arrive as data-change notifications instead of repeated reads. This is not the separate OPC UA PubSub transport model.

### Error Handling
- Connection errors display in the log panel without crashing
- Automatic reconnection with exponential backoff (1s, 2s, 4s, 8s)
- Graceful handling of bad node IDs and access denied errors
- CSV recording logs write failures, counts failed/dropped records, and reports data loss when recording stops

## CI/CD Workflows

### CI Workflow (ci.yml)
Runs on push/PR to main and gates locked dependency restore, third-party
inventory validation, formatting, the Release solution build, cross-platform
tests, single-file layout, CLI smoke, and Linux real-PTY E2E tests against the
exact published artifact.

### Release Workflow (release.yml)
Runs the cross-platform suite, then builds six locked RIDs. The native Linux
artifact also passes the real-PTY E2E suite; native host artifacts pass CLI
smokes. Archives preserve executable permissions, licenses, exact runtime
notices, and are published with `SHA256SUMS` under least-privilege permissions.

## Common Issues

1. **`dotnet` command not found**: Install .NET SDK using the install script (see Environment Setup above)
2. **Tests fail with Xunit errors in main project**: Ensure `Tests/**` is excluded in Opcilloscope.csproj
3. **UI thread exceptions**: Always use `UiThread.Run()` for UI updates from background threads (it marshals via the instance-based `IApplication.Invoke`; do not call the deprecated static `Application.Invoke()`)
4. **Ambiguous NodeBrowser reference**: OPC Foundation has its own `Browser` class - use fully qualified names if needed
5. **Certificate validation errors**: Trust the server certificate in the path reported by the connection log, or re-run with `--insecure` for development only. This does not reduce message security: automatic/partial profiles still require `SignAndEncrypt`; only explicit `Sign` or anonymous `None` opts down.
6. **Integration tests fail with "Unexpected error starting application"**: The OPC UA test server requires specific environment permissions - unit tests will still pass
7. **Theme not applying correctly**: Ensure `ApplyTheme()` is called after all controls are created

## Keyboard Shortcuts

**Global:**
| Key | Action |
|-----|--------|
| ? | Show help |
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
| F5 | Refresh address space tree |
| W | Write value to selected node |

**Monitored Variables:**
| Key | Action |
|-----|--------|
| Delete | Unsubscribe from selected variable |
| Space | Toggle selection (for Scope/Recording) |
| W | Write value to selected variable |
| R | Toggle CSV recording |
| S | Open Scope with selected variables |

**Scope View:**
| Key | Action |
|-----|--------|
| Space | Pause/resume plotting |
| +/= | Zoom in (increase scale) |
| - | Zoom out (decrease scale) |
| R | Reset to auto-scale |
| [ | Widen time window (show more) |
| ] | Narrow time window (zoom in) |
| Cursor Up/Down | Pan view up/down |
| Cursor Left/Right | Move cursor left/right (when paused) |

## Rules
Always use Context7 MCP when I need library/API documentation, code generation, setup or configuration steps without me having to explicitly ask.
