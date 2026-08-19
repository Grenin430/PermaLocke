using System.Windows;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

public partial class RegisterCaptureWindow : Window
{
    public RegisterCaptureWindow(RegisterCaptureViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        viewModel.Finished += (_, _) => DialogResult = true;
    }
}
