using Blippy.Core;
using Blippy.Ui;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Blippy.Tray;

/// <summary>
/// Owns the tray: one icon per device (or one summary icon), their tooltips, the
/// right-click menu, low battery nudges, and opening the flyout on a left click.
/// </summary>
internal sealed class TrayController : IDisposable
{
    private readonly SettingsStore settings;
    private readonly BatteryMonitor monitor;
    private readonly FlyoutWindow flyout;
    private readonly LowBatteryAlerts alerts = new();
    private readonly Forms.ContextMenuStrip menu;
    private readonly Dictionary<string, Forms.NotifyIcon> deviceIcons = new();
    private Forms.NotifyIcon? summaryIcon;
    private IReadOnlyList<BatteryReading> readings = [];

    public TrayController(SettingsStore settings, BatteryMonitor monitor, FlyoutWindow flyout)
    {
        this.settings = settings;
        this.monitor = monitor;
        this.flyout = flyout;

        menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Blippy", null, (_, _) => flyout.Toggle(forceOpen: true));
        menu.Items.Add("Check now", null, (_, _) => monitor.RefreshNow());
        menu.Items.Add("Settings", null, (_, _) => flyout.Toggle(forceOpen: true, settings: true));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => System.Windows.Application.Current.Shutdown());

        monitor.Updated += OnUpdated;
        settings.Changed += _ => Redraw();
        Theme.WatchForChanges(Redraw);
        Redraw();
    }

    private void OnUpdated(IReadOnlyList<BatteryReading> latest)
    {
        readings = latest;
        Redraw();
        if (!settings.Current.LowBatteryAlerts) return;
        alerts.Retain(latest.Select(r => r.Id));
        foreach (var r in latest)
        {
            if (alerts.Check(r) is not { } threshold) continue;
            var icon = deviceIcons.GetValueOrDefault(r.Id) ?? summaryIcon;
            icon?.ShowBalloonTip(
                5000,
                threshold <= 10 ? $"{r.Name} is almost out" : $"{r.Name} is getting low",
                $"{r.Level}% left. Time to plug it in!",
                Forms.ToolTipIcon.Warning);
        }
    }

    private void Redraw()
    {
        var s = settings.Current;
        if (s.IconPerDevice && readings.Count > 0)
        {
            RemoveSummary();
            foreach (var r in readings)
            {
                if (!deviceIcons.TryGetValue(r.Id, out var icon)) deviceIcons[r.Id] = icon = Create();
                SetIcon(icon, TrayIconRenderer.Render(r, s.TrayStyle));
                icon.Text = Tooltip(r);
            }
            foreach (var gone in deviceIcons.Keys.Except(readings.Select(r => r.Id)).ToList())
            {
                Discard(deviceIcons[gone]);
                deviceIcons.Remove(gone);
            }
        }
        else
        {
            foreach (var icon in deviceIcons.Values) Discard(icon);
            deviceIcons.Clear();
            summaryIcon ??= Create();
            var lowest = readings.MinBy(r => r.Level);
            SetIcon(summaryIcon, TrayIconRenderer.Render(lowest, s.TrayStyle));
            summaryIcon.Text = readings.Count == 0
                ? (monitor.HasRead ? "Blippy: no batteries found yet" : "Blippy: looking for devices…")
                : Fit("Blippy\n" + string.Join("\n", readings.Select(Tooltip)));
        }
    }

    private static string Tooltip(BatteryReading r) =>
        Fit($"{r.Name}: {r.Level}%{(r.IsCharging ? " (charging)" : r.Charge == ChargeState.Full ? " (full)" : "")}");

    // Windows caps tray tooltips at 127 characters.
    private static string Fit(string text) => text.Length <= 127 ? text : text[..126] + "…";

    private Forms.NotifyIcon Create()
    {
        var icon = new Forms.NotifyIcon { ContextMenuStrip = menu, Visible = false };
        icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) flyout.Toggle();
        };
        return icon;
    }

    private static void SetIcon(Forms.NotifyIcon target, Drawing.Icon image)
    {
        var old = target.Icon;
        target.Icon = image;
        target.Visible = true;
        TrayIconRenderer.Destroy(old);
    }

    private void RemoveSummary()
    {
        if (summaryIcon is null) return;
        Discard(summaryIcon);
        summaryIcon = null;
    }

    private static void Discard(Forms.NotifyIcon icon)
    {
        icon.Visible = false;
        var image = icon.Icon;
        icon.Dispose();
        TrayIconRenderer.Destroy(image);
    }

    public void Dispose()
    {
        foreach (var icon in deviceIcons.Values) Discard(icon);
        deviceIcons.Clear();
        RemoveSummary();
        menu.Dispose();
    }
}
