namespace PermaLocke.GameLink.Battle;

/// <summary>A Pokémon that has just fainted in the battle in progress.</summary>
public sealed record BattleFaint(int BattleId, int Species, bool IsPlayers);

/// <summary>
/// Decides, from successive readings of the battle tables, the moment a Pokémon faints.
/// </summary>
/// <remarks>
/// <para>
/// There are two tables and they do not agree in time, measured on one hit: the calculation table
/// drops at once (Salamence 199 → 173) and the display table follows the HP bar, up to three seconds
/// later and through intermediate values (199 → 197 → 173). <b>A faint is when both say zero.</b> The
/// later of the two is the bar reaching empty, which is the moment the player sees, and requiring both
/// means neither can report one alone.
/// </para>
/// <para>
/// Only a <b>fall</b> counts: a Pokémon has to be seen standing in this battle before its zero means
/// anything. The fallen of the run sit at zero in the tables from the first reading, and they are not
/// news. And each position is reported once per battle.
/// </para>
/// <para>
/// A battle is in progress when <b>at least two tables hold an opponent</b>. The display table outlives
/// the battle, with the party's last HP still in it, and a Pokémon healed later at a Pokémon Center
/// would read as fainted there for ever; its opponent's block, however, is reused as soon as the
/// battle ends. Without that gate the leftovers would be deaths.
/// </para>
/// </remarks>
public sealed class BattleFaintTracker
{
    private readonly HashSet<int> _seenStanding = [];
    private readonly HashSet<int> _reported = [];
    private uint[] _origins = [];

    /// <summary>Whether the last reading was a battle in progress.</summary>
    public bool InBattle { get; private set; }

    /// <summary>The tables of the battle in progress, or none.</summary>
    public static IReadOnlyList<BattleTable> InProgress(IReadOnlyList<BattleTable> tables)
    {
        var live = tables.Where(table => table.HasOpponent).ToList();

        return live.Count >= 2 ? live : [];
    }

    public IReadOnlyList<BattleFaint> Observe(IReadOnlyList<BattleTable> tables)
    {
        var live = InProgress(tables);

        if (live.Count == 0)
        {
            Forget();
            return [];
        }

        // Otras tablas son otro combate: lo visto en el anterior no vale en este.
        var origins = live.Select(table => table.Origin).Order().ToArray();

        if (!origins.SequenceEqual(_origins))
        {
            Forget();
            _origins = origins;
        }

        InBattle = true;

        var faints = new List<BattleFaint>();

        foreach (var id in live.SelectMany(table => table.Blocks).Select(block => block.BattleId).Distinct().Order())
        {
            var blocks = live.Select(table => table.Block(id)).ToList();

            // Una posición que falta en una tabla, o que no es el mismo Pokémon en las dos, no se
            // juzga: se espera a la siguiente lectura.
            if (blocks.Any(block => block is null) || blocks.Select(block => block!.Species).Distinct().Count() > 1)
            {
                continue;
            }

            if (blocks.Any(block => block!.CurrentHp > 0))
            {
                _seenStanding.Add(id);
                continue;
            }

            if (_seenStanding.Contains(id) && _reported.Add(id))
            {
                faints.Add(new BattleFaint(id, blocks[0]!.Species, blocks[0]!.IsPlayers));
            }
        }

        return faints;
    }

    private void Forget()
    {
        InBattle = false;
        _seenStanding.Clear();
        _reported.Clear();
        _origins = [];
    }
}
