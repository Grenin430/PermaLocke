using PKHeX.Core;

namespace PermaLocke.GameLink.Data;

/// <summary>What a Pokémon PermaLocke is about to put in the game looks like.</summary>
/// <param name="Ivs">Six, in HP/Atk/Def/Spe/SpA/SpD order, which is how the roll stores them.</param>
public sealed record NewPokemon(
    int Species,
    int Level,
    int Nature,
    int AbilityId,
    IReadOnlyList<int> Ivs,
    bool IsShiny,
    int Form = 0);

/// <summary>
/// Builds a PK7 for the player's own game.
/// </summary>
/// <remarks>
/// Shared by the gacha delivery and the wonder trade so that both produce exactly the same kind
/// of Pokémon: <b>the player's own</b>, with their trainer name and ids. Anything else and the
/// game treats it as traded, which means it would not obey them.
/// </remarks>
public static class PokemonBuilder
{
    /// <summary>
    /// How a Pokémon PermaLocke hands over goes into the save: it counts for the Pokédex, but not
    /// for the trainer card's counters.
    /// </summary>
    /// <remarks>
    /// PKHeX treats putting a Pokémon in a box as acquiring it, and by default bumps <em>captures,
    /// Poké Balls used and wild battles</em>. None of that happened: nobody threw a ball at a
    /// gacha roll. The Pokédex entry does stay, because the Pokémon really is the player's now.
    /// </remarks>
    public static readonly EntityImportSettings Handover = new()
    {
        UpdateRecord = EntityImportOption.Disable,
    };

    /// <summary>
    /// How a Pokémon that is already the player's goes back into its slot: nothing at all is
    /// updated.
    /// </summary>
    /// <remarks>
    /// For editing in place — renaming, retraining — where the Pokémon never arrived because it was
    /// already there. §42: left at the default, putting a hundred and fifty of them back added a
    /// hundred and fifty captures, Poké Balls used and wild battles that never happened.
    /// </remarks>
    public static readonly EntityImportSettings InPlace = new()
    {
        UpdateToSaveFile = EntityImportOption.Disable,
        UpdatePokeDex = EntityImportOption.Disable,
        UpdateRecord = EntityImportOption.Disable,
    };

    public static PK7 Build(NewPokemon spec, SAV7USUM save)
    {
        var pokemon = new PK7
        {
            Species = (ushort)spec.Species,

            // La forma regional que salió en la tirada (§139); 0 es la normal.
            Form = (byte)spec.Form,

            Nature = (Nature)spec.Nature,

            // El hueco de habilidad tiene que ser uno de los tres que la especie declara; el 0
            // vale siempre y evita que el juego muestre un hueco imposible.
            AbilityNumber = 1,

            OriginalTrainerName = save.OT,
            TID16 = save.TID16,
            SID16 = save.SID16,
            OriginalTrainerGender = save.Gender,
            Language = save.Language,
            Version = save.Version,
            Ball = (byte)PKHeX.Core.Ball.Poke,
            MetLevel = (byte)spec.Level,
            MetDate = DateOnly.FromDateTime(DateTime.Now),
            CurrentHandler = 1,
            HandlingTrainerName = save.OT,
            HandlingTrainerGender = save.Gender
        };

        pokemon.IV_HP = spec.Ivs[0];
        pokemon.IV_ATK = spec.Ivs[1];
        pokemon.IV_DEF = spec.Ivs[2];
        pokemon.IV_SPE = spec.Ivs[3];
        pokemon.IV_SPA = spec.Ivs[4];
        pokemon.IV_SPD = spec.Ivs[5];

        // Con la curva del juego que se juega. PKHeX pone crecimiento Medio a todo lo que pasa de
        // la 807, así que un Dragapult pedido a nivel 40 llegaba al 37 (§134).
        GameLevels.Set(pokemon, spec.Level);

        // Con el noveno bit, después del hueco: el hueco vive en los bits bajos del mismo byte.
        PokemonAbility.Set(pokemon, spec.AbilityId);

        GiveItAnIdentity(pokemon, spec.IsShiny);

        NameIt(pokemon);
        SetMovesFor(pokemon);
        pokemon.HealPP();
        SetWorldPP(pokemon);
        pokemon.ResetPartyStats();
        pokemon.RefreshChecksum();

        return pokemon;
    }

    /// <summary>
    /// Gives the Pokémon a personality value and an encryption constant of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Left out until now, and a <see cref="PK7"/> starts at zero, so everything PermaLocke had
    /// ever handed over shared the same PID — 149 of the 154 Pokémon in the real save. The PID is
    /// the one field that identifies a Pokémon for life: <c>GameWatcher</c> matches the live party
    /// against the run by it and by nothing else, so a party full of zeros is a party the run
    /// cannot recognise, which is why not one death had ever been recorded.
    /// </para>
    /// <para>
    /// The shininess is set <b>after</b> the roll of the dice and not before it, because a random
    /// PID is shiny about one time in four thousand: rolling first and correcting second is what
    /// keeps a Pokémon that was not meant to be shiny from becoming one by accident, and a shiny
    /// one from losing it.
    /// </para>
    /// </remarks>
    private static void GiveItAnIdentity(PK7 pokemon, bool shiny)
    {
        pokemon.PID = Random32();
        pokemon.EncryptionConstant = Random32();
        pokemon.SetIsShiny(shiny);
    }

