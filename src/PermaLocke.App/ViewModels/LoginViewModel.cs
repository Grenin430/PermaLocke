using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// The door before the application: ENTRAR CON DISCORD, and in only with an account on the tournament's whitelist.
/// </summary>
/// <remarks>
/// Asked by the player on 2026-09-24: one button and nothing else. What really protects the tournament is the server
/// refusing what an account off the list sends (phase 2); this screen only keeps out whoever does not change the code.
/// </remarks>
public sealed partial class LoginViewModel(DiscordLogin discord, ILogger<LoginViewModel> logger) : ObservableObject
{
    /// <summary>Raised once the account is signed in and on the list.</summary>
    public event EventHandler? Admitted;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    private bool _busy;

    /// <summary>Checks the session kept on this PC; true lets the application open without showing this screen.</summary>
    public async Task<bool> TryKeptSessionAsync()
    {
        try
        {
            if (await discord.RefreshAsync() is not { } account) return false;
            if (account.Allowed) return true;
            Refuse(account);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo comprobar la sesión de Discord al abrir");
            Status = "No hay conexión con el servidor del torneo. Comprueba internet y vuelve a entrar.";
        }

        return false;
    }

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignInAsync()
    {
        Busy = true;
        Status = "Termina de entrar en el navegador...";

        try
        {
            var account = await discord.SignInAsync();

            if (account.Allowed)
            {
                Admitted?.Invoke(this, EventArgs.Empty);
                return;
            }

            Refuse(account);
        }
        catch (OperationCanceledException)
        {
            Status = "Se ha cansado de esperar al navegador. Vuelve a intentarlo.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el inicio de sesión con Discord");
            Status = "No se ha podido entrar con Discord. Comprueba internet y vuelve a intentarlo.";
        }
        finally
        {
            Busy = false;
        }
    }

    private bool CanSignIn => !Busy;

    private void Refuse(DiscordAccount account)
    {
        discord.SignOut();
        Status = $"{account.Name}, tu cuenta no está en la lista del torneo. Pásale este número al organizador: {account.DiscordId}";
    }
}
