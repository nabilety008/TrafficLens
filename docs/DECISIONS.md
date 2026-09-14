# TrafficLens — Decisions (ADR)

This log records architecture/design decisions and the reasons behind them.

## ADR-001: Project split into four assemblies

**Status:** Accepted (TL-001)

Decided to split the codebase into `App / Core / Network / Infrastructure`.

Reasoning:
- Keeps UI, domain contracts, collection logic, and infrastructure replaceable.
- Prevents circular dependencies and makes testing easier.
- Matches the spec's required solution structure.

## ADR-002: Microsoft.Extensions.DependencyInjection for DI

**Status:** Accepted (TL-001)

Using the standard MS DI container with a composition root in `App.xaml.cs`.

Reasoning:
- Built into .NET, familiar to agents, no third-party dependency needed for a small app.
- Can be replaced later if requirements grow.

## ADR-003: .NET resx resources + ILocalizationService for localization

**Status:** Accepted (TL-001)

User-facing strings come from `Strings.resx` (neutral/English) and satellite
`Strings.fa-IR.resx`. An `ILocalizationService` abstracts lookup and fallback.

Reasoning:
- Standard .NET approach; no third-party library.
- English is neutral so fallback is trivial.
- `IsRightToLeft` + `FlowDirection` switching prepare the UI for Persian without redesign.

## ADR-004: Structured JSON file logging in Infrastructure

**Status:** Accepted (TL-001)

Custom `FileLoggerProvider` writes one JSON object per line under
`%LOCALAPPDATA%\TrafficLens\logs`.

Reasoning:
- Lightweight, no external provider; infrastructure project owns it.
- Structured output is machine-parseable for later diagnostics.
- Logging never throws (must not crash the app).

## ADR-005: Network collection deferred to TL-002

**Status:** Accepted (TL-001, changed to TBD)

Contracts (`INetworkTrafficCollector`, etc.) are defined now; `TrafficLens.Network`
stays empty until the collection mechanism is researched.

Reasoning:
- Spec explicitly forbids implementing collection during bootstrap milestone.
- Choosing the correct Windows mechanism (perf counters vs IP Helper vs ETW) requires
  investigation documented in `docs/NETWORK_COLLECTION.md`.

## ADR-006: .NET 8 (LTS) targeting

**Status:** Accepted (TL-001)

Targets `net8.0` (projects) and `net8.0-windows` (App). SDK 8.0.425 installed locally.

Reasoning:
- .NET 8 is LTS; net8.0-windows enables WPF.
- Widely supported on Windows 10/11 x64.