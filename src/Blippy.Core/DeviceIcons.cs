namespace Blippy.Core;

/// <summary>Which bundled SVG (in assets/icons) stands for each kind of device.</summary>
public static class DeviceIcons
{
    public static string For(DeviceKind kind) => kind switch
    {
        DeviceKind.DualSense or DeviceKind.Xbox or DeviceKind.Gamepad => "device-gamepad-2",
        DeviceKind.Headphones => "headphones",
        DeviceKind.Headset => "headset",
        DeviceKind.Earbuds => "earphone-bluetooth",
        DeviceKind.Mouse => "mouse",
        DeviceKind.Keyboard => "keyboard",
        DeviceKind.Speaker => "device-speaker",
        DeviceKind.Watch => "device-watch",
        _ => "bluetooth",
    };

    /// <summary>A small brand mark drawn in the corner of controller icons, or null.</summary>
    public static string? BadgeFor(DeviceKind kind) => kind switch
    {
        DeviceKind.DualSense => "playstation-triangle",
        DeviceKind.Xbox => "brand-xbox",
        _ => null,
    };

    public static readonly string[] All =
    [
        "device-gamepad-2", "headphones", "headset", "earphone-bluetooth", "mouse", "keyboard",
        "device-speaker", "device-watch", "bluetooth", "playstation-triangle", "brand-xbox",
        "bolt", "settings", "refresh", "x",
    ];
}
