using Microsoft.Extensions.Logging;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink;

/// <summary>
/// Writes a nature into the player's save, in the slot the Pokémon already occupies: the herbs of the shop (2026-09-27).
/// </summary>
/// <remarks>
/// The guards of <see cref="SaveRenamer"/>: game closed, same Pokémon by PID, checksum valid, the whole save copied first,
/// put back with <see cref="PokemonBuilder.InPlace"/> so no record counts a capture (§42), and read back after. A party
/// member stores its stats, so they are worked out again with <see cref="StatCalculator.Restat"/> from the installed
/// world's base stats; the current PS keep their distance to the maximum, and a Pokémon at zero stays at zero.
/// </remarks>
public sealed class SaveNatureChanger(PlayerSave save, string backupFolder, ILogger<SaveNatureChanger> logger) : INatureChanger
{
    public bool CanChangeNow(out string reason)
    {
        if (save.IsGameLoaded())
        {
            reason = "El juego está abierto. Guarda y cierra Azahar para usar la hierba.";
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

    public Task<DeliveryResult> ApplyAsync(NatureChange change, CancellationToken ct = default) =>
        Task.Run(() => CanChangeNow(out var reason)
            ? ApplyIn(save.Find()!, change)
            : new DeliveryResult(save.IsGameLoaded() ? DeliveryOutcome.GameRunning : DeliveryOutcome.SaveNotFound, reason), ct);

    /// <summary>Writes into one specific save file, so the whole edit can be exercised on a copy.</summary>
    public DeliveryResult ApplyIn(string path, NatureChange change)
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
            File.Copy(path, Path.Combine(backupFolder, $"main-{DateTime.Now:yyyyMMdd-HHmmss-fff}-hierba.sav"), overwrite: false);
            File.WriteAllBytes(path, game.Write().ToArray());

            if (!SaveUtil.TryGetSaveFile(path, out var reread) || reread is not SAV7USUM check
                || Read(check, change.Box, change.Slot) is not PK7 written || written.PID != change.Pid
                || !written.ChecksumValid || (int)written.Nature != change.Nature)
            {
                return new DeliveryResult(DeliveryOutcome.Failed, "No se ha podido guardar la naturaleza. Vuelve a intentarlo.");
            }

            logger.LogInformation("{Name} ({Pid:X8}) en {Where}: naturaleza -> {Nature}", change.Name, change.Pid, change.Where,
                change.Nature);
            return applied;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló cambiar la naturaleza de {Name}", change.Name);
            return new DeliveryResult(DeliveryOutcome.Failed, "No se pudo escribir en la partida.");
        }
    }

    /// <summary>Applies the change to an already opened save, without writing anything to disk.</summary>
    public DeliveryResult ApplyTo(SAV7USUM game, NatureChange change)
    {
        if (change.Nature is < 0 or > 24)
        {
            return new DeliveryResult(DeliveryOutcome.Failed, "Esa naturaleza no existe.");
        }

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
            return new DeliveryResult(DeliveryOutcome.Failed, $"{change.Name} no se puede tocar.");
        }

        pokemon.Nature = (Nature)change.Nature;
        var party = change.Box == BoxedPokemon.PartyBox;
        var restated = party && StatCalculator.Restat(pokemon);
        pokemon.RefreshChecksum();

        if (party)
        {
            game.SetPartySlotAtIndex(pokemon, change.Slot, PokemonBuilder.InPlace);
        }
        else
        {
            game.SetBoxSlotAtIndex(pokemon, change.Box, change.Slot, PokemonBuilder.InPlace);
        }

        return new DeliveryResult(DeliveryOutcome.Delivered,
            party && !restated ? "Sus estadísticas se actualizarán al subir de nivel." : string.Empty,
            change.Box + 1, change.Slot + 1, change.Pid);
    }

    private static PKM? Read(SAV7USUM game, int box, int slot) =>
        box == BoxedPokemon.PartyBox
            ? slot >= 0 && slot < game.PartyCount ? game.GetPartySlotAtIndex(slot) : null
            : game.GetBoxSlotAtIndex(box, slot);
}
