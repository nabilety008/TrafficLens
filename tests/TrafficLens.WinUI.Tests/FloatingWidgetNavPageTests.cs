using System.Xml.Linq;

namespace TrafficLens.WinUI.Tests;

/// <summary>
/// Guards the Floating Widget navigation item and its page. The page is only a
/// front end for the existing <c>IFloatingWidgetService</c>, so these tests assert
/// that the markup, the routing and the wiring all point at that one state owner
/// instead of keeping a second copy of the flags.
/// </summary>
public sealed class FloatingWidgetNavPageTests
{
    private const string EnabledKey = "FloatingWidgetEnabled";
    private const string AlwaysOnTopKey = "FloatingWidgetAlwaysOnTop";

    private const string XPrefixNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly XName XNameAttr = XName.Get($"{{{XPrefixNamespace}}}Name");

    [Fact]
    public void NavigationContainsAFloatingWidgetItem()
    {
        var item = NavItem();

        Assert.Equal("FloatingWidget", item.Attribute("Tag")?.Value);
        Assert.Equal("FloatingWidgetNavItem", item.Attribute(XNameAttr)?.Value);
    }

    [Fact]
    public void FloatingWidgetNavigationItem_HasANativeIcon()
    {
        var icon = NavItem().Descendants().Single(e => e.Name.LocalName == "FontIcon");

        Assert.Equal('\uE992', Assert.Single(icon.Attribute("Glyph")!.Value));
        Assert.Equal("Segoe Fluent Icons", icon.Attribute("FontFamily")?.Value);
    }

    [Fact]
    public void FloatingWidgetNavigationItem_SitsBetweenAlertsAndSettings()
    {
        var order = MainWindow().Descendants()
            .Where(e => e.Name.LocalName == "NavigationViewItem")
            .Select(e => e.Attribute("Tag")?.Value)
            .ToList();

        Assert.True(
            order.IndexOf("FloatingWidget") > order.IndexOf("Alerts"),
            "the widget item must come after Alerts");
        Assert.True(
            order.IndexOf("FloatingWidget") < order.IndexOf("Settings"),
            "the widget item must come before Settings");
    }

