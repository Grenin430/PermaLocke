using System.Windows;
using PermaLocke.App.Services;

namespace PermaLocke.App;

/// <summary>
/// The shell window. Holds no logic beyond making its own frame match the theme.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DarkFrame.Apply(this);
    }
}
