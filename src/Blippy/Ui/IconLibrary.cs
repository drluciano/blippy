using System.IO;
using System.Windows;
using System.Windows.Media;
using Blippy.Core;

namespace Blippy.Ui;

/// <summary>The bundled Tabler SVG icons (MIT), turned into WPF geometry once and cached.</summary>
internal static class IconLibrary
{
    private static readonly Dictionary<string, (Geometry Geometry, SvgIcon Icon)> Cache = new();

    public static (Geometry Geometry, SvgIcon Icon) Load(string name)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(name, out var hit)) return hit;
            using var stream = typeof(IconLibrary).Assembly.GetManifestResourceStream($"Blippy.Icons.{name}.svg")
                ?? throw new FileNotFoundException($"Icon '{name}' is not bundled");
            using var reader = new StreamReader(stream);
            var icon = SvgIcon.Parse(reader.ReadToEnd());
            var group = new GeometryGroup();
            foreach (var d in icon.Paths) group.Children.Add(Geometry.Parse(d));
            group.Freeze();
            return Cache[name] = (group, icon);
        }
    }

    public static Pen Pen(Brush brush, double thickness) => new(brush, thickness)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round,
        LineJoin = PenLineJoin.Round,
    };

    /// <summary>Draws an icon into a drawing context, fitted into <paramref name="bounds"/>.</summary>
    public static void Draw(DrawingContext dc, string name, Brush brush, Rect bounds, double strokeWidth)
    {
        var (geometry, icon) = Load(name);
        dc.PushTransform(new TranslateTransform(bounds.X, bounds.Y));
        dc.PushTransform(new ScaleTransform(bounds.Width / icon.Width, bounds.Height / icon.Height));
        dc.DrawGeometry(null, Pen(brush, strokeWidth), geometry);
        dc.Pop();
        dc.Pop();
    }
}
