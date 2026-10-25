namespace Blippy.Core;

/// <summary>
/// Combines readings from every source into one list with each device once.
/// </summary>
/// <remarks>
/// An Xbox controller shows up both through Windows.Gaming.Input and as a Bluetooth
/// device with a battery property. Readings with the same name from different sources
/// are paired off one to one, keeping the more specific source, so two identical
/// controllers still appear twice.
/// </remarks>
public static class ReadingMerger
{
    public static IReadOnlyList<BatteryReading> Merge(IEnumerable<BatteryReading> readings)
    {
        var kept = new List<BatteryReading>();
        // (kept reading, source that already matched it), so each kept reading
        // absorbs at most one twin from each other source.
        var claimed = new HashSet<(BatteryReading, BatterySource)>();
        foreach (var r in readings.OrderBy(r => r.Source))
        {
            var twin = kept.FirstOrDefault(k =>
                k.Source != r.Source && Same(k.Name, r.Name) && !claimed.Contains((k, r.Source)));
            if (twin is not null)
            {
                claimed.Add((twin, r.Source));
                continue;
            }
            kept.Add(r);
        }
        return kept
            .OrderBy(r => KindOrder(r.Kind))
            .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static bool Same(string a, string b) =>
        string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);

    private static string Normalize(string s) =>
        new(DeviceClassifier.CleanName(s).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>Controllers first, since they are what people check most.</summary>
    private static int KindOrder(DeviceKind k) => k switch
    {
        DeviceKind.DualSense or DeviceKind.Xbox or DeviceKind.Gamepad => 0,
        DeviceKind.Headset or DeviceKind.Headphones or DeviceKind.Earbuds => 1,
        DeviceKind.Mouse or DeviceKind.Keyboard => 2,
        _ => 3,
    };
}
