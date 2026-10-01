using System.Windows;
using System.Windows.Controls;

namespace PermaLocke.App.Shells;

public partial class ShellCaption : UserControl
{
    public static readonly DependencyProperty CentreProperty = DependencyProperty.Register(
        nameof(Centre), typeof(object), typeof(ShellCaption),
        new PropertyMetadata(null, (d, e) => ((ShellCaption)d).CentrePresenter.Content = e.NewValue));

    public static readonly DependencyProperty ShowSubtitleProperty = DependencyProperty.Register(
        nameof(ShowSubtitle), typeof(bool), typeof(ShellCaption), new PropertyMetadata(true));

    public ShellCaption()
    {
        InitializeComponent();
    }

    /// <summary>What goes between the brand and the window buttons.</summary>
    public object? Centre
    {
        get => GetValue(CentreProperty);
        set => SetValue(CentreProperty, value);
    }

    /// <summary>The «NUZLOCKE ULTRA LUNA» after the name: left out when the centre needs the room.</summary>
    public bool ShowSubtitle
    {
        get => (bool)GetValue(ShowSubtitleProperty);
        set => SetValue(ShowSubtitleProperty, value);
    }

    private void OnGallery(object sender, RoutedEventArgs e) => DesignGalleryWindow.Open(Window.GetWindow(this));

    private void OnMinimise(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window) window.WindowState = WindowState.Minimized;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();
}
