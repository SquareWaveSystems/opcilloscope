# Opcilloscope.E2ETests — black-box PTY end-to-end tests

Launches the **published** opcilloscope binary attached to a real pseudo-terminal,
reconstructs the rendered screen from the terminal output, and asserts on it. This is the
only layer that exercises the full stack: the self-contained single-file binary, the
Terminal.Gui console driver, and actual rendering.

**Pure .NET, no extra toolchain.** No Node, no Python, no NuGet PTY/VT library.

## How it works

| Piece | Responsibility |
|-------|----------------|
| `Pty` | Allocates a sized PTY via libc `openpty` and launches the binary on it with `posix_spawn` (`POSIX_SPAWN_SETSID`). Pure P/Invoke. |
| `VtScreen` | A minimal VT100/ANSI emulator: parses cursor moves, erases and printable runs into a character grid. Ignores colour/SGR and OSC. |
| `OpcilloscopeSession` | Pumps PTY output into `VtScreen`, **answers the terminal capability queries** Terminal.Gui sends (size `CSI 18 t`, cursor `DSR 6 n`, OSC 10/11 colours) so the app actually paints, and exposes `Snapshot()` / `WaitForText()` / key input. |
| `PublishedBinaryFixture` | Supplies the binary path: from `$OPCILLOSCOPE_BIN` if set, else publishes a self-contained linux-x64 build once. |

### Why a hand-rolled PTY instead of `script` or `Pty.Net`?

- **`script`** (util-linux) creates a **0×0** window when its stdout is a pipe, and has no size
  flag — Terminal.Gui refuses to draw without a known size. We must set the window size
  ourselves (`openpty` takes a `winsize`).
- **`Pty.Net`** (the usual .NET PTY package) is unmaintained (last release 2018, unlisted on
  NuGet, ships only a Windows `winpty.dll`). The VT libraries (`VtNetCore`, `XtermSharp`) are
  likewise stale, so the emulator is kept in-tree and scoped to what Terminal.Gui emits.

### Why answer terminal queries?

Terminal.Gui's net driver detects size/colours by **emitting escape sequences and waiting for
the terminal to reply**. A bare PTY has no emulator on the other end, so without our replies the
app prints its setup sequences and then blocks before the first paint. `OpcilloscopeSession`
replies to the size/cursor/colour queries, which unblocks rendering.

## Running

```bash
# Publishes the binary on first run, then drives it over a PTY:
dotnet test Tests/Opcilloscope.E2ETests

# Reuse an already-published binary (what CI does):
OPCILLOSCOPE_BIN=$PWD/publish/opcilloscope dotnet test Tests/Opcilloscope.E2ETests
```

**Linux only** (the TUI and the `openpty` P/Invoke). CI runs it on `ubuntu-latest` in a
dedicated `e2e` job (see `.github/workflows/ci.yml`); PTY allocation works there by default.

## Adding tests

Tests assert on `Snapshot()` text. Prefer `WaitForText(...)` over an immediate `Snapshot()` —
the UI paints over several frames, so racing the first paint is flaky. Send input with
`Send("?")` (text/keys) or `SendByte(0x11)` (control characters, e.g. Ctrl+Q).
