using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using TrafficLens.App.Commands;
using TrafficLens.App.Services;
using TrafficLens.Core.Localization;
using TrafficLens.Infrastructure.Services;

namespace TrafficLens.App.ViewModels;

/// <summary>
/// Backs the About page (TL-016 continuation): product identity, version,
/// runtime/environment facts and a diagnostics block with copy-to-clipboard
/// and open-logs-folder actions. All display strings are localized.
/// </summary>
public sealed class AboutViewModel : ViewModelBase, IDisposable
{
    private readonly ILocalizationService _localization;

    private string _aboutTitleLabel = string.Empty;
    private string _aboutDescriptionLabel = string.Empty;
    private string _getStartedLabel = string.Empty;
    private string _productNameText = string.Empty;
    private string _versionLabel = string.Empty;
    private string _versionText = string.Empty;
    private string _runtimeLabel = string.Empty;
    private string _runtimeText = string.Empty;
    private string _osLabel = string.Empty;
    private string _osText = string.Empty;
    private string _cultureLabel = string.Empty;
    private string _cultureText = string.Empty;
    private string _dataPathLabel = string.Empty;
    private string _dataPathText = string.Empty;
    private string _settingsPathLabel = string.Empty;
    private string _settingsPathText = string.Empty;
    private string _logsPathLabel = string.Empty;
    private string _logsPathText = string.Empty;
    private string _diagnosticsHeaderLabel = string.Empty;
    private string _copyDiagnosticsLabel = string.Empty;
    private string _openLogFolderLabel = string.Empty;
    private string _diagnosticsCopiedLabel = string.Empty;
    private bool _hasCopiedNotice;

    public AboutViewModel(ILocalizationService localization, OnboardingViewModel onboarding)
    {
        _localization = localization;

        CopyDiagnosticsCommand = new RelayCommand(CopyDiagnostics);
        OpenLogsCommand = new RelayCommand(OpenLogsFolder);
        ShowGuideCommand = new RelayCommand(onboarding.Show);

        _localization.CultureChanged += OnCultureChanged;
        RefreshLocalizedStrings();
    }

    public void Dispose()
    {
        _localization.CultureChanged -= OnCultureChanged;
    }

    public ICommand CopyDiagnosticsCommand { get; }

    public ICommand OpenLogsCommand { get; }

    public ICommand ShowGuideCommand { get; }

    public string GetStartedLabel
    {
        get => _getStartedLabel;
        private set => SetProperty(ref _getStartedLabel, value);
    }

    public string AboutTitleLabel
    {
        get => _aboutTitleLabel;
        private set => SetProperty(ref _aboutTitleLabel, value);
    }

    public string AboutDescriptionLabel
    {
        get => _aboutDescriptionLabel;
        private set => SetProperty(ref _aboutDescriptionLabel, value);
    }

    public string ProductNameText
    {
        get => _productNameText;
        private set => SetProperty(ref _productNameText, value);
    }

    public string VersionLabel
    {
        get => _versionLabel;
        private set => SetProperty(ref _versionLabel, value);
    }

    public string VersionText
    {
        get => _versionText;
        private set => SetProperty(ref _versionText, value);
    }

    public string RuntimeLabel
    {
        get => _runtimeLabel;
        private set => SetProperty(ref _runtimeLabel, value);
    }

    public string RuntimeText
    {
        get => _runtimeText;
        private set => SetProperty(ref _runtimeText, value);
    }

    public string OsLabel
    {
        get => _osLabel;
        private set => SetProperty(ref _osLabel, value);
    }

    public string OsText
    {
        get => _osText;
        private set => SetProperty(ref _osText, value);
    }

    public string CultureLabel
    {
        get => _cultureLabel;
        private set => SetProperty(ref _cultureLabel, value);
    }

    public string CultureText
    {
        get => _cultureText;
        private set => SetProperty(ref _cultureText, value);
    }

    public string DataPathLabel
    {
        get => _dataPathLabel;
        private set => SetProperty(ref _dataPathLabel, value);
    }

    public string DataPathText
    {
        get => _dataPathText;
        private set => SetProperty(ref _dataPathText, value);
    }

