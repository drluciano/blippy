using Blippy.Core;

namespace Blippy.Core.Tests;

public class DevicesTests
{
    [Theory]
    [InlineData("DualSense Wireless Controller", DeviceKind.DualSense)]
    [InlineData("Xbox Wireless Controller", DeviceKind.Xbox)]
    [InlineData("Pro Controller", DeviceKind.Gamepad)]
    [InlineData("WH-1000XM4", DeviceKind.Headphones)]
    [InlineData("Galaxy Buds2 Pro", DeviceKind.Earbuds)]
    [InlineData("AirPods Pro", DeviceKind.Earbuds)]
    [InlineData("Arctis Nova 7 Headset", DeviceKind.Headset)]
    [InlineData("MX Master 3S", DeviceKind.Mouse)]
    [InlineData("Logitech MX Keys", DeviceKind.Keyboard)]
    [InlineData("JBL Flip 6", DeviceKind.Speaker)]
    [InlineData("Something", DeviceKind.Other)]
    [InlineData("", DeviceKind.Other)]
    public void Classifies_by_name(string name, DeviceKind kind)
    {
        Assert.Equal(kind, DeviceClassifier.FromName(name));
    }

    [Theory]
    [InlineData("WH-1000XM4 Hands-Free AG", "WH-1000XM4")]
    [InlineData("Jabra Elite Avrcp Transport", "Jabra Elite")]
    [InlineData("MX Master 3S", "MX Master 3S")]
    [InlineData("  ", "Bluetooth device")]
    public void Cleans_profile_suffixes(string raw, string clean)
    {
        Assert.Equal(clean, DeviceClassifier.CleanName(raw));
    }

    private static BatteryReading R(string id, string name, BatterySource source, int level = 50) =>
        new(id, name, DeviceClassifier.FromName(name), level, ChargeState.Discharging, source);

    [Fact]
    public void The_same_device_from_two_sources_appears_once_with_the_better_source()
    {
        var merged = ReadingMerger.Merge(
        [
            R("pnp", "Xbox Wireless Controller", BatterySource.WindowsBluetooth, 40),
            R("gi", "Xbox Wireless Controller", BatterySource.GamingInput, 45),
        ]);
        var only = Assert.Single(merged);
        Assert.Equal("gi", only.Id);
    }

    [Fact]
    public void Two_identical_controllers_both_stay()
    {
        var merged = ReadingMerger.Merge(
        [
            R("gi1", "Xbox Wireless Controller", BatterySource.GamingInput),
            R("gi2", "Xbox Wireless Controller", BatterySource.GamingInput),
            R("pnp1", "Xbox Wireless Controller", BatterySource.WindowsBluetooth),
            R("pnp2", "Xbox Wireless Controller", BatterySource.WindowsBluetooth),
        ]);
        Assert.Equal(["gi1", "gi2"], merged.Select(m => m.Id).Order());
    }

    [Fact]
    public void Merging_ignores_profile_suffixes_and_case()
    {
        var merged = ReadingMerger.Merge(
        [
            R("a", "wh-1000xm4", BatterySource.GamingInput),
            R("b", "WH-1000XM4 Hands-Free AG", BatterySource.WindowsBluetooth),
        ]);
        Assert.Single(merged);
    }

    [Fact]
    public void Controllers_are_listed_first()
    {
        var merged = ReadingMerger.Merge(
        [
            R("m", "MX Master 3S", BatterySource.WindowsBluetooth),
            R("h", "WH-1000XM4", BatterySource.WindowsBluetooth),
            R("d", "DualSense Wireless Controller", BatterySource.DualSenseHid),
        ]);
        Assert.Equal(["d", "h", "m"], merged.Select(m => m.Id));
    }

    [Fact]
    public void Every_kind_has_a_bundled_icon()
    {
        foreach (var kind in Enum.GetValues<DeviceKind>())
        {
            Assert.Contains(DeviceIcons.For(kind), DeviceIcons.All);
            var badge = DeviceIcons.BadgeFor(kind);
            if (badge is not null) Assert.Contains(badge, DeviceIcons.All);
        }
    }

    [Fact]
    public void Every_bundled_icon_parses()
    {
        foreach (var name in DeviceIcons.All)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Icons", name + ".svg");
            var icon = SvgIcon.Parse(File.ReadAllText(path));
            Assert.Equal(24, icon.Width);
            Assert.Equal(2, icon.StrokeWidth);
            Assert.NotEmpty(icon.Paths);
            Assert.DoesNotContain(icon.Paths, p => p == "M0 0h24v24H0z");
        }
    }
}

public class GenericNameTests
{
    [Theory]
    [InlineData("HID device", true)]
    [InlineData("HID-compliant game controller", true)]
    [InlineData("Bluetooth Low Energy GATT compliant HID device", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("Xbox Wireless Controller", false)]
    [InlineData("WH-1000XM4 Hands-Free AG", false)]
    [InlineData("MX Master 3S", false)]
    public void Spots_plumbing_names(string? name, bool generic)
    {
        Assert.Equal(generic, DeviceClassifier.IsGeneric(name));
    }

    [Fact]
    public void Prefers_the_real_name_from_the_same_device()
    {
        Assert.Equal("Xbox Wireless Controller",
            DeviceClassifier.BestName(["Bluetooth Low Energy GATT compliant HID device", "Xbox Wireless Controller"]));
        Assert.Equal("HID device", DeviceClassifier.BestName(["HID device"]));
        Assert.Equal("Bluetooth device", DeviceClassifier.BestName([]));
    }

    private static BatteryReading R(string id, string name) =>
        new(id, name, DeviceClassifier.FromName(name), 50, ChargeState.Discharging, BatterySource.GamingInput);

    [Fact]
    public void A_generic_twin_of_a_named_controller_is_dropped()
    {
        var kept = ReadingMerger.DropGenericTwins(
        [
            (0x045E, 0x0B13, R("a", "Xbox Wireless Controller")),
            (0x045E, 0x0B13, R("b", "HID device")),
        ]);
        Assert.Equal(["a"], kept.Select(k => k.Id));
    }

    [Fact]
    public void Two_named_controllers_of_one_model_both_stay()
    {
        var kept = ReadingMerger.DropGenericTwins(
        [
            (0x045E, 0x0B13, R("a", "Xbox Wireless Controller")),
            (0x045E, 0x0B13, R("b", "Xbox Wireless Controller")),
        ]);
        Assert.Equal(2, kept.Count);
    }

    [Fact]
    public void A_generic_controller_with_no_named_twin_stays()
    {
        var kept = ReadingMerger.DropGenericTwins(
        [
            (0x045E, 0x0B13, R("a", "Xbox Wireless Controller")),
            (0x2DC8, 0x6001, R("b", "HID device")),
        ]);
        Assert.Equal(2, kept.Count);
    }
}
