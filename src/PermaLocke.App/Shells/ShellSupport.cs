using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PermaLocke.App.Views.Pixel;

namespace PermaLocke.App.Shells;

/// <summary>What the alternative shells (2026-10-01) have in common: motion, the list fix and the section entrance.</summary>
public static class ShellSupport
{
    /// <summary>The player asked Windows for no animations: the shells keep still, every state stays readable.</summary>
    public static bool ReducedMotion => PixelTheme.ReducedMotion;

    /// <summary>Short on room: the tabs that are not selected keep only their icon. Inherited by the items of the list.</summary>
    public static readonly DependencyProperty CompactProperty = DependencyProperty.RegisterAttached(
        "Compact", typeof(bool), typeof(ShellSupport),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetCompact(DependencyObject element) => (bool)element.GetValue(CompactProperty);

    public static void SetCompact(DependencyObject element, bool value) => element.SetValue(CompactProperty, value);

    public static readonly DependencyProperty ReleaseCaptureProperty = DependencyProperty.RegisterAttached(
        "ReleaseCapture", typeof(bool), typeof(ShellSupport), new PropertyMetadata(false, OnReleaseCaptureChanged));

    public static bool GetReleaseCapture(DependencyObject element) => (bool)element.GetValue(ReleaseCaptureProperty);

    public static void SetReleaseCapture(DependencyObject element, bool value) => element.SetValue(ReleaseCaptureProperty, value);

    /// <summary>
    /// A ListBox captures the mouse when pressed and keeps selecting what it passes over while the button is down:
    /// dragging along a bar changed section without letting go. Without the capture only what is clicked changes.
    /// </summary>
    private static void OnReleaseCaptureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListBox list && e.NewValue is true)
        {
            list.AddHandler(Mouse.GotMouseCaptureEvent, new MouseEventHandler((_, args) =>
            {
                if (args.OriginalSource is ListBox source) source.ReleaseMouseCapture();
            }), handledEventsToo: true);
        }
    }

    /// <summary>The new section fades in and settles a few pixels, unless the player asked for no motion.</summary>
    public static void Enter(UIElement host, TranslateTransform slide)
    {
        if (ReducedMotion) return;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        host.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(140), EasingFunction = ease
        });
        slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
        {
            From = 10, To = 0, Duration = TimeSpan.FromMilliseconds(180), EasingFunction = ease
        });
    }
}
