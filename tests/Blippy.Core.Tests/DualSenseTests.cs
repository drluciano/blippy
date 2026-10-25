using Blippy.Core;

namespace Blippy.Core.Tests;

public class DualSenseTests
{
    private static byte[] UsbReport(byte status)
    {
        var r = new byte[64];
        r[0] = 0x01;
        r[1 + 52] = status;
        return r;
    }

    private static byte[] BluetoothReport(byte status)
    {
        var r = new byte[78];
        r[0] = 0x31;
        r[2 + 52] = status;
        return r;
    }

    [Theory]
    [InlineData(0x00, 5, ChargeState.Discharging)]
    [InlineData(0x06, 65, ChargeState.Discharging)]
    [InlineData(0x0A, 100, ChargeState.Discharging)]
    [InlineData(0x13, 35, ChargeState.Charging)]
    [InlineData(0x2A, 100, ChargeState.Full)]
    public void Reads_battery_over_usb_and_bluetooth(byte status, int level, ChargeState charge)
    {
        Assert.Equal((level, charge), DualSense.ParseBattery(UsbReport(status)));
        Assert.Equal((level, charge), DualSense.ParseBattery(BluetoothReport(status)));
    }

    [Theory]
    [InlineData(0xA5)]
    [InlineData(0xB5)]
    [InlineData(0xF5)]
    public void Battery_errors_are_not_a_level(byte status)
    {
        Assert.Null(DualSense.ParseBattery(UsbReport(status)));
    }

    [Fact]
    public void The_short_bluetooth_report_has_no_battery()
    {
        var simple = new byte[10];
        simple[0] = 0x01;
        Assert.Null(DualSense.ParseBattery(simple));
        Assert.Null(DualSense.ParseBattery([]));
    }

    [Fact]
    public void Usb_lightbar_report_sets_only_the_lightbar()
    {
        var r = DualSense.LightbarReport(bluetooth: false, new Rgb(1, 2, 3));
        Assert.Equal(48, r.Length);
        Assert.Equal(0x02, r[0]);
        Assert.Equal(0x00, r[1]);  // valid_flag0: no rumble or trigger changes
        Assert.Equal(0x04, r[2]);  // valid_flag1: lightbar
        Assert.Equal(new byte[] { 1, 2, 3 }, r[45..48]);
    }

    [Fact]
    public void Bluetooth_lightbar_report_has_header_color_and_crc()
    {
        var r = DualSense.LightbarReport(bluetooth: true, new Rgb(0xAA, 0xBB, 0xCC), sequence: 3);
        Assert.Equal(78, r.Length);
        Assert.Equal(0x31, r[0]);
        Assert.Equal(0x30, r[1]);
        Assert.Equal(0x10, r[2]);
        Assert.Equal(0x04, r[4]);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC }, r[47..50]);

        var expected = Crc32.Compute([0xA2, .. r[..74]]);
        Assert.Equal(expected, BitConverter.ToUInt32(r, 74));
    }

    [Fact]
    public void Lightbar_report_pads_to_the_device_length()
    {
        Assert.Equal(547, DualSense.LightbarReport(true, Rgb.Red, 0, 547).Length);
    }

    [Fact]
    public void Crc32_matches_the_standard_check_value()
    {
        Assert.Equal(0xCBF43926u, Crc32.Compute("123456789"u8));
    }

    [Theory]
    [InlineData(0x054C, 0x0CE6, true)]
    [InlineData(0x054C, 0x0DF2, true)]
    [InlineData(0x054C, 0x09CC, false)] // DualShock 4
    [InlineData(0x045E, 0x0B13, false)] // Xbox
    public void Recognises_dualsense(int vid, int pid, bool expected)
    {
        Assert.Equal(expected, DualSense.IsDualSense(vid, pid));
    }
}
