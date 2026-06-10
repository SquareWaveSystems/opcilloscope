# Testing opcilloscope

The suite has three layers. All run under `dotnet test` with **no extra language
toolchain** (pure .NET).

## 1. Unit / integration tests (existing)

`Tests/Opcilloscope.Tests/` — xUnit tests for the OPC UA, configuration, theming and
utility layers, plus integration tests against the in-process `Opcilloscope.TestServer`.

```bash
dotnet test                                    # everything
dotnet test --filter "FullyQualifiedName~Integration"
```

## 2. In-process TUI component tests

`Tests/Opcilloscope.Tests/Tui/` — constructs the **real** Terminal.Gui views and dialogs
(`MonitoredVariablesView`, `ConnectDialog`, the theme/`Scheme` system, …) and asserts on
their observable behaviour and state.

```bash
dotnet test --filter "FullyQualifiedName~Tui"
```

These tests live in a **non-parallel xUnit collection** (`[Collection("Tui")]`) because
Terminal.Gui's `Application` is global mutable state and must not be shared across parallel
tests.

> **Why these assert on state, not rendered cells.** Terminal.Gui **2.4.5 (stable)** does not
> expose a public headless driver: `Application.Create()` leaves `Driver` null until the real
> console event loop runs, and the cell-buffer assertion helper (`DriverAssert`) only exists
> on the upstream `develop` branch. Assertions on the *rendered screen* are therefore done by
> layer 3 (below), which drives the real published binary. When upstream ships a public test
> driver, these component tests can add cell-level snapshots.

## 3. Black-box end-to-end (PTY) tests

`Tests/Opcilloscope.E2ETests/` — launches the **published binary** attached to a pseudo-
terminal, reconstructs the rendered screen from the VT/ANSI output, and asserts on it. Pure
.NET (uses the system `script` PTY + an in-process ANSI→grid parser); no Node/Python.

```bash
dotnet test Tests/Opcilloscope.E2ETests          # publishes the binary on first run
```

See `Tests/Opcilloscope.E2ETests/README.md` for details and CI notes.
