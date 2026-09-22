using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace TrafficLens.App.Tests;

public class DarkThemeTests
{
    private static string FindThemePath()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "src", "TrafficLens.App", "Themes", "DarkTheme.xaml");
            if (File.Exists(candidate))
                return candidate;
            candidate = Path.Combine(dir, "TrafficLens.App", "Themes", "DarkTheme.xaml");
            if (File.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException("DarkTheme.xaml not found from " + AppDomain.CurrentDomain.BaseDirectory);
    }

    private static ResourceDictionary LoadTheme()
    {
        var path = FindThemePath();
        var xaml = File.ReadAllText(path);
        return (ResourceDictionary)XamlReader.Parse(xaml);
    }

    [Fact]
    public void DarkTheme_ContainsAllRequiredBrushResources()
    {
        var theme = LoadTheme();

        Assert.NotNull(theme["BackgroundBrush"]);
        Assert.NotNull(theme["SurfaceBrush"]);
        Assert.NotNull(theme["SurfaceAltBrush"]);
        Assert.NotNull(theme["BorderBrush"]);
        Assert.NotNull(theme["TextBrush"]);
        Assert.NotNull(theme["TextMutedBrush"]);
        Assert.NotNull(theme["AccentBrush"]);
        Assert.NotNull(theme["AccentAltBrush"]);
    }

    [Fact]
    public void DarkTheme_ComboBoxStyleExists()
    {
        var theme = LoadTheme();
        var style = theme[typeof(ComboBox)] as Style;

        Assert.NotNull(style);
        Assert.Equal(typeof(ComboBox), style.TargetType);
    }

    [Fact]
    public void DarkTheme_ComboBoxItemStyleExists()
    {
        var theme = LoadTheme();
        var style = theme[typeof(ComboBoxItem)] as Style;

        Assert.NotNull(style);
        Assert.Equal(typeof(ComboBoxItem), style.TargetType);
    }

    [Fact]
    public void DarkTheme_ContextMenuStyleExists()
    {
        var theme = LoadTheme();
        var style = theme[typeof(ContextMenu)] as Style;

        Assert.NotNull(style);
        Assert.Equal(typeof(ContextMenu), style.TargetType);
    }

    [Fact]
    public void DarkTheme_MenuItemStyleExists()
    {
        var theme = LoadTheme();
        var style = theme[typeof(MenuItem)] as Style;

        Assert.NotNull(style);
        Assert.Equal(typeof(MenuItem), style.TargetType);
    }

    [Fact]
    public void DarkTheme_ComboBoxStyle_HasControlTemplate()
    {
        var theme = LoadTheme();
        var style = theme[typeof(ComboBox)] as Style;

        Assert.NotNull(style);
        var templateSetter = style.Setters
            .OfType<Setter>()
            .FirstOrDefault(s => s.Property == Control.TemplateProperty);
        Assert.NotNull(templateSetter);
    }

    [Fact]
    public void DarkTheme_ComboBoxItemStyle_HasControlTemplate()
    {
        var theme = LoadTheme();
        var style = theme[typeof(ComboBoxItem)] as Style;

        Assert.NotNull(style);
        var templateSetter = style.Setters
            .OfType<Setter>()
            .FirstOrDefault(s => s.Property == Control.TemplateProperty);
        Assert.NotNull(templateSetter);
    }

    [Fact]
    public void DarkTheme_ComboBoxTemplate_ContainsDropDownBorder()
    {
        var path = FindThemePath();
        var xaml = File.ReadAllText(path);

        Assert.Contains("DropDownBorder", xaml);
        Assert.Contains("x:Name=\"DropDownBorder\"", xaml);
    }

    [Fact]
    public void DarkTheme_ComboBoxDropDownBorder_UsesSurfaceBrush()
    {
        var path = FindThemePath();
        var xaml = File.ReadAllText(path);

        Assert.Contains("Background=\"{DynamicResource SurfaceBrush}\"", xaml);
    }

    [Fact]
    public void DarkTheme_Xaml_DoesNotReferenceSystemColorsWindowBrushKey()
    {
        var path = FindThemePath();
        var xaml = File.ReadAllText(path);

        Assert.DoesNotContain("SystemColors.WindowBrushKey", xaml);
        Assert.DoesNotContain("WindowBrushKey", xaml);
    }

    [Fact]
    public void DarkTheme_TextBoxStyleExists()
    {
        var theme = LoadTheme();
        var style = theme[typeof(TextBox)] as Style;

        Assert.NotNull(style);
        Assert.Equal(typeof(TextBox), style.TargetType);
    }

    [Fact]
    public void DarkTheme_ButtonStyleExists()
    {
        var theme = LoadTheme();
        var style = theme[typeof(Button)] as Style;

        Assert.NotNull(style);
        Assert.Equal(typeof(Button), style.TargetType);
    }

    [Fact]
    public void DarkTheme_CheckBoxStyleExists()
    {
        var theme = LoadTheme();
        var style = theme[typeof(CheckBox)] as Style;

        Assert.NotNull(style);
        Assert.Equal(typeof(CheckBox), style.TargetType);
    }
}
