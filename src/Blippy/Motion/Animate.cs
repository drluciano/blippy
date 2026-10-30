using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Blippy.Core;

namespace Blippy.Motion;

/// <summary>Code-side animation helpers shared by the flyout's pieces.</summary>
public static class Animate
{
    public static Duration For(Spring spring) =>
        MotionPrefs.Reduced ? new Duration(TimeSpan.FromMilliseconds(1)) : new Duration(spring.SettleTime());

    /// <summary>Animates a dependency property to <paramref name="to"/> on a spring.</summary>
    public static void To(IAnimatable target, DependencyProperty property, double to, Spring spring, TimeSpan delay = default, double? from = null)
    {
        var anim = new DoubleAnimation
        {
            To = to,
            Duration = For(spring),
            EasingFunction = SpringEase.From(spring),
            BeginTime = MotionPrefs.Reduced ? TimeSpan.Zero : delay,
        };
        if (from is { } f) anim.From = f;
        target.BeginAnimation(property, anim, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>Plain eased fade, for opacity where a spring's overshoot would look like a flicker.</summary>
    public static void Fade(UIElement target, double to, int ms = 160, TimeSpan delay = default, Action? done = null)
    {
        var anim = new DoubleAnimation(to, new Duration(TimeSpan.FromMilliseconds(MotionPrefs.Reduced ? 1 : ms)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            BeginTime = MotionPrefs.Reduced ? TimeSpan.Zero : delay,
        };
        if (done is not null) anim.Completed += (_, _) => done();
        target.BeginAnimation(UIElement.OpacityProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// Gives an element a centred ScaleTransform and TranslateTransform (in that order)
    /// it can be bounced with, reusing them if already there.
    /// </summary>
    public static (ScaleTransform Scale, TranslateTransform Move) Transforms(UIElement e)
    {
        if (e.RenderTransform is TransformGroup g && g.Children.Count == 2
            && g.Children[0] is ScaleTransform s && g.Children[1] is TranslateTransform t && !g.IsFrozen)
            return (s, t);
        var scale = new ScaleTransform(1, 1);
        var move = new TranslateTransform();
        e.RenderTransformOrigin = new Point(0.5, 0.5);
        e.RenderTransform = new TransformGroup { Children = { scale, move } };
        return (scale, move);
    }

    /// <summary>Springs an element in from small and low: the flippy "pop".</summary>
    public static void PopIn(UIElement e, TimeSpan delay = default, double fromScale = 0.6, double rise = 14)
    {
        var (scale, move) = Transforms(e);
        e.Opacity = 0;
        Fade(e, 1, 140, delay);
        To(scale, ScaleTransform.ScaleXProperty, 1, Spring.Pop, delay, fromScale);
        To(scale, ScaleTransform.ScaleYProperty, 1, Spring.Pop, delay, fromScale);
        To(move, TranslateTransform.YProperty, 0, Spring.Bouncy, delay, rise);
    }

    /// <summary>A quick boing: squash a little, then spring back past full size.</summary>
    public static void Boing(UIElement e, double amount = 0.18)
    {
        if (MotionPrefs.Reduced) return;
        var (scale, _) = Transforms(e);
        To(scale, ScaleTransform.ScaleXProperty, 1, Spring.Pop, from: 1 + amount);
        To(scale, ScaleTransform.ScaleYProperty, 1, Spring.Pop, from: 1 - amount / 2);
    }

    /// <summary>Gently bobs an element up and down forever.</summary>
    public static void Float(UIElement e, double distance = 5, double seconds = 2.2)
    {
        if (MotionPrefs.Reduced) return;
        var (_, move) = Transforms(e);
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -distance, TimeSpan.FromSeconds(seconds / 2))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        });
    }

    /// <summary>Rocks an element side to side forever (the charging bolt).</summary>
    public static void Wiggle(UIElement e, double degrees = 12)
    {
        if (MotionPrefs.Reduced) return;
        var rotate = new RotateTransform();
        e.RenderTransformOrigin = new Point(0.5, 0.5);
        e.RenderTransform = rotate;
        rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-degrees, degrees, TimeSpan.FromMilliseconds(700))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        });
    }

    /// <summary>Spins an element one full turn with a springy finish (the refresh button).</summary>
    public static void Spin(UIElement e, double degrees = 360)
    {
        var rotate = e.RenderTransform as RotateTransform ?? new RotateTransform();
        e.RenderTransformOrigin = new Point(0.5, 0.5);
        e.RenderTransform = rotate;
        To(rotate, RotateTransform.AngleProperty, degrees, Spring.Bouncy, from: 0);
    }
}
