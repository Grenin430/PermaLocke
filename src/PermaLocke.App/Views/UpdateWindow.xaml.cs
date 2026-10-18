using System.ComponentModel;
using System.Windows;
using PermaLocke.App.Services;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

/// <summary>The update window (§201). Closing it during the download cancels it; after that it waits.</summary>
public partial class UpdateWindow : Window
{
    private readonly UpdateProgressViewModel _model;

    public UpdateWindow(UpdateProgressViewModel model)
    {
        _model = model;
        DataContext = model;
        InitializeComponent();
        DarkFrame.Apply(this);
    }

    /// <summary>Closes it for good, from the service, when the update is over one way or another.</summary>
    public void Finish()
    {
        Closing -= OnClosing;
        Close();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Mientras descarga, cerrar es cancelar. Instalando, no se deja a medias.
        if (_model.CancelCommand.CanExecute(null))
        {
            _model.CancelCommand.Execute(null);
        }

        e.Cancel = true;
    }
}
