using System.Windows;
using System.Windows.Controls;

namespace PermaLocke.App.Shells;

public partial class ShellOverlays : UserControl
{
    public static readonly DependencyProperty OffsetProperty = DependencyProperty.Register(
        nameof(Offset), typeof(Thickness), typeof(ShellOverlays), new PropertyMetadata(new Thickness(0, 58, 26, 0)));

    public ShellOverlays()
    {
        InitializeComponent();
    }

    /// <summary>Where the trays open: the distance from the top right corner of the window.</summary>
    public Thickness Offset
    {
        get => (Thickness)GetValue(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }
}
