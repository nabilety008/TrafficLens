using System.Xml.Linq;

namespace TrafficLens.WinUI.Tests;

/// <summary>
/// Guards the Floating Widget quick control in the persistent shell. The widget is
/// deliberately not a navigation destination: it is a show/hide switch that must be
/// reachable from every page and must stay a view of the one enabled state owned by
/// <c>IFloatingWidgetService</c>.
/// </summary>
public sealed class WidgetQuickToggleTests
{
    private const string XPrefixNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly XName XNameAttr = XName.Get($"{{{XPrefixNamespace}}}Name");

    private const string EnabledKey = "FloatingWidgetEnabled";
    private const string AlwaysOnTopKey = "FloatingWidgetAlwaysOnTop";

    [Fact]
    public void FloatingWidgetIsNotANavigationDestination()
    {
        var tags = MainWindow().Descendants()
            .Where(e => e.Name.LocalName == "NavigationViewItem")
            .Select(e => e.Attribute("Tag")?.Value)
            .ToList();

        Assert.DoesNotContain("FloatingWidget", tags);
        Assert.Equal(
            new[] { "Dashboard", "Applications", "Connections", "History", "Alerts", "Settings", "About" },
            tags);
    }

    [Fact]
    public void NoNavigationMappingPointsAtAWidgetPage()
    {
        var source = File.ReadAllText(MainWindowCodePath());

        Assert.DoesNotContain("FloatingWidgetPage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"FloatingWidget\" =>", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DedicatedWidgetPageFilesAreGone()
    {
        var pages = Resolve("src", "TrafficLens.WinUI", "Pages");

        Assert.False(File.Exists(Path.Combine(pages, "FloatingWidgetPage.xaml")));
        Assert.False(File.Exists(Path.Combine(pages, "FloatingWidgetPage.xaml.cs")));
    }

    [Fact]
    public void WidgetPageDescriptionResourceIsGone()
    {
        foreach (var resx in new[] { "Strings.resx", "Strings.fa-IR.resx" })
        {
            var text = File.ReadAllText(Resolve("src", "TrafficLens.App", "Resources", resx));
            Assert.DoesNotContain("FloatingWidgetPageDescriptionLabel", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void QuickControlLivesInThePersistentTitleBar()
    {
        var titleBar = MainWindow().Descendants()
            .Single(e => e.Name.LocalName == "Grid" && e.Attribute(XNameAttr)?.Value == "AppTitleBar");

        Assert.NotNull(titleBar.Descendants()
            .SingleOrDefault(e => e.Name.LocalName == "ToggleButton"
                && e.Attribute(XNameAttr)?.Value == "WidgetQuickToggle"));
    }

    [Fact]
    public void QuickControlIsOutsideTheNavigationView()
    {
        // It must not be a nav item, and it must live in the shell, not in a page, so
        // it stays available whichever page is selected.
        var nav = MainWindow().Descendants().Single(e => e.Name.LocalName == "NavigationView");

        Assert.DoesNotContain(nav.Descendants(),
            e => e.Attribute(XNameAttr)?.Value == "WidgetQuickToggle");
    }

    [Fact]
    public void QuickControlIsInTheFirstColumnSoItStaysTopLeft()
    {
        var toggle = QuickToggle();
        var titleBar = MainWindow().Descendants()
            .Single(e => e.Name.LocalName == "Grid" && e.Attribute(XNameAttr)?.Value == "AppTitleBar");

        // Column order follows FlowDirection, so the grid is pinned to left-to-right
        // to keep the control physically in the top-left corner in Persian too.
        Assert.Equal("0", toggle.Attribute("Grid.Column")?.Value);
        Assert.Equal("LeftToRight", titleBar.Attribute("FlowDirection")?.Value);
        Assert.Equal("Left", toggle.Attribute("HorizontalAlignment")?.Value);
    }

    [Fact]
    public void QuickControlIsNotShiftedByTheCaptionSafeArea()
    {
        var titleBar = MainWindow().Descendants()
            .Single(e => e.Name.LocalName == "Grid" && e.Attribute(XNameAttr)?.Value == "AppTitleBar");
        var code = File.ReadAllText(MainWindowCodePath());

        // The caption reserve is applied to the title, not as padding on the title bar
        // grid, so the control's left margin is a constant and cannot be pushed
        // around by whichever side the caption buttons sit on.
        Assert.Equal("0", titleBar.Attribute("Padding")?.Value);
        Assert.Contains("AppTitleBar.Padding = new Thickness(0)", code, StringComparison.Ordinal);
        Assert.Contains("TitleText.Margin =", code, StringComparison.Ordinal);

        // left 12, top 0, right 12 (gap to the title column), bottom 0
        var margin = QuickToggle().Attribute("Margin")!.Value.Split(',');
        Assert.Equal("12", margin[0].Trim());
        Assert.Equal("0", margin[1].Trim());
        Assert.Equal("12", margin[2].Trim());
        Assert.Equal("0", margin[3].Trim());
    }

    [Fact]
    public void QuickControlIsCompactAndUsesANativeIcon()
    {
        var toggle = QuickToggle();
        var icon = toggle.Descendants().Single(e => e.Name.LocalName == "FontIcon");

        Assert.Equal("Segoe Fluent Icons", icon.Attribute("FontFamily")?.Value);
        Assert.Equal('\uE992', Assert.Single(icon.Attribute("Glyph")!.Value));
        Assert.True(
            double.Parse(toggle.Attribute("Height")!.Value, System.Globalization.CultureInfo.InvariantCulture) <= 32,
            "the control must not dominate the title bar");
    }

    [Fact]
    public void QuickControlShowsIconAndLocalizedLabel()
    {
        var toggle = QuickToggle();

        Assert.Contains(
            toggle.Descendants(),
            e => e.Name.LocalName == "TextBlock"
                && e.Attribute(XNameAttr)?.Value == "WidgetQuickToggleLabel");

        var source = File.ReadAllText(MainWindowCodePath());
        Assert.Contains("WidgetQuickToggleLabel.Text = widgetLabel", source, StringComparison.Ordinal);
        Assert.Contains("ToolTipService.SetToolTip(WidgetQuickToggle, widgetLabel)", source, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(WidgetQuickToggle, widgetLabel)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickControlLabelComesFromTheExistingWidgetResource()
    {
        var en = File.ReadAllText(Resolve("src", "TrafficLens.App", "Resources", "Strings.resx"));
        var fa = File.ReadAllText(Resolve("src", "TrafficLens.App", "Resources", "Strings.fa-IR.resx"));

        Assert.Contains("<data name=\"FloatingWidgetLabel\"", en, StringComparison.Ordinal);
        Assert.Contains("<value>Floating Widget</value>", en, StringComparison.Ordinal);
        Assert.Contains("<data name=\"FloatingWidgetLabel\"", fa, StringComparison.Ordinal);
        Assert.Contains("<value>ویجت شناور</value>", fa, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickControlDrivesTheExistingService()
    {
        var source = File.ReadAllText(MainWindowCodePath());

        Assert.Contains("_widgetService.SetEnabled(WidgetQuickToggle.IsChecked == true)", source, StringComparison.Ordinal);
        Assert.Contains("WidgetToggleSync.Resolve", source, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickControlFollowsServiceEvents()
    {
        // Settings switches and the widget's own close button all go through the
        // service, so following its events is what keeps the shell in step.
        var source = File.ReadAllText(MainWindowCodePath());

        Assert.Contains("_widgetService.EnabledChanged += OnWidgetEnabledChanged", source, StringComparison.Ordinal);
        Assert.Contains("_widgetService.IsVisibleChanged += OnWidgetVisibleChanged", source, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickControlKeepsNoSecondEnabledFlag()
    {
        var source = File.ReadAllText(MainWindowCodePath());

        Assert.DoesNotContain("private bool _widgetEnabled", source, StringComparison.Ordinal);
        Assert.DoesNotContain(EnabledKey, source, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickControlStartsNoPolling()
    {
        var source = File.ReadAllText(MainWindowCodePath());

        foreach (var forbidden in new[] { "DispatcherTimer", "System.Timers.Timer", "Task.Delay", "while (true" })
        {
            Assert.False(
                source.Contains(forbidden, StringComparison.Ordinal),
                $"the shell must stay event driven, but it contains '{forbidden}'");
        }
    }

    [Fact]
    public void QuickControlDoesNotCarryAlwaysOnTop()
    {
        // Always On Top stays a detailed option in Settings; the shell control is
        // only the show/hide switch.
        var toggle = QuickToggle();
        var source = File.ReadAllText(MainWindowCodePath());

        Assert.DoesNotContain(toggle.Descendants(),
            e => e.Attribute(XNameAttr)?.Value == "AlwaysOnTopToggle");
        Assert.DoesNotContain("AlwaysOnTopToggle", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsSwitchStillFollowsTheSameService()
    {
        var settings = File.ReadAllText(Resolve("src", "TrafficLens.WinUI", "Pages", "SettingsPage.xaml.cs"));

        Assert.Contains("_widgetService.SetEnabled(enabled)", settings, StringComparison.Ordinal);
        Assert.Contains("WidgetToggleSync.Resolve", settings, StringComparison.Ordinal);
        Assert.Contains("_widgetService.EnabledChanged += OnWidgetEnabledChanged", settings, StringComparison.Ordinal);
        Assert.Contains("_widgetService.AlwaysOnTopChanged += OnAlwaysOnTopChanged", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTwoSwitchesWriteThroughTheServiceInBothDirections()
    {
        // Quick action ON and Settings OFF have to be the same round trip, so both
        // windows must write only through SetEnabled and both must read the one
        // resolved value back out of the service.
        var shell = File.ReadAllText(MainWindowCodePath());
        var settings = File.ReadAllText(Resolve("src", "TrafficLens.WinUI", "Pages", "SettingsPage.xaml.cs"));

        foreach (var source in new[] { shell, settings })
        {
            Assert.Contains("_widgetService.SetEnabled(", source, StringComparison.Ordinal);
            Assert.Contains("WidgetToggleSync.Resolve", source, StringComparison.Ordinal);
            Assert.DoesNotContain(EnabledKey, source, StringComparison.Ordinal);
        }

        // Each window listens to the service, which is how a change made in the other
        // window, or by the widget's own close button, reaches it.
        Assert.Contains("_widgetService.EnabledChanged += OnWidgetEnabledChanged", shell, StringComparison.Ordinal);
        Assert.Contains("_widgetService.IsVisibleChanged += OnWidgetVisibleChanged", shell, StringComparison.Ordinal);
        Assert.Contains("_widgetService.EnabledChanged += OnWidgetEnabledChanged", settings, StringComparison.Ordinal);
        Assert.Contains("_widgetService.IsVisibleChanged += OnWidgetVisibleChanged", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void NeitherWindowStartsAPollerForTheOtherSwitch()
    {
        // The two switches agree because both read the one service, never because one
        // window watches the other.
        var sources = new[]
        {
            File.ReadAllText(MainWindowCodePath()),
            File.ReadAllText(Resolve("src", "TrafficLens.WinUI", "Pages", "SettingsPage.xaml.cs")),
        };

        foreach (var source in sources)
        {
            foreach (var forbidden in new[] { "DispatcherTimer", "System.Timers.Timer", "Task.Delay", "while (true" })
            {
                Assert.False(
                    source.Contains(forbidden, StringComparison.Ordinal),
                    $"a window must stay event driven, but it contains '{forbidden}'");
            }
        }
    }

    [Fact]
    public void WidgetCloseButtonStillGoesThroughSetEnabled()
    {
        // Requirement: the native X disables the widget and persists it, without
        // exiting the app. That stays in the service, untouched by the shell control.
        var service = File.ReadAllText(Resolve("src", "TrafficLens.WinUI", "Services", "FloatingWidgetService.cs"));
        var window = File.ReadAllText(Resolve("src", "TrafficLens.WinUI", "Views", "FloatingWidgetWindow.xaml.cs"));

        Assert.Contains("UserCloseRequested", window, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(false)", service, StringComparison.Ordinal);
    }

    [Fact]
    public void PersistedWidgetKeysAreStillTheOnesTheServiceUses()
    {
        var service = File.ReadAllText(Resolve("src", "TrafficLens.WinUI", "Services", "FloatingWidgetService.cs"));
        var state = File.ReadAllText(Resolve("src", "TrafficLens.WinUI", "Services", "WidgetEnabledState.cs"));

        Assert.Contains($"\"{EnabledKey}\"", service, StringComparison.Ordinal);
        Assert.Contains($"\"{AlwaysOnTopKey}\"", service, StringComparison.Ordinal);
        Assert.Contains("FloatingWidgetSettings.EnabledKey", state, StringComparison.Ordinal);
    }

    [Fact]
    public void WidgetMovementBoundsAreStillImplemented()
    {
        var service = File.ReadAllText(Resolve("src", "TrafficLens.WinUI", "Services", "FloatingWidgetService.cs"));

        // The whole widget stays inside the work area of the monitor it is on, and
        // the monitor is chosen from the live work areas rather than assumed primary.
        Assert.Contains("WidgetPositionHelper.Clamp", service, StringComparison.Ordinal);
        Assert.Contains("SelectWorkArea", service, StringComparison.Ordinal);
        Assert.Contains("GetWorkAreas", service, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp", service, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows.Forms.Screen.PrimaryScreen", service, StringComparison.Ordinal);
    }

    [Fact]
    public void CollapsedNavigationRemainsIconOnly()
    {
        var nav = MainWindow().Descendants().Single(e => e.Name.LocalName == "NavigationView");

        Assert.Equal("48", nav.Attribute("CompactPaneLength")?.Value);
        Assert.Equal("True", nav.Attribute("IsPaneToggleButtonVisible")?.Value);
    }

    [Fact]
    public void RtlCaptionFixIsStillInPlace()
    {
        var source = File.ReadAllText(MainWindowCodePath());

        // The insets must still be resolved in flow order against the layout
        // direction; reading them as literal left/right is the bug that was fixed.
        // The measured fallback sits after that path and must not replace it.
        Assert.Contains("AppWindow.TitleBar.LeftInset", source, StringComparison.Ordinal);
        Assert.Contains("AppWindow.TitleBar.RightInset", source, StringComparison.Ordinal);
        Assert.Contains("RootGrid.FlowDirection == FlowDirection.RightToLeft", source, StringComparison.Ordinal);
        Assert.Contains("isRightToLeft", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PriPublishFixIsStillInPlace()
    {
        var project = File.ReadAllText(Resolve("src", "TrafficLens.WinUI", "TrafficLens.WinUI.csproj"));

        Assert.Contains("IncludeProjectPriFileInPublish", project, StringComparison.Ordinal);
    }

    private static XElement QuickToggle() =>
        MainWindow().Descendants()
            .Single(e => e.Name.LocalName == "ToggleButton"
                && e.Attribute(XNameAttr)?.Value == "WidgetQuickToggle");

    private static XDocument MainWindow() => XDocument.Load(Resolve("src", "TrafficLens.WinUI", "MainWindow.xaml"));

    private static string MainWindowCodePath() => Resolve("src", "TrafficLens.WinUI", "MainWindow.xaml.cs");

    private static string Resolve(params string[] relativePath)
    {
        var relative = Path.Combine(relativePath);
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, relative);
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        throw new FileNotFoundException($"Could not locate {relative} from {AppContext.BaseDirectory}");
    }
}
