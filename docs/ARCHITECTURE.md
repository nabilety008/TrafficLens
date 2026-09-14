# TrafficLens — Architecture

## Overview

TrafficLens is a modular .NET 8 WPF desktop application. The layout separates UI,
domain contracts, collection logic, and infrastructure so each can evolve independently.

```
┌────────────────────────────────────────────────────────────────┐
│                       TrafficLens.App (WPF)                     │
│  Views · ViewModels · Commands · Tray · Widget · Resources       │
│  Composition root: App.xaml.cs (builds IServiceProvider)        │
└───────────┬──────────────────────────────┬───────────────────┬───┘
            │                              │                   │
            ▼                              ▼                   ▼
┌────────────────────┐  ┌────────────────────────┐  ┌───────────────────────┐
│  TrafficLens.Core  │  │   TrafficLens.Network  │  │ TrafficLens.Infrastructure │
│  models, interfaces│  │  collectors (TL-002+)  │  │  SQLite, settings, log    │
└────────────────────┘  └────────────────────────┘  └───────────────────────┘
```

## Projects

### TrafficLens.App
- WPF UI and composition root.
- Views bind to ViewModels via `DataContext`; no business logic in code-behind.
- Localization resources live here (`Resources/Strings*.resx`).
- Everything public is resolved through DI.

### TrafficLens.Core
- Contains domain records and interface contracts (`INetworkTrafficCollector`,
  `IProcessTrafficCollector`, `IConnectionProvider`, `INetworkAdapterProvider`,
  `ILocalizationService`, `ISettingsService`).
- No WPF, no OS-specific networking code.
- Consumers depend on abstractions so implementations are replaceable.

### TrafficLens.Network
- Owns Windows collection implementations (upcoming milestones).
- Consumes Core contracts only.

### TrafficLens.Infrastructure
- SQLite persistence (upcoming), JSON settings, structured file logging.
- Depends only on Core.

## Dependency Injection

- `Microsoft.Extensions.DependencyInjection`.
- Registration happens in `App.OnStartup` via `ConfigureServices`.
- `MainWindow`, `MainViewModel`, `ILocalizationService`, `ISettingsService`,
  and `ILogger<T>` are resolved from the container.
- App data lives under `%LOCALAPPDATA%\TrafficLens` (`AppPaths`).

## MVVM

- Every ViewModel derives from `ViewModelBase` (`INotifyPropertyChanged`).
- Commands use `RelayCommand`.
- Data flows: collector -> service -> ViewModel -> View binding.

## Logging

- `AddFileLogging(logDirectory)` registers a `FileLoggerProvider`.
- Writes structured JSON lines to `%LOCALAPPDATA%\TrafficLens\logs\trafficlens-{date}.log`.
- Logging failures are swallowed and never crash the app.

## Localization

- Core defines `ILocalizationService` (culture, `GetString`, fallback, RTL flag).
- App implements it over embedded resx resources.
  - Neutral: `Strings.resx` (English), satellite: `Strings.fa-IR.resx`.
- Views bind localized labels through the ViewModel; window `FlowDirection` follows
  culture so RTL is supported later without redesign.
- Technical values (IPs, ports, paths, protocol names) stay untranslated and LTR.

## Async & UI Threading

- Collectors run in background (later milestones). UI updates must be marshalled to
  the dispatcher. Never block the UI thread (graph requirement).

## Future Plans

- Live traffic graph: ring buffers of samples; 30s/1m/5m ranges.
- History: aggregated SQLite samples; retention policies.
- Tray + floating widget.
- See `docs/ROADMAP.md`.