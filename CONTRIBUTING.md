# Contributing to opcilloscope

Thank you for your interest in contributing to opcilloscope!

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/YOUR-USERNAME/opcilloscope.git`
3. Create a branch: `git checkout -b feature/your-feature-name`
4. Make your changes
5. Run the applicable test layers described in [docs/TESTING.md](docs/TESTING.md)
6. Commit and push
7. Open a Pull Request

## Development Setup

### Prerequisites

- [.NET SDK 10.0.109](https://dotnet.microsoft.com/download/dotnet/10.0),
  matching the exact version pinned in `global.json`
- **Linux only:** ICU libraries (`sudo apt install libicu-dev` on Debian/Ubuntu, `sudo dnf install libicu-devel` on Fedora/RHEL)

### Building and Testing

If needed, install the pinned SDK with Microsoft's `dotnet-install.sh` using
`--version 10.0.109`; the repository intentionally does not roll forward to a
different feature band.

```bash
dotnet restore Opcilloscope.sln
dotnet build Opcilloscope.sln
dotnet test Opcilloscope.sln

# Linux only: publish and test the real TUI through a PTY
dotnet test Tests/Opcilloscope.E2ETests/Opcilloscope.E2ETests.csproj
```

## Code Style

- Follow C# conventions and .NET naming guidelines
- Enable nullable reference types
- Keep methods focused and small
- Use meaningful variable and method names

## Pull Request Guidelines

1. **Keep PRs focused** - One feature or fix per PR
2. **Write tests** - Add tests for new functionality
3. **Update documentation** - Update README if adding user-facing features
4. **Follow existing patterns** - Look at existing code for style guidance

## Reporting Issues

When reporting issues, please include:

- OS and version
- .NET SDK version (`dotnet --version`)
- Steps to reproduce
- Expected vs actual behavior
- Any error messages or logs

## Architecture Overview

```
Opcilloscope/
├── App/              # UI (Terminal.Gui v2)
│   ├── Views/        # View panels
│   ├── Dialogs/      # Modal dialogs
│   ├── Keybindings/  # Key binding system
│   └── Themes/       # Theme system
├── Configuration/    # Config file load/save
├── OpcUa/            # OPC UA client logic
│   └── Models/       # Data models
├── Utilities/        # Helpers (logging, threading, CSV)
└── Tests/            # Unit and integration tests
    ├── Opcilloscope.TestServer/  # In-process OPC UA test server
    ├── Opcilloscope.Tests/       # Cross-platform xUnit tests
    └── Opcilloscope.E2ETests/    # Linux published-binary PTY tests (outside the solution)
```

### Key Patterns

- **Thread marshalling**: Use `UiThread.Run()` for UI updates from background threads; the legacy static `Application` API is obsolete
- **Lazy loading**: Address space tree loads children on-demand
- **Subscriptions**: Uses OPC UA client/server subscriptions and monitored items (not repeated reads and not the OPC UA PubSub transport model)
- **Integration tests**: Run against an in-process OPC UA test server (no external dependencies needed)
- **Security profiles**: Automatic/omitted or partial profiles require the strongest matching `SignAndEncrypt` endpoint; explicit `Sign` opts into signed-but-unencrypted traffic, explicit anonymous `None` opts into unsecured plaintext, and `--insecure` bypasses certificate validation only
