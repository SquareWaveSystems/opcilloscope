# Opcilloscope - Marketing Description Document

**Prepared for**: Marketing Strategy Development
**Product**: opcilloscope
**Version**: 1.0.0
**Date**: January 2026

---

## Executive Summary

**Opcilloscope** is a lightweight, open-source, terminal-based OPC UA client designed for industrial automation professionals. It enables real-time monitoring, visualization, and troubleshooting of OPC UA server data directly from the command line—without the bloat, complexity, or licensing costs of traditional desktop clients.

**One-line pitch**: *The lightweight OPC UA client that gets out of your way.*

---

## The Problem We Solve

### Industry Pain Points

1. **Heavyweight Desktop Applications**: Existing OPC UA clients (UAExpert, Prosys, Kepware) are large, resource-intensive GUI applications that require significant installation and configuration time.

2. **Licensing Costs**: Many professional OPC UA tools require expensive per-seat licenses, creating barriers for smaller teams and individual engineers.

3. **Platform Lock-in**: Most clients are Windows-only, excluding automation professionals who work in Linux/macOS environments or need to run tools on edge devices.

4. **Workflow Friction**: GUI-based tools interrupt terminal-centric workflows common among modern DevOps and automation engineers.

5. **Portability Issues**: Traditional tools can't easily be deployed to headless servers, embedded systems, or cloud VMs for remote troubleshooting.

---

## Our Solution

Opcilloscope is a **terminal-native OPC UA client** that provides:

- **Instant Deployment**: Single portable binary, zero dependencies, one-line installation
- **Cross-Platform Support**: Windows, Linux, and macOS (x64 & ARM64)
- **Keyboard-First Interface**: Efficient workflows without leaving the terminal
- **Real-Time Visualization**: Multi-signal oscilloscope view for live data inspection
- **Zero Cost**: MIT open-source license with no usage restrictions

---

## Target Audience

### Primary Users

| Persona | Description | Key Pain Points |
|---------|-------------|-----------------|
| **Automation Engineer** | Designs and maintains PLC/SCADA systems | Needs quick diagnostics without heavyweight tools |
| **Systems Integrator** | Commissions OPC UA systems across sites | Requires portable, repeatable configurations |
| **DevOps/SRE Engineer** | Manages industrial infrastructure | Wants terminal-native tools that fit existing workflows |
| **Maintenance Technician** | Troubleshoots production issues | Needs fast, reliable monitoring during incidents |

### Target Industries

- **Manufacturing**: Discrete and process manufacturing, packaging, assembly
- **Energy & Utilities**: Power generation, oil & gas, water treatment
- **Building Automation**: HVAC, lighting, access control systems
- **Pharmaceutical**: Process validation, batch monitoring
- **Food & Beverage**: Production line monitoring, quality control
- **Automotive**: Assembly line diagnostics, robotics monitoring

### Company Size

- Small to mid-market manufacturers (50-500 employees)
- System integrators and automation consultancies
- Enterprise DevOps teams managing industrial infrastructure

---

## Key Features & Benefits

### 1. Address Space Browser
**Feature**: Hierarchical tree view of OPC UA server namespace with lazy loading
**Benefit**: Navigate large address spaces efficiently without memory issues

### 2. Real-Time Monitoring
**Feature**: Live subscription-based updates (not polling) with configurable intervals
**Benefit**: See value changes instantly with minimal server/network load

### 3. Multi-Signal Scope View
**Feature**: Visualize up to 5 signals simultaneously in oscilloscope-style display
**Benefit**: Correlate multiple variables in real-time for faster troubleshooting

### 4. CSV Data Recording
**Feature**: Background recording with ISO 8601 timestamps and non-blocking I/O
**Benefit**: Capture data for analysis without interrupting monitoring

### 5. Write Support
**Feature**: Write values to OPC UA nodes with type-safe validation
**Benefit**: Test setpoints and triggers directly from the terminal

### 6. Configuration Management
**Feature**: Save/load JSON configuration files with server, settings, and node lists
**Benefit**: Repeatable commissioning workflows across multiple sites

### 7. Theme Support
**Feature**: Dark and Light themes with real-time switching
**Benefit**: Comfortable viewing in any environment

### 8. Automatic Reconnection
**Feature**: Exponential backoff reconnection when connection drops
**Benefit**: Resilient monitoring during network instability

---

## Competitive Differentiation

| Feature | opcilloscope | UAExpert | Prosys Client | Kepware |
|---------|--------------|----------|---------------|---------|
| **Price** | Free (MIT) | Free (limited) | Commercial | Commercial |
| **Platform** | Win/Linux/macOS | Windows | Win/Linux/macOS | Windows |
| **Installation** | Single binary | Installer | Installer | Installer |
| **Interface** | Terminal/TUI | GUI | GUI | GUI |
| **Size** | ~50MB | ~200MB+ | ~150MB+ | ~500MB+ |
| **ARM Support** | Yes | No | Limited | No |
| **CLI Scriptable** | Yes | No | No | No |
| **Edge Device Ready** | Yes | No | No | No |

