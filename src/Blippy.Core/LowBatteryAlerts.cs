namespace Blippy.Core;

/// <summary>
/// Decides when to nudge the person about a draining battery: once when a device drops
/// to 20% and again at 10%. Charging, or climbing back above a threshold, re-arms it.
/// </summary>
public sealed class LowBatteryAlerts
{
    public static readonly int[] Thresholds = [20, 10];

    // Lowest threshold already announced, per device.
    private readonly Dictionary<string, int> announced = new();

    /// <summary>Returns the threshold just crossed, or null when there is nothing new to say.</summary>
    public int? Check(BatteryReading reading)
    {
        if (reading.Charge is ChargeState.Charging or ChargeState.Full)
        {
            announced.Remove(reading.Id);
            return null;
        }

        int? crossed = null;
        foreach (var t in Thresholds)
            if (reading.Level <= t) crossed = t;

        if (crossed is null)
        {
            announced.Remove(reading.Id);
            return null;
        }

        if (announced.TryGetValue(reading.Id, out var last) && last <= crossed) return null;
        announced[reading.Id] = crossed.Value;
        return crossed;
    }

    /// <summary>Forgets devices that are gone, so they alert again when they come back low.</summary>
    public void Retain(IEnumerable<string> ids)
    {
        var keep = ids.ToHashSet();
        foreach (var id in announced.Keys.Where(k => !keep.Contains(k)).ToList()) announced.Remove(id);
    }
}