    [Fact]
    public void FloatingWidgetNavigationItem_IsLocalizedFromTheResourceString()
    {
        // Like every other item, the markup carries an English placeholder and
        // SetNavItem replaces content, tooltip and accessibility name at startup.
        Assert.Equal("FloatingWidgetNavItem", NavItem().Attribute(XNameAttr)?.Value);

        var source = File.ReadAllText(MainWindowCodePath());
        Assert.Contains("SetNavItem(FloatingWidgetNavItem,", source, StringComparison.Ordinal);
        Assert.Contains("FloatingWidgetLabel", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FloatingWidgetTagIsRoutedToTheWidgetPage()
    {
        var source = File.ReadAllText(MainWindowCodePath());

        Assert.Contains("\"FloatingWidget\" => typeof(FloatingWidgetPage)", source, StringComparison.Ordinal);
        Assert.Contains("SetNavItem(FloatingWidgetNavItem,", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WidgetPageExistsWithBothControls()
    {
        var page = PageDoc();

        Assert.NotNull(page.Root);
        Assert.Contains(page.Descendants(), e => e.Name.LocalName == "ToggleSwitch");
        Assert.Equal(2, page.Descendants().Count(e => e.Name.LocalName == "ToggleSwitch"));
    }

    [Fact]
    public void WidgetPageTogglesAreNamedForRuntimeLocalization()
    {
        var source = File.ReadAllText(PagePath("FloatingWidgetPage.xaml.cs"));

        Assert.Contains("EnableFloatingWidgetLabel", source, StringComparison.Ordinal);
        Assert.Contains("AlwaysOnTopLabel", source, StringComparison.Ordinal);
        Assert.Contains("FloatingWidgetPageDescriptionLabel", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WidgetPageHasNoBakedInEnglishStrings()
    {
        // Only attribute values that are actually shown to the user matter here;
        // x:Name values such as EnableToggle legitimately contain the word.
        var shown = PageDoc().Descendants()
            .SelectMany(e => new[] { "Text", "Content", "Header", "Title" }
                .Select(a => e.Attribute(a)?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v)))
            .ToList();

        foreach (var value in shown)
        {
            foreach (var literal in new[] { "Floating Widget", "Always On Top", "Widget" })
            {
                Assert.False(
                    value!.Contains(literal, StringComparison.Ordinal),
                    $"'{value}' is hard coded instead of coming from the resource file");
            }
        }
    }

    [Fact]
    public void WidgetPageResubscribesWhenTheFrameLoadsItAgain()
    {
        // A cached page is unloaded and loaded again. If the handlers are only
        // attached in the constructor and released in Unloaded, the second visit
        // would silently stop following the service.
        var source = File.ReadAllText(PagePath("FloatingWidgetPage.xaml.cs"));

        Assert.Contains("private void Subscribe()", source, StringComparison.Ordinal);
        Assert.Contains("private void Unsubscribe()", source, StringComparison.Ordinal);
        Assert.Contains("private bool _subscribed", source, StringComparison.Ordinal);

        // Subscribe must be reachable from the load path, not only the constructor.
        var loaded = source[source.IndexOf("private void OnLoaded", StringComparison.Ordinal)..];
        var loadedBody = loaded[..loaded.IndexOf("private void OnUnloaded", StringComparison.Ordinal)];
        Assert.Contains("Subscribe();", loadedBody, StringComparison.Ordinal);
    }

    [Fact]
    public void WidgetPageTogglesReadAndWriteThroughTheExistingService()
    {
        var source = File.ReadAllText(PagePath("FloatingWidgetPage.xaml.cs"));

        Assert.Contains("IFloatingWidgetService", source, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(", source, StringComparison.Ordinal);
        Assert.Contains("SetAlwaysOnTop(", source, StringComparison.Ordinal);
        Assert.Contains("WidgetToggleSync.Resolve", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WidgetPageKeepsNoPrivateCopyOfTheFlags()
    {
        var source = File.ReadAllText(PagePath("FloatingWidgetPage.xaml.cs"));

        // A second bool field would drift from the widget and the Settings page.
        Assert.DoesNotContain("private bool _isEnabled", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private bool _alwaysOnTop", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WidgetPageFollowsServiceChangeEvents()
    {
        var source = File.ReadAllText(PagePath("FloatingWidgetPage.xaml.cs"));

        Assert.Contains("EnabledChanged", source, StringComparison.Ordinal);
        Assert.Contains("AlwaysOnTopChanged", source, StringComparison.Ordinal);
        Assert.Contains("IsVisibleChanged", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WidgetPageStartsNoPollingLoop()
    {
        var source = File.ReadAllText(PagePath("FloatingWidgetPage.xaml.cs"));

        foreach (var forbidden in new[] { "DispatcherTimer", "System.Timers.Timer", "Task.Delay", "while (true" })
        {
            Assert.False(
                source.Contains(forbidden, StringComparison.Ordinal),
                $"the page must be event driven, but it contains '{forbidden}'");
        }
    }

    [Fact]
    public void SettingsPageAlwaysOnTopToggleFollowsTheService()
    {
        var source = File.ReadAllText(PagePath("SettingsPage.xaml.cs"));

        Assert.Contains("AlwaysOnTopChanged", source, StringComparison.Ordinal);
        Assert.Contains("_widgetService.IsAlwaysOnTop", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsAndWidgetPageReadTheSameSettingKeys()
    {
        // Both front ends must go through the shared service/state instead of reading
        // or writing their own copy of the flags.
        var page = File.ReadAllText(PagePath("FloatingWidgetPage.xaml.cs"));
        var settings = File.ReadAllText(PagePath("SettingsPage.xaml.cs"));
        var state = File.ReadAllText(Resolve("src", "TrafficLens.WinUI", "Services", "WidgetEnabledState.cs"));
        var service = File.ReadAllText(Resolve("src", "TrafficLens.WinUI", "Services", "FloatingWidgetService.cs"));
        var keys = service;

        // Enable state is owned by WidgetEnabledState; always-on-top by the service.
        Assert.Contains("FloatingWidgetSettings.EnabledKey", state, StringComparison.Ordinal);
        Assert.Contains("FloatingWidgetSettings.AlwaysOnTopKey", service, StringComparison.Ordinal);
        Assert.Contains("IFloatingWidgetService", page, StringComparison.Ordinal);
        Assert.Contains("_widgetService", settings, StringComparison.Ordinal);

        // Neither page may hard code a persisted key; the keys stay centralized.
        foreach (var key in new[] { EnabledKey, AlwaysOnTopKey })
        {
            Assert.False(page.Contains(key, StringComparison.Ordinal), $"page hard codes {key}");
            Assert.False(settings.Contains(key, StringComparison.Ordinal), $"settings page hard codes {key}");
        }

        Assert.Contains(EnabledKey, keys, StringComparison.Ordinal);
        Assert.Contains(AlwaysOnTopKey, keys, StringComparison.Ordinal);
    }

    [Fact]
    public void CollapsedNavigationStaysIconOnly()
    {
        var pane = MainWindow().Descendants()
            .Single(e => e.Name.LocalName == "NavigationView");

        // The item is a normal NavigationViewItem, so the built-in compact mode
        // hides its label and leaves the icon; nothing per-item may opt out.
        var item = NavItem();
        Assert.DoesNotContain("Visibility", item.Attributes().Select(a => a.Name.LocalName));
        Assert.NotNull(pane.Attribute("IsPaneToggleButtonVisible"));
    }

    [Fact]
    public void WidgetPageUsesFlowDirectionForBothCultures()
    {
        var page = File.ReadAllText(PagePath("FloatingWidgetPage.xaml.cs"));
        var alerts = File.ReadAllText(PagePath("AlertsPage.xaml.cs"));

        Assert.Contains("FlowDirection", page, StringComparison.Ordinal);
        Assert.Contains("FlowDirection", alerts, StringComparison.Ordinal);
    }

    private static string MainWindowPath() => Resolve("src", "TrafficLens.WinUI", "MainWindow.xaml");

    private static string MainWindowCodePath() => Resolve("src", "TrafficLens.WinUI", "MainWindow.xaml.cs");

    private static string PagePath(string file) => Resolve("src", "TrafficLens.WinUI", "Pages", file);

    private static XDocument Load(string relativePath) => XDocument.Load(Resolve(relativePath.Split('/')));

    private static XDocument MainWindow() => Load("src/TrafficLens.WinUI/MainWindow.xaml");

    private static XDocument PageDoc() => Load("src/TrafficLens.WinUI/Pages/FloatingWidgetPage.xaml");

    private static string Resolve(params string[] relativePath)
    {
        var relative = Path.Combine(relativePath);
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        throw new FileNotFoundException($"Could not locate {relative} from {AppContext.BaseDirectory}");
    }

    private static XElement NavItem() =>
        MainWindow().Descendants()
            .Single(e => e.Name.LocalName == "NavigationViewItem"
                && e.Attribute(XNameAttr)?.Value == "FloatingWidgetNavItem");
}
