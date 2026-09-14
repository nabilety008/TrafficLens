# TrafficLens — TASKS.md

## How to use

- Optional tasks are marked `[optional]`.
- A task is DONE only when implemented, verified (built/run/tested), and documented.
- Task IDs are stable. Do not renumber existing IDs.

## Backlog

### TL-001 Project Bootstrap — **DONE**
- [x] Inspect .NET environment (installed .NET 8 SDK 8.0.425)
- [x] Create `TrafficLens.sln` and solution folder structure
- [x] Create `TrafficLens.App` (WPF), `TrafficLens.Core`, `TrafficLens.Network`, `TrafficLens.Infrastructure`
- [x] Configure project references (no circular dependencies)
- [x] Establish DI composition root (`App.xaml.cs`) and MVVM base classes
- [x] Configure structured file logging (Infrastructure `FileLogger`)
- [x] Create required documentation
- [x] Initialize Git repository
- [x] Minimal dark main window with dashboard placeholders
- [x] Localization foundation: `ILocalizationService`, `Strings.resx` (en), `Strings.fa-IR.resx`
- [x] RTL-ready architecture (`FlowDirection` switching, LTR-safe values)
- [x] Build solution: success, 0 warnings
- [x] Verify startup where environment permits
- [x] Update `PROJECT_STATUS.md` and `TASKS.md`

### TL-002 Global Network Collector
- [ ] Research and select collection mechanism (see `docs/NETWORK_COLLECTION.md`)
- [ ] Implement `INetworkTrafficCollector`
- [ ] Implement `INetworkAdapterProvider`
- [ ] Handle adapter connect/disconnect
- [ ] Document accuracy and limitations
- **Status: not started**

### TL-003 Download/Upload Calculation
- [ ] Compute speeds from collector deltas
- [ ] Readable units (KB/s, MB/s, Mbps)
- **Status: not started**

### TL-004 Network Adapter Detection
- [ ] Enumerate Ethernet, Wi-Fi, VPN, virtual adapters
- [ ] Default adapter detection (never assume first adapter)
- **Status: not started**

### TL-005 Dashboard
- [ ] Current download/upload, totals, active adapter, network status
- **Status: not started**

### TL-006 Live Traffic Graph
- [ ] Real-time download/upload graph (30s / 1m / 5m ranges)
- [ ] Never block UI thread
- **Status: not started**

### TL-007 Per-Process Traffic
- [ ] `IProcessTrafficCollector`, process mapping
- [ ] App list with sort, icons, totals
- **Status: not started**

### TL-008 Active Connections
- [ ] `IConnectionProvider`, TCP-first connections view
- **Status: not started**

### TL-009 SQLite History
- [ ] Aggregated sampling schema and repositories
- [ ] Today / Yesterday / 7d / 30d / Lifetime views
- **Status: not started**

### TL-010 Floating Widget
- [ ] Compact always-on-top widget
- **Status: not started**

### TL-011 System Tray
- [ ] Tray icon, show/hide, minimize to tray, exit
- **Status: not started**

### TL-012 Alerts
- [ ] Local alert architecture (usage thresholds, notifications)
- **Status: not started**

### TL-013 Settings
- [ ] Settings UI and persistence
- **Status: not started**

### TL-014 CSV Export
- [ ] Export app / daily / hourly usage to CSV
- **Status: not started**

### TL-015 Packaging / Installer
- [ ] x64 packaging and installer
- **Status: not started**

### TL-016 Localization / Persian UI
- [ ] Runtime language switcher, full fa-IR translation
- **Status: not started**

## Milestones

| Milestone | Title | Tasks | Status |
|---|---|---|---|
| M0 | Project bootstrap, docs, localization foundation | TL-001 | Done |
| M1 | Global network monitoring | TL-002, TL-003 | Not started |
| M2 | Dashboard and live graph | TL-005, TL-006 | Not started |
| M3 | Network interfaces | TL-004 | Not started |
| M4 | Per-process traffic | TL-007 | Not started |
| M5 | Active connections | TL-008 | Not started |
| M6 | SQLite history | TL-009 | Not started |
| M7 | Tray and widget | TL-010, TL-011 | Not started |
| M8 | Alerts and settings | TL-012, TL-013 | Not started |
| M9 | Stability, performance, tests, packaging | TL-015 | Not started |
| M10 | Full Persian localization | TL-016 | Not started |