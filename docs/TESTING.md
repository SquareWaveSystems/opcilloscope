# Testing opcilloscope

The suite has three layers and uses only .NET plus the operating-system facilities noted below.

## 1. Unit / integration tests (existing)

`Tests/Opcilloscope.Tests/` — xUnit tests for the OPC UA, configuration, theming and
utility layers, plus integration tests against the in-process `Opcilloscope.TestServer`.

```bash
dotnet test Opcilloscope.sln
dotnet test Tests/Opcilloscope.Tests/Opcilloscope.Tests.csproj \
  --filter "FullyQualifiedName~Integration"
```

The solution contains the cross-platform unit, integration, and component tests. The Linux-only
black-box project is invoked explicitly as described in layer 3.

## 2. In-process TUI component tests

`Tests/Opcilloscope.Tests/Tui/` — constructs the **real** Terminal.Gui views and dialogs
(`MonitoredVariablesView`, `ConnectDialog`, the theme/`Scheme` system, …) and asserts on
their observable behaviour and state.

```bash
dotnet test Tests/Opcilloscope.Tests/Opcilloscope.Tests.csproj \
  --filter "FullyQualifiedName~Tui"
```

These tests live in a **non-parallel xUnit collection** (`[Collection("Tui")]`) because
the app's `TerminalUi.App` reference and Terminal.Gui theme/driver state are process-global and
must not be shared across parallel tests.

> **Why these assert on state, not rendered cells.** Terminal.Gui **2.4.5 (stable)** does not
> expose a public headless driver: `Application.Create()` leaves `Driver` null until the real
> console event loop runs, and the cell-buffer assertion helper (`DriverAssert`) only exists
> on the upstream `develop` branch. Assertions on the *rendered screen* are therefore done by
> layer 3 (below), which drives the real published binary. When upstream ships a public test
> driver, these component tests can add cell-level snapshots.

## 3. Black-box end-to-end (PTY) tests

`Tests/Opcilloscope.E2ETests/` — launches the **published binary** attached to a sized Linux
pseudo-terminal, reconstructs the rendered screen from the VT/ANSI output, and asserts on it. The
harness uses .NET plus Linux libc (`openpty`/`posix_spawn`); it needs no Node, Python, `script`, or
external terminal emulator.

```bash
# Linux only; creates and removes a fresh temporary publish:
dotnet test Tests/Opcilloscope.E2ETests/Opcilloscope.E2ETests.csproj

# Or exercise one exact pre-published artifact, as CI does:
OPCILLOSCOPE_BIN="$PWD/publish/opcilloscope" \
  dotnet test Tests/Opcilloscope.E2ETests/Opcilloscope.E2ETests.csproj
```

The project intentionally stays out of `Opcilloscope.sln`, preserving normal solution builds and
tests on macOS and Windows. If `OPCILLOSCOPE_BIN` is set but missing, the suite fails rather than
silently publishing a different binary.

See `Tests/Opcilloscope.E2ETests/README.md` for details and CI notes.
