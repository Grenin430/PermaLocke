using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PermaLocke.App.Views.Pixel;

/// <summary>
/// A window of a game's menu: a <see cref="PixelPanel"/> with a title band, its icon and its name, and whatever the
/// panel holds underneath. What HOME, the viewer and every screen after them put their blocks in (§176).
/// </summary>
/// <remarks>
/// The drawing is in <c>Themes/Pixel.xaml</c>, as an implicit style: this class only carries what the template reads.
/// <see cref="HeaderContent"/> goes at the right end of the band, for a count or a small button.
/// </remarks>
public sealed class PixelWindow : ContentControl
{
    /// <summary>The band's height in units: twelve cells at the usual three pixels.</summary>
    public const double BandHeight = 36;

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PixelWindow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(string), typeof(PixelWindow), new PropertyMetadata(null));

    public static readonly DependencyProperty HeaderContentProperty = DependencyProperty.Register(
        nameof(HeaderContent), typeof(object), typeof(PixelWindow), new PropertyMetadata(null));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Color), typeof(PixelWindow), new PropertyMetadata(Color.FromRgb(0x18, 0x13, 0x2C)));

    public static readonly DependencyProperty HeaderFillProperty = DependencyProperty.Register(
        nameof(HeaderFill), typeof(Color), typeof(PixelWindow), new PropertyMetadata(Color.FromRgb(0x21, 0x1A, 0x3A)));

    public static readonly DependencyProperty HeaderAccentProperty = DependencyProperty.Register(
        nameof(HeaderAccent), typeof(Color), typeof(PixelWindow), new PropertyMetadata(Color.FromRgb(0xB0, 0x7B, 0xF0)));

    public static readonly DependencyProperty IsSunkenProperty = DependencyProperty.Register(
        nameof(IsSunken), typeof(bool), typeof(PixelWindow), new PropertyMetadata(false));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>A key of <see cref="PixelIcons"/>; none when null.</summary>
    public string? Icon
    {
        get => (string?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public object? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    public Color Fill
    {
        get => (Color)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public Color HeaderFill
    {
        get => (Color)GetValue(HeaderFillProperty);
        set => SetValue(HeaderFillProperty, value);
    }

    public Color HeaderAccent
    {
        get => (Color)GetValue(HeaderAccentProperty);
        set => SetValue(HeaderAccentProperty, value);
    }

    public bool IsSunken
    {
        get => (bool)GetValue(IsSunkenProperty);
        set => SetValue(IsSunkenProperty, value);
    }
}
