# Changelog

All notable changes to Opcilloscope will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-01-06

### Added

- OPC UA server connection with anonymous authentication
- Address space browser with lazy-loading TreeView
- Real-time value monitoring via OPC UA subscriptions
- Multi-signal Scope view (up to 5 signals) with auto-scaling
- Single-signal TrendPlot view
- CSV recording of monitored values
- Write value support for writable nodes
- Node details panel showing attributes
- Configuration file support (.opcilloscope JSON format)
- Dark and Light themes
- Cross-platform support (Windows, Linux, macOS)

### Technical

- Built with .NET 10, Terminal.Gui v2, and OPC Foundation UA-.NETStandard
- In-process test server for integration testing
