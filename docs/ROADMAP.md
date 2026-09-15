# TrafficLens — Roadmap

## Milestones

| Milestone | Scope | Status |
|---|---|---|
| M0 | Project bootstrap, docs, localization foundation | Done |
| M1 | Global network monitoring | Not started |
| M2 | Dashboard + live graph | Not started |
| M3 | Network interfaces | Not started |
| M4 | Per-process traffic | Not started |
| M5 | Active connections | Not started |
| M6 | SQLite history | Not started |
| M7 | System tray + floating widget | Done |
| M8 | Alerts + settings | In progress (TL-012 alerts done; TL-013 settings remains) |
| M9 | Stability, performance, tests, packaging | Not started |
| M10 | Full Persian localization + language switcher | Not started |

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
- Alerts/settings (M8), hardening/packaging (M9), Persian UI (M10) follow.