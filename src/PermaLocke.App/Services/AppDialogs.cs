using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PermaLocke.App.ViewModels;
using PermaLocke.App.Views;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.App.Services;

/// <summary>
/// Lets view models ask for a window without knowing that WPF exists, keeping window
/// handling out of the view models and the view models testable.
/// </summary>
public interface IAppDialogs
{
    /// <returns>True when a run was created.</returns>
    bool ShowCreateRun();

    /// <returns>True when a capture was registered.</returns>
    /// <param name="prefill">Pokémon detected in the game, to fill the form with.</param>
    bool ShowRegisterCapture(LivePartyMember? prefill = null);

    /// <returns>True when the run really changed role.</returns>
    bool ShowChangeRole();

    /// <summary>
    /// Asks the player to confirm something that is hard to undo, such as replacing the world of
    /// a game already in progress.
    /// </summary>
    /// <returns>True only when the player explicitly accepted.</returns>
    bool Confirm(string title, string message);

    /// <summary>Says something the player only has to read.</summary>
    void Tell(string title, string message);

    /// <summary>Lets the player pick a folder; null when they cancel.</summary>
    string? PickFolder(string title);
}

public sealed class AppDialogs(IServiceProvider services) : IAppDialogs
{
    public bool ShowCreateRun() =>
        Show(new CreateRunWindow(services.GetRequiredService<CreateRunViewModel>()));

    public bool ShowRegisterCapture(LivePartyMember? prefill = null)
    {
        var viewModel = services.GetRequiredService<RegisterCaptureViewModel>();

        if (prefill is not null)
        {
            viewModel.PrefillFrom(prefill);
        }

        return Show(new RegisterCaptureWindow(viewModel));
    }


    public bool ShowChangeRole() =>
        Show(new ChangeRoleWindow(services.GetRequiredService<ChangeRoleViewModel>()));

    public bool Confirm(string title, string message)
    {
        // La ventana puede no estar todavia, y MessageBox.Show con un dueño nulo LANZA. Sin este
        // respaldo, una confirmacion antes de que la ventana exista se pierde en silencio dentro
        // del comando asincrono, que se traga la excepcion.
        var owner = Application.Current?.MainWindow;

        var answer = owner is null
            ? MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning,
                MessageBoxResult.No)
            : MessageBox.Show(owner, message, title, MessageBoxButton.YesNo,
                MessageBoxImage.Warning, MessageBoxResult.No);

        return answer == MessageBoxResult.Yes;
    }

    public void Tell(string title, string message)
    {
        var owner = Application.Current?.MainWindow;
        if (owner is null) MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        else MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public string? PickFolder(string title)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = title };
        return dialog.ShowDialog(Application.Current?.MainWindow) == true ? dialog.FolderName : null;
    }

    private static bool Show(Window window)
    {
        window.Owner = Application.Current.MainWindow;
        return window.ShowDialog() == true;
    }
}
