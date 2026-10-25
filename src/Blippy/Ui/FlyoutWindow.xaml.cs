using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Blippy.Core;
using Blippy.Motion;
using Forms = System.Windows.Forms;

namespace Blippy.Ui;

/// <summary>
/// The bouncy panel that springs up above the tray: every device with its battery,
/// and the settings behind the gear.
/// </summary>
public partial class FlyoutWindow : Window
{
    private static readonly (string Name, string Hex)[] SwatchColors =
    [
        ("Lavender", "#6E64E8"),
        ("Sky", "#2F9BFF"),
        ("Mint", "#1FD1A0"),
        ("Lime", "#7BE33A"),
        ("Sunny", "#FFD400"),
        ("Bubblegum", "#FF5FB4"),
        ("Grape", "#A43BFF"),
        ("Snow", "#FFFFFF"),
    ];

    private readonly SettingsStore settings;
    private readonly BatteryMonitor monitor;
    private readonly Dictionary<string, DeviceCard> cards = new();
    private readonly DispatcherTimer footerTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private RadioButton? customSwatch;
    private DateTime? lastChecked;
    private DateTime hiddenAt;
    private bool closing;
    private bool holdOpen;
    private bool syncing;
    private bool showingSettings;
    private bool preview;

    private bool HasRead => preview || monitor.HasRead;

