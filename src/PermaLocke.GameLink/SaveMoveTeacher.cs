using Microsoft.Extensions.Logging;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink;

/// <summary>
/// Writes one remembered move into the player's save, in the slot the Pokémon already occupies.
/// </summary>
/// <remarks>
/// <para>
/// The EV writer's guards, one for one (<see cref="SaveEvTrainer"/>): the game closed, the slot checked to still hold
/// the <b>same Pokémon</b> by PID, the whole save copied first, and the file read back afterwards. One more, because
/// this overwrites something: the move slot has to still hold the move the screen showed. A player who changed moves
/// in the game between reading and writing would otherwise lose one they never chose to forget.
/// </para>
/// <para>
/// The move gets its full PP and loses the PP Ups of the move it replaces, which is what the game's own reminder
/// does. The PP come from the caller, who read them from the installed world: PKHeX stops at the cartridge's moves
/// and would give a gen 8-9 move zero, which is Struggle waiting to happen.
/// </para>
/// </remarks>
public sealed class SaveMoveTeacher(PlayerSave save, string backupFolder, ILogger<SaveMoveTeacher> logger)
    : IMoveTeacher
{
    public bool CanTeachNow(out string reason)
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

    public Task<DeliveryResult> ApplyAsync(MoveChange change, CancellationToken ct = default) =>
        Task.Run(() => Apply(change), ct);

    private DeliveryResult Apply(MoveChange change)
    {
        if (!CanTeachNow(out var reason))
        {
            return new DeliveryResult(
                save.IsGameLoaded() ? DeliveryOutcome.GameRunning : DeliveryOutcome.SaveNotFound, reason);
        }

        return ApplyIn(save.Find()!, change);
    }

    /// <summary>Writes into one specific save file, so the whole edit can be exercised on a copy.</summary>
    public DeliveryResult ApplyIn(string path, MoveChange change)
    {
        try
        {
            if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
            {
                return new DeliveryResult(DeliveryOutcome.SaveUnreadable, "No se ha podido leer tu partida.");
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
                    "No se ha podido guardar el movimiento. Vuelve a intentarlo.");
            }

            logger.LogInformation("{Name} ({Pid:X8}) en {Where}: movimiento {Slot} {Old} -> {New} ({PP} PP)",
                change.Name, change.Pid, change.Where, change.MoveSlot + 1, change.Replaced, change.Move, change.PP);

            return applied;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el recuerda-movimientos de {Name}", change.Name);
            return new DeliveryResult(DeliveryOutcome.Failed, "No se pudo escribir en la partida.");
        }
    }

    /// <summary>Applies the change to an already opened save, without writing anything to disk.</summary>
    public DeliveryResult ApplyTo(SAV7USUM game, MoveChange change)
    {
        if (change.MoveSlot is < 0 or > 3 || change.Move <= 0 || change.PP <= 0)
        {
            return new DeliveryResult(DeliveryOutcome.Failed, "Ese movimiento no se puede escribir.");
        }

        if (Read(game, change.Box, change.Slot) is not PK7 { Species: > 0 } pokemon)
        {
            return new DeliveryResult(DeliveryOutcome.SlotChanged,
                $"En {change.Where} no hay ningún Pokémon. Vuelve a intentarlo.");
        }

        if (pokemon.PID != change.Pid)
        {
            return new DeliveryResult(DeliveryOutcome.SlotChanged,
                $"En {change.Where} ya no está {change.Name}. Vuelve a intentarlo.");
        }

        if (!pokemon.ChecksumValid)
        {
            return new DeliveryResult(DeliveryOutcome.Failed,
                $"{change.Name} está dañado en la partida y no se toca.");
        }

        if (pokemon.GetMove(change.MoveSlot) != change.Replaced)
        {
            return new DeliveryResult(DeliveryOutcome.SlotChanged,
                $"Los movimientos de {change.Name} han cambiado desde que se leyó la partida. Vuelve a intentarlo.");
        }

        if (Enumerable.Range(0, 4).Any(slot => slot != change.MoveSlot && pokemon.GetMove(slot) == change.Move))
        {
            return new DeliveryResult(DeliveryOutcome.Failed, $"{change.Name} ya sabe ese movimiento.");
        }

        pokemon.SetMove(change.MoveSlot, (ushort)change.Move);
        SetPP(pokemon, change.MoveSlot, change.PP);

        pokemon.RefreshChecksum();
        Store(game, pokemon, change.Box, change.Slot);

        return new DeliveryResult(DeliveryOutcome.Delivered, $"{change.Name} ya lo sabe.", change.Box + 1,
            change.Slot + 1, change.Pid);
    }

    private static void SetPP(PK7 pokemon, int slot, int pp)
    {
        switch (slot)
        {
            case 0:
                pokemon.Move1_PP = pp;
                pokemon.Move1_PPUps = 0;
                break;
            case 1:
                pokemon.Move2_PP = pp;
                pokemon.Move2_PPUps = 0;
                break;
            case 2:
                pokemon.Move3_PP = pp;
                pokemon.Move3_PPUps = 0;
                break;
            default:
                pokemon.Move4_PP = pp;
                pokemon.Move4_PPUps = 0;
                break;
        }
    }

    private static int PPOf(PK7 pokemon, int slot) => slot switch
    {
        0 => pokemon.Move1_PP,
        1 => pokemon.Move2_PP,
        2 => pokemon.Move3_PP,
        _ => pokemon.Move4_PP
    };

    private static PKM? Read(SAV7USUM game, int box, int slot) =>
        box == BoxedPokemon.PartyBox
            ? slot >= 0 && slot < game.PartyCount ? game.GetPartySlotAtIndex(slot) : null
            : game.GetBoxSlotAtIndex(box, slot);

    /// <summary>Back where it was, with <see cref="PokemonBuilder.InPlace"/> so no record thinks it was caught (§42).</summary>
    private static void Store(SAV7USUM game, PK7 pokemon, int box, int slot)
    {
        if (box == BoxedPokemon.PartyBox)
        {
            game.SetPartySlotAtIndex(pokemon, slot, PokemonBuilder.InPlace);
            return;
        }

        game.SetBoxSlotAtIndex(pokemon, box, slot, PokemonBuilder.InPlace);
    }

    private static bool Verify(string path, MoveChange change)
    {
        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
        {
            return false;
        }

        return Read(game, change.Box, change.Slot) is PK7 written
               && written.PID == change.Pid
               && written.ChecksumValid
               && written.GetMove(change.MoveSlot) == change.Move
               && PPOf(written, change.MoveSlot) == change.PP;
    }

    /// <summary>Copies the whole save before touching it; without a copy nothing is written.</summary>
    private void Backup(string path)
    {
        Directory.CreateDirectory(backupFolder);

        var name = $"main-{DateTime.Now:yyyyMMdd-HHmmss-fff}-mov.sav";
        File.Copy(path, Path.Combine(backupFolder, name), overwrite: false);

        logger.LogInformation("Copia de la partida guardada en {Name}", name);
    }
}
