namespace Blippy.Core;

/// <summary>Colors for battery levels in the tray and the flyout, in the flippy palette.</summary>
public static class LevelPalette
{
    public static readonly Rgb Good = Rgb.Parse("#2F9E78");
    public static readonly Rgb Okay = Rgb.Parse("#D99A1E");
    public static readonly Rgb Low = Rgb.Parse("#E0603C");
    public static readonly Rgb Charging = Rgb.Parse("#6E64E8");

    public static Rgb For(int level, ChargeState charge = ChargeState.Discharging) => charge switch
    {
        ChargeState.Charging => Charging,
        _ when level > 50 => Good,
        _ when level > 20 => Okay,
        _ => Low,
    };

    /// <summary>A short word to go with the color, so state is never color alone.</summary>
    public static string Word(int level, ChargeState charge) => charge switch
    {
        ChargeState.Charging => "Charging",
        ChargeState.Full => "Full",
        _ when level > 50 => "Plenty left",
        _ when level > 20 => "Getting low",
        _ => "Charge soon",
    };
}
