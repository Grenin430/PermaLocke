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

    public bool Confirm(string title, string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, title,
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;

    private static bool Show(Window window)
    {
        window.Owner = Application.Current.MainWindow;
        return window.ShowDialog() == true;
    }
}
