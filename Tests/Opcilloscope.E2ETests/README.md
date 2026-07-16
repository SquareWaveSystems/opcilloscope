# opcilloscope black-box E2E tests

These Linux-only tests launch the published `opcilloscope` binary on a sized pseudo-terminal,
answer Terminal.Gui's terminal-capability queries, reconstruct its VT/ANSI output, and assert
against the rendered screen. They exercise the self-contained publish, native console driver,
real rendering, keyboard input, and clean shutdown.

The harness is pure .NET plus Linux libc. It does not need Node, Python, `script`, or an external
terminal emulator.

## Run locally on Linux

```bash
dotnet test Tests/Opcilloscope.E2ETests/Opcilloscope.E2ETests.csproj
```

Without configuration, the suite creates a fresh `linux-x64` publish in a uniquely named system
temporary directory and removes it when the suite finishes. It never reuses repository-local
publish output.

To test an existing artifact, set its exact path:

```bash
OPCILLOSCOPE_BIN="$PWD/publish/opcilloscope" \
  dotnet test Tests/Opcilloscope.E2ETests/Opcilloscope.E2ETests.csproj
```

If `OPCILLOSCOPE_BIN` is set but does not exist, the suite fails instead of silently publishing a
different binary. CI uses this mode so the screen tests exercise the same artifact whose layout it
validated.

`Opcilloscope.E2ETests` intentionally stays out of `Opcilloscope.sln`; the normal solution remains
portable across Linux, macOS, and Windows. Run this project explicitly only on Linux.
