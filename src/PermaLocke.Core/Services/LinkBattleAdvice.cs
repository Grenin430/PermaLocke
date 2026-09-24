using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <param name="Note">Who can link-battle whom, in words. Empty with nobody published.</param>
/// <param name="State">"ok", "warn" or "none", for the colour band.</param>
public sealed record LinkBattleNote(string Note, string State);

/// <summary>
/// Whether the published players can link-battle each other, said once for the whole group.
/// </summary>
/// <remarks>
/// <para>
/// It used to judge each run by its randomizer options: shuffled stats or random abilities meant
/// «cannot battle». That was the §80 answer, and COMBATES made it obsolete — it sets the randomized
/// world aside and installs the base game for the battle, so what each world randomized no longer
/// matters. The player saw it say «Grenin no puede» about a run that could (§124).
/// </para>
/// <para>
/// What still has to match is the <b>base game</b>: the gen 8-9 expansion on one side and the plain
/// cartridge on the other are different games, and they drift apart exactly like two randomizations
/// did. That is read from how many species the world was generated with — 1025 with the expansion,
/// 807 without — which the randomization event records and the snapshot carries.
/// </para>
/// </remarks>
public static class LinkBattleAdvice
{
    public static LinkBattleNote Summarize(IReadOnlyList<RunSnapshot> players)
    {
        if (players.Count == 0)
        {
            return new LinkBattleNote(string.Empty, "none");
        }

        const string how = "Para pelear, entrad todos en COMBATES antes de abrir Azahar.";

        var known = players.Where(p => p.WorldSpecies is > 0).ToList();
        var unknown = players.Where(p => p.WorldSpecies is null or <= 0).ToList();
        var bases = known.GroupBy(p => p.WorldSpecies!.Value).OrderByDescending(g => g.Key).ToList();

        var parts = new List<string> { how };

        if (bases.Count > 1)
        {
            parts.Add("Pero no tenéis todos el mismo juego: "
                      + string.Join("; ", bases.Select(g => $"{Names(g.ToList())} con {BaseName(g.Key)}"))
                      + ". Así no se puede pelear.");
        }

        if (unknown.Count > 0)
        {
            parts.Add($"De {Names(unknown)} no se sabe qué juego "
                      + (unknown.Count == 1 ? "tiene." : "tienen."));
        }

        if (players.Count == 1)
        {
            parts.Add("Todavía no hay nadie más con quien pelear.");
        }

        var state = bases.Count > 1 || unknown.Count > 0 ? "warn" : players.Count > 1 ? "ok" : "none";
        return new LinkBattleNote(string.Join(" ", parts), state);
    }

    private static string BaseName(int species) => species > 807
        ? "las generaciones 8 y 9"
        : "el cartucho sin mod";

    private static string Names(IReadOnlyList<RunSnapshot> who) => string.Join(", ", who.Select(s => s.PlayerName));
}
