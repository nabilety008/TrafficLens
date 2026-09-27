using System.Xml.Linq;
using TrafficLens.WinUI.Services;

namespace TrafficLens.WinUI.Tests;

/// <summary>
/// The navigation pane, the Alerts page and the Settings widget switches are all
/// declared in XAML. These assert the declarations themselves, so the checks are
/// about the markup rather than a fragile UI automation walk.
/// </summary>
public class WinUiPageMarkupTests
{
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const string XPrefixNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    // x:Name and x:DataType live in the x: prefix namespace, not the presentation one.
    private static readonly XName XNameAttr = XName.Get($"{{{XPrefixNamespace}}}Name");
    private static readonly XName XNameDataType = XName.Get($"{{{XPrefixNamespace}}}DataType");

    // The XAML stores the glyphs as &#xE...; entities, which the XML reader decodes
    // into the single Segoe Fluent Icons code point they stand for.
    private static readonly (string Name, char Glyph, string Tag)[] ExpectedNavItems =
    {
        ("DashboardNavItem", '', "Dashboard"),
        ("ApplicationsNavItem", '', "Applications"),
        ("ConnectionsNavItem", '', "Connections"),
        ("HistoryNavItem", '', "History"),
        ("AlertsNavItem", '', "Alerts"),
        ("SettingsNavItem", '', "Settings"),
        ("AboutNavItem", '', "About")
    };

    [Theory]
    [InlineData("DashboardNavItem", '', "Dashboard")]
    [InlineData("ApplicationsNavItem", '', "Applications")]
    [InlineData("ConnectionsNavItem", '', "Connections")]
    [InlineData("HistoryNavItem", '', "History")]
    [InlineData("AlertsNavItem", '', "Alerts")]
    [InlineData("SettingsNavItem", '', "Settings")]
    [InlineData("AboutNavItem", '', "About")]
    public void NavigationItem_DeclaresIconGlyphAndTag(string name, char glyph, string tag)
    {
        var item = NavItem(name);

        Assert.Equal(tag, item.Attribute("Tag")?.Value);

        var icon = item.Descendants().Single(e => e.Name.LocalName == "FontIcon");
        Assert.Equal(glyph, Assert.Single(icon.Attribute("Glyph")!.Value));
        Assert.Equal("Segoe Fluent Icons", icon.Attribute("FontFamily")?.Value);
    }

    [Fact]
    public void AllSevenNavigationItemsArePresentAndHaveIcons()
    {
        var items = Doc("MainWindow.xaml").Descendants()
            .Where(e => e.Name.LocalName == "NavigationViewItem")
            .ToList();

        Assert.Equal(7, items.Count);
        Assert.All(items, item =>
        {
            Assert.NotNull(item.Attribute("Tag"));
            Assert.Contains(item.Descendants(), e => e.Name.LocalName == "FontIcon");
        });
    }

    [Fact]
    public void NavigationItemsAreNamedForRuntimeLocalization()
    {
        foreach (var (name, _, _) in ExpectedNavItems)
        {
            var item = NavItem(name);
            Assert.NotNull(item.Attribute(XNameAttr));
        }
    }

