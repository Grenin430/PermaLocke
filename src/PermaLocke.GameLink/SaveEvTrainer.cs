using Microsoft.Extensions.Logging;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink;

/// <summary>
/// Writes effort values into the player's save, in the slot the Pokémon already occupies.
/// </summary>
/// <remarks>
/// <para>
/// It edits something that exists, so before touching anything it checks the slot still holds the
/// <b>same Pokémon</b> — by PID, which survives nicknames, levels and evolutions, and is the only
/// field that still matches after the player has kept playing. A mismatch refuses rather than
/// rewriting whatever is there now.
/// </para>
/// <para>
/// Then the whole save is copied, the EVs are written, and the file is read back to confirm the
/// six values landed. Nothing is reported as trained that has not been seen on disk afterwards.
/// </para>
/// <para>
/// <b>Only the EVs are touched. The battle stats a party member carries are left exactly as the
/// game wrote them</b>, and this was measured rather than assumed. Recomputing them looked like
/// the tidy thing to do — the party stores its stats, unlike a box — but PKHeX works a stat out
/// from its own table of base stats, and this run is played on a randomized ROM with
/// <c>shuffleBaseStats</c> on, so that table is not the cartridge's. Tried on a copy of the real
/// partida, a Kommo-o with 168 HP came back with 151: a visible, wrong change to somebody's
/// Pokémon.
/// </para>
/// <para>
/// Leaving them alone costs nothing. The stat catches up when the game next recalculates it, which
/// is exactly what happens to a Pokémon that gains EVs in battle before it levels up.
/// </para>
/// </remarks>
public sealed class SaveEvTrainer(PlayerSave save, string backupFolder, ILogger<SaveEvTrainer> logger)
    : IEvTrainer
{
    public bool CanTrainNow(out string reason)
    {
        if (save.IsGameLoaded())
        {
            reason = "El juego está abierto en el emulador. Guarda la partida y cierra Azahar: "
                     + "mientras esté cargado, el emulador reescribiría el save y los EV se perderían.";
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

    public Task<DeliveryResult> ApplyAsync(EvChange change, CancellationToken ct = default) =>
        Task.Run(() => Apply(change), ct);

    private DeliveryResult Apply(EvChange change)
    {
        if (!CanTrainNow(out var reason))
        {
            return new DeliveryResult(
                save.IsGameLoaded() ? DeliveryOutcome.GameRunning : DeliveryOutcome.SaveNotFound, reason);
        }

        return ApplyIn(save.Find()!, change);
    }

    /// <summary>
    /// Writes into one specific save file. Kept separate from the checks so the whole edit can be
    /// exercised against a copy of a save, with no emulator anywhere near it.
    /// </summary>
    public DeliveryResult ApplyIn(string path, EvChange change)
    {
        try
        {
            if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
            {
                return new DeliveryResult(DeliveryOutcome.SaveUnreadable,
                    $"El fichero de partida no se ha podido leer como Ultra Luna: {path}");
            }

            var applied = ApplyTo(game, change);

            if (!applied.Delivered)
            {
                return applied;
            }

            Backup(path);
            File.WriteAllBytes(path, game.Write().ToArray());

            if (!Verify(path, change))
            {
                return new DeliveryResult(DeliveryOutcome.Failed,
                    "Se escribió la partida pero al releerla los EV no eran los pedidos. "
                    + "La copia de seguridad está en Saves/backup.");
            }

            logger.LogInformation("EV de {Name} ({Pid:X8}) en {Where}: {Evs}",
                change.Name, change.Pid, change.Where, string.Join('/', change.Evs));

            return applied;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallaron los EV de {Name}", change.Name);

            return new DeliveryResult(DeliveryOutcome.Failed,
                "No se pudo escribir en la partida. El detalle está en la carpeta Logs.");
        }
    }

    /// <summary>
    /// Applies the change to an already opened save, without writing anything to disk.
    /// </summary>
    /// <remarks>
    /// This is where the whole edit lives, so it can be tested against a save built in memory:
    /// PKHeX does not recognise a blank save written back to disk, so a full round trip through a
    /// file would need the player's own partida.
    /// </remarks>
    public DeliveryResult ApplyTo(SAV7USUM game, EvChange change)
    {
        if (Read(game, change.Box, change.Slot) is not PK7 { Species: > 0 } pokemon)
        {
            return new DeliveryResult(DeliveryOutcome.SlotChanged,
                $"En {change.Where} no hay ningún Pokémon. La partida ha cambiado desde que se leyó: "
                + "vuelve a leerla e inténtalo otra vez.");
        }

        if (pokemon.PID != change.Pid)
        {
            return new DeliveryResult(DeliveryOutcome.SlotChanged,
                $"En {change.Where} ya no está {change.Name}. La partida ha cambiado desde que se leyó: "
                + "vuelve a leerla e inténtalo otra vez.");
        }

        Write(pokemon, change.Evs);
        pokemon.RefreshChecksum();
        Store(game, pokemon, change.Box, change.Slot);

        return new DeliveryResult(DeliveryOutcome.Delivered,
            $"EV de {change.Name} guardados en {change.Where}.",
            change.Box + 1, change.Slot + 1);
    }

    private static PKM? Read(SAV7USUM game, int box, int slot) =>
        box == BoxedPokemon.PartyBox
            ? slot >= 0 && slot < game.PartyCount ? game.GetPartySlotAtIndex(slot) : null
            : game.GetBoxSlotAtIndex(box, slot);

    /// <summary>
    /// Puts it back exactly where it was, touching nothing else.
    /// </summary>
    /// <remarks>
    /// With <see cref="PokemonBuilder.InPlace"/>, for the reason §42 cost a run to learn: PKHeX
    /// treats putting a Pokémon into a slot as <em>acquiring</em> it and, left at the default,
    /// bumps captures, Poké Balls used and wild battles. None of that happens when somebody edits
    /// the EVs of a Pokémon they already have.
    /// </remarks>
    private static void Store(SAV7USUM game, PK7 pokemon, int box, int slot)
    {
        if (box == BoxedPokemon.PartyBox)
        {
            game.SetPartySlotAtIndex(pokemon, slot, PokemonBuilder.InPlace);
            return;
        }

        game.SetBoxSlotAtIndex(pokemon, box, slot, PokemonBuilder.InPlace);
    }

    private static void Write(PK7 pokemon, IReadOnlyList<int> evs)
    {
        pokemon.EV_HP = evs[0];
        pokemon.EV_ATK = evs[1];
        pokemon.EV_DEF = evs[2];
        pokemon.EV_SPA = evs[3];
        pokemon.EV_SPD = evs[4];
        pokemon.EV_SPE = evs[5];
    }

    private static bool Verify(string path, EvChange change)
    {
        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
        {
            return false;
        }

        if (Read(game, change.Box, change.Slot) is not PK7 written || written.PID != change.Pid)
        {
            return false;
        }

        int[] found =
        [
            written.EV_HP, written.EV_ATK, written.EV_DEF,
            written.EV_SPA, written.EV_SPD, written.EV_SPE
        ];

        return found.SequenceEqual(change.Evs);
    }

    /// <summary>
    /// Copies the whole save before touching it. If the backup cannot be written, nothing is
    /// written either: never edit a partida that cannot be put back.
    /// </summary>
    private void Backup(string path)
    {
        Directory.CreateDirectory(backupFolder);

        var name = $"main-{DateTime.Now:yyyyMMdd-HHmmss}.sav";
        File.Copy(path, Path.Combine(backupFolder, name), overwrite: false);

        logger.LogInformation("Copia de la partida guardada en {Name}", name);
    }
}
