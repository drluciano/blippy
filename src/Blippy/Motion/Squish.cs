using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Blippy.Core;

namespace Blippy.Motion;

/// <summary>
/// Attached behaviour: <c>m:Squish.Enabled="True"</c> makes an element grow a touch on
/// hover, squish when pressed and spring back with an overshoot on release.
/// </summary>
public static class Squish
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(Squish), new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject d, bool value) => d.SetValue(EnabledProperty, value);

    public static readonly DependencyProperty HoverScaleProperty = DependencyProperty.RegisterAttached(
        "HoverScale", typeof(double), typeof(Squish), new PropertyMetadata(1.04));

    public static double GetHoverScale(DependencyObject d) => (double)d.GetValue(HoverScaleProperty);
    public static void SetHoverScale(DependencyObject d, double value) => d.SetValue(HoverScaleProperty, value);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement el) return;
        if ((bool)e.NewValue)
        {
            el.MouseEnter += OnEnter;
            el.MouseLeave += OnLeave;
            el.PreviewMouseLeftButtonDown += OnDown;
            el.PreviewMouseLeftButtonUp += OnUp;
        }
        else
        {
            el.MouseEnter -= OnEnter;
            el.MouseLeave -= OnLeave;
            el.PreviewMouseLeftButtonDown -= OnDown;
            el.PreviewMouseLeftButtonUp -= OnUp;
        }
    }

    private static void ScaleTo(object sender, double to, Spring spring)
    {
        var el = (UIElement)sender;
        var (scale, _) = Animate.Transforms(el);
        Animate.To(scale, ScaleTransform.ScaleXProperty, to, spring);
        Animate.To(scale, ScaleTransform.ScaleYProperty, to, spring);
    }

    private static void OnEnter(object sender, MouseEventArgs e) => ScaleTo(sender, GetHoverScale((DependencyObject)sender), Spring.Bouncy);
    private static void OnLeave(object sender, MouseEventArgs e) => ScaleTo(sender, 1, Spring.Bouncy);
    private static void OnDown(object sender, MouseButtonEventArgs e) => ScaleTo(sender, 0.9, Spring.Snappy);
    private static void OnUp(object sender, MouseButtonEventArgs e) =>
        ScaleTo(sender, ((UIElement)sender).IsMouseOver ? GetHoverScale((DependencyObject)sender) : 1, Spring.Pop);
}
