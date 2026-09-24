using PKHeX.Core;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink.Data;

/// <summary>
/// Learnsets and moves from the installed world when it has published them, from PKHeX's Ultra Sun / Ultra Moon
/// tables otherwise.
/// </summary>
/// <remarks>
/// <para>
/// The two sources are not interchangeable and the catalog says which one answered (<see cref="Source"/>). The
/// installed world is what the game plays with — randomized learnsets, the gen 8-9 moves of the expansion. PKHeX is
/// the cartridge, which is exactly right when no mod replaces the learnsets and exactly wrong when one does, so it is
/// only asked when the world has nothing to say.
/// </para>
/// <para>
/// PKHeX knows no power or accuracy, so without an installed world those show as unknown rather than as a number
/// that might not be the game's.
/// </para>
/// </remarks>
public sealed class WorldMoveCatalog(string language = "es") : IMoveCatalog
{
    private readonly GameStrings _strings = GameInfo.GetStrings(language);

    /// <summary>The last move of Ultra Sun and Ultra Moon, Clangorous Soulblaze.</summary>
    private const int CartridgeMaxMove = 728;

    public string Source => WorldMoves.Learnsets is null ? "cartucho" : "mundo instalado";

    public bool IsBanned(int move) => WorldMoves.Banned.Contains(move);

    public IReadOnlyList<LevelUpMove>? LevelUp(int species, int form)
    {
        if (WorldMoves.LevelUpOf(species, form) is { } world)
        {
            return world;
        }

        if (species <= 0 || species > PersonalTable.USUM.MaxSpeciesID)
        {
            return null;
        }

        var learnset = LearnSource7USUM.Instance.GetLearnset((ushort)species, (byte)form);
        var moves = learnset.GetAllMoves();
        var levels = learnset.GetAllLevels();
        var list = new List<LevelUpMove>(moves.Length);

        for (var index = 0; index < moves.Length && index < levels.Length; index++)
        {
            // Sin mundo instalado se tira del cartucho, que sí tiene los prohibidos (§162).
            if (!WorldMoves.Banned.Contains(moves[index]))
            {
                list.Add(new LevelUpMove(moves[index], levels[index]));
            }
        }

        return list;
    }

    public MoveSheet? Describe(int move)
    {
        if (move <= 0)
        {
            return null;
        }

        if (WorldMoves.MoveOf(move) is { } world)
        {
            // Con cero PP también se describe: quien decide si se puede usar es quien pregunta, y los huecos
            // vacíos del mod (los de Let's Go y los Dinamax) son justo eso, cero PP.
            return new MoveSheet(move, Name(move), world.Type, TypeName(world.Type), world.Category, world.Power,
                world.Accuracy, world.PP, Description(move));
        }

        // Sin tabla del mundo: la del cartucho, que no llega más allá de sus 728.
        if (WorldMoves.Moves is not null || move > CartridgeMaxMove)
        {
            return null;
        }

        var type = MoveInfo.GetType((ushort)move, EntityContext.Gen7);
        var pp = MoveInfo.GetPP(EntityContext.Gen7, (ushort)move);

        return new MoveSheet(move, Name(move), type, TypeName(type), MoveSheet.Unknown, 0, 0, pp);
    }

    /// <summary>What the installed world says the move does; PKHeX has no descriptions, so without it there is none.</summary>
    private static string Description(int move) =>
        WorldMoves.MoveDescriptions is { } all && move < all.Count ? all[move] : string.Empty;

    /// <summary>The installed world's name when it has one: it is what the player reads in the game.</summary>
    private string Name(int move)
    {
        if (WorldMoves.MoveNames is { } names && move < names.Count && !string.IsNullOrWhiteSpace(names[move]))
        {
            return names[move];
        }

        return move < _strings.Move.Count && !string.IsNullOrWhiteSpace(_strings.Move[move])
            ? _strings.Move[move]
            : $"Movimiento {move}";
    }

    private string TypeName(int type) =>
        type >= 0 && type < _strings.Types.Count && !string.IsNullOrWhiteSpace(_strings.Types[type])
            ? _strings.Types[type]
            : "?";
}
