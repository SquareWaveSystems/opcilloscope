# Deep repository review — pre-release (2026-07-07)

> [!IMPORTANT]
> **Historical snapshot — superseded by the 2026-07-11 release sweep.**
> The findings, source line numbers, workflow descriptions, warning counts, and
> test totals below describe the repository when this review was performed;
> they are not the current release status. The later sweep resolved the
> remaining timestamp/scope-key issues, lifecycle and reconnect races, static
> Terminal.Gui deprecations, SignAndEncrypt-by-default endpoint selection,
> packaging and installer gaps, and added locked six-RID publishing plus
> real-PTY E2E tests.
> Use the current [README](../README.md), [testing guide](TESTING.md), and
> [CI/release workflows](../.github/workflows/) as operational guidance. The
> original review body is retained unchanged as an audit record.

Scope: full read-through of all production source (`OpcUa/`, `App/`, `Configuration/`,
`Utilities/`, `Program.cs`), project files, and CI/CD workflows, plus a clean
Release build and full test run.

**Verdict: the codebase is in good shape for a major release.** Build is clean
(0 errors), all **696 tests pass**, the security posture is solid, and the
recent hardening work (reconnect races, CSV invariance, formula-injection
neutralization, secure-by-default certificates) clearly shows. The findings
below are ordered by severity; items 1–4 are the ones worth fixing before
tagging the release.

> **Update (same day):** findings 1–4 are now **fixed on this branch**
> (see the follow-up commit). CSV timestamps are UTC with a `Z` designator,
> the scope samples `RawValue` (and plots booleans as 0/1), the config-load
> path marshals all UI work via `UiThread.Run`/`UiThread.RunAsync`, and the
> `--connect` warning prints before terminal init. 710/710 tests pass,
> including 14 new tests covering the fixes.

---

## Correctness findings

### 1. UI-thread marshalling is inconsistent in `MainWindow.LoadConfigurationAsync` / `ConnectAsync`

The codebase's own convention (e.g. `DisconnectAsync`, `OnConnectionError`,
`WriteToAddressSpaceNodeAsync`) is that continuations after `await` may resume
off the UI thread, so all UI work is wrapped in `UiThread.Run(...)`. The
config-load path violates that convention in several places:

- `MainWindow.cs:1197-1200` — `Application.Run(pwDialog)` (password prompt) runs directly after `await _configService.LoadAsync(...)`.
- `MainWindow.cs:1240` and `MainWindow.cs:433` — `_addressSpaceView.Initialize(...)` called directly after awaited connects.
- `MainWindow.cs:1266-1273` and `MainWindow.cs:1291` — `_configService.Reset()`, `UpdateWindowTitle()`, and `MessageBox.ErrorQuery(...)` called directly after awaits.

Either these are latent cross-thread UI calls (the CLI `--config` startup path
exercises them on every launch), or the marshalling elsewhere is unnecessary —
the two patterns can't both be right. Recommend wrapping the UI portions of
`LoadConfigurationAsync` in `UiThread.Run` (or restructuring so the dialog and
view updates happen on the UI thread) to match the rest of the file.

### 2. CSV recordings can mix UTC and local timestamps in the same column

`CsvRecordingManager.WriteRecord` (`Utilities/CsvRecordingManager.cs:426-427`):

```csharp
var timestamp = item.Timestamp?.ToString("yyyy-MM-ddTHH:mm:ss.fff", ...)
    ?? DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff", ...);
```

`item.Timestamp` is the OPC UA `SourceTimestamp`, which is **UTC**;
the fallback `DateTime.Now` is **local time**. Rows in a single file can
therefore be hours apart for the same instant, and there is no timezone
designator to disambiguate. This undercuts the README's "locale-independent
ISO 8601" claim. Recommend normalizing both to UTC and appending `Z`
(e.g. `DateTime.UtcNow` fallback + `yyyy-MM-ddTHH:mm:ss.fffZ`, or `"O"` on a
UTC-kind value).

### 3. Scope plots the truncated display string instead of the full-precision value

`ScopeView.OnValueChanged` parses `node.Value` (`App/Views/ScopeView.cs:203-206`),
which is the display string produced by `SubscriptionManager.FormatValue` —
`"F2"`, i.e. quantized to two decimal places. Consequences for an app named
*opcilloscope*:

- Any signal with amplitude below ~0.01 flatlines in the scope.
- All plotted data is stair-stepped to 0.01 resolution.
- Boolean signals ("True"/"False") are not plottable at all.

The model already carries a lossless representation: `MonitoredNode.RawValue`
(round-trip, invariant). Recommend parsing `RawValue` (falling back to
`Value`), and optionally mapping booleans to 0/1.

### 4. The "--connect not implemented" warning is invisible

`Program.cs:83-88` writes the warning to stderr *after* `Application.Init()`,
so it is lost in the alternate screen buffer — the same problem the config-path
validation at `Program.cs:63-67` was explicitly moved before init to avoid.
Users passing an `opc.tcp://` URL get silence. Move the check before
`Application.Init()` (the URL is already parsed by then).

