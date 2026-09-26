using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink;
using PermaLocke.Infrastructure;
using PermaLocke.Randomizer.Rom;

namespace PermaLocke.App.ViewModels;

/// <summary>One step of the first time, and whether it is done.</summary>
/// <param name="Action">What its button does, or null for a step with no button.</param>
/// <param name="Second">A second button, such as bringing the partida from another folder instead of starting one.</param>
public sealed record GuideStep(int Number, string Title, string Detail, bool Done,
    string? ActionLabel = null, IRelayCommand? Action = null, string? SecondLabel = null, IRelayCommand? Second = null)
{
    public bool HasAction => !Done && Action is not null;

    public bool HasSecond => !Done && Second is not null;
}

/// <summary>
/// PRIMEROS PASOS (§197, plan del próximo torneo, paso 4): the first time, in JUGAR, what is left before playing — the
/// ROM, the emulator, Discord, the run (or bringing it from another folder, §195), the world and the first save — each
/// ticked off as soon as it is true, with the button that does it.
/// </summary>
/// <remarks>
/// It decides nothing and stores nothing but whether the player hid it: every tick is read from the real thing (a ROM in
/// <c>ROM/</c>, the emulator's executable, the session, the run, the world installed in the emulator, the save). Gone by
/// itself once all is done; hidden for good with OCULTAR.
/// </remarks>
public sealed partial class FirstRunGuide(AppPaths paths, IRunContext runs, DiscordLogin discord, AzaharInstallation azahar,
    PlayerSave save, IAppDialogs dialogs, TransferOffer transfer, AppSettings settings) : ObservableObject
{
    private const string ProgramId = "00040000001B5100";

    public ObservableCollection<GuideStep> Steps { get; } = [];

    /// <summary>Shown while something is left and the player has not hidden it.</summary>
    [ObservableProperty]
    private bool _visible;

    [ObservableProperty]
    private string _progress = string.Empty;

    /// <summary>Opens a section by its title; set by JUGAR, which knows how to.</summary>
    public Action<string>? Navigate { get; set; }

    /// <summary>Looks at everything again. Cheap: files and the session, no game.</summary>
    [RelayCommand]
    public void Refresh()
    {
        var location = azahar.Locate(AppContext.BaseDirectory);
        var hasRun = runs.Current is not null;
        var mod = AzaharInstallation.ModDirectory(location, ProgramId);

        GuideStep[] steps =
        [
            new(1, "Entrar con Discord", "Con una cuenta de la lista del torneo.", discord.Saved is { Allowed: true }),
            new(2, "Tu ROM de Ultra Luna", "Desencriptada, .3ds o .cci, en la carpeta ROM.",
                RomInspector.ScanFolder(paths.Rom).Count > 0, "ABRIR CARPETA ROM", OpenRomFolderCommand),
            new(3, "El emulador", location.ExecutablePath is not null
                    ? "Viene con PermaLocke, ya preparado."
                    : "Falta Emulator\\azahar.exe: vuelve a extraer la carpeta entera.",
                location.ExecutablePath is not null),
            new(4, "Tu run", "Con tu nombre y el rol que quieras. ¿Ya jugabas en otra carpeta? Tráetela entera.",
                hasRun, "CREAR RUN", CreateRunCommand, "TRAER MI PARTIDA DE OTRA CARPETA", BringCommand),
            new(5, "Tu mundo", "En RANDOMIZADOR: genéralo y pulsa INSTALAR EN AZAHAR, con el emulador cerrado.",
                Directory.Exists(mod) && Directory.EnumerateFileSystemEntries(mod).Any(), "IR AL RANDOMIZADOR", GoToRandomizerCommand),
            new(6, "Juega y guarda", "Pulsa JUGAR y guarda en el juego en cuanto tengas tu primer Pokémon.", save.Find() is not null)
        ];

        Steps.Clear();
        foreach (var step in steps) Steps.Add(step);

        var done = steps.Count(step => step.Done);
        Progress = $"{done} DE {steps.Length}";
        Visible = done < steps.Length && !settings.Current.GuideHidden;
    }

    [RelayCommand]
    private void OpenRomFolder()
    {
        Directory.CreateDirectory(paths.Rom);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{paths.Rom}\"") { UseShellExecute = true });
    }

    [RelayCommand]
    private void CreateRun()
    {
        dialogs.ShowCreateRun();
        Refresh();
    }

    [RelayCommand]
    private void Bring() => transfer.Offer();

    [RelayCommand]
    private void GoToRandomizer() => Navigate?.Invoke("RANDOMIZADOR");

    [RelayCommand]
    private void Hide()
    {
        settings.Update(settings.Current with { GuideHidden = true });
        Visible = false;
    }
}
