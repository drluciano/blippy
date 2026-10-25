using Blippy.Core;

namespace Blippy.Core.Tests;

public class LowBatteryAlertTests
{
    private static BatteryReading At(int level, ChargeState charge = ChargeState.Discharging) =>
        new("pad", "DualSense", DeviceKind.DualSense, level, charge, BatterySource.DualSenseHid);

    [Fact]
    public void Alerts_once_at_twenty_and_once_at_ten()
    {
        var a = new LowBatteryAlerts();
        Assert.Null(a.Check(At(45)));
        Assert.Equal(20, a.Check(At(15)));
        Assert.Null(a.Check(At(15)));
        Assert.Null(a.Check(At(12)));
        Assert.Equal(10, a.Check(At(5)));
        Assert.Null(a.Check(At(5)));
    }

    [Fact]
    public void A_device_already_at_five_gets_one_alert_not_two()
    {
        var a = new LowBatteryAlerts();
        Assert.Equal(10, a.Check(At(5)));
        Assert.Null(a.Check(At(5)));
    }

    [Fact]
    public void Charging_rearms_the_alerts()
    {
        var a = new LowBatteryAlerts();
        Assert.Equal(20, a.Check(At(15)));
        Assert.Null(a.Check(At(15, ChargeState.Charging)));
        Assert.Equal(20, a.Check(At(15)));
    }

    [Fact]
    public void Forgotten_devices_alert_again_when_they_return()
    {
        var a = new LowBatteryAlerts();
        Assert.Equal(20, a.Check(At(15)));
        a.Retain([]);
        Assert.Equal(20, a.Check(At(15)));
    }
}

public class SettingsTests
{
    [Fact]
    public void Round_trips_through_json()
    {
        var s = new Settings { LightbarEnabled = true, LightbarColor = "#12AB34", TrayStyle = TrayStyle.Glyph, RefreshSeconds = 60 };
        Assert.Equal(s, Settings.FromJson(s.ToJson()));
        Assert.Contains("\"trayStyle\": \"glyph\"", s.ToJson());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ not json")]
    public void Unreadable_files_mean_defaults(string? json)
    {
        Assert.Equal(new Settings(), Settings.FromJson(json));
    }

    [Fact]
    public void Out_of_range_values_are_clamped()
    {
        var s = Settings.FromJson("""{ "refreshSeconds": 0, "lightbarColor": "nope" }""");
        Assert.Equal(5, s.RefreshSeconds);
        Assert.Equal("#6E64E8", s.LightbarColor);
    }

    [Fact]
    public void Saves_atomically_and_loads_back()
    {
        var dir = Path.Combine(Path.GetTempPath(), "blippy-test-" + Guid.NewGuid());
        var path = Path.Combine(dir, "settings.json");
        try
        {
            var s = new Settings { StartWithWindows = true };
            s.Save(path);
            Assert.Equal(s, Settings.Load(path));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class SpringTests
{
    [Fact]
    public void Pop_overshoots_then_settles_at_one()
    {
        var s = Spring.Pop;
        var samples = Enumerable.Range(0, 300).Select(i => s.ValueAt(i / 100.0)).ToList();
        Assert.Equal(0, s.ValueAt(0));
        Assert.True(samples.Max() > 1.1, "a pop should visibly overshoot");
        Assert.Equal(1, s.ValueAt(s.SettleTime().TotalSeconds), 2);
    }

    [Fact]
    public void Snappy_settles_faster_than_bouncy()
    {
        Assert.True(Spring.Snappy.SettleTime() < Spring.Bouncy.SettleTime());
    }

    [Fact]
    public void Overdamped_springs_never_overshoot()
    {
        var s = new Spring(100, 100);
        Assert.All(Enumerable.Range(0, 200), i => Assert.True(s.ValueAt(i / 50.0) <= 1));
    }
}

public class LevelPaletteTests
{
    [Theory]
    [InlineData(90, ChargeState.Discharging, "Plenty left")]
    [InlineData(35, ChargeState.Discharging, "Getting low")]
    [InlineData(10, ChargeState.Discharging, "Charge soon")]
    [InlineData(10, ChargeState.Charging, "Charging")]
    [InlineData(100, ChargeState.Full, "Full")]
    public void Every_color_comes_with_a_word(int level, ChargeState charge, string word)
    {
        Assert.Equal(word, LevelPalette.Word(level, charge));
    }
}