### 5. Monitored-variable timestamps display raw UTC beside local log times

`MonitoredNode.TimestampString` (`OpcUa/Models/MonitoredNode.cs:84`) formats the
UTC `SourceTimestamp` as bare `HH:mm:ss`, while the log pane shows local
`DateTime.Now` times. In any non-UTC timezone the "Time" column visibly
disagrees with the log for the same event. Recommend `ToLocalTime()` for
display (CSV export is a separate concern — see finding 2).

### 6. Dead/incorrect key mappings in `ScopeView.OnKeyDown`

`App/Views/ScopeView.cs:911-919`: `KeyCode.D0 when key.IsShift` is commented
"`+` key" but Shift+0 is `)` on US layouts; `KeyCode.D9 when key.IsShift`
(`(`) is mapped to zoom-out. Harmless in practice because the `'='`/`'+'`/`'-'`
cases handle the real keys, but the shifted-digit cases are wrong and should be
removed or corrected.

---

## Robustness notes (lower priority)

- **`OpcUaClientWrapper._session` swap race** (`OpcUa/OpcUaClientWrapper.cs:265-287, 638-660`):
  `Disconnect`/`DisconnectAsync` both do `var session = _session; if (session != null) { _session = null; ... }`.
  Two concurrent callers can both capture the same session and double-close/dispose it
  (exceptions are swallowed, but `Disconnected` fires twice). A small lock around the
  field swap (as already done for `_reconnectCts`) would close it.
- **Stuck "(reconnecting...)" rows**: if all four reconnect attempts fail,
  `ConnectionManager` reports `Disconnected` but the monitored rows keep the stale
  "(reconnecting...)" value until a manual reconnect. Consider marking them Bad/disconnected.
- **Doc/code mismatch**: `SubscriptionSettings.SamplingIntervalMs` doc comment says
  "Valid range: 0-10000" (`Configuration/Models/OpcilloscopeConfig.cs:83`) but
  `SubscriptionManager` clamps to 0–60000 (`OpcUa/SubscriptionManager.cs:57`).
- **Write support for custom data types**: `DataTypeResolver.Resolve` maps any non-ns0
  data type (including server-defined subtypes of Double etc.) to `Variant`, so writes
  send the raw string and may be rejected by the server. Worth documenting as a limitation.
- **`WriteValueDialog` trims input** (`ValidateAndParse`), so a string value with
  intentional leading/trailing whitespace cannot be written.
- **`NodeBrowser.GetChildrenAsync`** issues one `Read` per variable for data-type names;
  a single batched `ReadAsync` would reduce chatter on large folders (the cache already
  helps on repeat types).
- **`MainWindow.Dispose`** does not unhook `_connectionManager.StateChanged/ValueChanged/
  ConnectionError/AutoReconnectTriggered` or `_addressSpaceView.NodeSelected/...`.
  Benign for an app-lifetime window; listed for completeness.

## Tech debt

- **200 CS0618 warnings** in a Release build, all from Terminal.Gui 2.4.5 deprecations
  (`Application.Invoke`, `Application.AddTimeout`, static `Clipboard`, ...). These are
  the announced removal set for a future Terminal.Gui release — worth a scheduled
  migration to the instance-based `IApplication` APIs, and consider `TreatWarningsAsErrors`
  with a curated `NoWarn` afterwards so new warnings can't accumulate silently.

## Security posture — good

- Certificate validation is secure-by-default; `--insecure` is an explicit, logged opt-in
  decided per-certificate (`OpcUaClientWrapper.OnCertificateValidation`).
- Credentials force secure-endpoint preference, are never persisted, and a clear warning
  is logged if they would travel over `SecurityMode=None`. (Consider hard-failing that
  case unless `--insecure` is set, rather than warning only.)
- CSV formula injection (CWE-1236) is neutralized with a numeric-value carve-out;
  RFC 4180 quoting applied after neutralization.
- Config loading caps file size (1 MB), rejects newer major versions, and normalizes
  explicit JSON nulls.

## Release engineering — good

- CI builds, tests, publishes, and smoke-tests the self-contained binary under a PTY.
- Release workflow: 6 RIDs, per-platform smoke tests, license + third-party notices
  bundled, SHA256SUMS generated, MinVer-driven versioning with the informational-version
  About display already handling `+metadata` stripping.
- `InvariantGlobalization=true` plus the explicit `InvariantCulture` call sites make the
  locale behavior consistent; tests cover the tricky cultures.

## Test suite

696/696 passing locally (Release, .NET 10). Coverage is strong across
configuration round-trips, CSV invariance/injection, subscription lifecycle,
reconnect, authentication, and the keybinding system. The scope/braille layer
has unit coverage; finding 3 above suggests adding a test asserting that
sub-0.01-amplitude signals survive into `SeriesData.Samples`.
