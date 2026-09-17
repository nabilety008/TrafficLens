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