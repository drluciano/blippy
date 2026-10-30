using System.Xml.Linq;

namespace Blippy.Core;

/// <summary>
/// The parts of a stroke icon (Tabler style: 24×24, round 2px strokes) the app draws.
/// Only path elements are read; Tabler's invisible bounding-box path (stroke="none") is skipped.
/// </summary>
public sealed record SvgIcon(double Width, double Height, double StrokeWidth, IReadOnlyList<string> Paths)
{
    public static SvgIcon Parse(string svg)
    {
        var root = XDocument.Parse(svg).Root ?? throw new FormatException("Empty SVG");
        double w = 24, h = 24;
        var viewBox = (string?)root.Attribute("viewBox");
        if (viewBox is not null)
        {
            var parts = viewBox.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4)
            {
                w = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                h = double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        double stroke = double.TryParse((string?)root.Attribute("stroke-width"),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sw) ? sw : 2;

        var paths = root.Descendants()
            .Where(e => e.Name.LocalName == "path")
            .Where(e => (string?)e.Attribute("stroke") != "none")
            .Select(e => (string?)e.Attribute("d"))
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d!)
            .ToList();
        return new SvgIcon(w, h, stroke, paths);
    }
}