### Unique Selling Points

1. **"The grep of OPC UA clients"** - Simple, single-purpose, powerful
2. **Terminal-native** - Built for command-line workflows from the ground up
3. **Truly portable** - Runs on Raspberry Pi to enterprise servers
4. **Zero friction** - One command to install, immediate productivity
5. **Open source** - No vendor lock-in, community-driven development

---

## Use Cases

### 1. Commissioning & Startup
*"Verify PLC tags are publishing correctly before go-live"*

Engineers can quickly connect to an OPC UA server, browse the address space, subscribe to critical variables, and validate data flow—all from a terminal session on any platform.

### 2. Production Troubleshooting
*"Diagnose why the line stopped without leaving the terminal"*

When production issues occur, maintenance teams can SSH into a server, launch opcilloscope, and monitor relevant tags in real-time. The multi-signal scope helps correlate cause and effect.

### 3. Remote Monitoring
*"Monitor industrial data on headless servers or edge devices"*

Opcilloscope runs on any Linux server without GUI dependencies, making it ideal for headless monitoring scenarios and edge computing deployments.

### 4. CI/CD Integration
*"Validate OPC UA server configurations automatically"*

The command-line interface and configuration files enable automated testing of OPC UA server deployments in continuous integration pipelines.

### 5. Training & Documentation
*"Export CSV snapshots for operator training"*

CSV recording capability allows engineers to capture sample data for training materials, documentation, or offline analysis.

---

## Technical Specifications

### Supported Platforms
| Platform | Architecture | Format |
|----------|--------------|--------|
| Linux | x64 | .tar.gz |
| Linux | ARM64 | .tar.gz |
| macOS | x64 (Intel) | .tar.gz |
| macOS | ARM64 (Apple Silicon) | .tar.gz |
| Windows | x64 | .zip |
| Windows | ARM64 | .zip |

### System Requirements
- **Memory**: ~50MB RAM
- **Disk**: ~50MB (self-contained binary)
- **Network**: TCP access to OPC UA server (port 4840 default)
- **Dependencies**: None (self-contained .NET runtime)

### OPC UA Capabilities
- Browse address space (Objects, Variables, Methods)
- Read/Write variable values
- Native OPC UA Subscriptions with MonitoredItems
- Anonymous authentication (username/password planned)
- Certificate handling with auto-accept option

### Configuration Format
JSON-based `.cfg` files containing:
- Server endpoint URL and security settings
- Publishing and sampling intervals
- List of monitored nodes with display names
- Metadata (name, description, timestamps)

---

## Installation

### Linux/macOS
```bash
curl -fsSL https://github.com/BrettKinny/opcilloscope/releases/latest/download/install.sh | bash
```

### Windows (PowerShell)
```powershell
iwr -useb https://github.com/BrettKinny/opcilloscope/releases/latest/download/install.ps1 | iex
```

### Manual Download
Download platform-specific archives from GitHub Releases.

---

## Keyboard Shortcuts

Opcilloscope is designed for keyboard efficiency:

| Shortcut | Action |
|----------|--------|
| `Enter` | Subscribe to selected node |
| `Delete` | Unsubscribe from variable |
| `Space` | Toggle scope selection |
| `W` | Write value to variable |
| `Ctrl+G` | Open multi-signal scope |
| `Ctrl+R` | Toggle CSV recording |
| `Ctrl+O/S` | Open/Save configuration |
| `F5` | Refresh address space |
| `Tab` | Switch between panels |

---

## Pricing & Licensing

**License**: MIT (permissive open-source)

- Free for commercial and personal use
- No per-seat licensing fees
- No usage restrictions
- Full source code available
- Community contributions welcome

---

## Company Background

**Square Wave Systems** specializes in automation software for mid-market manufacturers. Our tools are built by automation engineers, for automation engineers—with deep domain expertise in industrial protocols and real-world production environments.

---

## Brand Guidelines

### Naming
- **Product name**: opcilloscope (lowercase)
- **In sentences**: "opcilloscope is a terminal-based OPC UA client"
- **Never**: Opcilloscope, OPCilloscope, OPC-illoscope

### Taglines
- Primary: *"The lightweight OPC UA client that gets out of your way."*
- Technical: *"Terminal-native OPC UA monitoring."*
- Casual: *"OPC UA, but make it terminal."*

### Tone
- Technical but approachable
- Confident without being arrogant
- Practical and solutions-focused
- Respects users' time and intelligence

---

## Key Messages

### For Engineers
> "Work where you want, without bloated desktop apps. opcilloscope gives you real-time OPC UA monitoring directly in your terminal."

