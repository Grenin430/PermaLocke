using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <param name="Species">Species id, as the wonder trade event recorded it.</param>
public sealed record TradedAwayCandidate(
    Guid Id, int Species, string SpeciesName, string? Nickname, DateTimeOffset ObtainedAt);

/// <param name="HandedOver">Wonder trades in the history, by what each event said it gave away.</param>
/// <param name="AliveWithoutPid">Records still counted alive that are not in the save any more.</param>
/// <param name="Matched">The ones that can be closed without guessing.</param>
/// <param name="Disputed">Species where the two counts disagree, in words. These are left alone.</param>
/// <param name="Written">True when the repair actually ran.</param>
public sealed record TradedAwayReport(
    int HandedOver,
    int AliveWithoutPid,
    IReadOnlyList<TradedAwayCandidate> Matched,
    IReadOnlyList<string> Disputed,
    bool Written,
    string Message);

/// <summary>
/// Closes the records of Pokémon handed over in a wonder trade that the run still counts as alive.
/// </summary>
/// <remarks>
/// <para>
/// A wonder trade registers the Pokémon that <b>arrives</b> and used to say nothing about the one
/// that <b>leaves</b>, so its record stayed <see cref="PokemonStatus.Alive"/> for ever. In the real
/// run that was 28 Pokémon that are not in the game at all, and HOME counting them among the
/// living is the same class of error as not counting a death: a number describing something other
/// than what it claims to.
/// </para>
/// <para>
/// The deliveries themselves were fixed at the source, so this only ever has to deal with what
/// happened before. It is a repair, not a rule.
/// </para>
/// <para>
/// <b>How they are identified, and what it refuses to do.</b> The species comes from what the
/// wonder trade event itself wrote down as handed over — not deduced from anything — and is matched
/// against the records with no PID, which now that everything living in the save has one is exactly
/// the set that is no longer there. When the two counts for a species disagree, that species is
/// <b>reported and left alone</b>: choosing which of two Giratina left would be inventing history
/// inside a log chained by hash.
/// </para>
/// </remarks>
public sealed class TradedAwayReconciler(
    IPokemonRepository pokemon,
    IEventStore events,
    IClock clock)
{
    /// <summary>Works out what could be closed, and writes nothing.</summary>
    public Task<TradedAwayReport> InspectAsync(Run run, CancellationToken ct = default)
        => RunAsync(run, write: false, ct);

    /// <summary>Closes what can be closed without guessing, leaving an event for each.</summary>
    public Task<TradedAwayReport> RepairAsync(Run run, CancellationToken ct = default)
        => RunAsync(run, write: true, ct);

    private async Task<TradedAwayReport> RunAsync(Run run, bool write, CancellationToken ct)
    {
        var registered = await pokemon.GetAllAsync(run.Id, ct);
        var history = await events.GetAllAsync(run.Id, ct);

        // Lo entregado, segun lo que el propio evento guardo. No se deduce de nada.
        var handedOver = history
            .Where(e => e.Type == GameEventType.WonderTrade)
            .Select(e => e.Data.TryGetValue("entregado", out var species)
                         && int.TryParse(species, out var id) ? id : 0)
            .Where(id => id > 0)
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        // Sin PID = no esta en la partida, ahora que todo lo que vive en ella tiene uno.
        var gone = registered
            .Where(p => p.Pid is null or 0 && p.Status == PokemonStatus.Alive)
            .GroupBy(p => p.Species)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.ObtainedAt).ToList());

        var matched = new List<PokemonEntry>();
        var disputed = new List<string>();

        foreach (var (species, count) in handedOver)
        {
            var candidates = gone.TryGetValue(species, out var list) ? list : [];

            if (candidates.Count != count)
            {
                disputed.Add($"{candidates.FirstOrDefault()?.SpeciesName ?? $"Especie {species}"}: "
                             + "no se puede saber cuál fue, así que no se toca.");
                continue;
            }

            matched.AddRange(candidates);
        }

        var found = matched
            .Select(p => new TradedAwayCandidate(p.Id, p.Species, p.SpeciesName, p.Nickname, p.ObtainedAt))
            .ToList();

        if (!write)
        {
            return new TradedAwayReport(handedOver.Values.Sum(),
                gone.Values.Sum(l => l.Count), found, disputed, false,
                found.Count == 0
                    ? "No hay nada que arreglar."
                    : $"Hay {found.Count} Pokémon intercambiados por quitar.");
        }

        foreach (var entry in matched)
        {
            ct.ThrowIfCancellationRequested();

            await pokemon.SaveAsync(entry with { Status = PokemonStatus.Traded }, ct);

            await events.AppendAsync(new GameEvent
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                Timestamp = clock.Now,
                Type = GameEventType.PokemonTraded,
                Source = EventSource.System,
                Actor = run.PlayerName,
                Description = $"{entry.Nickname ?? entry.SpeciesName} se entregó en un wonder trade "
                              + "y ya no está en la partida.",
                PokemonId = entry.Id,
                Data = new Dictionary<string, string>
                {
                    ["especie"] = entry.Species.ToString(),
                    ["motivo"] = "reparacion: entregado en wonder trade y contado como vivo"
                }
            }, ct);
        }

        // Se relee: no se da por escrito lo que no se ha vuelto a ver.
        var after = await pokemon.GetAllAsync(run.Id, ct);
        var stillOpen = after.Count(p => matched.Any(m => m.Id == p.Id) && p.Status != PokemonStatus.Traded);

        return new TradedAwayReport(handedOver.Values.Sum(), gone.Values.Sum(l => l.Count),
            found, disputed, stillOpen == 0,
            stillOpen == 0
                ? $"{found.Count} Pokémon quitados de tus vivos."
                : $"No se han podido guardar {stillOpen}. Vuelve a intentarlo.");
    }
}
