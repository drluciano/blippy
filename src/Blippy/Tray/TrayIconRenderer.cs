using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Blippy.Core;
using Blippy.Ui;
using Drawing = System.Drawing;

namespace Blippy.Tray;

/// <summary>
/// Draws the little tray pictures with WPF and turns them into Windows icons.
/// The caller owns the returned icon and must pass it to <see cref="Destroy"/>.
/// </summary>
internal static class TrayIconRenderer
{
    private static readonly Typeface Bold = new(new FontFamily("Segoe UI Variable Display, Segoe UI"),
        FontStyles.Normal, FontWeights.Black, FontStretches.Condensed);

    public static int Size
    {
        get
        {
            const int SM_CXSMICON = 49;
            var s = GetSystemMetrics(SM_CXSMICON);
            return s > 0 ? s : 16;
        }
    }

    /// <summary>Renders one device, or the "nothing yet" icon when <paramref name="reading"/> is null.</summary>
    public static Drawing.Icon Render(BatteryReading? reading, TrayStyle style)
    {
        int size = Size;
        return ToIcon(Draw(reading, style, size), size);
    }

    private static Brush Foreground => Theme.TaskbarDark ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x26, 0x25, 0x3A));

    private static void DrawIdle(DrawingContext dc, int size)
    {
        IconLibrary.Draw(dc, "bluetooth", Foreground, new Rect(0, 0, size, size), 2.2);
    }

    /// <summary>A rounded tile in the level's color with the percentage on it.</summary>
    private static void DrawNumber(DrawingContext dc, BatteryReading r, int size)
    {
        var color = LevelPalette.For(r.Level, r.Charge);
        var tile = new Rect(0, 0, size, size);
        dc.DrawRoundedRectangle(Theme.Solid(color), null, tile, size * 0.28, size * 0.28);

        var text = r.Level.ToString(CultureInfo.InvariantCulture);
        double em = size * (text.Length >= 3 ? 0.62 : 0.8);
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Bold, em, Brushes.White, 1.0);
        // Shrink to fit "100" inside the tile.
        double maxWidth = size * 0.94;
        if (formatted.Width > maxWidth)
        {
            formatted.SetFontSize(em * maxWidth / formatted.Width);
        }
        dc.DrawText(formatted, new Point((size - formatted.Width) / 2, (size - formatted.Height) / 2));

        if (r.IsCharging)
        {
            // A small dot in the corner: the tooltip and flyout say "Charging" in words.
            double d = size * 0.3;
            dc.DrawEllipse(Brushes.White, null, new Point(size - d / 2, d / 2), d / 2, d / 2);
        }
    }

    /// <summary>The device's own icon, with a battery bar underneath that drains.</summary>
    private static void DrawGlyph(DrawingContext dc, BatteryReading r, int size)
    {
        double barHeight = Math.Max(2, Math.Round(size * 0.18));
        double glyph = size - barHeight - 1;
        IconLibrary.Draw(dc, DeviceIcons.For(r.Kind), Foreground, new Rect((size - glyph) / 2, 0, glyph, glyph), 2.4);

        var track = new Rect(0, size - barHeight, size, barHeight);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(90, 128, 128, 128)), null, track, barHeight / 2, barHeight / 2);
        var fill = new Rect(0, size - barHeight, Math.Max(barHeight, size * r.Level / 100.0), barHeight);
        dc.DrawRoundedRectangle(Theme.Solid(LevelPalette.For(r.Level, r.Charge)), null, fill, barHeight / 2, barHeight / 2);
    }

    /// <summary>The same picture as a PNG, at any size (the preview renderer).</summary>
    public static byte[] RenderPng(BatteryReading? reading, TrayStyle style, int size)
    {
        return ToPng(Draw(reading, style, size), size);
    }

    private static DrawingVisual Draw(BatteryReading? reading, TrayStyle style, int size)
    {
        var visual = new DrawingVisual();
        using var dc = visual.RenderOpen();
        if (reading is null) DrawIdle(dc, size);
        else if (style == TrayStyle.Number) DrawNumber(dc, reading, size);
        else DrawGlyph(dc, reading, size);
        return visual;
    }

    private static byte[] ToPng(Visual visual, int size)
    {
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var png = new MemoryStream();
        encoder.Save(png);
        return png.ToArray();
    }

    private static Drawing.Icon ToIcon(Visual visual, int size)
    {
        using var png = new MemoryStream(ToPng(visual, size));
        using var gdi = new Drawing.Bitmap(png);
        return Drawing.Icon.FromHandle(gdi.GetHicon());
    }

    /// <summary>Frees an icon made by <see cref="Render"/>; Icon.FromHandle does not own its handle.</summary>
    public static void Destroy(Drawing.Icon? icon)
    {
        if (icon is null) return;
        var handle = icon.Handle;
        icon.Dispose();
        DestroyIcon(handle);
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
