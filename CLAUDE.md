# Opcilloscope Project Guide

Terminal-based OPC UA client/monitor: .NET 10, Terminal.Gui v2, and the OPC Foundation
client SDK (`OPCFoundation.NetStandard.Opc.Ua.Client`).

<!-- Maintainer note: keep this file under ~200 lines. It loads into every session, so it
holds only rules and pitfalls that can't be read off the code. Layout, features, keybindings
and config format live in README.md, CONTRIBUTING.md and docs/TESTING.md. -->

Read on demand rather than duplicating here:
- `README.md` — features, keyboard shortcuts, install, config file format
- `CONTRIBUTING.md` — architecture overview, code style, PR guidelines
- `docs/TESTING.md` — the three test layers and exact-artifact E2E usage
- `App/Keybindings/DefaultKeybindings.cs` — source of truth for keybindings
- `Tests/Opcilloscope.TestServer/TestNodeManager.cs` — source of truth for test server nodes

## Commands

```bash
dotnet build Opcilloscope.sln
dotnet run --project Opcilloscope.csproj -- [config.cfg] [--insecure]
dotnet test Opcilloscope.sln                      # unit, integration, TUI component tests
dotnet test Tests/Opcilloscope.Tests/Opcilloscope.Tests.csproj --filter "FullyQualifiedName~Tui"
dotnet format Opcilloscope.sln --verify-no-changes   # CI gate; drop the flag to fix

# Linux only: publishes the binary and drives the real TUI through a PTY
dotnet test Tests/Opcilloscope.E2ETests/Opcilloscope.E2ETests.csproj

# Standalone test server at opc.tcp://localhost:4840/UA/OpcilloscopeTest
dotnet run --project Tests/Opcilloscope.TestServer
```

