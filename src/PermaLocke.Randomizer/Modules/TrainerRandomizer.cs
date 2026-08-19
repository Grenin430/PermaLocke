using PermaLocke.Core.Abstractions;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Modules;

/// <param name="Trainers">Trainers whose party changed.</param>
/// <param name="Pokemon">Individual trainer Pokémon replaced.</param>
/// <param name="MovesCleared">Entries whose explicit moveset was handed back to the game.</param>
public sealed record TrainerResult(int Trainers, int Pokemon, int MovesCleared);

/// <summary>
/// Replaces the species of every trainer Pokémon in <c>trpoke</c> (<c>a/1/0/7</c>).
/// <para>
/// Levels are never touched. The competition's level caps are read off the Kahuna parties, so
/// moving a trainer's level would silently move the cap that governs ten players.
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
        return new TrainerResult(trainers, replaced, movesCleared);
    }

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
