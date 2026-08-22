using PermaLocke.Core.Abstractions;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Modules;

/// <param name="Trainers">Trainers whose party changed.</param>
/// <param name="Pokemon">Individual trainer Pokémon replaced.</param>
/// <param name="MovesCleared">Entries whose explicit moveset was handed back to the game.</param>
/// <param name="LevelsRaised">Pokémon whose level the role moved.</param>
public sealed record TrainerResult(int Trainers, int Pokemon, int MovesCleared, int LevelsRaised = 0);

/// <summary>
/// Replaces the species of every trainer Pokémon in <c>trpoke</c> (<c>a/1/0/7</c>), and raises
/// their levels by whatever the <b>role</b> asks for.
/// <para>
/// The randomization of species still never touches levels: the competition's caps are read off
/// the Kahuna parties, and moving a level as a side effect of shuffling species would silently
/// move the cap that governs ten players. Raising them is a separate, declared decision that
/// comes from the role and applies to every trainer alike.
/// </para>
/// </summary>
public sealed class TrainerRandomizer(RomWorkspace workspace, RandomizerOptions options)
{
    public async Task<TrainerResult> ApplyAsync(IRandomSource random, SpeciesPool pool,
        LayeredFsMod mod, CancellationToken ct = default)
    {
        var path = mod.Stage(GameFiles.TrainerPokemon);
        var untouchable = options.ProtectedSpecies.ToHashSet();

        var trainers = 0;
        var replaced = 0;
        var movesCleared = 0;
        var levelsRaised = 0;

        using (var patcher = new GarcPatcher(path))
        {
            for (var trainer = 0; trainer < patcher.FileCount; trainer++)
            {
                ct.ThrowIfCancellationRequested();

                var party = patcher.Read(trainer);
                var count = TrainerPokemonTable.Count(party);
                if (count == 0)
                {
                    continue; // the cartridge holds one six byte subfile with no party at all
                }

                var changed = false;
                for (var slot = 0; slot < count; slot++)
                {
                    // El nivel lo sube el ROL, no la randomización, y se sube SIEMPRE: también en
                    // los Pokémon protegidos, porque un Cosmog al nivel del cartucho en un juego
                    // donde todo lo demás va un 20% por encima sería un regalo, no una protección.
                    if (options.TrainerLevelPercent > 0)
                    {
                        var raised = Raise(TrainerPokemonTable.GetLevel(party, slot),
                            options.TrainerLevelPercent);

                        if (raised != TrainerPokemonTable.GetLevel(party, slot))
                        {
                            TrainerPokemonTable.SetLevel(party, slot, raised);
                            levelsRaised++;
                            changed = true;
                        }
                    }

                    var original = TrainerPokemonTable.GetSpecies(party, slot);
                    if (original == 0 || untouchable.Contains(original))
                    {
                        continue;
                    }

                    TrainerPokemonTable.SetSpecies(party, slot, pool.Pick(random, original));
                    replaced++;
                    changed = true;

                    // A moveset chosen for the old species means nothing on the new one.
                    if (options.TrainerMovesFromLearnset && TrainerPokemonTable.HasExplicitMoves(party, slot))
                    {
                        TrainerPokemonTable.ClearMoves(party, slot);
                        movesCleared++;
                    }
                }

                if (!changed)
                {
                    continue;
                }

                patcher.Write(trainer, party);
                trainers++;
            }
        }

        await VerifyAsync(path, ct);
        return new TrainerResult(trainers, replaced, movesCleared, levelsRaised);
    }

    /// <summary>
    /// A cartridge level raised by a percentage, rounded away from zero and capped at 100.
    /// </summary>
    /// <remarks>
    /// Rounding away from zero matters at the bottom of the game: the first trainers are level 5,
    /// and rounding down would leave +20% meaning nothing at all for the whole first island.
    /// </remarks>
    public static int Raise(int level, int percent) =>
        Math.Clamp((int)Math.Round(level * (1 + (percent / 100.0)), MidpointRounding.AwayFromZero), 1, 100);

    /// <summary>
    /// Reads the result back with the pk3DS reader and checks the two things that would ruin a
    /// run: a banned species in a party, or a party that changed size.
    /// </summary>
    private async Task VerifyAsync(string path, CancellationToken ct)
    {
        var vanilla = new GARC.LazyGARC(await File.ReadAllBytesAsync(
            workspace.PathOf(GameFiles.TrainerPokemon), ct));
        var patched = new GARC.LazyGARC(await File.ReadAllBytesAsync(path, ct));

        if (patched.FileCount != vanilla.FileCount)
        {
            throw new InvalidDataException(
                $"trpoke se quedó con {patched.FileCount} entrenadores en vez de {vanilla.FileCount}.");
        }

        var banned = options.BannedSpecies.ToHashSet();
        var untouchable = options.ProtectedSpecies.ToHashSet();

        for (var trainer = 0; trainer < patched.FileCount; trainer++)
        {
            var party = patched[trainer];
            if (party.Length != vanilla[trainer].Length)
            {
                throw new InvalidDataException(
                    $"El equipo del entrenador {trainer} cambió de tamaño: {vanilla[trainer].Length} -> {party.Length}.");
            }

            for (var slot = 0; slot < TrainerPokemonTable.Count(party); slot++)
            {
                var species = TrainerPokemonTable.GetSpecies(party, slot);
                if (species != 0 && banned.Contains(species) && !untouchable.Contains(species))
                {
                    throw new InvalidDataException(
                        $"El randomizador dejó la especie prohibida {species} en el entrenador {trainer}, hueco {slot}.");
                }
            }
        }
    }
}
