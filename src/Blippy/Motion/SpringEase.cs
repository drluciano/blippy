using System.Windows;
using System.Windows.Media.Animation;
using Blippy.Core;

namespace Blippy.Motion;

/// <summary>
/// An easing curve that follows a damped spring, so things overshoot and settle like
/// flippy's Reanimated springs. Usable from XAML storyboards. When Windows asks for
/// fewer animations it jumps straight to the end.
/// </summary>
public sealed class SpringEase : EasingFunctionBase
{
    public SpringEase()
    {
        // EaseIn means "use EaseInCore as is"; the spring already has its own shape.
        EasingMode = EasingMode.EaseIn;
    }

    public static readonly DependencyProperty DampingProperty =
        DependencyProperty.Register(nameof(Damping), typeof(double), typeof(SpringEase), new PropertyMetadata(Spring.Bouncy.Damping));

    public static readonly DependencyProperty StiffnessProperty =
        DependencyProperty.Register(nameof(Stiffness), typeof(double), typeof(SpringEase), new PropertyMetadata(Spring.Bouncy.Stiffness));

    public static readonly DependencyProperty MassProperty =
        DependencyProperty.Register(nameof(Mass), typeof(double), typeof(SpringEase), new PropertyMetadata(Spring.Bouncy.Mass));

    public double Damping { get => (double)GetValue(DampingProperty); set => SetValue(DampingProperty, value); }
    public double Stiffness { get => (double)GetValue(StiffnessProperty); set => SetValue(StiffnessProperty, value); }
    public double Mass { get => (double)GetValue(MassProperty); set => SetValue(MassProperty, value); }

    public static SpringEase From(Spring s) => new() { Damping = s.Damping, Stiffness = s.Stiffness, Mass = s.Mass };

    public Spring Spring => new(Damping, Stiffness, Mass);

    protected override double EaseInCore(double normalizedTime)
    {
        if (normalizedTime >= 1 || MotionPrefs.Reduced) return normalizedTime > 0 ? 1 : 0;
        var spring = Spring;
        return spring.ValueAt(normalizedTime * spring.SettleTime().TotalSeconds);
    }

    protected override Freezable CreateInstanceCore() => new SpringEase();
}

/// <summary>The "Show animations in Windows" setting.</summary>
public static class MotionPrefs
{
    public static bool Reduced => !SystemParameters.ClientAreaAnimation;
}
