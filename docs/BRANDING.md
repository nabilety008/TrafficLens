# TrafficLens Branding Foundation (TL-016)

This document describes the replaceable branding foundation introduced to
complete TL-016 (Product Polish + Persian Localization + Branding Foundation).
It is deliberately **foundation, not final marketing artwork**: assets are easy
to swap without redesigning any code or packaging.

## Brand assets

| Path | Description |
| --- | --- |
| `assets/branding/TrafficLens.ico` | Multi-size application icon (16/24/32/48/64/128/256 px, PNG-encoded frames). Used for the EXE, window/taskbar icon, tray icon, widget glyph and installer. |
| `assets/branding/TrafficLens-256.png` | Full-size PNG for stores/web/docs. |
| `assets/branding/TrafficLens-128.png` | Mid-size PNG for documents. |

The glyph mirrors the runtime-drawn tray icon established in TL-011: a rounded
dark square (`#1E1E2E`) with a cyan down-arrow (`#4FC3F7`) over a teal
up-arrow (`#26A69A`).

## Regeneration

`scripts/generate-icons.ps1` renders every size and writes the ICO + PNGs
deterministically from one drawing routine. Run it whenever the glyph changes:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\generate-icons.ps1
```

The script, the assets, and every code path that consumes them are all checked
into the repository (the assets are small binaries; `.gitignore` does **not**
exclude `assets/`).

## Wiring map

| Surface | Wiring |
| --- | --- |
| EXE icon | `TrafficLens.App.csproj` → `<ApplicationIcon>..\..\assets\branding\TrafficLens.ico</ApplicationIcon>` |
| In-app resource | `TrafficLens.App.csproj` → `<Resource Include="..\..\assets\branding\TrafficLens.ico" />` (loadable via `pack://application:,,,/TrafficLens.ico`) |
| Main window | `MainWindow.xaml` → `Icon="pack://application:,,,/TrafficLens.ico"` |
| Floating widget | title row 14px brand glyph (same pack URI) |
| System tray | `SystemTrayService.TryLoadBrandIcon()` loads the embedded 32px frame; falls back to the runtime-drawn glyph when the resource is unavailable |
| Installer | `TrafficLens.iss` → `SetupIconFile={#BrandIcon}` (`assets\branding\TrafficLens.ico`) plus `VersionInfo*` product metadata |
| About page | shows the brand icon, product name and version |
| Shortcuts | Start Menu / Desktop shortcuts inherit the EXE icon |

## Replacing the artwork

1. Edit the drawing routine in `scripts/generate-icons.ps1` (or replace the
   files in `assets\branding\` entirely with new art at the same filenames).
2. Re-run the generator so the ICO is kept in sync.
3. No application code, XAML, or installer changes are required.

## Versioning

Version state remains the v0.1.0 baseline (`Directory.Build.props`:
`<Version>0.1.0</Version>`, `<AssemblyVersion>0.1.0.0</AssemblyVersion>`,
`<FileVersion>0.1.0.0</FileVersion>`,
`<InformationalVersion>0.1.0</InformationalVersion>`). The About page reads the
informational version at runtime. Version bumps belong to the next release
candidate at release time (see `docs/PACKAGING.md` for the publish flow).

## Brand consistency rules

- The **product name is always the untranslated mark** `TrafficLens`
  (`ProductNameLabel` is identical in `Strings.resx` and
  `Strings.fa-IR.resx`), consistent with ADR-023/ADR-024 (UI text is localized;
  the brand name is not).
- Do not add more icon files to the repo than the generator produces; keep the
  single source of truth in `scripts/generate-icons.ps1`.