# TrafficLens — Project Specification

## 1. Project Goal

TrafficLens is a local-first Windows desktop network monitoring application.

Its purpose is to show:

- Real-time download/upload speed
- Total network usage
- Per-application network usage
- Top bandwidth-consuming applications
- Active network connections
- Network interface usage
- Historical traffic statistics
- A compact floating network widget

TrafficLens V1 is monitoring-only.

Do NOT implement blocking, bandwidth limiting, firewall manipulation, packet modification, or custom kernel drivers in V1.

---

## 2. Platform & Stack

Target:

- Windows 10/11
- x64

Preferred stack:

- C#
- .NET
- WPF
- MVVM
- Dependency Injection
- SQLite
- Async/background collectors
- Local structured logging

The application must work locally without:

- Cloud services
- Web servers
- Docker
- Node.js backend
- External accounts

---

## 3. Localization / Persian Support

TrafficLens must be localization-ready from the first milestone.

Initial UI language may be English, but the architecture must support adding Persian later without redesigning the application.

Requirements:

- Do not hard-code user-facing UI strings directly in Views/ViewModels where avoidable.
- Use .NET resource files or an equivalent localization service.
- Design for at least:
  - English (`en-US`)
  - Persian (`fa-IR`)
- Support runtime language selection from Settings in a later milestone.
- Persist the user's selected language locally.
- Prepare the UI architecture for RTL.
- Persian mode must support `FlowDirection=RightToLeft` where appropriate.
- Numeric/network values such as IP addresses, ports, speeds, protocol names, and file paths should remain readable and may remain LTR inside RTL layouts.
- Do not translate technical identifiers, executable names, IP addresses, protocol names, or paths unless explicitly required.
- Avoid layouts that break when text expands after translation.
- All future user-facing strings should be localization-ready.

Do NOT spend time translating the full UI during Milestone 0. Only establish the localization foundation.

---

## 4. Solution Structure

Use:

TrafficLens/
- src/
  - TrafficLens.App/
  - TrafficLens.Core/
  - TrafficLens.Network/
  - TrafficLens.Infrastructure/
- tests/
- docs/
- AGENTS.md
- TASKS.md
- CHANGELOG.md
- README.md
- TRAFFICLENS_SPEC.md
- TrafficLens.sln

Responsibilities:

TrafficLens.App:
- WPF UI
- Views
- ViewModels
- Navigation
- Tray
- Floating widget
- Localization resources

TrafficLens.Core:
- Domain models
- Interfaces
- Business logic
- Shared abstractions

TrafficLens.Network:
- Windows network collectors
- Network adapters
- Connections
- Process mapping
- Traffic collection

TrafficLens.Infrastructure:
- SQLite
- Repositories
- Settings
- Logging
- Export

Avoid circular dependencies.

---

## 5. V1 Features

### Dashboard

Display:

- Current Download
- Current Upload
- Total Download
- Total Upload
- Total Traffic
- Active Adapter
- Network Status

Support readable units:

- KB/s
- MB/s
- Mbps
- GB

### Live Graph

Display real-time:

- Download
- Upload

Target time ranges:

- 30 seconds
- 1 minute
- 5 minutes

Never block the UI thread.

### Applications

Show network usage by application/process when technically reliable.

Display:

- App name
- Executable name
- PID where useful
- Icon
- Current download
- Current upload
- Downloaded bytes
- Uploaded bytes
- Total traffic

Allow sorting.

### Top Consumers

Show:

- Top consumer right now
- Top consumer today
- Top 5 applications

Sorting options should eventually include:

- Download speed
- Upload speed
- Total speed
- Downloaded data
- Uploaded data
- Total data

### Connections

Create an Active Connections view.

Display when available:

- Process
- PID
- Protocol
- Local IP
- Local Port
- Remote IP
- Remote Port
- Connection State

Start with TCP.

Add UDP only if implementation is reliable.

Never fabricate unavailable data.

### Network Interfaces

Support systems containing:

- Ethernet
- Wi-Fi
- OpenVPN
- WireGuard
- VPN adapters
- Hyper-V
- VMware
- Other virtual adapters

Provide:

