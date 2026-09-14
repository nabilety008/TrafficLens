# TrafficLens — README

TrafficLens is a local-first Windows desktop network monitoring application.

It shows real-time download/upload speed, total network usage, per-application usage,
top consumers, active connections, interface statistics, and historical traffic.

V1 is monitoring-only.

## Status

Milestone 0 (project bootstrap) — initial build. See `docs/PROJECT_STATUS.md`.

## Requirements

- Windows 10/11 x64
- .NET 8 SDK / runtime

## Build

```
dotnet build TrafficLens.sln
```

## Run

```
dotnet run --project src/TrafficLens.App
```

## Solution Layout

- `TrafficLens.App` — WPF UI (Views, ViewModels, tray, widget, localization).
- `TrafficLens.Core` — domain models, interfaces, business logic.
- `TrafficLens.Network` — Windows network collectors.
- `TrafficLens.Infrastructure` — SQLite, settings, logging, repositories.

## Documentation

- `AGENTS.md` — instructions for coding agents.
- `docs/ARCHITECTURE.md`
- `docs/DECISIONS.md`
- `docs/PROJECT_STATUS.md`
- `docs/ROADMAP.md`
- `docs/NETWORK_COLLECTION.md`
- `docs/DATABASE.md`
- `TASKS.md`
- `CHANGELOG.md`
- `TRAFFICLENS_SPEC.md`

## Golden Rules

LOCAL-FIRST. WINDOWS-FIRST. LIGHTWEIGHT. MODULAR. AGENT-FRIENDLY. LOCALIZATION-READY.