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
    bool IsShiny);

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
    public static PK7 Build(NewPokemon spec, SAV7USUM save)
    {
        var pokemon = new PK7
        {
            Species = (ushort)spec.Species,
            Form = 0,
            CurrentLevel = (byte)spec.Level,
            Nature = (Nature)spec.Nature,
            Ability = spec.AbilityId,

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

        if (spec.IsShiny)
        {
            pokemon.SetShiny();
        }

        NameIt(pokemon);
        SetMovesFor(pokemon);
        pokemon.HealPP();
        pokemon.ResetPartyStats();
        pokemon.RefreshChecksum();

        return pokemon;
    }

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
        try
        {
            Span<ushort> moves = stackalloc ushort[4];
            new LegalityAnalysis(pokemon).GetSuggestedCurrentMoves(moves);
            pokemon.SetMoves(moves);
        }
        catch (Exception)
        {
            // Sin movimientos, pero entregado.
        }
    }
}
