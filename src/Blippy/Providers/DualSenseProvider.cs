using System.IO;
using Blippy.Core;
using HidSharp;

namespace Blippy.Providers;

/// <summary>
/// Talks to DualSense controllers directly over HID: reads the battery from their input
/// report and, when the person turns it on, paints the battery level onto the lightbar.
/// </summary>
internal sealed class DualSenseProvider : IBatteryProvider
{
    private sealed record Paint(Rgb Color, string Connections);

    // Each controller (by address) and the HID devices it is reachable through right now.
    private readonly Dictionary<string, IReadOnlyList<HidDevice>> pads = new();
    private readonly Dictionary<string, Paint> painted = new();
    // Device path → Bluetooth address; a path only lives as long as its connection.
    private readonly Dictionary<string, string?> macs = new();
    private readonly object gate = new();
    private byte sequence;

    public string Name => "DualSense";

    public Task<IReadOnlyList<BatteryReading>> ReadAsync(CancellationToken cancel) =>
        Task.Run<IReadOnlyList<BatteryReading>>(() => Read(cancel), cancel);

    /// <summary>
    /// Forgets which colors were painted, so the next round paints every pad again. Called
    /// when devices come and go: plugging a pad in or out over USB resets its lightbar.
    /// </summary>
    public void ForceRepaint()
    {
        lock (gate) painted.Clear();
    }

    private List<BatteryReading> Read(CancellationToken cancel)
    {
        var found = DeviceList.Local.GetHidDevices(DualSense.SonyVendorId)
            .Where(d => DualSense.IsDualSense(d.VendorID, d.ProductID))
            .ToList();

        var sightings = new List<DualSense.Sighting>();
        var devicesByPath = new Dictionary<string, HidDevice>();
        lock (gate)
        {
            foreach (var path in macs.Keys.Except(found.Select(d => d.DevicePath)).ToList()) macs.Remove(path);

            foreach (var device in found)
            {
                cancel.ThrowIfCancellationRequested();
                devicesByPath[device.DevicePath] = device;
                if (!macs.TryGetValue(device.DevicePath, out var mac))
                    macs[device.DevicePath] = mac = ReadMac(device);
                if (ReadBattery(device) is not { } battery) continue;
                var name = device.ProductID == DualSense.DualSenseEdgeProductId
                    ? "DualSense Edge"
                    : "DualSense Wireless Controller";
                sightings.Add(new DualSense.Sighting(mac, IsBluetooth(device), new BatteryReading(
                    device.DevicePath, name, DeviceKind.DualSense, battery.Level, battery.Charge, BatterySource.DualSenseHid)));
            }

            var combined = DualSense.Combine(sightings);
            pads.Clear();
            foreach (var (reading, connections) in combined)
                pads[reading.Id] = connections.Select(c => devicesByPath[c.Reading.Id]).ToList();
            // Forget pads that are gone, so one that comes back gets painted again.
            foreach (var id in painted.Keys.Except(pads.Keys).ToList()) painted.Remove(id);
            return combined.Select(c => c.Reading).ToList();
        }
    }

    private static bool IsBluetooth(HidDevice device) =>
        device.GetMaxInputReportLength() >= 78;

    /// <summary>The pad's Bluetooth address, so the same pad on USB and Bluetooth is recognised as one.</summary>
    private static string? ReadMac(HidDevice device)
    {
        try
        {
            if (device.TryOpen(out HidStream stream))
            {
                using (stream)
                {
                    var report = new byte[Math.Max(DualSense.PairingFeatureReportLength, device.GetMaxFeatureReportLength())];
                    report[0] = DualSense.PairingFeatureReportId;
                    stream.GetFeature(report);
                    if (DualSense.MacFromPairingReport(report) is { } mac) return mac;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            Log.Error($"Reading DualSense pairing info {device.DevicePath}", ex);
        }

        try
        {
            // Over Bluetooth Windows also puts the address in the serial number.
            return DualSense.MacFromSerial(device.GetSerialNumber());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

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
    /// Colors each pad's lightbar for its battery level, on every connection it has.
    /// Writes only when the color or the pad's connections change, so a game that sets
    /// its own color is not fought every few seconds.
    /// </summary>
    public void ApplyLightbar(Settings settings, IEnumerable<BatteryReading> readings)
    {
        if (!settings.LightbarEnabled)
        {
            lock (gate) painted.Clear();
            return;
        }
        var gradient = LightbarGradient.Default(settings.LightbarRgb);
        lock (gate)
        {
            foreach (var r in readings.Where(r => r.Source == BatterySource.DualSenseHid))
            {
                if (!pads.TryGetValue(r.Id, out var connections)) continue;
                var paint = new Paint(gradient.ColorFor(r.Level), string.Join("|", connections.Select(d => d.DevicePath).Order()));
                if (painted.TryGetValue(r.Id, out var last) && last == paint) continue;
                // Paint every link: the pad shows whichever one it last heard from.
                var wrote = false;
                foreach (var device in connections) wrote |= WriteLightbar(device, paint.Color);
                if (wrote) painted[r.Id] = paint;
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