### For IT/DevOps
> "Terminal-native monitoring, zero installation friction. Deploy to any server, edge device, or workstation in seconds."

### For Management
> "Open-source OPC UA client with no licensing costs. Reduce tooling expenses without sacrificing capability."

### For Integrators
> "Portable, scriptable configurations for repeatable commissioning. Same workflow on every site."

---

## Launch Channels

### Primary Channels (Priority)

| Channel | Platform | Target Audience | Content Strategy |
|---------|----------|-----------------|------------------|
| **SASE Slack** | Slack | Industrial automation professionals | Announce in relevant channels, share use cases, engage in discussions |
| **LinkedIn** | Social | Automation engineers, managers, integrators | Professional posts, demo videos, thought leadership articles |
| **OTEE Discord** | Discord | OT/ICS security and engineering community | Technical deep-dives, live demos, community engagement |
| **Reddit r/commandline** | Reddit | CLI enthusiasts, terminal power users | Show terminal-native workflow, highlight keyboard efficiency |
| **Self-Hosted Blog** | Web | Organic search, direct traffic | SEO-optimized tutorials, release announcements, use case stories |
| **Email Newsletter** | Email | Engaged prospects, existing users | Release notes, tips & tricks, industry insights |
| **GitHub** | Open Source | Developers, contributors, technical evaluators | README optimization, awesome-list submissions, community building |

### Secondary Channels (Industrial Automation Communities)

| Community | Platform | Focus | Notes |
|-----------|----------|-------|-------|
| **PLCTalk Forum** | Forum | PLC programming, industrial automation | Long-standing community, high engagement |
| **Reddit r/PLC** | Reddit | PLC/automation discussion | 50K+ members, active daily |
| **Reddit r/SCADA** | Reddit | SCADA systems, industrial control | Niche but highly targeted |
| **Reddit r/IndustrialAutomation** | Reddit | Broader automation topics | Growing community |
| **Control.com** | Forum | Process control, instrumentation | Professional engineers |
| **Automation World** | News/Forum | Manufacturing automation news | Industry publication with community |
| **ISA (International Society of Automation)** | Professional | Automation standards, education | Professional network, local chapters |
| **OPC Foundation Community** | Forum | OPC UA specifically | Directly relevant, technical audience |
| **Ignition Forum (Inductive Automation)** | Forum | Ignition/SCADA users | Often need OPC UA tools |
| **Codesys Forum** | Forum | IEC 61131-3, PLC development | Technical automation developers |

### Developer & DevOps Communities

| Community | Platform | Angle |
|-----------|----------|-------|
| **Hacker News** | News | "Show HN" post, technical differentiators |
| **Reddit r/selfhosted** | Reddit | Self-hosted monitoring, homelab use cases |
| **Reddit r/homelab** | Reddit | Industrial protocol monitoring for enthusiasts |
| **Dev.to** | Blog | Technical tutorials, launch announcement |
| **Lobsters** | News | Technical deep-dive, open source focus |
| **ICS/OT Security Twitter/X** | Social | Security angle, protocol visibility |

### Content Calendar Suggestions

**Week 1 (Soft Launch)**
- GitHub README polish, awesome-list PRs
- Blog post: "Why We Built opcilloscope"
- Email to existing contacts

**Week 2 (Community Launch)**
- SASE Slack announcement
- OTEE Discord introduction
- Reddit r/commandline "Show r/commandline" post

**Week 3 (Professional Launch)**
- LinkedIn announcement + demo video
- PLCTalk forum post
- Reddit r/PLC, r/SCADA posts

**Week 4 (Expansion)**
- Hacker News "Show HN"
- Dev.to technical article
- Control.com, OPC Foundation forums

---

## Metrics for Success

### Adoption Metrics
- GitHub stars and forks
- Download counts per platform
- Active installations (opt-in telemetry)

### Engagement Metrics
- GitHub issues and pull requests
- Community forum activity
- Social media mentions

### Business Metrics
- Leads generated for Square Wave Systems
- Brand awareness in target industries
- Partnership inquiries from system integrators

---

## Appendix: Screenshot Descriptions

*(For marketing asset creation)*

1. **Main Interface**: Four-panel layout showing address space tree, monitored variables table, node details, and log output
2. **Scope View**: Multi-signal oscilloscope with 3-5 signals in distinct colors, time-based X-axis
3. **Connection Dialog**: Simple endpoint URL input with security mode selection
4. **Configuration File**: JSON configuration example in terminal editor
5. **Terminal Installation**: One-line curl command executing successfully

---

## Contact

For questions about this document or opcilloscope:

- **GitHub**: https://github.com/BrettKinny/opcilloscope
- **Issues**: https://github.com/BrettKinny/opcilloscope/issues
- **Company**: Square Wave Systems

---

*Document prepared for marketing strategy development. All features described are available in version 1.0.0.*
