using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Blippy.Core;
using Blippy.Motion;

namespace Blippy.Ui;

/// <summary>One device in the flyout: its icon, name, a springy battery bar and the percentage.</summary>
internal sealed class DeviceCard : Border
{
    private readonly Border tile = new() { Width = 48, Height = 48, CornerRadius = new CornerRadius(16) };
    private readonly Icon glyph = new() { Width = 26, Height = 26, StrokeWidth = 2 };
    private readonly Border badge = new()
    {
        Width = 22, Height = 22, CornerRadius = new CornerRadius(11),
        HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
        Margin = new Thickness(0, 0, -5, -5), BorderThickness = new Thickness(2),
    };
    private readonly Icon badgeGlyph = new() { Width = 13, Height = 13, StrokeWidth = 2.4 };
    private readonly TextBlock name = new() { FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock word = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border lightbarDot = new()
    {
        Width = 9, Height = 9, CornerRadius = new CornerRadius(4.5), Margin = new Thickness(8, 1, 0, 0),
        VerticalAlignment = VerticalAlignment.Center, ToolTip = "Lightbar color right now",
    };
    private readonly BatteryBar bar = new() { Height = 8, Margin = new Thickness(0, 9, 0, 0) };
    private readonly TextBlock percent = new() { FontSize = 22, FontWeight = FontWeights.Black, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right, MinWidth = 50 };
    private readonly Icon bolt = new() { Glyph = "bolt", Width = 16, Height = 16, StrokeWidth = 2.4, Margin = new Thickness(0, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center };

    public DeviceCard(BatteryReading reading)
    {
        Reading = reading;
        CornerRadius = new CornerRadius(20);
        Padding = new Thickness(12);
        Margin = new Thickness(0, 0, 0, 10);
        BorderThickness = new Thickness(1);
        SetResourceReference(BackgroundProperty, "Card");
        SetResourceReference(BorderBrushProperty, "CardBorder");
        Squish.SetHoverScale(this, 1.02);
        Squish.SetEnabled(this, true);

        name.SetResourceReference(TextBlock.ForegroundProperty, "Text");
        word.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
        percent.SetResourceReference(TextBlock.ForegroundProperty, "Text");
        bar.SetResourceReference(BatteryBar.TrackProperty, "Track");
        badge.SetResourceReference(BackgroundProperty, "Card");
        badge.SetResourceReference(BorderBrushProperty, "Card");
        badgeGlyph.SetResourceReference(Icon.StrokeProperty, "Text");
        bolt.SetResourceReference(Icon.StrokeProperty, "Primary");
        bar.ProgressChanged += () =>
            percent.Text = Math.Clamp((int)Math.Round(bar.Progress * 100), 0, 100).ToString(CultureInfo.CurrentCulture) + "%";

        badge.Child = badgeGlyph;
        tile.Child = new Grid { Children = { glyph, badge } };
        glyph.HorizontalAlignment = HorizontalAlignment.Center;
        glyph.VerticalAlignment = VerticalAlignment.Center;
        badgeGlyph.HorizontalAlignment = HorizontalAlignment.Center;
        badgeGlyph.VerticalAlignment = VerticalAlignment.Center;

        var middle = new StackPanel { Margin = new Thickness(12, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        middle.Children.Add(name);
        middle.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0), Children = { word, lightbarDot } });
        middle.Children.Add(bar);

        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { bolt, percent } };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(middle, 1);
        Grid.SetColumn(right, 2);
        grid.Children.Add(tile);
        grid.Children.Add(middle);
        grid.Children.Add(right);
        Child = grid;

        Animate.Wiggle(bolt);
        Show(reading, lightbar: null);
        bar.Progress = reading.Level / 100.0;
        percent.Text = reading.Level.ToString(CultureInfo.CurrentCulture) + "%";
    }

    public BatteryReading Reading { get; private set; }

    /// <summary>Updates the card. A level change springs the bar and boings the icon.</summary>
    public void Show(BatteryReading reading, Rgb? lightbar)
    {
        var levelChanged = reading.Level != Reading.Level;
        Reading = reading;

        var color = LevelPalette.For(reading.Level, reading.Charge);
        tile.Background = Theme.Solid(color, 0x2E);
        glyph.Glyph = DeviceIcons.For(reading.Kind);
        glyph.Stroke = Theme.Solid(color);
        bar.Fill = new LinearGradientBrush(Lighten(color), ToColor(color), 0);

        var badgeName = DeviceIcons.BadgeFor(reading.Kind);
        badge.Visibility = badgeName is null ? Visibility.Collapsed : Visibility.Visible;
        if (badgeName is not null) badgeGlyph.Glyph = badgeName;

        name.Text = reading.Name;
        word.Text = LevelPalette.Word(reading.Level, reading.Charge);
        bolt.Visibility = reading.IsCharging ? Visibility.Visible : Visibility.Collapsed;

        if (lightbar is { } lb)
        {
            lightbarDot.Visibility = Visibility.Visible;
            lightbarDot.Background = Theme.Solid(lb);
            lightbarDot.Effect = new DropShadowEffect { Color = ToColor(lb), BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9 };
        }
        else
        {
            lightbarDot.Visibility = Visibility.Collapsed;
        }

        AutomationName = $"{reading.Name}, {reading.Level} percent, {word.Text}";
        if (levelChanged)
        {
            Animate.To(bar, BatteryBar.ProgressProperty, reading.Level / 100.0, Spring.Bouncy);
            Animate.Boing(tile);
        }
    }

    private string AutomationName
    {
        set => System.Windows.Automation.AutomationProperties.SetName(this, value);
    }

    /// <summary>The opening flourish: pop in after <paramref name="delay"/> and fill the bar from empty.</summary>
    public void Arrive(TimeSpan delay)
    {
        Animate.PopIn(this, delay, fromScale: 0.7, rise: 18);
        Animate.To(bar, BatteryBar.ProgressProperty, Reading.Level / 100.0, Spring.Bouncy, delay + TimeSpan.FromMilliseconds(90), from: 0);
    }

    private static Color ToColor(Rgb c) => Color.FromRgb(c.R, c.G, c.B);

    private static Color Lighten(Rgb c) =>
        Color.FromRgb((byte)(c.R + (255 - c.R) * 0.35), (byte)(c.G + (255 - c.G) * 0.35), (byte)(c.B + (255 - c.B) * 0.35));
}
