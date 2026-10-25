using System.Globalization;

namespace Blippy.Core;

/// <summary>An 8-bit sRGB color.</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static readonly Rgb Red = new(0xFF, 0x00, 0x00);

    /// <summary>Parses "#RRGGBB" or "RRGGBB". Returns false for anything else.</summary>
    public static bool TryParse(string? text, out Rgb color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim().TrimStart('#');
        if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            return false;
        color = new Rgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }

    public static Rgb Parse(string text) =>
        TryParse(text, out var c) ? c : throw new FormatException($"Not a #RRGGBB color: '{text}'");

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public override string ToString() => ToHex();

    /// <summary>Hue in degrees [0, 360), saturation and value in [0, 1].</summary>
    public (double H, double S, double V) ToHsv()
    {
        double r = R / 255.0, g = G / 255.0, b = B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;
        double h = 0;
        if (delta > 0)
        {
            if (max == r) h = 60 * (((g - b) / delta) % 6);
            else if (max == g) h = 60 * (((b - r) / delta) + 2);
            else h = 60 * (((r - g) / delta) + 4);
        }
        if (h < 0) h += 360;
        double s = max == 0 ? 0 : delta / max;
        return (h, s, max);
    }

    public static Rgb FromHsv(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        v = Math.Clamp(v, 0, 1);
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        double m = v - c;
        (double r, double g, double b) = (int)(h / 60) switch
        {
            0 => (c, x, 0d),
            1 => (x, c, 0d),
            2 => (0d, c, x),
            3 => (0d, x, c),
            4 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        static byte To8(double f) => (byte)Math.Round(Math.Clamp(f, 0, 1) * 255);
        return new Rgb(To8(r + m), To8(g + m), To8(b + m));
    }
}
