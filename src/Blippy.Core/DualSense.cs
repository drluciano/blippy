namespace Blippy.Core;

/// <summary>
/// Reads battery state from, and builds lightbar reports for, Sony's DualSense
/// (PS5) controller over USB or Bluetooth.
/// </summary>
/// <remarks>
/// Layouts follow the Linux kernel driver (drivers/hid/hid-playstation.c).
/// Every buffer here starts with the HID report id, as Windows hands them over.
/// </remarks>
public static class DualSense
{
    public const int SonyVendorId = 0x054C;
    public const int DualSenseProductId = 0x0CE6;
    public const int DualSenseEdgeProductId = 0x0DF2;

    public static bool IsDualSense(int vendorId, int productId) =>
        vendorId == SonyVendorId && productId is DualSenseProductId or DualSenseEdgeProductId;

    public const byte UsbInputReportId = 0x01;
    public const byte BluetoothInputReportId = 0x31;
    public const byte UsbOutputReportId = 0x02;
    public const byte BluetoothOutputReportId = 0x31;
    public const int BluetoothOutputReportLength = 78;
    public const int UsbOutputReportLength = 48;

    /// <summary>
    /// Feature report 0x05 (calibration). Reading it once over Bluetooth switches the
    /// controller from its short "simple" input report to the full 0x31 report, which
    /// is the one that carries battery state.
    /// </summary>
    public const byte CalibrationFeatureReportId = 0x05;
    public const int CalibrationFeatureReportLength = 41;

    // Offset of the status byte inside the common input report body.
    private const int StatusOffsetInBody = 52;

    /// <summary>
    /// Pulls the battery level out of an input report. Returns null for reports that do
    /// not carry it (the Bluetooth simple report, anything too short, or a battery error).
    /// </summary>
    public static (int Level, ChargeState Charge)? ParseBattery(ReadOnlySpan<byte> report)
    {
        if (report.Length == 0) return null;
        int bodyStart = report[0] switch
        {
            UsbInputReportId when report.Length >= 64 => 1,
            BluetoothInputReportId when report.Length >= 78 => 2, // report id, then a sequence byte
            _ => -1,
        };
        if (bodyStart < 0) return null;

        byte status = report[bodyStart + StatusOffsetInBody];
        int capacity = status & 0x0F;
        int charging = (status & 0xF0) >> 4;
        int level = Math.Min(capacity * 10 + 5, 100);
        return charging switch
        {
            0x0 => (level, ChargeState.Discharging),
            0x1 => (level, ChargeState.Charging),
            0x2 => (100, ChargeState.Full),
            _ => null, // 0xA/0xB voltage or temperature error, 0xF charging error
        };
    }

    /// <summary>
    /// Builds an output report that sets only the lightbar color and leaves rumble,
    /// triggers and the player LEDs alone.
    /// </summary>
    /// <param name="sequence">0 to 15; the controller ignores repeats over Bluetooth, so bump it each send.</param>
    /// <param name="reportLength">The device's output report length; the buffer is padded to it.</param>
    public static byte[] LightbarReport(bool bluetooth, Rgb color, byte sequence = 0, int? reportLength = null)
    {
        int length = Math.Max(reportLength ?? 0, bluetooth ? BluetoothOutputReportLength : UsbOutputReportLength);
        var report = new byte[length];
        int common;
        if (bluetooth)
        {
            report[0] = BluetoothOutputReportId;
            report[1] = (byte)((sequence & 0x0F) << 4);
            report[2] = 0x10; // DS_OUTPUT_TAG
            common = 3;
        }
        else
        {
            report[0] = UsbOutputReportId;
            common = 1;
        }

        report[common + 1] = 0x04; // valid_flag1: lightbar control enable
        report[common + 44] = color.R;
        report[common + 45] = color.G;
        report[common + 46] = color.B;

        if (bluetooth)
        {
            // CRC over a 0xA2 seed byte ("output report" HID header) and everything but the CRC itself.
            int crcAt = BluetoothOutputReportLength - 4;
            uint crc = Crc32.Append(Crc32.Compute([0xA2]), report.AsSpan(0, crcAt));
            report[crcAt] = (byte)crc;
            report[crcAt + 1] = (byte)(crc >> 8);
            report[crcAt + 2] = (byte)(crc >> 16);
            report[crcAt + 3] = (byte)(crc >> 24);
        }
        return report;
    }
}
