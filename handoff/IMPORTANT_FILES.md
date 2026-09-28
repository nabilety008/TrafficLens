# IMPORTANT FILES — where to look for a given task

Task-oriented. Open these instead of scanning the repository. Every path below was
verified to exist. Paths are relative to the repository root.

Ignore `bin/`, `obj/`, and `artifacts/publish/` — build output.

## Shell / startup

| Task | File |
|---|---|
| DI composition root, service startup order | `src\TrafficLens.WinUI\App.xaml.cs` |
| App XAML, resources, theme | `src\TrafficLens.WinUI\App.xaml` |
| Main window shell, nav, widget quick action | `src\TrafficLens.WinUI\MainWindow.xaml` / `.xaml.cs` |
| Single-instance guard | `src\TrafficLens.WinUI\Infrastructure\SingleInstanceGuard.cs` |
| UAC / manifest | `src\TrafficLens.WinUI\app.manifest` |

## Dashboard

| Task | File |
|---|---|
| Dashboard page | `src\TrafficLens.WinUI\Pages\DashboardPage.xaml` / `.xaml.cs` |
| Dashboard view model | `src\TrafficLens.WinUI\ViewModels\DashboardViewModel.cs` |
| Live graph control | `src\TrafficLens.WinUI\Controls\TrafficGraphView.xaml` / `.xaml.cs` |
| Graph scale / sampling | `src\TrafficLens.Core\Graph\AdaptiveGraphScale.cs`, `TrafficSampleBuffer.cs`, `GraphTimeRange.cs`, `TrafficGraphPoint.cs` |
| Rate unit formatting | `src\TrafficLens.Core\Conversion\DataRateConverter.cs`, `DataRateFormatter.cs` |

## Applications (per-process traffic)

| Task | File |
|---|---|
| Applications page | `src\TrafficLens.WinUI\Pages\ApplicationsPage.xaml` / `.xaml.cs` |
| Applications view model | `src\TrafficLens.WinUI\ViewModels\ApplicationsViewModel.cs` |
| Row model / sorting | `src\TrafficLens.WinUI\ViewModels\ProcessRowViewModel.cs`, `ApplicationSortOption.cs` |
| Process icon cache | `src\TrafficLens.WinUI\Infrastructure\ProcessIconCache.cs` |
| Sample model | `src\TrafficLens.Core\Models\ProcessTrafficSample.cs`, `ProcessTrafficCollectorStatus.cs` |

## Connections

| Task | File |
|---|---|
| Connections page | `src\TrafficLens.WinUI\Pages\ConnectionsPage.xaml` / `.xaml.cs` |
| Connections view model | `src\TrafficLens.WinUI\ViewModels\ConnectionsViewModel.cs` |
| Row model / sorting / selection | `src\TrafficLens.WinUI\ViewModels\ConnectionRowViewModel.cs`, `ConnectionSortOption.cs`, `src\TrafficLens.Core\Selection\ConnectionSelection.cs` (holds `ConnectionFilter`, `ConnectionSortKey`, `ConnectionFiltering`, `ConnectionSort`) |
| Endpoint / IP formatting | `src\TrafficLens.Core\Conversion\EndpointFormatter.cs` |
| Reverse DNS | `src\TrafficLens.Infrastructure\Services\DnsResolverService.cs` |

## History

| Task | File |
|---|---|
| History page | `src\TrafficLens.WinUI\Pages\HistoryPage.xaml` / `.xaml.cs` |
| History view model (+ CSV) | `src\TrafficLens.WinUI\ViewModels\HistoryViewModel.cs`, `HistoryChartPoint.cs` |
| Bar chart control | `src\TrafficLens.WinUI\Controls\HistoryBarChart.xaml` / `.xaml.cs` |
| Bucketing / ranges / hourly series | `src\TrafficLens.Core\History\TrafficHistoryAccumulator.cs`, `TrafficHistoryBucket.cs`, `HistoryRangeCalculator.cs`, `HourlyHistoryBuilder.cs` |
| Database schema reference | `docs\DATABASE.md` |

## Database

| Task | File |
|---|---|
| SQLite repository (schema v2, pruning) | `src\TrafficLens.Infrastructure\History\SqliteTrafficHistoryRepository.cs` |
| History service | `src\TrafficLens.Infrastructure\History\TrafficHistoryService.cs` |
| Repository/service interfaces | `src\TrafficLens.Core\History\ITrafficHistoryRepository.cs`, `ITrafficHistoryService.cs` |
| DI registration | `src\TrafficLens.Infrastructure\History\HistoryServiceCollectionExtensions.cs` |

## Alerts

| Task | File |
|---|---|
| Alerts page (rule cards) | `src\TrafficLens.WinUI\Pages\AlertsPage.xaml` / `.xaml.cs` |
| Alerts view model | `src\TrafficLens.WinUI\ViewModels\AlertsViewModel.cs` |
| Threshold engine + cooldown | `src\TrafficLens.Core\Alerts\AlertEngine.cs`, `AlertHistoryBuffer.cs`, `AlertConfig.cs`, `AlertEvent.cs`, `AlertSignal.cs`, `AlertType.cs` |
| Notification service + persistence | `src\TrafficLens.App\Services\AlertService.cs`, `IAlertService.cs`, `AlertSettings.cs`, `AlertNotification.cs` |

