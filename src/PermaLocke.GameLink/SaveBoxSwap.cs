using Microsoft.Extensions.Logging;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink;

/// <summary>
/// Performs a wonder trade on the player's save: the Pokémon handed over is replaced, in its own
/// slot, by the one that came back.
/// </summary>
/// <remarks>
/// <para>
/// This is the one write in PermaLocke that <b>destroys</b> something of the player's, so it is
/// the most careful. Before touching anything it checks that the slot still holds the species the
/// screen thinks it does: the boxes were read from the file, and if the player has played since,
/// that slot may hold something else entirely. A mismatch refuses the trade instead of
/// overwriting whatever is there now.
/// </para>
/// <para>
/// Then the whole save is copied, the swap is written, and the file is read back to confirm the
/// new Pokémon is really in that slot. Nothing is reported as traded that has not been seen on
/// disk afterwards.
/// </para>
/// </remarks>
public sealed class SaveBoxSwap(PlayerSave save, string backupFolder, ILogger<SaveBoxSwap> logger)
    : IPokemonSwap
{
    public bool CanSwapNow(out string reason)
    {
        if (save.IsGameLoaded())
        {
            reason = "El juego está abierto en el emulador. Guarda la partida y cierra Azahar: "
                     + "mientras esté cargado, el emulador reescribiría el save y el intercambio se perdería.";
            return false;
        }

        if (save.Find() is null)
        {
            reason = "No se encuentra la partida de Ultra Luna. ¿Has jugado y guardado alguna vez "
                     + "con este emulador?";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public Task<DeliveryResult> SwapAsync(WonderTradeOffer offer, int box, int slot,
        CancellationToken ct = default) =>
        Task.Run(() => Swap(offer, box, slot), ct);

    private DeliveryResult Swap(WonderTradeOffer offer, int box, int slot)
    {
        if (!CanSwapNow(out var reason))
        {
            return new DeliveryResult(
                save.IsGameLoaded() ? DeliveryOutcome.GameRunning : DeliveryOutcome.SaveNotFound, reason);
        }

        return SwapIn(save.Find()!, offer, box, slot);
    }

    /// <summary>
    /// Writes into one specific save file. Kept separate from the checks so the whole swap can be
    /// exercised against a copy of a save, with no emulator anywhere near it.
    /// </summary>
    public DeliveryResult SwapIn(string path, WonderTradeOffer offer, int box, int slot)
    {
        try
        {
            if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
            {
                return new DeliveryResult(DeliveryOutcome.SaveUnreadable,
                    $"El fichero de partida no se ha podido leer como Ultra Luna: {path}");
            }

            // El visor enseña también el equipo, y el equipo es otro almacén del save. Un índice
            // de equipo aquí caería en la caja 0 y destruiría a un Pokémon que nadie eligió, así
            // que se corta en la puerta aunque la pantalla ya no lo ofrezca.
            if (box < 0)
            {
                return new DeliveryResult(DeliveryOutcome.SlotChanged,
                    "El wonder trade solo funciona con Pokémon del PC. Deposita primero en una caja "
                    + "al que quieras entregar.");
            }

            if (game.GetBoxSlotAtIndex(box, slot) is not { } current || current.Species != offer.GivenSpecies)
            {
                return new DeliveryResult(DeliveryOutcome.SlotChanged,
                    $"En la caja {box + 1}, hueco {slot + 1} ya no está {offer.GivenName}. "
                    + "La partida ha cambiado desde que se leyó: vuelve a leerla y repite el intercambio.");
            }

            Backup(path);

            var received = PokemonBuilder.Build(
                new NewPokemon(offer.Species, offer.Level, offer.Nature, offer.AbilityId, offer.Ivs, offer.IsShiny),
                game);

            game.SetBoxSlotAtIndex(received, box, slot, PokemonBuilder.Handover);
            File.WriteAllBytes(path, game.Write().ToArray());

            if (!Verify(path, box, slot, offer))
            {
                return new DeliveryResult(DeliveryOutcome.Failed,
                    "Se escribió la partida pero al releerla el intercambio no estaba hecho. "
                    + "La copia de seguridad está en Saves/backup.");
            }

            logger.LogInformation("Wonder trade: {Given} sale y entra {Received} en la caja {Box}, hueco {Slot}",
                offer.GivenName, offer.Name, box + 1, slot + 1);

            return new DeliveryResult(DeliveryOutcome.Delivered,
                $"{offer.GivenName} se ha ido y {offer.Name} ocupa su sitio: caja {box + 1}, hueco {slot + 1}.",
                box + 1, slot + 1, received.PID);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el wonder trade de {Given} por {Received}", offer.GivenName, offer.Name);

            return new DeliveryResult(DeliveryOutcome.Failed,
                "No se pudo escribir en la partida. El detalle está en la carpeta Logs.");
        }
    }

    private static bool Verify(string path, int box, int slot, WonderTradeOffer offer)
    {
        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
        {
            return false;
        }

        var written = game.GetBoxSlotAtIndex(box, slot);
        return written is { } found && found.Species == offer.Species && found.CurrentLevel == offer.Level;
    }

    /// <summary>
    /// Copies the whole save before touching it. If the backup cannot be written, the trade does
    /// not happen: never destroy a Pokémon in a partida that cannot be put back.
    /// </summary>
    private void Backup(string path)
    {
        Directory.CreateDirectory(backupFolder);

        var name = $"main-{DateTime.Now:yyyyMMdd-HHmmss}.sav";
        File.Copy(path, Path.Combine(backupFolder, name), overwrite: false);

        logger.LogInformation("Copia de la partida guardada en {Name}", name);
    }
}
