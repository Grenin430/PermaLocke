using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;

namespace PermaLocke.Randomizer.Modules;

/// <param name="Trades">Trade evolutions turned into something reachable alone.</param>
/// <param name="Moves">Move-taught evolutions given a level instead.</param>
/// <param name="Left">Entries that still need another player, which should be zero.</param>
public sealed record EvolutionFixResult(int Trades, int Moves, int Left);

/// <summary>
/// Rewrites the evolutions a solo player can never reach.
/// </summary>
/// <remarks>
/// <para>
/// A Nuzlocke is played alone, so an evolution that needs a second console is an evolution that
/// does not exist: Kadabra, Machoke, Haunter and twenty-seven others simply stop halfway. The same
/// goes for the nine that wait for a specific move once the learnsets are randomized, because the
/// move they wait for may never be taught.
/// </para>
/// <para>
/// Which entries, and what to replace them with, follows the Universal Pokémon Randomizer ZX list
/// for Sun/Moon/USUM. But the entries are found <b>by method, not by a list of species</b>, and
/// that is not tidiness: the cartridge has four trade entries the list never enumerates — the three
/// Pumpkaboo sizes and Alolan Geodude — and a species list would have left them behind.
/// </para>
/// <para>
/// The method numbers come from the cartridge, not from a table off the internet. Dumping
/// <c>a/0/1/4</c> shows 5 with no argument (Kadabra to Alakazam), 6 carrying an item id (Poliwhirl
/// to Politoed with 221, the King's Rock), 7 for the two that swap, 19 for holding an item by day
/// (Happiny to Chansey with the Oval Stone), 22 for a party member (Mantyke to Mantine with
/// Remoraid) and 8 for a stone (Pikachu to Raichu with 83).
/// </para>
/// </remarks>
public sealed class ImpossibleEvolutionFixer(RandomizerOptions options)
{
    private const int EntrySize = 8;

    private const int Trade = 5;
    private const int TradeHoldingItem = 6;
    private const int TradeForTheOther = 7;
    private const int KnowingMove = 21;

    private const int LevelUp = 4;
    private const int UseItem = 8;
    private const int LevelUpHoldingItemByDay = 19;
    private const int LevelUpWithPartyMember = 22;

    /// <summary>Slowpoke, and the Water Stone it gets instead of the King's Rock.</summary>
    /// <remarks>
    /// The one hand-made exception, and it is the referenced list's. Slowpoke already turns into
    /// Slowbro on levelling, so leaving Slowking on "level up holding a King's Rock" puts two
    /// evolutions on overlapping triggers and makes which one you get depend on slot order. A
    /// stone is a separate trigger, so both stay reachable. Item 84 is Piedra Agua, checked
    /// against the cartridge's own item table rather than typed from memory (§52).
    /// </remarks>
    private const int Slowpoke = 79;

    private const int Slowking = 199;

    private const int WaterStone = 84;

    /// <summary>
    /// The level each move-taught evolution gets instead, keyed by the pair it applies to.
    /// </summary>
    /// <remarks>
    /// Keyed by species <em>and</em> target on purpose: an entry that does not point where this
    /// table expects is left alone rather than handed a level that belongs to something else. All
    /// nine were read out of the cartridge before being written down here.
    /// <para>
    /// The expansion adds six more, and five of them follow the same rule. The sixth, Dipplin to
    /// Hydrapple, learns its move only at level 1, the same situation as Piloswine and Poipole, whose
    /// levels came from the referenced list and not from the learnset. There is no list for it, so the
    /// player decided: 45, like those two.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<(int Species, int Target), int> MoveLevels = new()
    {
        [(108, 463)] = 33, // Lickitung -> Lickilicky, esperaba Rizo Defensa
        [(114, 465)] = 38, // Tangela   -> Tangrowth,  esperaba Poder Pasado
        [(190, 424)] = 32, // Aipom     -> Ambipom,    esperaba Doble Golpe
        [(193, 469)] = 33, // Yanma     -> Yanmega,    esperaba Poder Pasado
        [(221, 473)] = 45, // Piloswine -> Mamoswine,  esperaba Poder Pasado
        [(438, 185)] = 15, // Bonsly    -> Sudowoodo,  esperaba Mimetico
        [(439, 122)] = 15, // Mime Jr.  -> Mr. Mime,   esperaba Mimetico
        [(762, 763)] = 29, // Steenee   -> Tsareena,   esperaba Pisoton
        [(803, 804)] = 45, // Poipole   -> Naganadel,  esperaba Pulso Dragon

        // Las del mod de gen 8-9, con la misma regla: el nivel al que la especie aprende ese
        // movimiento en la capa base sin randomizar. Faltaban y se quedaron imposibles en el mundo
        // instalado: se vieron en la revision del 2026-09-21, que leyo estos niveles de a/0/1/3.
        [(57, 979)] = 35,   // Primeape  -> Annihilape, esperaba Puño Furia
        [(203, 981)] = 32,  // Girafarig -> Farigiraf,  esperaba Láser Doble
        [(234, 899)] = 21,  // Stantler  -> Wyrdeer,    esperaba Asalto Barrera
        [(852, 853)] = 35,  // Clobbopus -> Grapploct,  esperaba Mofa
        [(1100, 904)] = 28, // Qwilfish de Hisui (fila de forma 1100) -> Overqwil, esperaba Mil Púas Tóxicas

        // Este no sale de la tabla: Dipplin aprende Bramido Dragón solo a nivel 1, igual que
        // Piloswine y Poipole, y como ellos va al 45. Lo decidió el jugador el 2026-09-21.
        [(1011, 1019)] = 45, // Dipplin -> Hydrapple, esperaba Bramido Dragón
    };