> The alert services above live in the **WPF** project and are **linked into the
> WinUI project** by its `.csproj`. Edit them where they are; do not fork a copy.

## Settings

| Task | File |
|---|---|
| Settings page | `src\TrafficLens.WinUI\Pages\SettingsPage.xaml` / `.xaml.cs` |
| Settings storage | `src\TrafficLens.Infrastructure\Services\JsonSettingsService.cs`, `src\TrafficLens.Core\Abstractions\ISettingsService.cs` |
| User-data paths | `src\TrafficLens.Infrastructure\Services\AppPaths.cs` |
| Start-with-Windows | `src\TrafficLens.Infrastructure\Services\StartupRegistrationService.cs`, `src\TrafficLens.Core\Abstractions\IStartupRegistrationService.cs` |
| Windows Update integration | `src\TrafficLens.Infrastructure\Services\WindowsUpdateService.cs`, `WindowsUpdateCommandBuilder.cs`, `WindowsUpdateStateResolver.cs`, `src\TrafficLens.Core\Abstractions\IWindowsUpdateService.cs` |

## Widget

| Task | File |
|---|---|
| Widget window | `src\TrafficLens.WinUI\Views\FloatingWidgetWindow.xaml` / `.xaml.cs` |
| Widget service (lifetime, drag, clamp) | `src\TrafficLens.WinUI\Services\FloatingWidgetService.cs`, `IFloatingWidgetService.cs` |
| Single source of truth for enabled state | `src\TrafficLens.WinUI\Services\WidgetEnabledState.cs` |
| Shared toggle resolution | `src\TrafficLens.WinUI\Services\WidgetToggleSync.cs` |
| Widget view model | `src\TrafficLens.WinUI\ViewModels\FloatingWidgetViewModel.cs` |
| Position clamp logic | `tests\TrafficLens.WinUI.Tests\WidgetPositionClampTests.cs`, `WidgetPositionHelperTests.cs` |

> Enabled state has **one** owner. The shell quick action and the Settings switch
> are views of the same `FloatingWidgetEnabled` value.

## Tray

| Task | File |
|---|---|
| Tray icon host (separate project) | `src\TrafficLens.WinUI.Tray\TrayIconHost.cs` |
| Tray service | `src\TrafficLens.WinUI\Services\SystemTrayService.cs`, `ISystemTrayService.cs` |
| Tray behaviour (close/minimize/balloon) | `src\TrafficLens.WinUI\Services\TrayBehavior.cs` |
| Exit ownership | `src\TrafficLens.WinUI\Services\ApplicationExitCoordinator.cs` |

## Title bar / window chrome

| Task | File |
|---|---|
| Title bar layout in the shell | `src\TrafficLens.WinUI\MainWindow.xaml` |
| Caption safe-area resolution (flow → physical) | `src\TrafficLens.WinUI\Infrastructure\TitleBarCaptionLayout.cs` |
| Native caption hit-test probe (fallback) | `src\TrafficLens.WinUI\Infrastructure\NativeCaptionButtons.cs` |
| Window / taskbar / Alt+Tab icon | `src\TrafficLens.WinUI\Infrastructure\WindowIcon.cs` |
| Caption layout tests | `tests\TrafficLens.WinUI.Tests\TitleBarCaptionLayoutTests.cs` |

## Localization

| Task | File |
|---|---|
| Localization service (culture + RTL) | `src\TrafficLens.WinUI\LocalizationService.cs`, `src\TrafficLens.Core\Localization\ILocalizationService.cs` |
| English strings (neutral) | `src\TrafficLens.App\Resources\Strings.resx` |
| Persian strings | `src\TrafficLens.App\Resources\Strings.fa-IR.resx` |

> Add a key to **both** `.resx` files. A missing key renders as `[KeyName]`.
> Technical values stay LTR inside the Persian layout.

## Network collection

| Task | File |
|---|---|
| Collector interface | `src\TrafficLens.Core\Abstractions\INetworkTrafficCollector.cs`, `INetworkAdapterProvider.cs` |
| Global traffic collector (byte counters) | `src\TrafficLens.Network\Collectors\WindowsNetworkTrafficCollector.cs` |
| Adapter enumeration | `src\TrafficLens.Network\Adapters\WindowsNetworkAdapterProvider.cs`, `AdapterFilter.cs`, `DefaultAdapterSelector.cs`, `NetworkInterfaceSource.cs`, `AdapterSnapshotCache.cs` |
| Counter → rate math | `src\TrafficLens.Network\Calculation\NetworkSpeedCalculator.cs`, `SpeedRateTracker.cs` |
| Multi-adapter aggregation | `src\TrafficLens.Network\Aggregation\NetworkTrafficAggregator.cs` |
| Sample models | `src\TrafficLens.Core\Models\NetworkCounterSample.cs`, `NetworkSpeedSample.cs`, `NetworkAdapterInfo.cs` |
| Connections collection | `src\TrafficLens.Network\Connections\WindowsConnectionProvider.cs`, `NativeConnectionTableReader.cs`, `ConnectionTableParser.cs`, `ConnectionProcessResolver.cs` |
| DI registration | `src\TrafficLens.Network\NetworkServiceCollectionExtensions.cs` |
| Deeper background | `docs\NETWORK_COLLECTION.md`, `docs\PERFORMANCE.md` |

