# opcilloscope v1.x — manual regression checklist

Use this for post-release maintenance and before future v1.x tags. The v1.0.0
tag has already shipped; unchecked boxes describe manual coverage still to run,
not the state of the published release. Items marked **[v1]** changed during the
original v1 fix sweep and deserve extra attention.

## 0. Setup

- [ ] **Test the actual release artifact, not just `dotnet run`.** Publish the self-contained
      single-file binary and run *that*:
      `dotnet publish Opcilloscope.csproj -c Release -r linux-x64 -o ./publish`
      then `./publish/opcilloscope`.
      **[v1]** Confirm it launches with no `$schema` / startup error (trimming is now disabled).
- [ ] Start the in-process test server in another terminal:
      `dotnet run --project Tests/Opcilloscope.TestServer`
      → it prints `Endpoint: opc.tcp://localhost:4840/UA/OpcilloscopeTest`.
      (Test nodes live under **Simulation** — Counter, SineWave, SquareWave, etc. — and **StaticData**.)
- [ ] Have a terminal at least ~100×30 for the TUI to lay out comfortably.

## 1. CLI / startup

- [ ] **[v1]** `opcilloscope --help` prints usage and exits 0 **without opening the TUI / a terminal**
      (works over SSH / non-interactive too). Same for `-h`.
- [ ] `opcilloscope --version` prints the application version and exits 0 without opening the TUI.
- [ ] `opcilloscope` (no args) starts with an empty configuration.
- [ ] `opcilloscope <file>.cfg` and `opcilloscope --config <file>.json` load that config on startup.
- [ ] `opcilloscope --connect opc.tcp://…` connects directly to that endpoint.
- [ ] `opcilloscope /does/not/exist.cfg` prints `Error: Configuration file not found:` and exits 1.
- [ ] **[v1]** `opcilloscope --insecure` is accepted (no "unknown argument"); app starts normally.
- [ ] `Ctrl+Q` quits cleanly — terminal is fully restored (no leftover colors/alt-screen, cursor visible).

## 2. Connection & security

- [ ] Connect (menu / Connect dialog) to `opc.tcp://localhost:4840/UA/OpcilloscopeTest`, **Anonymous** →
      status shows Connected; address space populates.
- [ ] Connect with **username/password** (the test server accepts credentialed sessions) → Connected.
- [ ] **[v1] Secure-by-default:** launch *without* `--insecure` and connect **with credentials** →
      connection is **refused** with a clear log message about an untrusted certificate / pointing at `--insecure`.
- [ ] **[v1]** Relaunch *with* `--insecure`, connect with the same credentials → now **succeeds**.
- [ ] Connect to a bad endpoint (e.g. `opc.tcp://localhost:4999`) → error appears in the log pane,
      app does **not** crash.
- [ ] **[v1]** Disconnect while connected → UI stays **responsive** (no multi-second freeze), status returns to Disconnected.

## 3. Address space browsing

- [ ] Tree shows the server root; expanding a folder lazily loads children.
- [ ] Select a node → **Node Details** pane shows its attributes (NodeId, DataType, Value, etc.).
- [ ] `F5` in the address space refreshes the tree.
- [ ] `Tab` cycles focus between panes; focus indicator moves correctly.

## 4. Subscribe / monitor

- [ ] Select `Simulation/Counter`, press `Enter` → it appears in **Monitored Variables** and the value **increments every second**.
- [ ] Subscribe to `SineWave`, `SquareWave`, `TriangleWave`, `SawtoothWave` → values update live and look correct (sine oscillates, square toggles 0/100, etc.).
- [ ] Select a monitored variable, press `Delete` → it is removed and stops updating.
- [ ] Subscribing to an invalid/again-existing node is handled gracefully (no crash; sensible log).

## 5. Write values (`W`)

- [ ] `W` on `Simulation/WritableString` (from the address space **or** monitored pane) → write dialog opens; writing a new string updates the value.
- [ ] `W` on `Simulation/WritableNumber` (Int32) → write an integer; value updates.
- [ ] `W` on `Simulation/ToggleBoolean` → write `true`/`false`; value updates.
- [ ] `W` on `Simulation/Counter` (read-only) → graceful "not writable" / access-denied message, no crash.
- [ ] **[v1] Culture-correct numeric write:** write `3.14` to a Double (e.g. `SineFrequency`) → stored as **3.14**.
      Confirm a comma form like `3,14` is **not** silently turned into `314` (it should be rejected or parsed as 3.14, never 314).

