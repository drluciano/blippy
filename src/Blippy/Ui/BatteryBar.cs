using System.Windows;
using System.Windows.Media;

namespace Blippy.Ui;

/// <summary>A rounded pill that fills from the left. <see cref="Progress"/> may overshoot a little while it springs.</summary>
public sealed class BatteryBar : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(BatteryBar),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((BatteryBar)d).ProgressChanged?.Invoke()));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(BatteryBar), new FrameworkPropertyMetadata(Brushes.MediumPurple, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track), typeof(Brush), typeof(BatteryBar), new FrameworkPropertyMetadata(Brushes.LightGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public Brush Track { get => (Brush)GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    public event Action? ProgressChanged;

    protected override Size MeasureOverride(Size availableSize) =>
        new(0, double.IsNaN(Height) ? 8 : Height);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight, r = h / 2;
        dc.DrawRoundedRectangle(Track, null, new Rect(0, 0, w, h), r, r);
        double fill = Math.Clamp(Progress, 0, 1.04) * w;
        if (fill <= 0.5) return;
        // Never thinner than a dot, so 3% still reads as "some".
        fill = Math.Max(fill, h);
        dc.DrawRoundedRectangle(Fill, null, new Rect(0, 0, Math.Min(fill, w + 2), h), r, r);
    }
}