    public async Task<EvolutionFixResult> ApplyAsync(LayeredFsMod mod, CancellationToken ct = default)
    {
        var path = mod.Stage(GameFiles.Evolution);

        var trades = 0;
        var moves = 0;

        using (var patcher = new GarcPatcher(path))
        {
            var partners = Partners(patcher);

            for (var species = 0; species < patcher.FileCount; species++)
            {
                ct.ThrowIfCancellationRequested();

                var entry = patcher.Read(species);
                var fixedHere = FixSpecies(entry, species, partners);

                trades += fixedHere.Trades;
                moves += fixedHere.Moves;

                if (fixedHere.Trades + fixedHere.Moves > 0)
                {
                    patcher.Write(species, entry);
                }
            }
        }

        return await VerifyAsync(path, trades, moves, ct);
    }

    /// <summary>
    /// Rewrites one species' whole block of entries, in place, and says what it changed.
    /// </summary>
    /// <remarks>
    /// Public because it is the part with the decisions in it, and a decision that can only be
    /// exercised by unpacking a 3,7 GB cartridge is a decision nobody checks. <paramref name="partners"/>
    /// is passed in rather than looked up so the swap pair can be stated in a test.
    /// </remarks>
    public EvolutionFixResult FixSpecies(byte[] entry, int species,
        IReadOnlyDictionary<int, int> partners)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var trades = 0;
        var moves = 0;

        for (var at = 0; at + EntrySize <= entry.Length; at += EntrySize)
        {
            var before = Method(entry, at);

            if (!Fix(entry, at, species, partners))
            {
                continue;
            }

            if (before == KnowingMove)
            {
                moves++;
            }
            else
            {
                trades++;
            }
        }

        return new EvolutionFixResult(trades, moves, 0);
    }

    /// <summary>Rewrites one entry, and says whether it did.</summary>
    private bool Fix(byte[] entry, int at, int species, IReadOnlyDictionary<int, int> partners)
    {
        var target = Read(entry, at + 4);

        switch (Method(entry, at))
        {
            case Trade:
                Write(entry, at, LevelUp);
                entry[at + 7] = (byte)options.TradeEvolutionLevel;
                return true;

            case TradeHoldingItem when species == Slowpoke && target == Slowking:
                Write(entry, at, UseItem);
                Write(entry, at + 2, WaterStone);
                entry[at + 7] = 0;
                return true;

            case TradeHoldingItem:
                // El objeto se conserva: es el que el cartucho ya pedia y sigue teniendo sentido.
                Write(entry, at, LevelUpHoldingItemByDay);
                entry[at + 7] = 0;
                return true;

            case TradeForTheOther when partners.TryGetValue(species, out var partner):
                Write(entry, at, LevelUpWithPartyMember);
                Write(entry, at + 2, partner);
                entry[at + 7] = 0;
                return true;

            case KnowingMove when options.RandomizeLearnsets
                                  && MoveLevels.TryGetValue((species, target), out var level):
                // El argumento era el movimiento que habia que saber; con un nivel ya no pinta nada.
                Write(entry, at, LevelUp);
                Write(entry, at + 2, 0);
                entry[at + 7] = (byte)level;
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Who each "trade for the other one" species has to be swapped with.
    /// </summary>
    /// <remarks>
    /// Worked out rather than written down: the cartridge has exactly two of these and each is the
    /// other's partner, so the pairing falls out of the data. With any other number the pairing is
    /// not obvious, so nothing is guessed and those entries are left alone — the verify step then
    /// counts them as still impossible instead of letting them pass quietly.
    /// </remarks>
    private static Dictionary<int, int> Partners(GarcPatcher patcher)
    {
        var swappers = new List<int>();

        for (var species = 0; species < patcher.FileCount; species++)
        {
            var entry = patcher.Read(species);

            for (var at = 0; at + EntrySize <= entry.Length; at += EntrySize)
            {
                if (Method(entry, at) == TradeForTheOther)
                {
                    swappers.Add(species);
                    break;
                }
            }
        }

        return swappers.Count == 2
            ? new Dictionary<int, int> { [swappers[0]] = swappers[1], [swappers[1]] = swappers[0] }
            : [];
    }

    /// <summary>
    /// Reads the file back and counts what still cannot be reached alone.
    /// </summary>
    /// <remarks>
    /// From disk with a fresh handle, never from the buffer that was just written: the point is to
    /// check the file, and checking the thing you still have in your hand proves nothing (§19).
    /// </remarks>
    private static async Task<EvolutionFixResult> VerifyAsync(string path, int trades, int moves,
        CancellationToken ct)
    {
        await Task.Yield();
        ct.ThrowIfCancellationRequested();

        using var patcher = new GarcPatcher(path);
        var left = 0;

        for (var species = 0; species < patcher.FileCount; species++)
        {
            var entry = patcher.Read(species);

            for (var at = 0; at + EntrySize <= entry.Length; at += EntrySize)
            {
                if (Method(entry, at) is Trade or TradeHoldingItem or TradeForTheOther)
                {
                    left++;
                }
            }
        }

        return new EvolutionFixResult(trades, moves, left);
    }

    private static int Method(byte[] entry, int at) => Read(entry, at);

    private static int Read(byte[] entry, int at) => BitConverter.ToUInt16(entry, at);

    private static void Write(byte[] entry, int at, int value) =>
        BitConverter.GetBytes((ushort)value).CopyTo(entry, at);
}
