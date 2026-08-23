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

        GiveItAnIdentity(pokemon, spec.IsShiny);

        NameIt(pokemon);
        SetMovesFor(pokemon);
        pokemon.HealPP();
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
