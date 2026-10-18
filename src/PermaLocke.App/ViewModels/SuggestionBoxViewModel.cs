using PermaLocke.Infrastructure;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// The suggestion box in the header (1.0.5.5): whatever the player writes goes to the organiser's Admin, through the
/// tournament server (<c>tools/supabase/18-sugerencias.sql</c>). Nothing else reads it.
/// </summary>
public sealed partial class SuggestionBoxViewModel : ObservableObject
{
    public const int MaxLength = 1000;

    private readonly DiscordLogin _discord;
    private readonly ILogger<SuggestionBoxViewModel> _logger;

    public SuggestionBoxViewModel(DiscordLogin discord, ILogger<SuggestionBoxViewModel> logger)
    {
        Fleeting.Fade(this, nameof(Status));
        _discord = discord;
        _logger = logger;
    }

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _text = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isSending;

    [ObservableProperty]
    private string _status = string.Empty;

    [RelayCommand]
    private void Toggle() => IsOpen = !IsOpen;

    [RelayCommand]
    private void Close() => IsOpen = false;

    private bool CanSend() => !IsSending && !string.IsNullOrWhiteSpace(Text);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (_discord.Saved is not { } account)
        {
            Status = "Entra con Discord para mandarla.";
            return;
        }

        var text = Text.Trim();
        if (text.Length > MaxLength) text = text[..MaxLength];

        IsSending = true;

        try
        {
            var sent = await _discord.PostAsync("sugerencias", JsonSerializer.Serialize(new
            {
                nombre = account.Name,
                texto = text,
                version = AppUpdate.Display(UpdateService.Current)
            }));

            if (sent)
            {
                Text = string.Empty;
                Status = "Enviada. ¡Gracias!";
                _logger.LogInformation("Sugerencia enviada ({Length} letras)", text.Length);
            }
            else
            {
                Status = "No se ha podido mandar. Prueba otra vez en un rato.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo mandar la sugerencia");
            Status = "No se ha podido mandar. Prueba otra vez en un rato.";
        }
        finally
        {
            IsSending = false;
        }
    }
}
