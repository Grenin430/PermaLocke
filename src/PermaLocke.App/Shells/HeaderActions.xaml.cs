using System.Windows;
using System.Windows.Controls;

namespace PermaLocke.App.Shells;

public partial class HeaderActions : UserControl
{
    public static readonly DependencyProperty BoxHeightProperty = DependencyProperty.Register(
        nameof(BoxHeight), typeof(double), typeof(HeaderActions), new PropertyMetadata(40.0));

    public static readonly DependencyProperty IconWidthProperty = DependencyProperty.Register(
        nameof(IconWidth), typeof(double), typeof(HeaderActions), new PropertyMetadata(46.0));

    public static readonly DependencyProperty PlayPaddingProperty = DependencyProperty.Register(
        nameof(PlayPadding), typeof(Thickness), typeof(HeaderActions), new PropertyMetadata(new Thickness(15, 9, 21, 14)));

    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(
        nameof(Compact), typeof(bool), typeof(HeaderActions), new PropertyMetadata(false, (d, _) => ((HeaderActions)d).Fit()));

    public HeaderActions()
    {
        InitializeComponent();
    }

    /// <summary>Smaller plates, for the bar of the window: 30 units tall instead of 40.</summary>
    public bool Compact
    {
        get => (bool)GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    public double BoxHeight
    {
        get => (double)GetValue(BoxHeightProperty);
        set => SetValue(BoxHeightProperty, value);
    }

    public double IconWidth
    {
        get => (double)GetValue(IconWidthProperty);
        set => SetValue(IconWidthProperty, value);
    }

    public Thickness PlayPadding
    {
        get => (Thickness)GetValue(PlayPaddingProperty);
        set => SetValue(PlayPaddingProperty, value);
    }

    private void Fit()
    {
        var compact = Compact;
        BoxHeight = compact ? 32 : 40;
        IconWidth = compact ? 40 : 46;
        PlayPadding = compact ? new Thickness(11, 4, 15, 9) : new Thickness(15, 9, 21, 14);
        Role.Margin = new Thickness(10, 0, 0, 0);
    }
}
