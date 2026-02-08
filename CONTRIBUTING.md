# Contributing to Opcilloscope

Thank you for your interest in contributing to Opcilloscope!

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/YOUR-USERNAME/opcilloscope.git`
3. Create a branch: `git checkout -b feature/your-feature-name`
4. Make your changes
5. Run tests: `dotnet test`
6. Commit and push
7. Open a Pull Request

## Development Setup

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Linux only:** ICU libraries (`sudo apt install libicu-dev` on Debian/Ubuntu, `sudo dnf install libicu-devel` on Fedora/RHEL)

### Building and Testing

```bash
dotnet restore
dotnet build
dotnet test
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
    └── Opcilloscope.Tests/       # xUnit tests
```

### Key Patterns

- **Thread marshalling**: Use `Application.Invoke()` or `UiThread.Run()` for UI updates from background threads
- **Lazy loading**: Address space tree loads children on-demand
- **Subscriptions**: Uses OPC UA Publish/Subscribe (not polling)
- **Integration tests**: Run against an in-process OPC UA test server (no external dependencies needed)
