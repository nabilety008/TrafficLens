# Contributing to TrafficLens

Thank you for your interest in improving TrafficLens. Bug reports, suggestions, and
pull requests are all welcome.

## Reporting bugs

Please use the **Bug report** issue form:
[`.github/ISSUE_TEMPLATE/bug_report.yml`](.github/ISSUE_TEMPLATE/bug_report.yml)

Before submitting, please check the existing
[issues](https://github.com/nabilety008/TrafficLens/issues) to see whether the problem
has already been reported, and remove any sensitive information from attached logs.

## Suggesting features

Please use the **Feature request** issue form:
[`.github/ISSUE_TEMPLATE/feature_request.yml`](.github/ISSUE_TEMPLATE/feature_request.yml)

Describing the problem or use case you are trying to solve is more useful than
describing a specific implementation.

## Pull requests

If you would like to contribute code:

1. Fork the repository and create a focused branch.
2. Keep changes scoped to one concern; unrelated refactoring makes review harder.
3. Follow the existing project architecture and code style.
4. Add or update tests when behavior changes.
5. Verify the relevant build and tests before opening the pull request.
6. Explain what changed and why in the pull request description.

### Project context

TrafficLens is a **C# / .NET 8 / WinUI 3** application for **Windows x64**. A few
constraints shape most changes:

- **Avoid unnecessary polling and timers.** TrafficLens is expected to idle cheaply.
  Prefer reusing existing services and events over adding parallel mechanisms.
- **Preserve lightweight CPU and RAM behavior**, including on battery.
- **Never capture packet payload contents.** Network counters and connection metadata
  only.
- **Keep localization in mind.** User-facing strings belong in the shared resource
  files for both English and Persian, not hard-coded in views.
- **Preserve RTL behavior** where relevant, and keep technical values (numbers, units,
  rates, addresses) left-to-right inside the Persian layout.

### Building

The WinUI 3 project requires the `x64` platform:

```powershell
dotnet restore TrafficLens.sln
dotnet build TrafficLens.sln -c Release -p:Platform=x64
dotnet test TrafficLens.sln -c Release -p:Platform=x64 --no-build
```

Building requires Windows and the .NET 8 SDK. Creating the installer additionally
requires Inno Setup 6.

## Security issues

Please do not report security vulnerabilities through public issues or pull requests.
See [SECURITY.md](SECURITY.md) for the private reporting process.

## Licensing

TrafficLens does not currently have a LICENSE file. Please open an issue before
contributing code so that the licensing terms can be agreed first.