- All Interfaces
- Individual interface selection

Never assume the first adapter is the active internet connection.

### Floating Widget

Optional compact widget showing:

- Download speed
- Upload speed
- Top consuming application

Requirements:

- Always On Top option
- Movable
- Remember position
- Low resource usage
- Enable/Disable

### System Tray

Support:

- Open TrafficLens
- Show/Hide Widget
- Minimize to Tray
- Exit

### History

Use SQLite.

Provide:

- Today
- Yesterday
- Last 7 Days
- Last 30 Days
- Lifetime

Do NOT store every packet.

Use efficient aggregated samples.

Design storage so retention policies can be added later.

### Application Details

Selecting an application should eventually show:

- Name
- Icon
- Executable path
- Current speed
- Download
- Upload
- Total
- Daily history
- Active connections
- Usage graph

### Alerts

Architecture should support local alerts such as:

- App used more than X GB
- App exceeded X Mbps
- Daily system traffic exceeded X GB

Use Windows notifications when implemented.

### Settings

Plan for:

- Start with Windows
- Launch minimized
- Minimize to tray
- Floating widget
- Always On Top
- Unit preference
- Refresh interval
- Database retention
- Theme
- Language

Primary UI theme:

Dark.

### Export

V1 should eventually support CSV export for:

- Application usage
- Daily usage
- Hourly usage

---

## 6. Network Collection

Prefer built-in Windows mechanisms.

Investigate and document:

- Windows Performance Counters
- IP Helper API
- ETW
- Other appropriate Windows networking APIs

Do not add WinDivert or custom kernel drivers in V1 unless a later architectural decision explicitly requires it.

Use abstractions such as:

- `INetworkTrafficCollector`
- `IProcessTrafficCollector`
- `IConnectionProvider`
- `INetworkAdapterProvider`

Collector implementations must be replaceable.

Create:

`docs/NETWORK_COLLECTION.md`

Document:

- Selected mechanism
- Accuracy
- Limitations
- Required privileges
- Performance
- Windows compatibility
- Alternatives considered

If accurate per-process byte accounting cannot be achieved with the selected mechanism, document the limitation.

Never fake measurements.

---

## 7. Performance

TrafficLens should remain lightweight during long-running operation.

Avoid:

- Excessive polling
- UI blocking
- High CPU usage
- Memory leaks
- Unlimited memory collections
- Excessive SQLite writes

Handle gracefully:

- Adapter disconnect
- VPN connect/disconnect
- Process termination
- PID reuse
- Connection termination
- Database errors
- Missing permissions

The application must not crash because a monitored process or adapter disappears.

---

## 8. Privacy & Security

TrafficLens V1 monitors metadata and traffic usage.

Do NOT capture:

- Packet payloads
- Passwords
- Tokens
- Browser content
- Messages
- TLS contents

All collected information remains local.

Never commit secrets.

---

## 9. Required Project Documentation

Maintain:

- `AGENTS.md`
- `README.md`
- `TASKS.md`
- `CHANGELOG.md`
- `docs/ARCHITECTURE.md`
- `docs/PROJECT_STATUS.md`
- `docs/DECISIONS.md`
- `docs/ROADMAP.md`
- `docs/NETWORK_COLLECTION.md`
- `docs/DATABASE.md`

Documentation must reflect actual code.

Never mark unfinished work as completed.

---

## 10. Multi-Agent Development

CRITICAL:

TrafficLens may be developed using OpenCode, Codex, Claude Code, or other coding agents.

Any agent must be able to continue the project without the user explaining previous work.

`AGENTS.md` must instruct future agents to read:

1. `AGENTS.md`
2. `docs/PROJECT_STATUS.md`
3. `TASKS.md`
4. `docs/ARCHITECTURE.md`
5. `docs/DECISIONS.md`

before making changes.

Do not redesign working architecture without documenting why.

At the end of every meaningful development session update:

- `PROJECT_STATUS.md`
- `TASKS.md`
- `CHANGELOG.md`
- Relevant architecture documentation

---

## 11. Task IDs

All significant work should use TrafficLens task IDs.

Format:

`TL-XXX`

Initial backlog:

