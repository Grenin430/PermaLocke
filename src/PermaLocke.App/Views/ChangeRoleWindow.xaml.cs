using System.Windows;
using PermaLocke.App.Services;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

public partial class ChangeRoleWindow : Window
{
    public ChangeRoleWindow(ChangeRoleViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        DarkFrame.Apply(this);

        // Cancelar y confirmar acaban los dos aquí; lo que distingue uno de otro es si el view
        // model llegó a cambiar el rol, no cuál de los dos botones cerró la ventana.
        viewModel.Finished += (_, _) => DialogResult = viewModel.Confirmed;
    }
}
