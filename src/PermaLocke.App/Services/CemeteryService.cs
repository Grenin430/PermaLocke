using System.IO;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Services;

/// <summary>One of the run's fallen, with what the run knows about how it went.</summary>
/// <param name="FellTo">The rival it fell to, when the battle had only one; null otherwise.</param>
/// <param name="BattleAgainst">Every rival of the battle it fell in, when the battle was recorded.</param>
/// <param name="HasBattleRecord">Whether the death was recorded by a version that kept the battle -- the ones that also record a killcam.</param>
/// <param name="KillcamPath">The replay of its death, or null when there is none.</param>
public sealed record Grave(
    Guid PokemonId,
    string Name,
    string SpeciesName,
    int Species,
    bool IsShiny,
    PokemonOrigin Origin,
    EncounterType Encounter,
    DateTimeOffset ObtainedAt,
    DateTimeOffset? DiedAt,
    string? FellTo,
    IReadOnlyList<string> BattleAgainst,
    string HowItWasSeen,
    bool HasBattleRecord,
    int Penalty,
    string? KillcamPath,
    int Form = 0);

/// <summary>
/// The run's fallen, told from the run's own record.
/// </summary>
/// <remarks>
/// Nothing here is new data. The fall is the <see cref="GameEventType.PokemonDied"/> event, the cost is
/// the penalty events of that Pokémon as they were charged, the rivals are what the battle tables held at
/// the moment and the event kept, and the replay is the file the killcam wrote. Deaths from before any of
/// that existed simply lack it, and the cemetery says so instead of filling the gap.
/// </remarks>
public sealed class CemeteryService(IRunContext runs, IPokemonRepository pokemon, IEventStore events,
    ISpeciesLookup species, AppPaths paths)
{
    public async Task<IReadOnlyList<Grave>> GravesAsync(CancellationToken ct = default)
    {
        if (runs.Current is not { } run)
        {
            return [];
        }

        var fallen = (await pokemon.GetAllAsync(run.Id, ct)).Where(entry => entry.Status == PokemonStatus.Dead).ToList();

        if (fallen.Count == 0)
        {
            return [];
        }

        var history = await events.GetAllAsync(run.Id, ct);

        return [.. fallen
            .Select(entry => Tell(run.Id, entry, history))
            .OrderBy(grave => grave.DiedAt ?? DateTimeOffset.MaxValue)];
    }

    private Grave Tell(Guid runId, PokemonEntry entry, IReadOnlyList<GameEvent> history)
    {
        var death = history.LastOrDefault(e => e.Type == GameEventType.PokemonDied && e.PokemonId == entry.Id);
        var penalty = -history.Where(e => e.Type == GameEventType.PointsPenalty && e.PokemonId == entry.Id).Sum(e => e.PointsDelta);

        var rivals = death?.Data.TryGetValue("rivales", out var list) == true
            ? [.. list.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.TryParse(id, out var number) ? species.GetName(number) : id)]
            : new List<string>();

        var killcam = KillcamClip.PathFor(paths.Saves, runId, entry.Id);

        return new Grave(
            entry.Id,
            entry.Nickname ?? entry.SpeciesName,
            entry.SpeciesName,
            entry.Species,
            entry.IsShiny,
            entry.Origin,
            entry.EncounterType,
            entry.ObtainedAt,
            entry.DiedAt ?? death?.Timestamp,
            rivals.Count == 1 ? rivals[0] : null,
            rivals,
            HowItWasSeen(death),
            death?.Data.ContainsKey("rivales") == true,
            Math.Max(0, penalty),
            File.Exists(killcam) ? killcam : null,
            entry.Form);
    }

    private static string HowItWasSeen(GameEvent? death)
    {
        if (death is null)
        {
            return "Sin registro de la muerte";
        }

        if (death.Source == EventSource.Player)
        {
            return "Marcada a mano";
        }

        return death.Data.TryGetValue("deteccion", out var detection) && detection.StartsWith("combate", StringComparison.Ordinal)
            ? "En el combate, en el momento"
            : "Vista en el equipo a 0 PS";
    }
}
