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
    public const double BandHeight = 32;

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PixelWindow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(string), typeof(PixelWindow), new PropertyMetadata(null));

    public static readonly DependencyProperty HeaderContentProperty = DependencyProperty.Register(
        nameof(HeaderContent), typeof(object), typeof(PixelWindow), new PropertyMetadata(null));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Color), typeof(PixelWindow),
        new PropertyMetadata(Color.FromRgb(0x18, 0x13, 0x2C), (d, e) => ((PixelWindow)d).FillBrush = Frozen((Color)e.NewValue)));

    /// <summary>The fill as a brush, for what has to cover the panel behind it (the HUD's title plate).</summary>
    public static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(
        nameof(FillBrush), typeof(Brush), typeof(PixelWindow), new PropertyMetadata(Frozen(Color.FromRgb(0x18, 0x13, 0x2C))));

    public static readonly DependencyProperty HeaderFillProperty = DependencyProperty.Register(
        nameof(HeaderFill), typeof(Color), typeof(PixelWindow), new PropertyMetadata(Color.FromRgb(0x21, 0x1A, 0x3A)));

    public static readonly DependencyProperty HeaderAccentProperty = DependencyProperty.Register(
        nameof(HeaderAccent), typeof(Color), typeof(PixelWindow), new PropertyMetadata(Color.FromRgb(0xB0, 0x7B, 0xF0)));

    public static readonly DependencyProperty IsSunkenProperty = DependencyProperty.Register(
        nameof(IsSunken), typeof(bool), typeof(PixelWindow), new PropertyMetadata(false));

    /// <summary>How the title is carried (2026-10-01): decided by the look in use, refreshed when it changes.</summary>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
        nameof(Variant), typeof(WindowKind), typeof(PixelWindow), new PropertyMetadata(WindowKind.Band));

    /// <summary>How far the title goes down for the edge of the look in use: the cells of its border beyond one.</summary>
    public static readonly DependencyProperty HeadInsetProperty = DependencyProperty.Register(
        nameof(HeadInset), typeof(double), typeof(PixelWindow), new PropertyMetadata(0.0));

    public PixelWindow()
    {
        Restyle();
    }

    public double HeadInset
    {
        get => (double)GetValue(HeadInsetProperty);
        set => SetValue(HeadInsetProperty, value);
    }

    /// <summary>Takes the look in use: how it carries its title, and how far down for its border.</summary>
    public void Restyle()
    {
        Variant = PixelTheme.Current.Windows;
        HeadInset = (PixelTheme.Current.Inset - 1) * 3.0;
    }

    public Brush FillBrush
    {
        get => (Brush)GetValue(FillBrushProperty);
        private set => SetValue(FillBrushProperty, value);
    }

    private static Brush Frozen(Color colour)
    {
        var brush = new SolidColorBrush(colour);
        brush.Freeze();
        return brush;
    }

    public WindowKind Variant
    {
        get => (WindowKind)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

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
