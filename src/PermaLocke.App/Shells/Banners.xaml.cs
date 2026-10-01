using System.Windows;
using System.Windows.Controls;

namespace PermaLocke.App.Shells;

public partial class Banners : UserControl
{
    public static readonly DependencyProperty ShowNeedProperty = DependencyProperty.Register(
        nameof(ShowNeed), typeof(bool), typeof(Banners),
        new PropertyMetadata(true, (d, e) => ((Banners)d).NeedHost.Visibility = e.NewValue is true ? Visibility.Visible : Visibility.Collapsed));

    public Banners()
    {
        InitializeComponent();
    }

    /// <summary>False when the design says the need elsewhere (the dialogue box, the ticker): only the update strip stays.</summary>
    public bool ShowNeed
    {
        get => (bool)GetValue(ShowNeedProperty);
        set => SetValue(ShowNeedProperty, value);
    }
}
