using System.Windows;
using System.Windows.Media;

namespace Blippy.Ui;

/// <summary>Draws one of the bundled SVG icons: <c>&lt;ui:Icon Glyph="bolt" Stroke="{DynamicResource Primary}" Width="16" Height="16"/&gt;</c>.</summary>
public sealed class Icon : FrameworkElement
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(Icon), new FrameworkPropertyMetadata("bluetooth", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Icon), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeWidthProperty = DependencyProperty.Register(
        nameof(StrokeWidth), typeof(double), typeof(Icon), new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Glyph { get => (string)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    /// <summary>In the icon's own 24-unit grid, so it scales with the icon.</summary>
    public double StrokeWidth { get => (double)GetValue(StrokeWidthProperty); set => SetValue(StrokeWidthProperty, value); }

    public Icon()
    {
        IsHitTestVisible = false;
        SnapsToDevicePixels = false;
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsNaN(Width) ? 24 : Width, double.IsNaN(Height) ? 24 : Height);

    protected override void OnRender(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(Glyph)) return;
        IconLibrary.Draw(dc, Glyph, Stroke, new Rect(RenderSize), StrokeWidth);
    }
}
