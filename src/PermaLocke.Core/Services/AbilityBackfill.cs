using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>
/// What ability each Pokémon was given, worked out from the history for those PermaLocke dealt before it kept a ledger
/// of its own (§237): the gacha and the wonder trade wrote the ability's name into their event.
/// </summary>
public static class AbilityBackfill
{
    /// <summary>
    /// By PID. Only a name that is in <paramref name="abilityNames"/> exactly once counts, so a name the world's list does not
    /// know, or knows twice, is left alone rather than guessed. The latest event of a Pokémon wins.
    /// </summary>
    public static IReadOnlyDictionary<uint, int> Intended(
        IEnumerable<GameEvent> events, IEnumerable<PokemonEntry> entries, IReadOnlyList<string> abilityNames)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(abilityNames);

        var history = events.ToList();

        // Las caras de habilidad de la ruleta cambian la habilidad de un Pokémon sin dejar a quién en el evento: en una run que
        // ya las ha sacado no se sabe cuál habilidad es la vigente, y deducirla del gacha podría deshacer la de la ruleta.
        if (history.Any(e => e.Type == GameEventType.RouletteSpun
                             && e.Data.TryGetValue("efecto", out var effect) && effect is "HabilidadBuena" or "HabilidadMala"))
        {
            return new Dictionary<uint, int>();
        }

        var pidOf = entries.Where(e => e.Pid is > 0).ToDictionary(e => e.Id, e => e.Pid!.Value);
        var found = new Dictionary<uint, int>();

        foreach (var given in history
                     .Where(e => e.Type is GameEventType.GachaRoll or GameEventType.WonderTrade && e.PokemonId is not null)
                     .OrderBy(e => e.Timestamp))
        {
            if (!pidOf.TryGetValue(given.PokemonId!.Value, out var pid)
                || !given.Data.TryGetValue("habilidad", out var name) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var matches = Enumerable.Range(1, Math.Max(0, abilityNames.Count - 1))
                .Where(i => string.Equals(abilityNames[i], name, StringComparison.Ordinal))
                .Take(2)
                .ToList();

            if (matches.Count == 1) found[pid] = matches[0];
        }

        return found;
    }
}