    [Fact]
    public void NoExternalIconLibraryIsReferenced()
    {
        var ns = Doc("MainWindow.xaml").Descendants()
            .Select(e => e.Name.NamespaceName)
            .ToList();

        Assert.DoesNotContain(ns, n => n.Contains("Fluent.UI", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NavigationView_CollapsesToIconOnlyWidth()
    {
        var nav = Doc("MainWindow.xaml").Descendants().Single(e => e.Name.LocalName == "NavigationView");

        Assert.Equal("48", nav.Attribute("CompactPaneLength")?.Value);
        Assert.Equal("True", nav.Attribute("IsPaneToggleButtonVisible")?.Value);
    }

    [Fact]
    public void NavigationTargetMapping_RemainsCorrect()
    {
        var expected = new[]
        {
            ("Dashboard", "DashboardPage"),
            ("Applications", "ApplicationsPage"),
            ("Connections", "ConnectionsPage"),
            ("History", "HistoryPage"),
            ("Alerts", "AlertsPage"),
            ("Settings", "SettingsPage"),
            ("About", "AboutPage")
        };

        var code = Source("MainWindow.xaml.cs");

        foreach (var (tag, page) in expected)
        {
            Assert.Contains($"\"{tag}\" => typeof({page})", code, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NavigationLabelsAreLocalizedInBothCultures()
    {
        var code = Source("MainWindow.xaml.cs");

        foreach (var key in new[]
                 {
                     "DashboardLabel", "ApplicationsLabel", "ConnectionsLabel",
                     "HistoryLabel", "AlertsNavLabel", "SettingsNavLabel", "AboutNavLabel"
                 })
        {
            Assert.Contains($"_localization[\"{key}\"]", code, StringComparison.Ordinal);
        }

        // Tooltip and accessible name come from the same localized label.
        Assert.Contains("ToolTipService.SetToolTip", code, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName", code, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------- alerts

    [Fact]
    public void AllFiveAlertRulesRemainExposed()
    {
        var types = Enum.GetValues<TrafficLens.Core.Alerts.AlertType>();

        Assert.Equal(5, types.Length);
        Assert.Equal(
            new[]
            {
                TrafficLens.Core.Alerts.AlertType.HighDownloadSpeed,
                TrafficLens.Core.Alerts.AlertType.HighUploadSpeed,
                TrafficLens.Core.Alerts.AlertType.DailyDownloadLimit,
                TrafficLens.Core.Alerts.AlertType.DailyUploadLimit,
                TrafficLens.Core.Alerts.AlertType.DailyTotalLimit
            },
            types);
    }

    [Fact]
    public void AlertRuleOrderAndIdentityArePreserved()
    {
        var code = Source(Path.Combine("ViewModels", "AlertsViewModel.cs"));

        Assert.Contains("foreach (var type in Enum.GetValues<AlertType>())", code, StringComparison.Ordinal);
        Assert.Contains("_rules.Add(row);", code, StringComparison.Ordinal);
    }

    [Fact]
    public void AlertRuleCardsBindToTheExistingRuleCollection()
    {
        var itemsControl = RuleItemsControl();

        Assert.Contains(
            "ViewModel.Rules",
            itemsControl.Attribute("ItemsSource")?.Value ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Alerts_UseAResponsiveWrappingGridWithUniformCardSize()
    {
        var wrapGrid = Doc(Page("AlertsPage.xaml")).Descendants()
            .Single(e => e.Name.LocalName == "ItemsWrapGrid");

        Assert.Equal("216", wrapGrid.Attribute("ItemWidth")?.Value);
        Assert.Equal("104", wrapGrid.Attribute("ItemHeight")?.Value);
        Assert.Equal("Horizontal", wrapGrid.Attribute("Orientation")?.Value);
        Assert.Equal("4", wrapGrid.Attribute("MaximumRowsOrColumns")?.Value);
    }

    [Fact]
    public void Alerts_DoNotScrollHorizontally()
    {
        var scroller = Doc(Page("AlertsPage.xaml")).Descendants()
            .Single(e => e.Name.LocalName == "ScrollViewer" && e.Attribute(XNameAttr)?.Value == "PageScroller");

        Assert.Equal("Disabled", scroller.Attribute("HorizontalScrollBarVisibility")?.Value);
    }

    [Fact]
    public void EachAlertCardKeepsEveryExistingControl()
    {
        var bound = BoundPaths(RuleCard());

        Assert.Contains(bound, p => p.Contains("RuleName", StringComparison.Ordinal));
        Assert.Contains(bound, p => p.Contains("StatusText", StringComparison.Ordinal));
        Assert.Contains(bound, p => p.Contains("ThresholdText", StringComparison.Ordinal));
        Assert.Contains(bound, p => p.Contains("ActiveUnitOptions", StringComparison.Ordinal));
        Assert.Contains(bound, p => p.Contains("IsEnabled", StringComparison.Ordinal));
    }

    [Fact]
    public void AlertCooldownIsNotInsideARuleCard()
    {
        var bound = BoundPaths(RuleCard());

        Assert.DoesNotContain(bound, p => p.Contains("CooldownText", StringComparison.Ordinal));
        Assert.DoesNotContain(bound, p => p.Contains("CooldownLabel", StringComparison.Ordinal));
        Assert.DoesNotContain(bound, p => p.Contains("MinutesLabel", StringComparison.Ordinal));
    }

    [Fact]
    public void CooldownIsItsOwnSectionWithValueUnitValidationAndRange()
    {
        var bound = BoundPaths(Named("CooldownCard", Page("AlertsPage.xaml")));

        Assert.Contains(bound, p => p.Contains("CooldownText", StringComparison.Ordinal));
        Assert.Contains(bound, p => p.Contains("CooldownLabel", StringComparison.Ordinal));
        Assert.Contains(bound, p => p.Contains("MinutesLabel", StringComparison.Ordinal));
        Assert.Contains(bound, p => p.Contains("CooldownRangeLabel", StringComparison.Ordinal));
        Assert.Contains(bound, p => p.Contains("ValidationError", StringComparison.Ordinal));
    }

    [Fact]
    public void AlertSaveButtonUsesAccentStyleWithComfortablePadding()
    {
        var button = Named("SaveRulesButton", Page("AlertsPage.xaml"));

        Assert.Equal("{StaticResource AccentButtonStyle}", button.Attribute("Style")?.Value);
        Assert.Equal("20,8", button.Attribute("Padding")?.Value);
        Assert.Contains(
            "SaveLabel",
            button.Attribute("Content")?.Value ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AlertBackendLogicIsUnchanged()
    {
        var vm = Source(Path.Combine("ViewModels", "AlertsViewModel.cs"));

        Assert.Contains("public void Save()", vm, StringComparison.Ordinal);
        Assert.Contains("AlertSettings.Save(_settings, newConfig);", vm, StringComparison.Ordinal);
        Assert.Contains("type.IsSpeedRule()", vm, StringComparison.Ordinal);
        Assert.Contains("MinCooldownMinutes", vm, StringComparison.Ordinal);
        Assert.Contains("MaxCooldownMinutes", vm, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ settings

    [Fact]
    public void SettingsHasAQuickWidgetToggleAndTheFullWidgetSection()
    {
        var doc = Doc(Page("SettingsPage.xaml"));

        var toggles = doc.Descendants()
            .Where(e => e.Name.LocalName == "ToggleSwitch")
            .Select(e => e.Attribute(XNameAttr)?.Value)
            .ToList();

        Assert.Contains("WidgetQuickToggle", toggles);
        Assert.Contains("ShowWidgetToggle", toggles);
    }

    [Fact]
    public void SettingsQuickWidgetToggleComesBeforeTheWidgetSection()
    {
        var ordered = Doc(Page("SettingsPage.xaml")).Descendants().ToList();

        var quick = ordered.IndexOf(Named("WidgetQuickToggle", Page("SettingsPage.xaml"), ordered));
        var lower = ordered.IndexOf(Named("ShowWidgetToggle", Page("SettingsPage.xaml"), ordered));

        Assert.True(quick > 0 && lower > 0, "both toggles must be found");
        Assert.True(quick < lower, "the quick control must sit above the full widget section");
    }

    [Fact]
    public void SettingsQuickWidgetToggleIsBeforeTheStartupSection()
    {
        var ordered = Doc(Page("SettingsPage.xaml")).Descendants().ToList();

        var quick = ordered.IndexOf(Named("WidgetQuickToggle", Page("SettingsPage.xaml"), ordered));
        var startup = ordered.IndexOf(Named("StartWithWindowsToggle", Page("SettingsPage.xaml"), ordered));

        Assert.True(quick > 0 && startup > 0);
        Assert.True(quick < startup, "the quick control must sit at the top of the page");
    }

    [Fact]
    public void BothWidgetToggles_AreWiredToTheSameHandler()
    {
        var code = Source(Page("SettingsPage.xaml.cs"));

        Assert.Contains("WidgetQuickToggle_Toggled", code, StringComparison.Ordinal);
        Assert.Contains("ShowWidgetToggle_Toggled", code, StringComparison.Ordinal);
        Assert.Contains("ApplyWidgetToggle", code, StringComparison.Ordinal);
    }

    [Fact]
    public void WidgetEnableKeyIsOwnedByTheStateClass()
    {
        var page = Source(Page("SettingsPage.xaml.cs"));
        var state = Source(Path.Combine("Services", "WidgetEnabledState.cs"));

        // Neither the page nor the service writes the key directly any more; the one
        // owner is the state class, which is what makes the write idempotent.
        Assert.DoesNotContain("FloatingWidgetSettings.EnabledKey", page, StringComparison.Ordinal);
        Assert.DoesNotContain("FloatingWidgetSettings.EnabledKey", Source(Path.Combine("Services", "FloatingWidgetService.cs")), StringComparison.Ordinal);
        Assert.Contains("FloatingWidgetSettings.EnabledKey", state, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsPageSubscribesToBothWidgetSignals()
    {
        var code = Source(Page("SettingsPage.xaml.cs"));

        Assert.Contains("_widgetService.EnabledChanged", code, StringComparison.Ordinal);
        Assert.Contains("_widgetService.IsVisibleChanged", code, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsLabelsAreLocalizedNotHardcoded()
    {
        var code = Source(Page("SettingsPage.xaml.cs"));
        var xaml = File.ReadAllText(Path.Combine(WinUiRoot(), Page("SettingsPage.xaml")));

        // Both quick-control labels come from the shared localization service.
        Assert.Contains("WidgetQuickHeader.Text = _localization[\"FloatingWidgetLabel\"]", code, StringComparison.Ordinal);
        Assert.Contains("WidgetQuickText.Text = _localization[\"EnableFloatingWidgetLabel\"]", code, StringComparison.Ordinal);
        Assert.Contains("ShowWidgetText.Text = _localization[\"EnableFloatingWidgetLabel\"]", code, StringComparison.Ordinal);

        // The quick card declares no literal text of its own; the code-behind fills it.
        var quick = Between(xaml, "WidgetQuickHeader", "StartupHeader");
        Assert.DoesNotContain("Text=\"", quick, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"[A-Za-z]", quick, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetToDefaults_GoesThroughTheWidgetService()
    {
        var code = Source(Page("SettingsPage.xaml.cs"));

        Assert.Contains("_widgetService.Hide();", code, StringComparison.Ordinal);
        Assert.DoesNotContain("FloatingWidgetSettings.EnabledKey", code, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------- widget

    [Fact]
    public void WidgetToggleSync_PushesOneValueIntoBothSwitches()
    {
        var quick = new List<bool>();
        var section = new List<bool>();

        WidgetToggleSync.Apply(quick.Add, section.Add, true);
        WidgetToggleSync.Apply(quick.Add, section.Add, false);

        Assert.Equal(new[] { true, false }, quick);
        Assert.Equal(quick, section);
    }

    [Fact]
    public void WidgetToggleSync_ResolvesFromTheSingleSource()
    {
        Assert.True(WidgetToggleSync.Resolve(isEnabled: true, isVisible: false));
        Assert.True(WidgetToggleSync.Resolve(isEnabled: false, isVisible: true));
        Assert.False(WidgetToggleSync.Resolve(isEnabled: false, isVisible: false));
    }

    [Fact]
    public void WidgetClose_RoutesThroughTheServiceNotDirectly()
    {
        var service = Source(Path.Combine("Services", "FloatingWidgetService.cs"));

        Assert.Contains("OnUserCloseRequested", service, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(false)", service, StringComparison.Ordinal);
    }

    [Fact]
    public void WidgetWindowCancelsItsOwnCloseAndDoesNotExitTheApp()
    {
        var code = Source(Path.Combine("Views", "FloatingWidgetWindow.xaml.cs"));

        Assert.Contains("args.Cancel = true;", code, StringComparison.Ordinal);
        Assert.Contains("UserCloseRequested?.Invoke", code, StringComparison.Ordinal);

        Assert.DoesNotContain("ExitRequested", code, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplicationExitCoordinator", code, StringComparison.Ordinal);
    }

    [Fact]
    public void WidgetDragIsClampedToTheWorkAreaUsingTheScaledWindowSize()
    {
        var code = Source(Path.Combine("Views", "FloatingWidgetWindow.xaml.cs"));

        Assert.Contains("MoveClamped", code, StringComparison.Ordinal);
        Assert.Contains("WidgetPositionHelper.Clamp", code, StringComparison.Ordinal);

        // The clamp must use the real scaled window size, not the DIP design size.
        Assert.Contains("AppWindow.Size", code, StringComparison.Ordinal);
    }

    [Fact]
    public void WidgetDragScalesThePointerDeltaIntoPhysicalPixels()
    {
        var code = Source(Path.Combine("Views", "FloatingWidgetWindow.xaml.cs"));

        // The pointer reports DIPs while AppWindow.Position is physical pixels, so
        // the delta has to be scaled or the widget lags the pointer above 100%.
        Assert.Contains("GetDpiForWindow", code, StringComparison.Ordinal);
        Assert.Contains("BaseDpi", code, StringComparison.Ordinal);
        Assert.Contains("CurrentScale()", code, StringComparison.Ordinal);
        Assert.Contains("* scale)", code, StringComparison.Ordinal);
    }

    [Fact]
    public void NoNewTimerPollerOrBackgroundWorkerWasIntroduced()
    {
        var files = new[]
        {
            Path.Combine("Views", "FloatingWidgetWindow.xaml.cs"),
            Path.Combine("Services", "FloatingWidgetService.cs"),
            Path.Combine("Services", "WidgetEnabledState.cs"),
            Path.Combine("Services", "WidgetToggleSync.cs"),
            Path.Combine("Infrastructure", "TitleBarCaptionLayout.cs"),
            "MainWindow.xaml.cs"
        };

        foreach (var file in files)
        {
            var code = Source(file);

            Assert.DoesNotContain("DispatcherQueueTimer", code, StringComparison.Ordinal);
            Assert.DoesNotContain("System.Threading.Timer", code, StringComparison.Ordinal);
            Assert.DoesNotContain("PeriodicTimer", code, StringComparison.Ordinal);
            Assert.DoesNotContain("Task.Run", code, StringComparison.Ordinal);
        }
    }

    // ----------------------------------------------------------------- title bar

    [Fact]
    public void TitleBarReservesTheCaptionAreaFromLiveGeometry()
    {
        var code = Source("MainWindow.xaml.cs");

        Assert.Contains("ApplyCaptionSafeArea", code, StringComparison.Ordinal);
        Assert.Contains("AppWindow.TitleBar.LeftInset", code, StringComparison.Ordinal);
        Assert.Contains("AppWindow.TitleBar.RightInset", code, StringComparison.Ordinal);
    }

    [Fact]
    public void TitleBarHasNoHardcodedPerLanguageMargin()
    {
        var xaml = File.ReadAllText(Path.Combine(WinUiRoot(), "MainWindow.xaml"));
        var code = Source("MainWindow.xaml.cs");

        Assert.Contains("Padding=\"16,0,16,0\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("IsRightToLeft ?", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("IsRightToLeft ?", code, StringComparison.Ordinal);
    }

    [Fact]
    public void TitleBarReactsToPresenterSizeAndPositionChanges()
    {
        var code = Source("MainWindow.xaml.cs");

        Assert.Contains("args.DidPresenterChange", code, StringComparison.Ordinal);
        Assert.Contains("args.DidSizeChange", code, StringComparison.Ordinal);
        Assert.Contains("args.DidPositionChange", code, StringComparison.Ordinal);
    }

    [Fact]
    public void TitleBarPaddingIsSuppliedEntirelyByTheGeometryHelper()
    {
        var code = Source("MainWindow.xaml.cs");

        // The grid padding is replaced, not offset by a language-specific constant.
        Assert.Contains("AppTitleBar.Padding = new Thickness(left, 0, right, 0)", code, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------- helpers

    private static string Page(string file) => Path.Combine("Pages", file);

    private static XDocument Doc(string relativePath) => Load(Path.Combine("src", "TrafficLens.WinUI", relativePath));

    private static XElement NavItem(string name) =>
        Doc("MainWindow.xaml").Descendants()
            .Single(e => e.Name.LocalName == "NavigationViewItem"
                && e.Attribute(XNameAttr)?.Value == name);

    private static XElement Named(string name, string page, IReadOnlyList<XElement>? scope = null)
    {
        var source = scope ?? Doc(page).Descendants().ToList();

        var match = source.SingleOrDefault(e => e.Attribute(XNameAttr)?.Value == name);
        return match ?? throw new InvalidOperationException($"No element named {name} in {page}");
    }

    private static XElement RuleItemsControl() =>
        Doc(Page("AlertsPage.xaml")).Descendants()
            .Single(e => e.Name.LocalName == "ItemsControl"
                && (e.Attribute("ItemsSource")?.Value ?? string.Empty).Contains("ViewModel.Rules", StringComparison.Ordinal));

    private static XElement RuleCard() =>
        Doc(Page("AlertsPage.xaml")).Descendants()
            .Single(e => e.Name.LocalName == "DataTemplate"
                && (e.Attribute(XNameDataType)?.Value ?? string.Empty)
                    .Split(':').Last() == "AlertRuleRowViewModel");

    private static List<string> BoundPaths(XElement element) =>
        element.Descendants()
            .Select(e => new[] { "Text", "ItemsSource", "IsOn", "Content" }
                .Select(a => e.Attribute(a)?.Value)
                .FirstOrDefault(v => !string.IsNullOrEmpty(v)))
            .Where(v => v is not null)
            .Select(v => v!)
            .ToList();

    private static string Between(string text, string startMarker, string endMarker)
    {
        var start = text.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"marker {startMarker} not found");

        var end = text.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"marker {endMarker} not found after {startMarker}");

        return text.Substring(start, end - start);
    }

    private static XDocument Load(string relativePath)
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate))
            {
                return XDocument.Load(candidate);
            }

            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        throw new FileNotFoundException($"Could not locate {relativePath} from {AppContext.BaseDirectory}");
    }

    private static string Source(string relativeToWinUi) =>
        File.ReadAllText(Path.Combine(WinUiRoot(), relativeToWinUi.Replace('/', Path.DirectorySeparatorChar)));

    private static string WinUiRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "src", "TrafficLens.WinUI");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        throw new DirectoryNotFoundException("Could not locate the TrafficLens.WinUI source root.");
    }
}
