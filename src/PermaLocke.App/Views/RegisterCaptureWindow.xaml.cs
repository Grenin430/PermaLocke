using System.Windows;
using PermaLocke.App.Services;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

public partial class RegisterCaptureWindow : Window
{
    public RegisterCaptureWindow(RegisterCaptureViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        DarkFrame.Apply(this);

        viewModel.Finished += (_, _) => DialogResult = true;
    }
}
