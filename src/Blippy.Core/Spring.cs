namespace Blippy.Core;

/// <summary>
/// A damped spring that settles from 0 to 1, used for the bouncy motion. The same
/// damping / stiffness / mass numbers as flippy's Reanimated springs give the same feel.
/// </summary>
public readonly record struct Spring(double Damping, double Stiffness, double Mass = 1)
{
    /// <summary>Big, playful overshoot for things popping into place.</summary>
    public static readonly Spring Pop = new(7, 260, 0.8);
    /// <summary>The default for movement and growth.</summary>
    public static readonly Spring Bouncy = new(10, 180, 0.9);
    /// <summary>Bouncy but quick to rest, for things that move often.</summary>
    public static readonly Spring Snappy = new(13, 320, 0.7);

    private double Omega => Math.Sqrt(Stiffness / Mass);
    private double Zeta => Damping / (2 * Math.Sqrt(Stiffness * Mass));

    /// <summary>Position at <paramref name="seconds"/> after release; overshoots past 1 when underdamped.</summary>
    public double ValueAt(double seconds)
    {
        if (seconds <= 0) return 0;
        double w = Omega, z = Zeta;
        if (z < 1)
        {
            double wd = w * Math.Sqrt(1 - z * z);
            return 1 - Math.Exp(-z * w * seconds) * (Math.Cos(wd * seconds) + z * w / wd * Math.Sin(wd * seconds));
        }
        // Critically or over damped: no overshoot.
        return 1 - Math.Exp(-w * seconds) * (1 + w * seconds);
    }

    /// <summary>How long until the spring stays within <paramref name="tolerance"/> of rest.</summary>
    public TimeSpan SettleTime(double tolerance = 0.005)
    {
        double envelope = Zeta < 1 ? Zeta * Omega : Omega;
        double seconds = Math.Log(1 / tolerance) / envelope;
        return TimeSpan.FromSeconds(Math.Clamp(seconds, 0.1, 3));
    }
}
