namespace Blippy.Core;

/// <summary>
/// Maps a battery level to a DualSense lightbar color: the person's chosen color
/// when the controller is full, sliding toward red as it drains.
/// </summary>
/// <remarks>
/// The blend runs through hue rather than straight RGB, so green to red passes
/// through yellow and blue to red passes through purple instead of a muddy brown.
/// Hue takes the shorter way round the color wheel.
/// </remarks>
public sealed record LightbarGradient(Rgb Full, Rgb Empty, int EmptyAt = 10, int FullAt = 100)
{
    public static LightbarGradient Default(Rgb full) => new(full, Rgb.Red);

    /// <summary>How far along the gradient a level sits: 0 = empty color, 1 = full color.</summary>
    public double Position(int level)
    {
        if (FullAt <= EmptyAt) return level >= FullAt ? 1 : 0;
        return Math.Clamp((level - EmptyAt) / (double)(FullAt - EmptyAt), 0, 1);
    }

    public Rgb ColorFor(int level) => Blend(Empty, Full, Position(level));

    /// <summary>Blends from <paramref name="a"/> (t = 0) to <paramref name="b"/> (t = 1) through hue.</summary>
    public static Rgb Blend(Rgb a, Rgb b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        if (t == 0) return a;
        if (t == 1) return b;
        var (h1, s1, v1) = a.ToHsv();
        var (h2, s2, v2) = b.ToHsv();
        // A gray has no meaningful hue; borrow the other end's so the blend
        // only changes saturation instead of sweeping through the rainbow.
        if (s1 == 0) h1 = h2;
        if (s2 == 0) h2 = h1;
        double dh = h2 - h1;
        if (dh > 180) dh -= 360;
        if (dh < -180) dh += 360;
        return Rgb.FromHsv(h1 + dh * t, s1 + (s2 - s1) * t, v1 + (v2 - v1) * t);
    }
}
