# UAT Checklist

## Application Startup
- [ ] App launches without arguments
- [ ] App launches with config file argument (`opcscope config.opcscope`)
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
- [ ] F5 refreshes tree
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
- [ ] Opens with selected variables (Ctrl+G)
- [ ] Up to 5 signals display simultaneously
- [ ] Signals use distinct colors
- [ ] Space pauses/resumes plotting
- [ ] +/- adjusts Y-axis scale
- [ ] R resets to auto-scale
- [ ] Time axis scrolls correctly

## Trend Plot View
- [ ] Opens for single variable
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
- [ ] Ctrl+N creates new configuration
- [ ] Ctrl+O opens existing .opcscope file
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
- [ ] F1 shows help
- [ ] F10 opens menu
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
