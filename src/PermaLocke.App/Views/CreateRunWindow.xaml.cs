using System.Windows;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

public partial class CreateRunWindow : Window
{
    public CreateRunWindow(CreateRunViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        // The view model owns the outcome; the window only closes when it says it is done.
        viewModel.Finished += (_, _) => DialogResult = true;
    }
}
