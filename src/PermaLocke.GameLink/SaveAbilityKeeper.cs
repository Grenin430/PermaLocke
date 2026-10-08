using PKHeX.Core;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink;

/// <summary>
/// Puts back, in a closed save, the ability PermaLocke gave a Pokémon when the game has recomputed it since (§237).
/// </summary>
/// <remarks>
/// Only with the game closed, the whole save copied first and read back after, like every other save writer. Matched by
/// PID, party and boxes alike (a Pokémon that evolved or megaevolved is wherever the player left it). Only the ability and
/// its checksum change; the ability slot bits are kept, because the game keeps them too.
/// </remarks>
public static class SaveAbilityKeeper
{
    private const int InParty = -1;

    /// <summary>One Pokémon found with an ability other than the one it was given.</summary>
    public sealed record Fix(uint Pid, string Name, int Was, int Should);

    /// <summary>What would change, without touching anything.</summary>
    public static IReadOnlyList<Fix> Pending(SAV7USUM game, IReadOnlyDictionary<uint, int> intended)
    {
        var found = new List<Fix>();

        foreach (var (pokemon, _, _) in Every(game))
        {
            if (pokemon.Species > 0 && !pokemon.IsEgg && intended.TryGetValue(pokemon.PID, out var should)
                && PokemonAbility.Of(pokemon) is var was && was != should)
            {
                found.Add(new Fix(pokemon.PID, pokemon.Nickname, was, should));
            }
        }

        return found;
    }

    /// <summary>Restores them and writes the save. Empty when there was nothing to do or the save could not be read.</summary>
    public static IReadOnlyList<Fix> Restore(string path, string backupFolder, IReadOnlyDictionary<uint, int> intended)
    {
        if (intended.Count == 0 || !SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
        {
            return [];
        }

        var pending = Pending(game, intended);
        if (pending.Count == 0) return [];

        Directory.CreateDirectory(backupFolder);
        File.Copy(path, Path.Combine(backupFolder, $"main-{DateTime.Now:yyyyMMdd-HHmmss-fff}-habilidades.sav"), overwrite: false);

        Apply(game, intended);
        File.WriteAllBytes(path, game.Write().ToArray());

        // Releída: solo vale lo que está en el fichero.
        return SaveUtil.TryGetSaveFile(path, out var again) && again is SAV7USUM check && Pending(check, intended).Count == 0
            ? pending
            : [];
    }

    /// <summary>The same restore on a save held in memory, so it can be exercised without a file.</summary>
    public static int Apply(SAV7USUM game, IReadOnlyDictionary<uint, int> intended)
    {
        var changed = 0;

        foreach (var (pokemon, box, slot) in Every(game).ToList())
        {
            if (pokemon.Species <= 0 || pokemon.IsEgg || !intended.TryGetValue(pokemon.PID, out var should)
                || PokemonAbility.Of(pokemon) == should)
            {
                continue;
            }

            PokemonAbility.Set(pokemon, should);
            pokemon.RefreshChecksum();

            if (box == InParty) game.SetPartySlotAtIndex(pokemon, slot, PokemonBuilder.InPlace);
            else game.SetBoxSlotAtIndex(pokemon, box, slot, PokemonBuilder.InPlace);

            changed++;
        }

        return changed;
    }

    private static IEnumerable<(PK7 Pokemon, int Box, int Slot)> Every(SAV7USUM game)
    {
        for (var slot = 0; slot < game.PartyCount; slot++)
        {
            if (game.GetPartySlotAtIndex(slot) is PK7 member) yield return (member, InParty, slot);
        }

        for (var box = 0; box < game.BoxCount; box++)
        {
            for (var slot = 0; slot < game.BoxSlotCount; slot++)
            {
                if (game.GetBoxSlotAtIndex(box, slot) is PK7 stored) yield return (stored, box, slot);
            }
        }
    }
}
