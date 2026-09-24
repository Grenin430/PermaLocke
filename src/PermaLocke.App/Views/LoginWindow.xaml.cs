using System.Windows;
using PermaLocke.App.Services;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        DarkFrame.Apply(this);
        viewModel.Admitted += (_, _) => DialogResult = true;
    }
}