- TL-001 Project Bootstrap
- TL-002 Global Network Collector
- TL-003 Download/Upload Calculation
- TL-004 Network Adapter Detection
- TL-005 Dashboard
- TL-006 Live Traffic Graph
- TL-007 Per-Process Traffic
- TL-008 Active Connections
- TL-009 SQLite History
- TL-010 Floating Widget
- TL-011 System Tray
- TL-012 Alerts
- TL-013 Settings
- TL-014 CSV Export
- TL-015 Packaging / Installer
- TL-016 Localization / Persian UI

Add new IDs when required.

Do not unnecessarily renumber existing IDs.

---

## 12. Development Milestones

M0:
Project bootstrap, documentation, and localization foundation.

M1:
Global network monitoring.

M2:
Dashboard and live graph.

M3:
Network interfaces.

M4:
Per-process/application traffic.

M5:
Active connections.

M6:
SQLite history.

M7:
System tray and floating widget.

M8:
Alerts and settings.

M9:
Stability, performance, tests and packaging.

M10:
Full Persian localization and language switcher if not completed earlier.

Work incrementally.

Do NOT attempt all milestones simultaneously.

---

## 13. Testing

Test meaningful logic including:

- Traffic calculations
- Unit conversion
- Aggregation
- SQLite repositories
- Settings
- History calculations
- Process identity handling
- Localization lookup/fallback where appropriate

Do not create meaningless tests just to increase test count.

Never claim a test passed unless it actually ran successfully.

---

## 14. Git Rules

Use Git.

Create meaningful commits such as:

- `feat: add global traffic collector`
- `feat: add dashboard`
- `feat: add process traffic monitoring`
- `fix: handle disconnected network adapter`
- `docs: update TrafficLens architecture`

Avoid combining unrelated major changes.

Never destroy existing working history.

---

## 15. Agent Efficiency Rules

Minimize unnecessary token/context usage.

Before exploring the repository extensively:

1. Read `AGENTS.md`
2. Read `docs/PROJECT_STATUS.md`
3. Read `TASKS.md`

Only inspect files relevant to the current task.

Do not repeatedly re-read the entire repository.

Do not regenerate existing working files unnecessarily.

Do not rewrite large files when a small targeted change is sufficient.

Do not repeatedly explain the entire project in responses.

Prefer concise engineering status reports.

Do not perform broad refactors unless required by the current task.

---

## 16. Handoff Report

At the end of each milestone/session return:

TRAFFICLENS STATUS

Current Milestone:

Task IDs:

Completed:

Verified:

Build:

Tests:

Files Changed:

Known Issues:

Architecture Decisions:

Documentation Updated:

Git Commit:

Next Recommended Task:

Keep this report concise.

---

## 17. Current Instruction

If this is a new repository, execute ONLY:

MILESTONE 0 / TL-001

Tasks:

1. Inspect available .NET environment.
2. Create TrafficLens solution.
3. Create App/Core/Network/Infrastructure projects.
4. Configure references.
5. Establish basic MVVM/DI architecture.
6. Configure basic local logging.
7. Create required documentation.
8. Initialize Git if necessary.
9. Create minimal dark TrafficLens main window.
10. Add localization foundation using resource files or equivalent.
11. Add at least English resource structure and Persian-ready structure.
12. Ensure architecture can support RTL later.
13. Build solution.
14. Run application and verify startup where environment permits.
15. Update `PROJECT_STATUS.md`.
16. Update `TASKS.md`.
17. Create initial Git commit if appropriate.

Do NOT implement network monitoring yet.

Do NOT begin TL-002 until TL-001 is verified.

---

# Golden Rules

Keep TrafficLens LOCAL-FIRST.

Keep TrafficLens WINDOWS-FIRST.

Keep TrafficLens LIGHTWEIGHT.

Keep TrafficLens MODULAR.

Keep TrafficLens AGENT-FRIENDLY.

Keep TrafficLens LOCALIZATION-READY.

Do not fabricate measurements.

Do not fabricate test results.

Do not hide failures.

Do not capture packet payloads.

Do not unnecessarily replace working code.

Document important architectural decisions.

Always leave the repository understandable and resumable by another coding agent.
