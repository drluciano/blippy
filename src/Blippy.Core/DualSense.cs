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

    /// <summary>Feature report 0x09 (pairing info) carries the controller's Bluetooth address over USB and Bluetooth alike.</summary>
    public const byte PairingFeatureReportId = 0x09;
    public const int PairingFeatureReportLength = 20;

    /// <summary>
    /// The controller's Bluetooth address from a pairing info feature report, as twelve
    /// lowercase hex digits ("a0ab51c3d4e5"), or null if the report is not one.
    /// </summary>
    /// <remarks>The address is stored least significant byte first, from byte 1.</remarks>
    public static string? MacFromPairingReport(ReadOnlySpan<byte> report)
    {
        if (report.Length < 7 || report[0] != PairingFeatureReportId) return null;
        var mac = report.Slice(1, 6);
        if (mac.IndexOfAnyExcept((byte)0) < 0) return null;
        Span<byte> ordered = stackalloc byte[6];
        for (int i = 0; i < 6; i++) ordered[i] = mac[5 - i];
        return Convert.ToHexStringLower(ordered);
    }

    /// <summary>
    /// The address from a HID serial number, which Windows sets to the Bluetooth address
    /// for Bluetooth HID devices ("a0ab51c3d4e5", sometimes with colons or dashes).
    /// </summary>
    public static string? MacFromSerial(string? serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return null;
        var hex = new string(serial.Where(Uri.IsHexDigit).Select(char.ToLowerInvariant).ToArray());
        return hex.Length == 12 && hex.Any(c => c != '0') ? hex : null;
    }

    /// <summary>One sighting of a controller on one connection.</summary>
    public sealed record Sighting(string? Mac, bool Bluetooth, BatteryReading Reading);

    /// <summary>
    /// A controller plugged in over USB while it is still paired over Bluetooth shows up
    /// as two HID devices. Folds sightings with the same address into one reading, keyed
    /// by the address so it stays the same device whichever cable or radio it uses.
    /// </summary>
    /// <remarks>
    /// The USB reading wins: it is the connection that knows about the charger. Sightings
    /// whose address could not be read stay separate rather than being guessed together.
    /// </remarks>
    public static IReadOnlyList<(BatteryReading Reading, IReadOnlyList<Sighting> Connections)> Combine(IEnumerable<Sighting> sightings)
    {
        var result = new List<(BatteryReading, IReadOnlyList<Sighting>)>();
        foreach (var group in sightings.GroupBy(s => s.Mac ?? "path:" + s.Reading.Id))
        {
            var all = group.OrderBy(s => s.Bluetooth).ToList();
            var best = all[0].Reading;
            if (all[0].Mac is { } mac) best = best with { Id = "dualsense:" + mac };
            // Charging shows on whichever link reports it.
            if (all.Any(s => s.Reading.IsCharging) && best.Charge != ChargeState.Full)
                best = best with { Charge = ChargeState.Charging };
            result.Add((best, all));
        }
        return result;
    }

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
