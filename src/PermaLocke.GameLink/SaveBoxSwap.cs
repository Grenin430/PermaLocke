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
            reason = "El juego está abierto. Guarda y cierra Azahar.";
            return false;
        }

        if (save.Find() is null)
        {
            reason = "No se encuentra tu partida de Ultra Luna.";
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
                    "No se ha podido leer tu partida.");
            }

            // El equipo es otro almacén del save, no la caja -1. Se acepta, pero por su propia
            // puerta: un índice de equipo tratado como caja caería en la caja 0 y destruiría a un
            // Pokémon que nadie eligió.
            if (box != BoxedPokemon.PartyBox && box < 0)
            {
                return new DeliveryResult(DeliveryOutcome.SlotChanged,
                    $"Caja {box} no existe. El intercambio no se ha hecho.");
            }

            if (Read(game, box, slot) is not { } current || current.Species != offer.GivenSpecies)
            {
                return new DeliveryResult(DeliveryOutcome.SlotChanged,
                    $"En {Where(box, slot)} ya no está {offer.GivenName}. "
                    + "Tu partida ha cambiado. Vuelve a intentarlo.");
            }

            Backup(path);

            var received = ApplyTo(game, offer, box, slot);
            File.WriteAllBytes(path, game.Write().ToArray());

            if (!Verify(path, box, slot, offer))
            {
                return new DeliveryResult(DeliveryOutcome.Failed,
                    "No se ha podido guardar el cambio. Vuelve a intentarlo.");
            }

            logger.LogInformation("Wonder trade: {Given} sale y entra {Received} en {Where}",
                offer.GivenName, offer.Name, Where(box, slot));

            // Del equipo se avisa de algo que en una caja no pasa: las estadísticas de combate las
            // calcula PKHeX con SU tabla de base, y la ROM las baraja (§51), así que el número que
            // enseñe hasta que el juego lo recalcule -curarse en un Centro basta- puede no cuadrar.
            var caveat = box == BoxedPokemon.PartyBox
                ? " Cúralo en un Centro Pokémon para ver sus estadísticas."
                : string.Empty;

            return new DeliveryResult(DeliveryOutcome.Delivered,
                $"{offer.GivenName} se ha ido y {offer.Name} ocupa su sitio: {Where(box, slot)}." + caveat,
                box + 1, slot + 1, received.PID);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el wonder trade de {Given} por {Received}", offer.GivenName, offer.Name);

            return new DeliveryResult(DeliveryOutcome.Failed,
                "No se pudo escribir en la partida.");
        }
    }

    private static bool Verify(string path, int box, int slot, WonderTradeOffer offer)
    {
        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
        {
            return false;
        }

        var written = Read(game, box, slot);
        return written is { } found && found.Species == offer.Species && found.Form == offer.Form && Data.GameLevels.Of(found) == offer.Level;
    }

    /// <summary>
    /// Does the swap on a loaded save: builds what arrives and puts it where the other one was.
    /// </summary>
    /// <remarks>
    /// Separate from the file handling so it can be exercised on a save built in memory — no
    /// emulator, no ROM, nobody's personal data. Same seam as the EV trainer's <c>ApplyTo</c>, and
    /// for the same reason: PKHeX will not read a blank save written back to disk, so a test that
    /// insisted on a real file could not cover this at all.
    /// </remarks>
    public static PK7 ApplyTo(SAV7USUM game, WonderTradeOffer offer, int box, int slot)
    {
        var received = PokemonBuilder.Build(
            new NewPokemon(offer.Species, offer.Level, offer.Nature, offer.AbilityId, offer.Ivs,
                offer.IsShiny, offer.Form),
            game);

        Store(game, received, box, slot);
        return received;
    }

    /// <summary>Reads a slot, from the party or from a box, whichever the index means.</summary>
    public static PKM? Read(SAV7USUM game, int box, int slot) =>
        box == BoxedPokemon.PartyBox
            ? slot >= 0 && slot < game.PartyCount ? game.GetPartySlotAtIndex(slot) : null
            : game.GetBoxSlotAtIndex(box, slot);

    /// <summary>
    /// Writes the one that arrived into the slot the other one left.
    /// </summary>
    /// <remarks>
    /// With <see cref="PokemonBuilder.Handover"/> in both cases: what arrives is a Pokémon the
    /// player did not have, so the Pokédex may learn about it, but no record moves — nobody threw a
    /// ball at a wonder trade (§42).
    /// </remarks>
    private static void Store(SAV7USUM game, PK7 pokemon, int box, int slot)
    {
        if (box == BoxedPokemon.PartyBox)
        {
            game.SetPartySlotAtIndex(pokemon, slot, PokemonBuilder.Handover);
            return;
        }

        game.SetBoxSlotAtIndex(pokemon, box, slot, PokemonBuilder.Handover);
    }

    private static string Where(int box, int slot) =>
        box == BoxedPokemon.PartyBox
            ? $"el equipo, puesto {slot + 1}"
            : $"la caja {box + 1}, hueco {slot + 1}";

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