## ETW (per-process)

| Task | File |
|---|---|
| ETW collector | `src\TrafficLens.Network\Process\WindowsEtwProcessTrafficCollector.cs` |
| Byte accounting engine | `src\TrafficLens.Network\Process\ProcessTrafficAccountingEngine.cs`, `ProcessProtocolTotals.cs`, `NetworkTransferEvent.cs` |
| PID-reuse handling | `src\TrafficLens.Network\Process\ProcessInstanceId.cs` |
| Process name/icon metadata | `src\TrafficLens.Network\Process\WindowsProcessMetadataProvider.cs`, `IProcessMetadataProvider.cs` |
| Probe scripts | `scripts\tl017-etw-probe.ps1`, `scripts\tl017-gccapture.ps1` |

## Packaging

| Task | File |
|---|---|
| Version metadata (single source) | `Directory.Build.props` |
| Release pipeline | `scripts\build-release.ps1` |
| Inno Setup script (AppId, shortcuts, uninstall) | `packaging\TrafficLens.iss` |
| Persian installer language | `packaging\Persian.isl` |
| WinUI project (release entry point) | `src\TrafficLens.WinUI\TrafficLens.WinUI.csproj` |
| Lifecycle validation | `scripts\tl023-release-lifecycle.ps1` |
| Full packaging reference | `docs\PACKAGING.md` |

## Branding

| Task | File |
|---|---|
| Canonical brand assets | `assets\branding\TrafficLens.ico`, `TrafficLens-256.png`, `TrafficLens-128.png` |
| Icon generator | `scripts\generate-icons.ps1` |
| Branding reference | `docs\BRANDING.md` |

> Swap artwork by replacing files in `assets\branding\` and re-running the
> pipeline. No `[Setup]` structural change is required.

## Logging

| Task | File |
|---|---|
| Structured JSON file logger | `src\TrafficLens.Infrastructure\Logging\FileLogger.cs`, `FileLoggerProvider.cs` |
| DI registration | `src\TrafficLens.Infrastructure\Logging\LoggingServiceCollectionExtensions.cs` |

> Filter on `"level":"Error"`. Every record contains `"exception":null`, so a grep
> for the word "exception" matches every line.

## Tests

| Project | Path | Tests at closeout |
|---|---|---|
| App | `tests\TrafficLens.App.Tests\TrafficLens.App.Tests.csproj` | 270 |
| Network | `tests\TrafficLens.Network.Tests\TrafficLens.Network.Tests.csproj` | 212 |
| Infrastructure | `tests\TrafficLens.Infrastructure.Tests\TrafficLens.Infrastructure.Tests.csproj` | 84 |
| WinUI | `tests\TrafficLens.WinUI.Tests\TrafficLens.WinUI.Tests.csproj` | 165 |
| **Total** | | **731** |

`tests\TrafficLens.Network.Verification` is a **console harness**, not an xunit
test project — it has no `Microsoft.NET.Test.Sdk` and is not part of the 731.

Notable WinUI tests: `TitleBarCaptionLayoutTests.cs`, `WidgetEnabledStateTests.cs`,
`WidgetQuickToggleTests.cs`, `WidgetPositionClampTests.cs`,
`WidgetPositionHelperTests.cs`, `AlertRuleThresholdTests.cs`,
`WinUiPageMarkupTests.cs`, `HistoryViewModelCsvTests.cs`.

## Project / process documentation

| File | Purpose |
|---|---|
| `AGENTS.md` | Repo conventions and golden rules. |
| `TASKS.md` | Task list of record; stable task IDs. |
| `CHANGELOG.md` | Per-version narrative of what changed and why. |
| `docs\PROJECT_STATUS.md` | Current milestone and release state. |
| `docs\ARCHITECTURE.md` | Deeper architecture narrative. |
| `docs\DECISIONS.md` | Why key technical decisions were made. |
| `docs\ROADMAP.md` | Longer-term direction. |
| `docs\DATABASE.md` | SQLite schema. |
| `docs\NETWORK_COLLECTION.md` | Collector internals. |
| `docs\PACKAGING.md` | Release pipeline and artifacts. |
| `docs\BRANDING.md` | Brand asset pipeline. |
| `TRAFFICLENS_SPEC.md` | Product specification. |
| `README.md` | Overview. |
| `.gitignore` | Note: `artifacts/` is ignored (line 10) — artifacts are **not** in git. |
