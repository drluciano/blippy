namespace Blippy.Core;

/// <summary>Guesses what kind of device something is from the name Windows gives it.</summary>
public static class DeviceClassifier
{
    // Order matters: the first match wins, so specific words come before general ones
    // ("xbox" before "controller", "headset" before "head").
    private static readonly (string Word, DeviceKind Kind)[] Rules =
    [
        ("dualsense", DeviceKind.DualSense),
        ("ps5 controller", DeviceKind.DualSense),
        ("xbox", DeviceKind.Xbox),
        ("controller", DeviceKind.Gamepad),
        ("gamepad", DeviceKind.Gamepad),
        ("joy-con", DeviceKind.Gamepad),
        ("pro controller", DeviceKind.Gamepad),
        ("dualshock", DeviceKind.Gamepad),
        ("headset", DeviceKind.Headset),
        ("hands-free", DeviceKind.Headset),
        ("buds", DeviceKind.Earbuds),
        ("airpods", DeviceKind.Earbuds),
        ("earbud", DeviceKind.Earbuds),
        ("earphone", DeviceKind.Earbuds),
        ("wf-", DeviceKind.Earbuds),
        ("headphone", DeviceKind.Headphones),
        ("wh-", DeviceKind.Headphones),
        ("qc", DeviceKind.Headphones),
        ("mouse", DeviceKind.Mouse),
        ("mx master", DeviceKind.Mouse),
        ("mx anywhere", DeviceKind.Mouse),
        ("trackpad", DeviceKind.Mouse),
        ("keyboard", DeviceKind.Keyboard),
        ("keys", DeviceKind.Keyboard),
        ("speaker", DeviceKind.Speaker),
        ("soundlink", DeviceKind.Speaker),
        ("jbl flip", DeviceKind.Speaker),
        ("jbl charge", DeviceKind.Speaker),
        ("watch", DeviceKind.Watch),
    ];

    public static DeviceKind FromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return DeviceKind.Other;
        var n = name.ToLowerInvariant();
        foreach (var (word, kind) in Rules)
            if (n.Contains(word, StringComparison.Ordinal)) return kind;
        return DeviceKind.Other;
    }

    // Windows names the per-profile device nodes after the device plus the profile.
    private static readonly string[] ProfileSuffixes =
    [
        " Hands-Free AG Audio",
        " Hands-Free AG",
        " Hands-Free",
        " Avrcp Transport",
        " Stereo",
    ];

    /// <summary>Strips Bluetooth profile suffixes so "WH-1000XM4 Hands-Free AG" reads as "WH-1000XM4".</summary>
    public static string CleanName(string? name)
    {
        var n = (name ?? "").Trim();
        foreach (var suffix in ProfileSuffixes)
            if (n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && n.Length > suffix.Length)
                return n[..^suffix.Length].Trim();
        return n.Length == 0 ? "Bluetooth device" : n;
    }

    // Names Windows gives to the plumbing under a device rather than to the device itself.
    private static readonly string[] GenericWords =
    [
        "hid device", "hid-compliant", "hid compliant", "gatt", "bluetooth low energy",
        "bluetooth le ", "generic", "input device", "game controller",
    ];

    /// <summary>True for names like "HID device" or "Bluetooth Low Energy GATT compliant HID device".</summary>
    public static bool IsGeneric(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        var n = name.ToLowerInvariant() + " ";
        return GenericWords.Any(w => n.Contains(w, StringComparison.Ordinal));
    }

    /// <summary>The first name that is not generic plumbing, cleaned; else the first name cleaned.</summary>
    public static string BestName(IEnumerable<string?> candidates)
    {
        var list = candidates.ToList();
        var specific = list.FirstOrDefault(n => !IsGeneric(n));
        return CleanName(specific ?? list.FirstOrDefault());
    }
}
