using Microsoft.Extensions.Logging;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink;

/// <summary>
/// Writes a nickname into the player's save, in the slot the Pokémon already occupies (2026-09-26).
/// </summary>
/// <remarks>
/// The guards of <see cref="SaveMoveTeacher"/>: game closed, same Pokémon by PID, checksum valid, the whole save copied
/// first, put back with <see cref="PokemonBuilder.InPlace"/> so no record counts a capture (§42), and read back after.
/// An empty name clears the nickname, which gives the Pokémon back its species name as the game's name rater does.
/// </remarks>
public sealed class SaveRenamer(PlayerSave save, string backupFolder, ILogger<SaveRenamer> logger) : IPokemonRenamer
{
    public bool CanRenameNow(out string reason)
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

    public Task<DeliveryResult> ApplyAsync(NicknameChange change, CancellationToken ct = default) =>
        Task.Run(() => CanRenameNow(out var reason)
            ? ApplyIn(save.Find()!, change)
            : new DeliveryResult(save.IsGameLoaded() ? DeliveryOutcome.GameRunning : DeliveryOutcome.SaveNotFound, reason), ct);

    /// <summary>Writes into one specific save file, so the whole edit can be exercised on a copy.</summary>
    public DeliveryResult ApplyIn(string path, NicknameChange change)
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

            Directory.CreateDirectory(backupFolder);
            File.Copy(path, Path.Combine(backupFolder, $"main-{DateTime.Now:yyyyMMdd-HHmmss-fff}-mote.sav"), overwrite: false);
            File.WriteAllBytes(path, game.Write().ToArray());

            if (!SaveUtil.TryGetSaveFile(path, out var reread) || reread is not SAV7USUM check
                || Read(check, change.Box, change.Slot) is not PK7 written || written.PID != change.Pid
                || !written.ChecksumValid || written.Nickname != Expected(written, change))
            {
                return new DeliveryResult(DeliveryOutcome.Failed, "No se ha podido guardar el mote. Vuelve a intentarlo.");
            }

            logger.LogInformation("{Name} ({Pid:X8}) en {Where}: mote -> «{Nickname}»", change.Name, change.Pid, change.Where,
                change.Nickname);
            return applied;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló cambiar el mote de {Name}", change.Name);
            return new DeliveryResult(DeliveryOutcome.Failed, "No se pudo escribir en la partida.");
        }
    }

    /// <summary>Applies the change to an already opened save, without writing anything to disk.</summary>
    public DeliveryResult ApplyTo(SAV7USUM game, NicknameChange change)
    {
        if (Read(game, change.Box, change.Slot) is not PK7 { Species: > 0 } pokemon)
        {
            return new DeliveryResult(DeliveryOutcome.SlotChanged, $"En {change.Where} no hay ningún Pokémon. Vuelve a intentarlo.");
        }

        if (pokemon.PID != change.Pid)
        {
            return new DeliveryResult(DeliveryOutcome.SlotChanged, $"En {change.Where} ya no está {change.Name}. Vuelve a intentarlo.");
        }

        if (!pokemon.ChecksumValid || pokemon.IsEgg)
        {
            return new DeliveryResult(DeliveryOutcome.Failed, $"{change.Name} no se puede renombrar.");
        }

        if (change.Nickname.Length == 0)
        {
            pokemon.ClearNickname();
        }
        else
        {
            pokemon.SetNickname(change.Nickname);
        }

        pokemon.RefreshChecksum();

        if (change.Box == BoxedPokemon.PartyBox)
        {
            game.SetPartySlotAtIndex(pokemon, change.Slot, PokemonBuilder.InPlace);
        }
        else
        {
            game.SetBoxSlotAtIndex(pokemon, change.Box, change.Slot, PokemonBuilder.InPlace);
        }

        return new DeliveryResult(DeliveryOutcome.Delivered, "Mote cambiado.", change.Box + 1, change.Slot + 1, change.Pid);
    }

    private static string Expected(PK7 written, NicknameChange change) =>
        change.Nickname.Length == 0 ? written.Nickname : change.Nickname;

    private static PKM? Read(SAV7USUM game, int box, int slot) =>
        box == BoxedPokemon.PartyBox
            ? slot >= 0 && slot < game.PartyCount ? game.GetPartySlotAtIndex(slot) : null
            : game.GetBoxSlotAtIndex(box, slot);
}
