using Blippy.Core;
using Windows.Devices.Enumeration;

namespace Blippy.Providers;

/// <summary>
/// The battery level Windows itself shows in Settings › Bluetooth &amp; devices. Covers
/// most headphones, earbuds, mice, keyboards and Bluetooth LE controllers (including
/// Xbox Series controllers), but not the DualSense, which Windows never reads.
/// </summary>
internal sealed class WindowsBluetoothProvider : IBatteryProvider
{
    // DEVPKEY_Bluetooth_Battery: a byte, 0 to 100, on the device node that reports it.
    private const string BatteryKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    // DEVPKEY_Device_IsConnected (Bluetooth) and DEVPKEY_Device_IsPresent. Paired devices that
    // are switched off keep their last battery value, so these filter them out.
    private const string ConnectedKey = "{83DA6326-97A6-4088-9453-A1923F573B29} 15";
    private const string PresentKey = "{540B947E-8B40-45BC-A8A2-6A0B894CBDA2} 5";
    private const string ContainerKey = "System.Devices.ContainerId";

    private static readonly string[] Properties = [BatteryKey, ConnectedKey, PresentKey, ContainerKey];
    private static readonly string[] MinimalProperties = [BatteryKey, ContainerKey];

    // Set if Windows ever refuses the full property list, so later rounds skip straight to the minimal one.
    private bool minimal;

    public string Name => "Windows Bluetooth";

    public async Task<IReadOnlyList<BatteryReading>> ReadAsync(CancellationToken cancel)
    {
        DeviceInformationCollection devices;
        try
        {
            devices = await DeviceInformation
                .FindAllAsync(string.Empty, minimal ? MinimalProperties : Properties, DeviceInformationKind.Device)
                .AsTask(cancel);
        }
        catch (Exception ex) when (!minimal && ex is ArgumentException or System.Runtime.InteropServices.COMException)
        {
            Log.Error("Reading connection state; falling back to battery only", ex);
            minimal = true;
            devices = await DeviceInformation
                .FindAllAsync(string.Empty, MinimalProperties, DeviceInformationKind.Device)
                .AsTask(cancel);
        }

        // The battery often sits on a child node with a plumbing name ("HID device",
        // "Bluetooth Low Energy GATT compliant HID device"); the device's own node in the
        // same container has the name people know it by, like "Xbox Wireless Controller".
        var containerNames = devices
            .Where(d => d.Id.StartsWith("BTH", StringComparison.OrdinalIgnoreCase))
            .Where(d => d.Properties.TryGetValue(ContainerKey, out var c) && c is Guid)
            .GroupBy(d => (Guid)d.Properties[ContainerKey])
            .ToDictionary(g => g.Key, g => DeviceClassifier.BestName(g.OrderBy(NamePreference).Select(d => d.Name)));

        var readings = new List<BatteryReading>();
        // A headset has several device nodes (one per profile); report each physical device once.
        var seenContainers = new HashSet<Guid>();
        foreach (var d in devices)
        {
            if (!d.Id.StartsWith("BTH", StringComparison.OrdinalIgnoreCase)) continue;
            if (!d.Properties.TryGetValue(BatteryKey, out var raw) || raw is null) continue;
            if (IsFalse(d.Properties, ConnectedKey) || IsFalse(d.Properties, PresentKey)) continue;
            if (d.Properties.TryGetValue(ContainerKey, out var c) && c is Guid container && !seenContainers.Add(container))
                continue;

            int level;
            try
            {
                level = Math.Clamp(Convert.ToInt32(raw), 0, 100);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                continue;
            }

            var name = c is Guid g && containerNames.TryGetValue(g, out var best) && DeviceClassifier.IsGeneric(d.Name)
                ? best
                : DeviceClassifier.CleanName(d.Name);
            readings.Add(new BatteryReading(
                d.Id, name, DeviceClassifier.FromName(name), level, ChargeState.Unknown, BatterySource.WindowsBluetooth));
        }
        return readings;
    }

    /// <summary>The device's own node (BTHLE\DEV_…, BTHENUM\DEV_…) is named after it; prefer it.</summary>
    private static int NamePreference(DeviceInformation d) =>
        d.Id.Contains(@"\DEV_", StringComparison.OrdinalIgnoreCase) ? 0 : 1;

    private static bool IsFalse(IReadOnlyDictionary<string, object> props, string key) =>
        props.TryGetValue(key, out var v) && v is bool b && !b;
}