    private static uint Random32() => (uint)Random.Shared.NextInt64(0, uint.MaxValue + 1L);

    /// <summary>
    /// Writes the species name into the nickname field, which is where the game reads it from.
    /// </summary>
    /// <remarks>
    /// A Pokémon with no nickname does not fall back to its species name: the field is part of the
    /// data and the game shows whatever is in it, so leaving it blank meant everything PermaLocke
    /// handed over — gacha and wonder trade alike — arrived nameless. It goes in the save's own
    /// language, and <see cref="PKM.IsNicknamed"/> stays false so the game still treats it as the
    /// plain species name and renames it on evolution.
    /// </remarks>
    private static void NameIt(PK7 pokemon)
    {
        pokemon.Nickname = SpeciesNameFor(pokemon);
        pokemon.IsNicknamed = false;
    }

    /// <summary>
    /// The species name as the game would write it, in the Pokémon's own language.
    /// </summary>
    /// <remarks>
    /// Falls back to Spanish when the language field says nothing, because PKHeX answers a blank
    /// string for an unknown one — and a blank name is exactly the bug this exists to avoid.
    /// Shared with <see cref="SaveNameRepair"/> so a delivery and a repair write the same name.
    /// </remarks>
    public static string SpeciesNameFor(PK7 pokemon)
    {
        var name = SpeciesName.GetSpeciesNameGeneration(pokemon.Species, pokemon.Language, pokemon.Format);

        return string.IsNullOrWhiteSpace(name)
            ? SpeciesName.GetSpeciesNameGeneration(pokemon.Species, (int)LanguageID.Spanish, pokemon.Format)
            : name;
    }

    /// <summary>
    /// Gives the Pokémon the moves it would know at that level.
    /// </summary>
    /// <remarks>
    /// PKHeX works them out from a legality analysis, which can baulk at a Pokémon PermaLocke made
    /// impossible — a random ability its species cannot have, for instance. If it does, the
    /// Pokémon arrives with no moves rather than not arriving at all: the player can teach it or
    /// use the move reminder, and that is a far smaller problem than a failed write.
    /// </remarks>
    private static void SetMovesFor(PK7 pokemon)
    {
        // Del mundo instalado antes que de PKHeX (§142). Los aprendizajes están randomizados, así que lo que PKHeX
        // sugiere es lo que la especie aprende en el CARTUCHO: un Pokémon del gacha llegaba sabiendo cosas que su
        // especie no aprende en el juego que se juega, y uno de gen 8-9 no llegaba sabiendo nada, porque la tabla
        // de PKHeX acaba en la 807. Se hace lo que hace el juego con uno salvaje: los últimos cuatro aprendidos.
        if (WorldMoves.MovesAt(pokemon.Species, pokemon.Form, GameLevels.Of(pokemon)) is { } world
            && world.Any(move => move > 0))
        {
            pokemon.Move1 = (ushort)world[0];
            pokemon.Move2 = (ushort)world[1];
            pokemon.Move3 = (ushort)world[2];
            pokemon.Move4 = (ushort)world[3];

            // Y los mismos como «para volver a aprender», que es lo que el juego guarda de un regalo: así el
            // recuerda-movimientos -el de la app y el del juego- los ofrece siempre, como las «iniciales» de Añil.
            pokemon.RelearnMove1 = (ushort)world[0];
            pokemon.RelearnMove2 = (ushort)world[1];
            pokemon.RelearnMove3 = (ushort)world[2];
            pokemon.RelearnMove4 = (ushort)world[3];
            return;
        }

        try
        {
            Span<ushort> moves = stackalloc ushort[4];
            new LegalityAnalysis(pokemon).GetSuggestedCurrentMoves(moves);

            // Lo que sugiere PKHeX sale del cartucho, que sí tiene los prohibidos (§162). Se quitan y se juntan los que
            // quedan delante: el juego nunca deja un hueco en medio.
            Span<ushort> allowed = stackalloc ushort[4];
            var kept = 0;
            foreach (var move in moves)
            {
                if (move > 0 && !WorldMoves.Banned.Contains(move)) allowed[kept++] = move;
            }

            pokemon.SetMoves(allowed);
        }
        catch (Exception)
        {
            // Sin movimientos, pero entregado.
        }
    }

    /// <summary>
    /// Puts each move's PP as the installed world has them, over PKHeX's.
    /// </summary>
    /// <remarks>
    /// PKHeX's table ends with the cartridge's moves and gives anything past it zero PP — a move the game will not
    /// let the player pick, so the Pokémon struggles. Only moves the world knows are touched; without a world the
    /// cartridge's PP are the right ones anyway.
    /// </remarks>
    private static void SetWorldPP(PK7 pokemon)
    {
        if (WorldMoves.MoveOf(pokemon.Move1) is { PP: > 0 } first)
        {
            pokemon.Move1_PP = first.PP;
        }

        if (WorldMoves.MoveOf(pokemon.Move2) is { PP: > 0 } second)
        {
            pokemon.Move2_PP = second.PP;
        }

        if (WorldMoves.MoveOf(pokemon.Move3) is { PP: > 0 } third)
        {
            pokemon.Move3_PP = third.PP;
        }

        if (WorldMoves.MoveOf(pokemon.Move4) is { PP: > 0 } fourth)
        {
            pokemon.Move4_PP = fourth.PP;
        }
    }
}
