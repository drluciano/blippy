namespace Blippy.Core;

public enum DeviceKind
{
    Other,
    DualSense,
    Xbox,
    Gamepad,
    Headphones,
    Headset,
    Earbuds,
    Mouse,
    Keyboard,
    Speaker,
    Watch,
}

public enum ChargeState
{
    Unknown,
    Discharging,
    Charging,
    Full,
}

/// <summary>Where a reading came from. Lower values are more specific and win when two sources see the same device.</summary>
public enum BatterySource
{
    DualSenseHid = 0,
    GamingInput = 1,
    WindowsBluetooth = 2,
}

/// <param name="Id">Stable per device for the life of the connection (a HID path, a PnP instance id, ...).</param>
/// <param name="Level">0 to 100.</param>
public sealed record BatteryReading(
    string Id,
    string Name,
    DeviceKind Kind,
    int Level,
    ChargeState Charge,
    BatterySource Source)
{
    public bool IsCharging => Charge is ChargeState.Charging;
}
