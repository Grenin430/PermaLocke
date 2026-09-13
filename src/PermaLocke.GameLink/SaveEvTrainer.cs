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
/// A party member also gets its <b>stored battle stats</b> put back in step, because it carries
/// them — unlike one in a box, which has none and is worked out on the way out. This used to be
/// left alone, and the reason was sound: PKHeX works a stat out from its own table of base stats,
/// this run is played with <c>shuffleBaseStats</c> on, and tried that way a Kommo-o with 168 PS
/// came back with 151. What was <em>not</em> sound was the sentence that followed it — «leaving
/// them alone costs nothing, the stat catches up when the game next recalculates». It does for a
/// Pokémon gaining EVs in battle. It does not here: nothing about writing a save makes the game
/// recalculate, so the stat waits for a level up, and <b>at the level cap there is no level up</b>.
/// The player trained six Pokémon at the cap and saw nothing change, which is exactly what that
/// sentence predicted if you read it carefully enough.
/// </para>
/// <para>
/// The fix is not to trust PKHeX's table but to read the installed world's own
/// (<see cref="WorldLimits.BaseStats"/>). When that table is not available the stats are still left
/// alone and <b>the message says so</b>, which is the part that was missing before: silence read as
/// success.
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

        var recomputed = change.Box == BoxedPokemon.PartyBox && Restat(pokemon);

        pokemon.RefreshChecksum();
        Store(game, pokemon, change.Box, change.Slot);

        return new DeliveryResult(DeliveryOutcome.Delivered,
            $"EV de {change.Name} guardados en {change.Where}."
            + (change.Box != BoxedPokemon.PartyBox
                ? " Está en una caja, así que sus estadísticas se calculan al sacarlo."
                : recomputed
                    ? " Estadísticas puestas al día."
                    : " Sus estadísticas NO se han podido poner al día: este mundo no publica su"
                      + " tabla de estadísticas base. Se verán al subir de nivel."),
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

    /// <summary>Shedinja, whose PS the game forces to one whatever the formula says.</summary>
    private const int Shedinja = 292;

    /// <summary>
    /// Puts a party member's stored battle stats back in step with its effort values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns false, and touches nothing, when the installed world has not published its base
    /// stats. That is the §51 rule kept rather than dropped: this run has <c>shuffleBaseStats</c>
    /// on, so a stat worked out from PKHeX's table is not an approximation but a wrong number
    /// written into somebody's Pokémon — 168 PS came back as 151 the day that was tried. What
    /// changed is that the world's own table is now readable (<see cref="WorldLimits.BaseStats"/>),
    /// so the honest answer is usually available instead of never.
    /// </para>
    /// <para>
    /// The current PS are the delicate part. They follow the maximum up by the same amount, which
    /// is what the game does on a level up — but <b>a Pokémon at zero stays at zero</b>. In this
    /// project zero PS is what a death IS (§98), so healing one here would quietly undo a death
    /// through a screen that has nothing to do with dying.
    /// </para>
    /// </remarks>
    private static bool Restat(PK7 pokemon)
    {
        if (WorldLimits.BaseStatsOf(pokemon.Species) is not { } bases)
        {
            return false;
        }

        var stats = StatCalculator.Compute(
            bases,
            [pokemon.IV_HP, pokemon.IV_ATK, pokemon.IV_DEF, pokemon.IV_SPA, pokemon.IV_SPD, pokemon.IV_SPE],
            [pokemon.EV_HP, pokemon.EV_ATK, pokemon.EV_DEF, pokemon.EV_SPA, pokemon.EV_SPD, pokemon.EV_SPE],
            pokemon.Stat_Level,
            (int)pokemon.Nature,
            pokemon.Species == Shedinja);

        var gained = stats[0] - pokemon.Stat_HPMax;

        pokemon.Stat_HPMax = stats[0];
        pokemon.Stat_ATK = stats[1];
        pokemon.Stat_DEF = stats[2];
        pokemon.Stat_SPA = stats[3];
        pokemon.Stat_SPD = stats[4];
        pokemon.Stat_SPE = stats[5];

        if (pokemon.Stat_HPCurrent > 0)
        {
            pokemon.Stat_HPCurrent = Math.Clamp(pokemon.Stat_HPCurrent + gained, 1, stats[0]);
        }

        return true;
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
