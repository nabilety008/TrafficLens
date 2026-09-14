# TrafficLens — CHANGELOG

All notable changes are documented here in reverse chronological order.

## [0.0.1] — 2026-09-14 (M0)

### Added
- Solution `TrafficLens.sln` with four projects:
  - `TrafficLens.App` (WPF UI, `net8.0-windows`)
  - `TrafficLens.Core` (models, interfaces)
  - `TrafficLens.Network` (empty placeholder project)
  - `TrafficLens.Infrastructure` (settings, logging)
- Project references configured (App -> Core/Network/Infrastructure; Network/Infrastructure -> Core).
- MVVM base: `ViewModelBase`, `RelayCommand`, `MainViewModel`.
- DI composition root in `App.xaml.cs` (Microsoft.Extensions.DependencyInjection).
- Structured JSON file logging (`Infrastructure/Logging/FileLogger`), logs under
  `%LOCALAPPDATA%\TrafficLens\logs`.
- JSON settings store (`JsonSettingsService`), persists language.
- Localization foundation:
  - Core `ILocalizationService`
  - App `LocalizationService` with resource-based lookup and English fallback
  - `Resources/Strings.resx` (English, neutral)
  - `Resources/Strings.fa-IR.resx` (Persian)
  - RTL support via `FlowDirection` tied to culture
- Minimal dark theme (`Themes/DarkTheme.xaml`) and dark main window with dashboard placeholders.
- Core domain contracts for later milestones:
  - `INetworkTrafficCollector`, `IProcessTrafficCollector`, `IConnectionProvider`,
    `INetworkAdapterProvider`, `ISettingsService`, `ILocalizationService`
  - Models: `NetworkSpeedSample`, `NetworkAdapterInfo`, `ProcessTrafficSample`,
    `ConnectionInfo`
- Repository documentation (`AGENTS.md`, `README.md`, `TASKS.md`, docs/...).

### Notes
- .NET 8 SDK 8.0.425 installed locally at `C:\dotnet` (system had no SDK).
- No network monitoring implemented in this milestone (by design).