    internal FlyoutWindow(SettingsStore settings, BatteryMonitor monitor)
    {
        this.settings = settings;
        this.monitor = monitor;
        InitializeComponent();
        BuildSwatches();
        BuildRefreshChips();
        SyncSettings(settings.Current);

        settings.Changed += s =>
        {
            SyncSettings(s);
            Render(monitor.Latest, animateArrivals: false);
        };
        monitor.Updated += r =>
        {
            lastChecked = DateTime.Now;
            Render(r, animateArrivals: IsVisible);
        };
        footerTimer.Tick += (_, _) => UpdateFooter();

        Deactivated += (_, _) =>
        {
            if (!holdOpen) HideAnimated();
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                if (showingSettings) ShowPage(settingsPage: false);
                else HideAnimated();
                e.Handled = true;
            }
        };
        SizeChanged += (_, _) => Place();
    }

    /// <summary>Opens or closes the flyout. A click on the tray icon that just closed it does not reopen it.</summary>
    public void Toggle(bool forceOpen = false, bool settings = false)
    {
        if (IsVisible && !closing)
        {
            if (!forceOpen)
            {
                HideAnimated();
                return;
            }
            ShowPage(settings);
            Activate();
            return;
        }
        if (!forceOpen && (closing || (DateTime.UtcNow - hiddenAt).TotalMilliseconds < 300)) return;
        Open(settings);
    }

    private void Open(bool settingsPage)
    {
        closing = false;
        ShowPage(settingsPage, animate: false);
        Render(monitor.Latest, animateArrivals: false);
        Opacity = 1;
        Show();
        Place();
        Activate();
        footerTimer.Start();
        UpdateFooter();

        Animate.PopIn(Panel, fromScale: 0.9, rise: 26);
        Animate.Float(Logo, distance: 3, seconds: 2.4);
        Drift();
        if (!showingSettings) ArriveCards();
        else ArriveSettings();
    }

    private void HideAnimated()
    {
        if (!IsVisible || closing) return;
        closing = true;
        footerTimer.Stop();
        var (scale, _) = Animate.Transforms(Panel);
        Animate.To(scale, ScaleTransform.ScaleXProperty, 0.94, Spring.Snappy);
        Animate.To(scale, ScaleTransform.ScaleYProperty, 0.94, Spring.Snappy);
        Animate.Fade(Panel, 0, 130, done: () =>
        {
            if (!closing) return;
            Hide();
            closing = false;
            hiddenAt = DateTime.UtcNow;
        });
    }

    /// <summary>Sits in the corner next to the tray, whichever edge the taskbar is on.</summary>
    private void Place()
    {
        var area = SystemParameters.WorkArea;
        var screen = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
        bool taskbarTop = area.Top > screen.Top;
        bool taskbarLeft = area.Left > screen.Left;
        Left = taskbarLeft ? area.Left : area.Right - ActualWidth;
        Top = taskbarTop ? area.Top : area.Bottom - ActualHeight;
    }

    // ---- Devices ----------------------------------------------------------------

    private void Render(IReadOnlyList<BatteryReading> readings, bool animateArrivals)
    {
        var s = settings.Current;
        var gradient = LightbarGradient.Default(s.LightbarRgb);

        foreach (var gone in cards.Keys.Except(readings.Select(r => r.Id)).ToList())
        {
            var card = cards[gone];
            cards.Remove(gone);
            Leave(card);
        }

        int index = 0;
        foreach (var r in readings)
        {
            Rgb? lightbar = s.LightbarEnabled && r.Source == BatterySource.DualSenseHid ? gradient.ColorFor(r.Level) : null;
            if (cards.TryGetValue(r.Id, out var card))
            {
                card.Show(r, lightbar);
            }
            else
            {
                card = new DeviceCard(r);
                card.Show(r, lightbar);
                cards[r.Id] = card;
                if (animateArrivals) card.Arrive(TimeSpan.Zero);
            }
            // Keep the list in the merged order.
            var at = DeviceList.Children.IndexOf(card);
            if (at != index)
            {
                if (at >= 0) DeviceList.Children.RemoveAt(at);
                DeviceList.Children.Insert(Math.Min(index, DeviceList.Children.Count), card);
            }
            index++;
        }

        bool empty = readings.Count == 0;
        if (empty != (EmptyState.Visibility == Visibility.Visible))
        {
            EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            if (empty && IsVisible) ArriveEmpty();
        }
        EmptyTitle.Text = HasRead ? "No batteries to show yet" : "Looking for devices…";
        Summary.Text = SummaryText(readings);
    }

    private void Leave(DeviceCard card)
    {
        var (scale, _) = Animate.Transforms(card);
        Animate.To(scale, ScaleTransform.ScaleXProperty, 0.8, Spring.Snappy);
        Animate.To(scale, ScaleTransform.ScaleYProperty, 0.8, Spring.Snappy);
        Animate.Fade(card, 0, 160, done: () => DeviceList.Children.Remove(card));
    }

    private string SummaryText(IReadOnlyList<BatteryReading> readings)
    {
        if (readings.Count == 0) return HasRead ? "Nothing with a battery is connected" : "Looking for devices…";
        var lowest = readings.MinBy(r => r.Level)!;
        var count = readings.Count == 1 ? "1 device" : $"{readings.Count} devices";
        return readings.Count == 1
            ? $"{lowest.Name} is at {lowest.Level}%"
            : $"{count} · lowest is {lowest.Name} at {lowest.Level}%";
    }

    private void ArriveCards()
    {
        int i = 0;
        foreach (UIElement child in DeviceList.Children)
            if (child is DeviceCard card) card.Arrive(TimeSpan.FromMilliseconds(70 + 60 * i++));
        if (EmptyState.Visibility == Visibility.Visible) ArriveEmpty();
    }

    private void ArriveEmpty()
    {
        Animate.PopIn(EmptyArt, TimeSpan.FromMilliseconds(80));
        Animate.Float(EmptyState.Children[0], distance: 6);
    }

    private void UpdateFooter()
    {
        var every = FormatSeconds(settings.Current.RefreshSeconds);
        if (lastChecked is not { } at)
        {
            Footer.Text = $"Checks every {every}";
            return;
        }
        var ago = DateTime.Now - at;
        var when = ago.TotalSeconds < 10 ? "just now" : ago.TotalMinutes < 1 ? $"{(int)ago.TotalSeconds}s ago" : $"{(int)ago.TotalMinutes} min ago";
        Footer.Text = $"Checked {when} · every {every}";
    }

    private static string FormatSeconds(int s) => s < 60 ? $"{s}s" : s % 60 == 0 ? $"{s / 60} min" : $"{s}s";

    private void Drift()
    {
        if (MotionPrefs.Reduced) return;
        var (_, move) = Animate.Transforms(DriftLight);
        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
        move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, -90, TimeSpan.FromSeconds(4.5)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease });
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, 40, TimeSpan.FromSeconds(3.7)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease });
    }

    /// <summary>Lays the flyout out with the given devices and no animation, for <see cref="Preview"/>.</summary>
    internal void PrepareForPreview(IReadOnlyList<BatteryReading> readings, bool settingsPage)
    {
        preview = true;
        holdOpen = true;
        lastChecked = DateTime.Now;
        ShowPage(settingsPage, animate: false);
        Render(readings, animateArrivals: false);
        UpdateFooter();
    }

    // ---- Header buttons --------------------------------------------------------

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        Animate.Spin(RefreshGlyph);
        monitor.RefreshNow();
    }

    private void OnSettings(object sender, RoutedEventArgs e) => ShowPage(!showingSettings);

    private void ShowPage(bool settingsPage, bool animate = true)
    {
        if (settingsPage == showingSettings && animate) return;
        showingSettings = settingsPage;
        DevicesPage.Visibility = settingsPage ? Visibility.Collapsed : Visibility.Visible;
        SettingsPage.Visibility = settingsPage ? Visibility.Visible : Visibility.Collapsed;
        SettingsGlyph.Glyph = settingsPage ? "x" : "settings";
        if (!animate) return;
        Animate.Boing(SettingsButton, 0.25);
        if (settingsPage) ArriveSettings();
        else ArriveCards();
    }

    private void ArriveSettings()
    {
        int i = 0;
        foreach (UIElement child in SettingsList.Children)
            Animate.PopIn(child, TimeSpan.FromMilliseconds(40 + 55 * i++), fromScale: 0.85, rise: 16);
    }

    // ---- Settings --------------------------------------------------------------

    private void BuildSwatches()
    {
        foreach (var (name, hex) in SwatchColors)
        {
            var swatch = new RadioButton
            {
                Style = (Style)FindResource("Swatch"),
                GroupName = "Lightbar",
                Background = Theme.Solid(Rgb.Parse(hex)),
                Tag = hex,
                ToolTip = name,
            };
            System.Windows.Automation.AutomationProperties.SetName(swatch, name);
            swatch.Checked += OnSwatch;
            Swatches.Children.Add(swatch);
        }

        // The last dot opens the full color picker.
        customSwatch = new RadioButton
        {
            Style = (Style)FindResource("Swatch"),
            GroupName = "Lightbar",
            ToolTip = "Pick any color",
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(Colors.HotPink, 0),
                    new GradientStop(Colors.Gold, 0.35),
                    new GradientStop(Colors.MediumSpringGreen, 0.65),
                    new GradientStop(Colors.DodgerBlue, 1),
                },
            },
        };
        System.Windows.Automation.AutomationProperties.SetName(customSwatch, "Custom color");
        // Click (not Checked) so picking again while it is already selected still opens the picker.
        customSwatch.Click += (_, _) => PickCustomColor();
        Swatches.Children.Add(customSwatch);
    }

    private void BuildRefreshChips()
    {
        foreach (var seconds in Settings.RefreshChoices)
        {
            var chip = new RadioButton
            {
                Style = (Style)FindResource("Chip"),
                GroupName = "Refresh",
                Content = FormatSeconds(seconds),
                Tag = seconds,
            };
            chip.Checked += (_, _) =>
            {
                if (!syncing) settings.Update(s => s with { RefreshSeconds = seconds });
            };
            RefreshChips.Children.Add(chip);
        }
    }

    private void OnSwatch(object sender, RoutedEventArgs e)
    {
        if (syncing || sender is not RadioButton { Tag: string hex }) return;
        settings.Update(s => s with { LightbarColor = hex });
    }

    private void PickCustomColor()
    {
        var current = settings.Current.LightbarRgb;
        using var dialog = new Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true,
            Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B),
        };
        holdOpen = true;
        try
        {
            // Owned by the flyout so the picker opens above it rather than behind (the flyout is topmost).
            if (dialog.ShowDialog(new DialogOwner(new WindowInteropHelper(this).Handle)) == Forms.DialogResult.OK)
            {
                var c = dialog.Color;
                settings.Update(s => s with { LightbarColor = new Rgb(c.R, c.G, c.B).ToHex() });
            }
        }
        finally
        {
            holdOpen = false;
            SyncSettings(settings.Current);
            Activate();
        }
    }

    private sealed record DialogOwner(IntPtr Handle) : Forms.IWin32Window;

    private void OnSettingToggled(object sender, RoutedEventArgs e)
    {
        if (syncing) return;
        settings.Update(s => s with
        {
            LightbarEnabled = LightbarToggle.IsChecked == true,
            IconPerDevice = PerDeviceToggle.IsChecked == true,
            TrayStyle = StyleGlyph.IsChecked == true ? TrayStyle.Glyph : TrayStyle.Number,
            LowBatteryAlerts = AlertsToggle.IsChecked == true,
            StartWithWindows = StartupToggle.IsChecked == true,
        });
    }

    private void SyncSettings(Settings s)
    {
        syncing = true;
        try
        {
            LightbarToggle.IsChecked = s.LightbarEnabled;
            PerDeviceToggle.IsChecked = s.IconPerDevice;
            StyleNumber.IsChecked = s.TrayStyle == TrayStyle.Number;
            StyleGlyph.IsChecked = s.TrayStyle == TrayStyle.Glyph;
            AlertsToggle.IsChecked = s.LowBatteryAlerts;
            StartupToggle.IsChecked = s.StartWithWindows;

            var hex = s.LightbarRgb.ToHex();
            var match = Swatches.Children.OfType<RadioButton>().FirstOrDefault(r => string.Equals(r.Tag as string, hex, StringComparison.OrdinalIgnoreCase));
            (match ?? customSwatch)!.IsChecked = true;

            foreach (var chip in RefreshChips.Children.OfType<RadioButton>())
                chip.IsChecked = chip.Tag is int sec && sec == s.RefreshSeconds;

            Swatches.Opacity = GradientPreview.Opacity = s.LightbarEnabled ? 1 : 0.45;
            GradientPreview.Background = GradientBrush(s.LightbarRgb);
        }
        finally
        {
            syncing = false;
        }
        UpdateFooter();
    }

    /// <summary>The lightbar gradient as it will really look, empty on the left and full on the right.</summary>
    private static Brush GradientBrush(Rgb full)
    {
        var gradient = LightbarGradient.Default(full);
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        for (int level = 0; level <= 100; level += 10)
        {
            var c = gradient.ColorFor(level);
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(c.R, c.G, c.B), level / 100.0));
        }
        brush.Freeze();
        return brush;
    }

    private void OnOpenLog(object sender, RoutedEventArgs e)
    {
        try
        {
            if (System.IO.File.Exists(Log.FilePath))
                Process.Start(new ProcessStartInfo(Log.FilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error("Opening the log", ex);
        }
    }

    private void OnQuit(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
