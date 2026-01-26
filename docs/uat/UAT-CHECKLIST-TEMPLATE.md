# UAT Checklist Template

> **Instructions:** Copy this file and rename with date (e.g., `UAT-2026-01-26.md`).
> Fill in the test session info below, then mark items as you test.

---

## Test Session Info

- **Date:** YYYY-MM-DD
- **Tester:**
- **Version/Commit:**
- **Platform:** Windows / Linux / macOS
- **OPC UA Server:**
- **Overall Result:** PASS / FAIL / PARTIAL

---

## Application Startup

- [ ] App launches without arguments
- [ ] App launches with config file argument (`opcilloscope config.cfg`)
- [ ] App launches with `--config` flag
- [ ] `--help` displays usage information
- [ ] Invalid config file shows error gracefully

## Connection

- [ ] Connect dialog opens (menu or keyboard)
- [ ] Connect to valid OPC UA server succeeds
- [ ] Connection failure shows error in log
- [ ] Disconnect works cleanly
- [ ] Reconnect after disconnect works
- [ ] Auto-reconnect triggers on connection loss

## Address Space Browser

- [ ] Root nodes load on connect
- [ ] Expanding nodes loads children (lazy loading)
- [ ] R refreshes tree
- [ ] Enter subscribes to selected variable node

## Monitored Variables

- [ ] Subscribed variables appear in table
- [ ] Values update in real-time
- [ ] Timestamp updates on value change
- [ ] Status displays correctly (Good/Bad)
- [ ] Delete key unsubscribes selected variable
- [ ] Space toggles recording selection
- [ ] W opens write dialog for selected variable

## Node Details Panel

- [ ] Selecting node shows attributes
- [ ] NodeId, DisplayName, DataType display correctly
- [ ] Value shows for variable nodes

## Write Value

- [ ] Write dialog opens for writable nodes
- [ ] Write succeeds with valid value
- [ ] Write failure shows error message
- [ ] Written value reflects in monitored view

## Scope View (Multi-Signal)

- [ ] Opens with selected variables (S key)
- [ ] Up to 5 signals display simultaneously
- [ ] Signals use distinct colors
- [ ] Space pauses/resumes plotting
- [ ] +/- adjusts Y-axis scale
- [ ] R resets to auto-scale
- [ ] Time axis scrolls correctly

## Trend Plot View

- [ ] Opens for single variable (T key)
- [ ] Real-time value plotting works
- [ ] Pause/resume functions correctly

## CSV Recording

- [ ] Ctrl+R starts recording
- [ ] File save dialog appears
- [ ] Recording status shows in UI
- [ ] CSV file created with correct format
- [ ] Timestamps in ISO 8601 format
- [ ] Ctrl+R stops recording
- [ ] Only selected variables are recorded

## Configuration Files

- [ ] Ctrl+O opens existing config file
- [ ] Ctrl+S saves current configuration
- [ ] Ctrl+Shift+S saves as new file
- [ ] Recent files list updates
- [ ] Monitored nodes restore on load
- [ ] Server settings restore on load

## Themes

- [ ] Dark theme displays correctly (default)
- [ ] Light theme displays correctly
- [ ] Theme toggle works via View menu
- [ ] Theme persists across UI updates

## Keyboard Shortcuts

- [ ] ? shows help
- [ ] M opens menu
- [ ] Tab switches between panes
- [ ] Ctrl+Q quits application
- [ ] All documented shortcuts functional

## Error Handling

- [ ] Invalid NodeId handled gracefully
- [ ] Network timeout shows appropriate message
- [ ] Access denied errors logged
- [ ] App remains stable after errors

## Log View

- [ ] Connection events logged
- [ ] Errors appear in log
- [ ] Log scrolls with new entries


---

## Issues Found

1. **Severity:**
   **Description:**
   **Steps to Reproduce:**

2. **Severity:**
   **Description:**
   **Steps to Reproduce:**

3. **Severity:**
   **Description:**
   **Steps to Reproduce:**

**Severity levels:** Critical / Major / Minor / Cosmetic

---

## Notes

```
(Additional observations, environment details, etc.)


```

---

## Sign-Off

- **Tester:** __________________ Date: __________
- **Reviewer:** __________________ Date: __________
