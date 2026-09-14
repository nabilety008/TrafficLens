# TrafficLens — AGENTS.md

## Before Making Changes

Read these files in order:

1. `AGENTS.md`
2. `docs/PROJECT_STATUS.md`
3. `TASKS.md`
4. `docs/ARCHITECTURE.md`
5. `docs/DECISIONS.md`

Then read only the files relevant to the current task.

## Project

TrafficLens is a local-first, Windows-first .NET 8 network monitoring WPF desktop application.
V1 is monitoring-only: no blocking, no bandwidth limiting, no firewall manipulation.

- Solution: `TrafficLens.sln`
- Stack: C#, WPF, MVVM, Microsoft.Extensions.DependencyInjection, SQLite, structured file logging.
- Platforms: Windows 10/11, x64.

## Project Layout

```
TrafficLens/
- src/
  - TrafficLens.App/          WPF UI, Views, ViewModels, Tray, Localization resources
  - TrafficLens.Core/         Domain models, interfaces, shared abstractions
  - TrafficLens.Network/      Windows network collectors, connections, process mapping
  - TrafficLens.Infrastructure/  SQLite, repositories, settings, logging
- tests/
- docs/
```

Reference rules:
- Core references nothing internal.
- Network references Core.
- Infrastructure references Core.
- App references Core, Network, Infrastructure.
- No circular references.

## Conventions

- .NET 8 (`net8.0`), App targets `net8.0-windows`.
- MVVM: Views in `Views`, ViewModels in `ViewModels`, no logic in code-behind.
- Dependency injection: composition root in `App.xaml.cs`.
- User-facing strings come from `TrafficLens.App/Resources/Strings*.resx` via `ILocalizationService`.
  Never hard-code UI strings in Views/ViewModels.
- Logging via `ILogger<T>` (structured JSON file logger in Infrastructure).
- Do not add comments unless necessary; keep code self-documenting.
- Unit conversion, aggregation, settings, history, and localization logic must be testable.

## Golden Rules

- LOCAL-FIRST, WINDOWS-FIRST, LIGHTWEIGHT, MODULAR, AGENT-FRIENDLY, LOCALIZATION-READY.
- Never fabricate measurements or test results.
- Never hide failures.
- Never capture packet payloads.
- Do not remove working code unless required.
- Do not start a new task ID until the current one is verified.

## Task IDs

All significant work uses `TL-XXX` IDs (see `TASKS.md`). Do not renumber existing IDs.

## Verification

Build: `dotnet build TrafficLens.sln`
After a milestone, update `docs/PROJECT_STATUS.md`, `TASKS.md`, and `CHANGELOG.md`.