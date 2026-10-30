using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Blippy.Core;
using Blippy.Tray;
using Blippy.Ui;

namespace Blippy;

/// <summary>
/// <c>Blippy.exe --preview &lt;folder&gt;</c> renders the flyout (light and dark, devices and
/// settings) and every tray icon style with made-up devices, then exits. CI runs it to
/// prove the UI loads on Windows and to give reviewers screenshots without real hardware.
/// </summary>
internal static class Preview
{
    public static readonly BatteryReading[] SampleDevices =
    [
        new("ds", "DualSense Wireless Controller", DeviceKind.DualSense, 65, ChargeState.Charging, BatterySource.DualSenseHid),
        new("xb", "Xbox Wireless Controller", DeviceKind.Xbox, 18, ChargeState.Discharging, BatterySource.GamingInput),
        new("wh", "WH-1000XM4", DeviceKind.Headphones, 90, ChargeState.Unknown, BatterySource.WindowsBluetooth),
        new("mx", "MX Master 3S", DeviceKind.Mouse, 42, ChargeState.Unknown, BatterySource.WindowsBluetooth),
    ];

    public static int Run(Application app, string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var settings = new SettingsStore(new Settings { LightbarEnabled = true });
            var monitor = new BatteryMonitor(settings, app.Dispatcher);

            foreach (var dark in new[] { false, true })
            {
                Theme.Apply(app.Resources, dark);
                var theme = dark ? "dark" : "light";
                Flyout(settings, monitor, SampleDevices, settingsPage: false, Path.Combine(folder, $"flyout-{theme}.png"));
                Flyout(settings, monitor, SampleDevices, settingsPage: true, Path.Combine(folder, $"settings-{theme}.png"));
                Flyout(settings, monitor, [], settingsPage: false, Path.Combine(folder, $"empty-{theme}.png"));
            }

            foreach (var style in Enum.GetValues<TrayStyle>())
                foreach (var r in SampleDevices.Prepend(null))
                    File.WriteAllBytes(
                        Path.Combine(folder, $"tray-{style.ToString().ToLowerInvariant()}-{r?.Id ?? "idle"}.png"),
                        TrayIconRenderer.RenderPng(r, style, 64));

            Console.WriteLine($"Preview written to {Path.GetFullPath(folder)}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void Flyout(SettingsStore settings, BatteryMonitor monitor, IReadOnlyList<BatteryReading> devices, bool settingsPage, string path)
    {
        var window = new FlyoutWindow(settings, monitor)
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            ShowActivated = false,
        };
        window.PrepareForPreview(devices, settingsPage);
        window.Show();
        window.Left = -20000;
        window.Top = -20000;
        window.UpdateLayout();

        // Render the window itself: rendering its content directly would shift it by the content's margin.
        const double scale = 2;
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(window.ActualWidth * scale), (int)Math.Ceiling(window.ActualHeight * scale),
            96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(path)) encoder.Save(file);
        window.Close();
    }
}
