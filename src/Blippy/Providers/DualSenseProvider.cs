using Blippy.Core;
using System.IO;
using HidSharp;

namespace Blippy.Providers;

/// <summary>
/// Talks to DualSense controllers directly over HID: reads the battery from their input
/// report and, when the person turns it on, paints the battery level onto the lightbar.
/// </summary>
internal sealed class DualSenseProvider : IBatteryProvider
{
    private readonly Dictionary<string, HidDevice> pads = new();
    private readonly Dictionary<string, Rgb> lastLightbar = new();
    private readonly object gate = new();
    private byte sequence;

    public string Name => "DualSense";

    public Task<IReadOnlyList<BatteryReading>> ReadAsync(CancellationToken cancel) =>
        Task.Run<IReadOnlyList<BatteryReading>>(() => Read(cancel), cancel);

    private List<BatteryReading> Read(CancellationToken cancel)
    {
        var readings = new List<BatteryReading>();
        var found = DeviceList.Local.GetHidDevices(DualSense.SonyVendorId)
            .Where(d => DualSense.IsDualSense(d.VendorID, d.ProductID))
            .ToList();

        lock (gate)
        {
            pads.Clear();
            foreach (var device in found)
            {
                cancel.ThrowIfCancellationRequested();
                pads[device.DevicePath] = device;
                if (ReadBattery(device) is not { } battery) continue;
                var name = device.ProductID == DualSense.DualSenseEdgeProductId
                    ? "DualSense Edge"
                    : "DualSense Wireless Controller";
                readings.Add(new BatteryReading(
                    device.DevicePath, name, DeviceKind.DualSense, battery.Level, battery.Charge, BatterySource.DualSenseHid));
            }
            // Forget lightbar colors for pads that are gone, so a reconnected pad gets painted again.
            foreach (var id in lastLightbar.Keys.Where(k => !pads.ContainsKey(k)).ToList()) lastLightbar.Remove(id);
        }
        return readings;
    }

    private static bool IsBluetooth(HidDevice device) =>
        device.GetMaxInputReportLength() >= 78;

    private static (int Level, ChargeState Charge)? ReadBattery(HidDevice device)
    {
        try
        {
            if (!device.TryOpen(out HidStream stream)) return null;
            using (stream)
            {
                stream.ReadTimeout = 400;
                if (IsBluetooth(device))
                {
                    // Ask for calibration once so the pad switches to full reports.
                    try
                    {
                        var feature = new byte[Math.Max(DualSense.CalibrationFeatureReportLength, device.GetMaxFeatureReportLength())];
                        feature[0] = DualSense.CalibrationFeatureReportId;
                        stream.GetFeature(feature);
                    }
                    catch (Exception ex) when (ex is IOException or TimeoutException)
                    {
                        // Already in full mode, or busy; the reads below will tell.
                    }
                }

                var buffer = new byte[device.GetMaxInputReportLength()];
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    int n = stream.Read(buffer, 0, buffer.Length);
                    if (DualSense.ParseBattery(buffer.AsSpan(0, n)) is { } battery) return battery;
                }
            }
        }
        catch (TimeoutException)
        {
            // An idle pad over Bluetooth can go quiet; try again next round.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            Log.Error($"Reading DualSense {device.DevicePath}", ex);
        }
        return null;
    }

    /// <summary>
    /// Colors each pad's lightbar for its battery level. Writes only when the color
    /// changes, so a game that sets its own color is not fought every few seconds.
    /// </summary>
    public void ApplyLightbar(Settings settings, IEnumerable<BatteryReading> readings)
    {
        if (!settings.LightbarEnabled)
        {
            lock (gate) lastLightbar.Clear();
            return;
        }
        var gradient = LightbarGradient.Default(settings.LightbarRgb);
        lock (gate)
        {
            foreach (var r in readings.Where(r => r.Source == BatterySource.DualSenseHid))
            {
                if (!pads.TryGetValue(r.Id, out var device)) continue;
                var color = gradient.ColorFor(r.Level);
                if (lastLightbar.TryGetValue(r.Id, out var last) && last == color) continue;
                if (WriteLightbar(device, color)) lastLightbar[r.Id] = color;
            }
        }
    }

    private bool WriteLightbar(HidDevice device, Rgb color)
    {
        try
        {
            if (!device.TryOpen(out HidStream stream)) return false;
            using (stream)
            {
                sequence = (byte)((sequence + 1) & 0x0F);
                var report = DualSense.LightbarReport(IsBluetooth(device), color, sequence, device.GetMaxOutputReportLength());
                stream.Write(report);
            }
            Log.Info($"Lightbar set to {color} on {device.DevicePath}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            Log.Error("Setting the DualSense lightbar", ex);
            return false;
        }
    }
}