    public string SettingsPathLabel
    {
        get => _settingsPathLabel;
        private set => SetProperty(ref _settingsPathLabel, value);
    }

    public string SettingsPathText
    {
        get => _settingsPathText;
        private set => SetProperty(ref _settingsPathText, value);
    }

    public string LogsPathLabel
    {
        get => _logsPathLabel;
        private set => SetProperty(ref _logsPathLabel, value);
    }

    public string LogsPathText
    {
        get => _logsPathText;
        private set => SetProperty(ref _logsPathText, value);
    }

    public string DiagnosticsHeaderLabel
    {
        get => _diagnosticsHeaderLabel;
        private set => SetProperty(ref _diagnosticsHeaderLabel, value);
    }

    public string CopyDiagnosticsLabel
    {
        get => _copyDiagnosticsLabel;
        private set => SetProperty(ref _copyDiagnosticsLabel, value);
    }

    public string OpenLogFolderLabel
    {
        get => _openLogFolderLabel;
        private set => SetProperty(ref _openLogFolderLabel, value);
    }

    public string DiagnosticsCopiedLabel
    {
        get => _diagnosticsCopiedLabel;
        private set => SetProperty(ref _diagnosticsCopiedLabel, value);
    }

    public bool HasCopiedNotice
    {
        get => _hasCopiedNotice;
        private set => SetProperty(ref _hasCopiedNotice, value);
    }

    private void CopyDiagnostics()
    {
        var text = DiagnosticsInfo.Build(new[]
        {
            ("Product", ProductNameText),
            (_versionLabel, VersionText),
            (_runtimeLabel, RuntimeText),
            (_osLabel, OsText),
            (_cultureLabel, CultureText),
            (_dataPathLabel, DataPathText),
            (_settingsPathLabel, SettingsPathText),
            (_logsPathLabel, LogsPathText)
        });

        try
        {
            Clipboard.SetText(text);
            HasCopiedNotice = true;
        }
        catch (Exception)
        {
            HasCopiedNotice = false;
        }
    }

    private void OpenLogsFolder()
    {
        var logsDirectory = AppPaths.LogsDirectory;
        var target = Directory.Exists(logsDirectory) ? logsDirectory : Path.GetDirectoryName(logsDirectory) ?? logsDirectory;
        if (string.IsNullOrWhiteSpace(target) || !Directory.Exists(target))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e) => RefreshLocalizedStrings();

    private void RefreshLocalizedStrings()
    {
        AboutTitleLabel = _localization["AboutTitleLabel"];
        AboutDescriptionLabel = _localization["AboutDescriptionLabel"];
        GetStartedLabel = _localization["GetStartedReopen"];
        ProductNameText = _localization["ProductNameLabel"];
        VersionLabel = _localization["VersionLabel"];
        RuntimeLabel = _localization["RuntimeLabel"];
        OsLabel = _localization["OsLabel"];
        CultureLabel = _localization["CultureLabel"];
        DataPathLabel = _localization["DataPathLabel"];
        SettingsPathLabel = _localization["SettingsPathLabel"];
        LogsPathLabel = _localization["LogsPathLabel"];
        DiagnosticsHeaderLabel = _localization["DiagnosticsHeaderLabel"];
        CopyDiagnosticsLabel = _localization["CopyDiagnosticsLabel"];
        OpenLogFolderLabel = _localization["OpenLogFolderLabel"];
        DiagnosticsCopiedLabel = _localization["DiagnosticsCopiedLabel"];

        VersionText = GetVersionText();
        RuntimeText = RuntimeInformation.FrameworkDescription;
        OsText = RuntimeInformation.OSDescription;
        CultureText = _localization.CurrentCulture?.NativeName ?? string.Empty;
        DataPathText = AppPaths.DataDirectory;
        SettingsPathText = AppPaths.SettingsFile;
        LogsPathText = AppPaths.LogsDirectory;
    }

    private static string GetVersionText()
    {
        try
        {
            var assembly = typeof(AboutViewModel).Assembly;
            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational))
            {
                var plus = informational.IndexOf('+');
                return plus > 0 ? informational[..plus] : informational;
            }

            var version = assembly.GetName().Version;
            return version is null ? string.Empty : $"{version.Major}.{version.Minor}.{version.Build}";
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}