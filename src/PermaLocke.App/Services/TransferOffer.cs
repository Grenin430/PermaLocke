using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.Logging;
using PermaLocke.GameLink;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Services;

/// <summary>
/// «TRAER MI PARTIDA DE OTRA CARPETA» (§195): the player picks their old PermaLocke folder, sees what will be brought,
/// and PermaLocke restarts to copy it before anything opens the database (<see cref="FolderTransfer"/>).
/// </summary>
/// <remarks>
/// Offered while this folder has no run (HOME, and the first time guided). Refused with the emulator open: the save it
/// copies must not be half written. The old PermaLocke cannot be open either, because only one runs at a time.
/// </remarks>
public sealed class TransferOffer(AppPaths paths, IAppDialogs dialogs, EmulatorLauncher emulator, ILogger<TransferOffer> logger)
{
    /// <summary>The argument of the restart that does the transfer: it waits for this instance to close.</summary>
    public const string RestartArgument = "--tras-traspaso";

    /// <summary>Asks, plans, confirms and restarts. Returns when the player cancels or it cannot be done.</summary>
    public void Offer()
    {
        if (emulator.IsRunning)
        {
            dialogs.Tell("Traer mi partida", "Cierra el emulador primero: la partida no se puede copiar mientras está abierta.");
            return;
        }

        if (dialogs.PickFolder("Elige la carpeta de PermaLocke de antes (la de prueba, la de amigos...)") is not { } folder)
        {
            return;
        }

        var plan = FolderTransfer.Plan(folder, paths.Root);
        if (!plan.CanGo)
        {
            dialogs.Tell("Traer mi partida", string.Join("\n", plan.Problems));
            return;
        }

        var list = string.Join("\n", plan.Items.Select(item => $"· {item.What}"));
        if (!dialogs.Confirm("Traer mi partida",
                $"Se copiará de «{plan.OldRoot}»:\n\n{list}\n\nLa carpeta de antes no se toca. PermaLocke se reinicia para " +
                "copiarlo con todo cerrado. ¿Seguir?"))
        {
            return;
        }

        FolderTransfer.Schedule(paths.Config, plan.OldRoot);
        logger.LogInformation("Traspaso pedido desde {Old}; se reinicia para hacerlo", plan.OldRoot);

        Process.Start(new ProcessStartInfo(Environment.ProcessPath!, RestartArgument) { UseShellExecute = false });
        BackgroundMode.Leaving = true;
        Application.Current.Shutdown();
    }
}