If `dotnet` is missing, install the SDK and add it to `PATH`:

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir ~/.dotnet
export PATH="$HOME/.dotnet:$PATH"
```

Before calling a change done, run `dotnet build Opcilloscope.sln`, the relevant tests, and
`dotnet format --verify-no-changes`. `TreatWarningsAsErrors` is on, so warnings fail the build.

## Naming

The name is **opcilloscope** (lowercase "o") everywhere except where .NET requires PascalCase.

| Context | Name |
|---------|------|
| User-facing text, CLI, URLs, release artifacts, config/data directories | `opcilloscope` |
| C# namespaces, classes, projects, code file/folder names | `Opcilloscope` |

Platform directories:
- Linux config: `${XDG_CONFIG_HOME:-$HOME/.config}/opcilloscope/`
- Linux app data (certificates, installed notices): `${XDG_DATA_HOME:-$HOME/.local/share}/opcilloscope/`
- macOS config and app data: `~/Library/Application Support/opcilloscope/`
- Windows config: `%APPDATA%\opcilloscope\`; certificates: `%LOCALAPPDATA%\opcilloscope\pki\`

## Terminal.Gui v2

**Never use the static `Application` object.** Terminal.Gui 2.4 marked the whole static
surface (`Application.Invoke`, `AddTimeout`, `Run`, `RequestStop`, `Driver`, `Init`/`Shutdown`,
static `Clipboard`, ...) `[Obsolete]`, so with warnings-as-errors a static call is a build error.

- `Program.Main` owns the lifecycle: `Application.Create()` → `app.Init()` →
  `app.Run(mainWindow)` → `app.Dispose()`, storing the instance in `TerminalUi.App`.
- Direct `IApplication` use is confined to `Program.cs`, `Utilities/TerminalUi.cs`, and
  `App/Themes/ThemeManager.cs`. Everywhere else goes through the helpers:
  - `UiThread.Run(...)` — marshal onto the UI thread
  - `TerminalUi.AddTimeout(...)` / `RemoveTimeout(...)` — main-loop timers
  - `TerminalUi.RunModal(dialog)` / `RequestStop()` — modal dialogs
  - `TerminalUi.Query(...)` / `ErrorQuery(...)` — message boxes
  - `TerminalUi.TrySetClipboardData(...)` — OS clipboard
  - `TerminalUi.Driver`, `TopRunnableView`, `IsTopRunnable(...)`, `Add`/`RemoveKeyDownHandler(...)`
- Need something new from `IApplication`? Add a helper to `TerminalUi`.
- In headless unit tests `TerminalUi.App` is null: fire-and-forget helpers (invoke, timers,
  clipboard) no-op, and interactive ones (modal dialogs, message boxes) throw.

v2 API differences from v1:
- `Shortcut`, not `StatusItem`, for status bar items
- `Button.Accepting`, not `Button.Accept`
- `Height = n`, not `Dim.Sized(n)`
- `SetNeedsLayout()` or `Update()`, not `SetNeedsDisplay()`
- `ListView.SetSource()` requires `ObservableCollection<T>`

Themes: call `ApplyTheme()` only after all controls are created. `TerminalTheme` uses only
the 16 named ANSI colors (`ColorName16`); `ThemeManager.SetTheme` toggles
`TerminalUi.Driver.Force16Colors` from `AppTheme.UseTerminalColors`, so new themes set that
property instead of touching the driver.

## Threading and async

- OPC UA callbacks arrive on background threads. Every UI update goes through
  `UiThread.Run(() => ...)`.
- From synchronous event handlers, start async work with `.FireAndForget(_logger)` so
  exceptions are logged instead of lost.
- The address space tree is lazy: fetch children only when a parent is expanded.

## OPC UA SDK

- Values arrive through client/server `Subscription` + `MonitoredItem.Notification` events.
  Do not poll with repeated reads, and do not confuse this with the OPC UA PubSub transport.
- `DiscoveryClient.Create` and `Session.Create` are obsolete in the current SDK. The existing
  calls in `OpcUa/OpcUaClientWrapper.cs` sit inside narrowly scoped `#pragma warning disable
  CS0618` blocks because the async factories need extra telemetry setup. Do not copy them into
  new code without the same documented justification, and never add a project-wide suppression.
- `DiscoveryClient.Create` takes an `EndpointConfiguration`
  (`EndpointConfiguration.Create(config)`), not an `ApplicationConfiguration`.
- Convert with `ExpandedNodeId.ToNodeId(expandedNodeId, session.NamespaceUris)`.
- `StatusCode.Code` is the raw `uint`; compare against `StatusCodes.Good` etc.
- OPC Foundation ships its own `Browser` class. Fully qualify if `NodeBrowser` becomes ambiguous.
- Server-side (test server) setup uses the async variants: `config.ValidateAsync`,
  `CheckApplicationInstanceCertificatesAsync`, `StartAsync`, `StopAsync`.

### Security semantics (do not weaken)

- An automatic, omitted, or partial security profile requires `SignAndEncrypt` and picks the
  strongest matching endpoint.
- Explicit `securityMode: "Sign"` opts into signed-but-unencrypted traffic.
- Explicit `securityMode: "None"` is the plaintext opt-in for anonymous connections only.
  Username credentials never permit `None`.
- Untrusted server certificates are rejected by default. `--insecure` changes certificate
  trust for a development run; it does not change transport security.
- `-c, --connect <url>` is parsed but reserved: direct URL connection is not implemented.

## Testing

- Integration tests use the in-process `Opcilloscope.TestServer`: derive from
  `IntegrationTestBase` (auto-connected `Client`) or use `[Collection("TestServer")]` with
  `TestServerFixture`. Test nodes live in namespace `urn:opcilloscope:testserver`; get the
  index with `GetNamespaceIndex()`.
- TUI component tests go in `Tests/Opcilloscope.Tests/Tui/` under `[Collection("Tui")]`
  (non-parallel), because `TerminalUi.App` and theme/driver state are process-global. They
  assert on view state, not rendered cells: Terminal.Gui 2.4.5 has no public headless driver.
- `Tests/Opcilloscope.E2ETests` is Linux-only and intentionally outside `Opcilloscope.sln`.
  Do not add it to the solution. `OPCILLOSCOPE_BIN` points it at a pre-published binary.
- `Tests/**` is excluded from `Opcilloscope.csproj`. Xunit errors in the main project mean
  that exclusion broke.
- Integration tests failing with "Unexpected error starting application" means the
  environment cannot start the test server; unit tests still pass.

## Dependencies and CI

- CI restores with `--locked-mode`. A package change must update all six
  `packages.<rid>.lock.json` files and `licenses/THIRD-PARTY-PACKAGES.tsv`, then pass
  `./scripts/verify-third-party-inventory.sh`.
- `ci.yml` gates locked restore, inventory, formatting, Release build, tests, single-file
  publish layout, and the Linux PTY E2E suite against the exact published artifact.
  `release.yml` builds six locked RIDs and publishes archives with `SHA256SUMS`.

## Rules

- Use the Context7 MCP for library/API documentation, code generation, and setup or
  configuration steps without being asked.
