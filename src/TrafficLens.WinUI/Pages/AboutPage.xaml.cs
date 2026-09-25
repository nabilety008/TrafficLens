using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using TrafficLens.Core.Localization;
using TrafficLens.Infrastructure.Services;
using Windows.Storage.Streams;

namespace TrafficLens.WinUI.Pages;

public sealed partial class AboutPage : Page
{
    private readonly ILocalizationService _localization;

    public AboutPage()
    {
        InitializeComponent();

        _localization = App.Services.Provider.GetRequiredService<ILocalizationService>();

        ApplyLocalization();
        ApplyFlowDirection();

        _localization.CultureChanged += OnCultureChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyFlowDirection();
        _ = LoadBrandIconAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _localization.CultureChanged -= OnCultureChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnCultureChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyLocalization();
            ApplyFlowDirection();
        });

    private void ApplyFlowDirection()
    {
        var direction = _localization.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
        PageScroller.FlowDirection = direction;
        RootLayout.FlowDirection = direction;
    }

    private void ApplyLocalization()
    {
        PageTitleText.Text = _localization["AboutTitleLabel"];
        ProductNameText.Text = _localization["ProductNameLabel"];
        VersionLabelText.Text = _localization["VersionLabel"];
        VersionValueText.Text = GetVersionText();
        DescriptionText.Text = _localization["AboutDescriptionLabel"];

        RuntimeLabelText.Text = _localization["RuntimeLabel"];
        RuntimeValueText.Text = RuntimeInformation.FrameworkDescription;
        OsLabelText.Text = _localization["OsLabel"];
        OsValueText.Text = RuntimeInformation.OSDescription;
        CultureLabelText.Text = _localization["CultureLabel"];
        CultureValueText.Text = _localization.CurrentCulture?.NativeName ?? string.Empty;
        VersionDetailLabelText.Text = _localization["VersionLabel"];
        VersionDetailValueText.Text = VersionValueText.Text;

        DiagnosticsHeaderText.Text = _localization["DiagnosticsHeaderLabel"];
        DataPathLabelText.Text = _localization["DataPathLabel"];
        DataPathValueText.Text = AppPaths.DataDirectory;
        SettingsPathLabelText.Text = _localization["SettingsPathLabel"];
        SettingsPathValueText.Text = AppPaths.SettingsFile;
        LogsPathLabelText.Text = _localization["LogsPathLabel"];
        LogsPathValueText.Text = AppPaths.LogsDirectory;
    }

    private async Task LoadBrandIconAsync()
    {
        if (AppIconImage.Source is not null)
        {
            return;
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "TrafficLens-256.png");
            var bytes = File.Exists(path)
                ? await File.ReadAllBytesAsync(path)
                : ReadEmbeddedBrandIcon();

            if (bytes is null || bytes.Length == 0)
            {
                return;
            }

            using var stream = new InMemoryRandomAccessStream();
            using (var outputStream = stream.GetOutputStreamAt(0))
            using (var writer = new DataWriter(outputStream))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
            }

            stream.Seek(0);
            var bitmap = new BitmapImage();
            bitmap.SetSource(stream);
            AppIconImage.Source = bitmap;
        }
        catch (Exception)
        {
            AppIconImage.Source = null;
        }
    }

    private static byte[]? ReadEmbeddedBrandIcon()
    {
        try
        {
            using var resource = typeof(AboutPage).Assembly
                .GetManifestResourceStream("TrafficLens.WinUI.Assets.TrafficLens-256.png");
            if (resource is null)
            {
                return null;
            }

            using var buffer = new MemoryStream();
            resource.CopyTo(buffer);
            return buffer.ToArray();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string GetVersionText()
    {
        try
        {
            var assembly = typeof(AboutPage).Assembly;
            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                var plus = informational.IndexOf('+');
                return plus > 0 ? informational[..plus] : informational;
            }

            var version = assembly.GetName().Version;
            return version is null
                ? string.Empty
                : $"{version.Major}.{version.Minor}.{version.Build}";
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
