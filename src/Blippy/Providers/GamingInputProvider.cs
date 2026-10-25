using Blippy.Core;
using Windows.Gaming.Input;
using Windows.System.Power;

namespace Blippy.Providers;

/// <summary>
/// Controllers that report a battery through Windows.Gaming.Input: Xbox controllers
/// over Bluetooth, the Xbox Wireless Adapter or USB, and some third-party pads.
/// </summary>
internal sealed class GamingInputProvider : IBatteryProvider
{
    private const ushort MicrosoftVendorId = 0x045E;

    public GamingInputProvider()
    {
        // The controller list fills in asynchronously once something subscribes, so
        // subscribe at start instead of on the first read.
        RawGameController.RawGameControllerAdded += (_, c) => Log.Info($"Controller connected: {c.DisplayName}");
        RawGameController.RawGameControllerRemoved += (_, c) => Log.Info($"Controller disconnected: {c.DisplayName}");
    }

    public string Name => "Windows.Gaming.Input";

    public Task<IReadOnlyList<BatteryReading>> ReadAsync(CancellationToken cancel)
    {
        var readings = new List<BatteryReading>();
        foreach (var c in RawGameController.RawGameControllers)
        {
            // The DualSense has its own, more accurate, provider.
            if (DualSense.IsDualSense(c.HardwareVendorId, c.HardwareProductId)) continue;

            var report = c.TryGetBatteryReport();
            if (report is null || report.Status == BatteryStatus.NotPresent) continue;
            if (report.FullChargeCapacityInMilliwattHours is not int full || full <= 0 || report.RemainingCapacityInMilliwattHours is not int left)
                continue;

            int level = Math.Clamp((int)Math.Round(left * 100.0 / full), 0, 100);
            var charge = report.Status switch
            {
                BatteryStatus.Charging => ChargeState.Charging,
                BatteryStatus.Idle when level >= 100 => ChargeState.Full,
                BatteryStatus.Discharging => ChargeState.Discharging,
                _ => ChargeState.Unknown,
            };
            var name = string.IsNullOrWhiteSpace(c.DisplayName) ? "Controller" : c.DisplayName;
            var kind = c.HardwareVendorId == MicrosoftVendorId ? DeviceKind.Xbox : DeviceClassifier.FromName(name);
            if (kind == DeviceKind.Other) kind = DeviceKind.Gamepad;
            readings.Add(new BatteryReading(c.NonRoamableId, name, kind, level, charge, BatterySource.GamingInput));
        }
        return Task.FromResult<IReadOnlyList<BatteryReading>>(readings);
    }
}
