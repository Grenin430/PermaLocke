using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace PermaLocke.App.Services;

/// <summary>
/// Makes a number arrive by counting up to itself instead of appearing.
/// </summary>
/// <remarks>
/// <para>
/// Attached rather than a control because it applies to a <see cref="TextBlock"/> that is already
/// styled and placed; wrapping every counter in something new to gain an animation would be paying
/// in layout for a flourish.
/// </para>
/// <para>
/// Two properties and not one: <c>To</c> is what the view model says, <c>Current</c> is what is on
/// screen this frame. A single one cannot be both the target of an animation and the thing a
/// binding writes — the animation would win permanently and the number would stop updating.
/// </para>
/// </remarks>
public static class Rolling
{
    /// <summary>The number to arrive at. Bind this where <c>Text</c> would have gone.</summary>
    public static readonly DependencyProperty ToProperty = DependencyProperty.RegisterAttached(
        "To", typeof(int), typeof(Rolling), new PropertyMetadata(0, OnToChanged));

    public static void SetTo(DependencyObject element, int value) => element.SetValue(ToProperty, value);

    public static int GetTo(DependencyObject element) => (int)element.GetValue(ToProperty);

    /// <summary>Where the count is right now, which is what actually gets written out.</summary>
    private static readonly DependencyProperty CurrentProperty = DependencyProperty.RegisterAttached(
        "Current", typeof(double), typeof(Rolling), new PropertyMetadata(0.0, OnCurrentChanged));

    private static void OnToChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not (TextBlock or Views.Pixel.PixelText) || element is not UIElement text)
        {
            return;
        }

        var from = (double)element.GetValue(CurrentProperty);
        var to = (int)e.NewValue;

        // La primera vez no se cuenta desde cero: al abrir la pestaña con la run a medias, ver
        // cuatro cifras subiendo desde nada es un truco, no una noticia.
        if (e.OldValue is 0 && from == 0)
        {
            element.SetValue(CurrentProperty, (double)to);
            return;
        }

        text.BeginAnimation(CurrentProperty, new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(Math.Clamp(Math.Abs(to - from) * 90, 160, 520)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private static void OnCurrentChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        var value = Math.Round((double)e.NewValue).ToString("0", CultureInfo.InvariantCulture);

        // El contador de la cabecera en píxeles (§176) cuenta igual que el de letra normal.
        if (element is TextBlock text)
        {
            text.Text = value;
        }
        else if (element is Views.Pixel.PixelText pixels)
        {
            pixels.Text = value;
        }
    }
}
