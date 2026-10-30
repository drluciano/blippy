using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Blippy.Ui;

/// <summary>
/// flippy's palette, light and dark, following the Windows app theme. Everything in the
/// flyout reads these brushes through DynamicResource, so a theme switch repaints it.
/// </summary>
internal static class Theme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool AppsDark => ReadDword("AppsUseLightTheme") == 0;

    /// <summary>The taskbar has its own light/dark setting.</summary>
    public static bool TaskbarDark => ReadDword("SystemUsesLightTheme") != 1;

    private static int? ReadDword(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue(name) is int v ? v : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static readonly Dictionary<string, string> Light = new()
    {
        ["BgTop"] = "#F4F1FF",
        ["BgBottom"] = "#EAF6F3",
        ["Card"] = "#D9FFFFFF",
        ["CardHover"] = "#FFFFFFFF",
        ["CardBorder"] = "#1A786EC8",
        ["Text"] = "#26253A",
        ["TextSecondary"] = "#62617C",
        ["TextMuted"] = "#9897AE",
        ["Primary"] = "#6E64E8",
        ["PrimarySoft"] = "#ECEAFE",
        ["OnPrimary"] = "#FFFFFF",
        ["Track"] = "#1A6E64E8",
        ["Shadow"] = "#5B4FD0",
        ["Hero1"] = "#8C7CF0",
        ["Hero2"] = "#7AA7F2",
        ["Hero3"] = "#7FD3C0",
    };

    private static readonly Dictionary<string, string> Dark = new()
    {
        ["BgTop"] = "#15142A",
        ["BgBottom"] = "#0F1C1F",
        ["Card"] = "#C724223E",
        ["CardHover"] = "#E62E2B4E",
        ["CardBorder"] = "#1AB4AAFF",
        ["Text"] = "#EEEDFB",
        ["TextSecondary"] = "#B4B2CF",
        ["TextMuted"] = "#7D7B99",
        ["Primary"] = "#A49BFF",
        ["PrimarySoft"] = "#29A49BFF",
        ["OnPrimary"] = "#15142A",
        ["Track"] = "#24A49BFF",
        ["Shadow"] = "#000000",
        ["Hero1"] = "#5B4FC8",
        ["Hero2"] = "#4A73C4",
        ["Hero3"] = "#3F9E8E",
    };

    public static void Apply(ResourceDictionary resources, bool? dark = null)
    {
        var palette = dark ?? AppsDark ? Dark : Light;
        foreach (var (key, hex) in palette)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[key + "Color"] = color;
            resources[key] = brush;
        }
        var bg = new LinearGradientBrush(
            (Color)resources["BgTopColor"], (Color)resources["BgBottomColor"], new Point(0, 0), new Point(0.4, 1));
        bg.Freeze();
        resources["Background"] = bg;
        var hero = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop((Color)resources["Hero1Color"], 0),
                new GradientStop((Color)resources["Hero2Color"], 0.55),
                new GradientStop((Color)resources["Hero3Color"], 1),
            },
        };
        hero.Freeze();
        resources["Hero"] = hero;
    }

    public static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    public static SolidColorBrush Solid(Core.Rgb c, byte alpha = 255)
    {
        var b = new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
        b.Freeze();
        return b;
    }

    /// <summary>Calls back when the person switches Windows between light and dark.</summary>
    public static void WatchForChanges(Action changed)
    {
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
                Application.Current.Dispatcher.BeginInvoke(changed);
        };
    }
}