## 6. Scope view (`S`)

- [ ] In Monitored Variables, `Space` to select 2–5 variables, then `s`/`S` → Scope opens with one coloured trace per signal (Green/Cyan/Yellow/Magenta/White).
- [ ] Traces scroll in real time; time axis advances.
- [ ] `Space` pauses/resumes; `+`/`-` zoom the Y scale; `r` resets to auto-scale.
- [ ] `[` widens / `]` narrows the time window; `↑`/`↓` pan; when **paused**, `←`/`→` move the cursor.
- [ ] Selecting >5 signals is capped at 5 (no crash); opening Scope with a single signal works.
- [ ] Close Scope and re-open several times → no slowdown or leftover artifacts **[v1]** (dialogs are now disposed; watch for any creeping lag over ~5 opens).

## 7. CSV recording (`R` / `Ctrl+R`)

- [ ] Start recording (`r`/`R` in Monitored Variables, or `Ctrl+R`) → save dialog; pick a path; status shows Recording.
- [ ] Let it run ~15 s on Counter + a wave, then stop. Open the CSV:
  - [ ] Header is exactly `Timestamp,DisplayName,NodeId,Value,Status`.
  - [ ] Timestamps are ISO-8601 with milliseconds.
  - [ ] **[v1]** Values match what was displayed at each sample — **no duplicated or skipped rows** under load (the recorded Counter sequence should be monotonic with no repeats/gaps).
  - [ ] **[v1]** The **last** samples right before you stopped are present (the trailing queue is flushed on stop, not dropped).
- [ ] Start a *second* recording to a new file → it contains only new data, **no leftover rows** from the first session **[v1]**.

## 8. Auto-reconnect **[v1]**

- [ ] While connected and monitoring live values, **kill the test server** (Ctrl+C in its terminal).
  - [ ] App detects the drop: status shows Reconnecting (retries with backoff 1s, 2s, 4s, 8s), app does not crash.
- [ ] **Restart** the test server → the client **reconnects automatically** and values resume updating
      (you should not have to reconnect manually). This is the core v1 reconnect fix.

## 9. Configuration save / load

- [ ] With several nodes subscribed and settings set, `Ctrl+S` (Save) / `Ctrl+Shift+S` (Save As) → file written.
- [ ] Quit, relaunch with that config (`opcilloscope <file>`) → server, monitored nodes, and settings are restored.
- [ ] `Ctrl+O` opens a different config at runtime and applies it.
- [ ] **[v1] Round-trip fidelity:** in a saved config, confirm `securityMode`, `securityPolicy`,
      `samplingIntervalMs`, and `queueSize` survive a load→save→reload (they are no longer dropped).
- [ ] **[v1]** Saving over an existing config never leaves a half-written/corrupt file (atomic write); a leftover `*.tmp` should not remain.

## 10. Themes, help & docs

- [ ] View menu → toggle **Dark/Light**: every pane *and* open dialog restyles correctly; no unreadable text.
- [ ] `?` shows quick help; full Help dialog opens.
- [ ] **[v1]** Help shows each shortcut **once** (no duplicate `R`/`S`/`W` rows) and lists `F5`, the scope pan keys (`[` `]`, arrows), etc.
- [ ] **[v1] Trend is gone:** there is **no** `T` shortcut and **no** Trend Plot dialog/menu anywhere; pressing `T` does nothing. (Multi-signal Scope is the only plot.)
- [ ] README / CLAUDE keyboard tables match what the app actually does (spot-check 3–4 shortcuts).

## 11. General robustness

- [ ] Resize the terminal while running → layout reflows without corruption.
- [ ] Leave the app monitoring for several minutes → no runaway memory, no UI thread stalls.
- [ ] Trigger a few error paths (bad node, disconnect mid-browse, write to read-only) → all handled in the log, never a crash.

## 12. Release-artifact sanity

- [ ] **[v1]** Built binary reports the real version (not `0.0.0-alpha`) — the MinVer `v` tag prefix fix.
      Check via the About/Help screen or release artifact name.
- [ ] The release workflow produces `opcilloscope-<rid>.tar.gz` / `.zip` artifacts and the publish smoke step passes.

---
**Sign-off:** _________________________   **Date:** ____________   **Build/commit:** ____________
