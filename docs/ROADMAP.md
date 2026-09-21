# TrafficLens — Roadmap

## Milestones

| Milestone | Scope | Status |
|---|---|---|
| M0 | Project bootstrap, docs, localization foundation | Done |
| M1 | Global network monitoring | Done |
| M2 | Dashboard + live graph | Done |
| M3 | Network interfaces | Done |
| M4 | Per-process traffic | Done |
| M5 | Active connections | Done |
| M6 | SQLite history | Done |
| M7 | System tray + floating widget | Done |
| M8 | Alerts + settings | Done |
| M9 | Stability, performance, tests | Done |
| M10 | Packaging / installer | Done (TL-015) |
| M11 | Full Persian localization + language switcher | Done (TL-016) |
| M11a | Product polish / branding foundation + About / Diagnostics | Done (TL-016 continuation) |
| R1 | Release v0.1.1 (first post-TL-016 build) | Released (tag `v0.1.1`) |
| M12 | Performance audit (Connections, allocations, startup, SQLite, idle, soak) | Done (TL-017; merged into `master` at `21dabb9`) |
| M13 | Current-day hourly history | Done (TL-018; branch `feature/tl018-hourly-history`, ready to merge on approval) |
| M14 | Connections Readability & Usability | Done (TL-020; branch `feature/tl020-connections-readability`) |
| M14 | First-run Get Started + contextual tunnel hint | Done (TL-019; branch `feature/tl019-get-started`) |
| M15 | History insights + CSV export | Done (TL-022; branch `feature/tl022-history-insights`) |
| M16 | Release hardening & clean-machine validation | Done (TL-023; branch `feature/tl023-release-hardening`) |
| M17 | Installer localization & packaging polish | Done (TL-024; branch `feature/tl024-installer-polish`) |
| M18 | Release candidate 0.1.2 | In Progress (TL-025; branch `release/0.1.2`) |

## Guidance

- Work incrementally. Do not attempt multiple milestones at once.
- Each milestone ends with a handoff report + doc updates.
- Tests for meaningful logic are added from M1 onward (traffic calc, conversion,
  aggregation, repositories, settings, history, process identity, localization fallback).

## Sequencing rationale

- Global collection (M1) proves the data model before UI (M2).
- Interfaces (M3) depend on the adapter provider built in M1/M2.
- Per-process (M4) and connections (M5) build on mapping and collector abstractions.
- Persistence (M6) consumes aggregated samples produced by earlier milestones.
- Tray/widget (M7) are presentation of existing data.
- Alerts/settings (M8), hardening/packaging (M9–M10), Persian UI (M11) follow.