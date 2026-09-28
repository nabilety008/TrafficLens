using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrafficLens.Core.History;
using TrafficLens.Core.Localization;
using TrafficLens.WinUI.ViewModels;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace TrafficLens.WinUI.Pages;

public sealed partial class HistoryPage : Page
{
    private readonly HistoryViewModel _viewModel;
    private readonly ILocalizationService _localization;
    private readonly ILogger<HistoryPage> _logger;

    public HistoryPage()
    {
        InitializeComponent();

        var services = App.Services.Provider;
        _localization = services.GetRequiredService<ILocalizationService>();
        _viewModel = new HistoryViewModel(
            services.GetRequiredService<ITrafficHistoryService>(),
            _localization,
            services.GetRequiredService<Microsoft.UI.Dispatching.DispatcherQueue>());
        _logger = services.GetRequiredService<ILogger<HistoryPage>>();

        Bindings.Update();
        ApplyFlowDirection();
        ApplyRangeButtons();
        DataContext = _viewModel;

        _localization.CultureChanged += OnCultureChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public HistoryViewModel ViewModel => _viewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.SetActive(true);
        ApplyRangeButtons();
        ApplyExportButton();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.SetActive(false);
        _localization.CultureChanged -= OnCultureChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        _viewModel.Dispose();
    }

    private void OnCultureChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyFlowDirection();
            ApplyRangeButtons();
            ApplyExportButton();
            Bindings.Update();
        });

    private void ApplyFlowDirection()
    {
        var direction = _localization.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
        PageScroller.FlowDirection = direction;
        RootLayout.FlowDirection = direction;
    }

    private void ApplyRangeButtons()
    {
        RangeTodayButton.Content = _viewModel.TodayLabel;
        RangeYesterdayButton.Content = _viewModel.YesterdayLabel;
        RangeLast7Button.Content = _viewModel.Last7DaysLabel;
        RangeLast30Button.Content = _viewModel.Last30DaysLabel;
        RangeLifetimeButton.Content = _viewModel.LifetimeLabel;
        RangeThisMonthButton.Content = _viewModel.ThisMonthLabel;
    }

    private void ApplyExportButton()
    {
        ExportCsvButton.Content = _viewModel.ExportCsvLabel;
    }

    private void Range_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } || !int.TryParse(tag, out var value))
        {
            return;
        }

        if (Enum.IsDefined(typeof(HistoryRange), value))
        {
            _viewModel.SelectRange((HistoryRange)value);
        }
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var csv = _viewModel.BuildCsv();
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = _viewModel.CsvSuggestedFileName
            };
            picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });

            var main = (App)Application.Current;
            var hwnd = WindowNative.GetWindowHandle(main.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                // Cancel is a normal outcome, not an export failure: no status, no log.
                return;
            }

            try
            {
                var bytes = new UTF8Encoding(true).GetBytes(csv);
                await FileIO.WriteBytesAsync(file, bytes);
            }
            catch (Exception writeEx)
            {
                _logger.LogError(
                    writeEx,
                    "History CSV write failed. Type={ExceptionType} HResult=0x{HResult:X8} Path={FilePath}",
                    writeEx.GetType().FullName,
                    writeEx.HResult,
                    file.Path);
                throw;
            }

            _viewModel.MarkExportSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "History CSV export failed. Type={ExceptionType} HResult=0x{HResult:X8}",
                ex.GetType().FullName,
                ex.HResult);
            _viewModel.MarkExportFailure();
        }

        Bindings.Update();
    }
}
