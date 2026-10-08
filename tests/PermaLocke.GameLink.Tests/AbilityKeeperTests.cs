using PKHeX.Core;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// The ability PermaLocke gave a Pokémon comes back after the game recomputed it (§237: a gacha Heracross with Espada
/// Indómita came out of a Mega Evolution with Gran Encanto). On saves built here, no emulator, nobody's partida.
/// </summary>
[Collection("AbilityLedger")]
public sealed class AbilityKeeperTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "permalocke-abilities-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        AbilityLedger.Configure(null);
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static PK7 Mon(SAV7USUM save, Species species, uint pid, int ability, bool egg = false)
    {
        var pokemon = new PK7
        {
            Species = (ushort)species, CurrentLevel = 50, OriginalTrainerName = "GRENIN", Language = save.Language,
            Version = save.Version, PID = pid, Move1 = (ushort)Move.Tackle, AbilityNumber = 1
        };
        pokemon.ClearNickname();
        PokemonAbility.Set(pokemon, ability);
        pokemon.IsEgg = egg;
        pokemon.RefreshChecksum();
        return pokemon;
    }

    [Fact]
    public void What_the_game_recomputed_is_put_back_in_the_party_and_in_the_boxes_and_nothing_else_moves()
    {
        var save = new SAV7USUM();
        save.SetPartySlotAtIndex(Mon(save, Species.Heracross, 0xC6AA4BB0, 56), 0);          // recalculada: tenía la 234
        save.SetPartySlotAtIndex(Mon(save, Species.Pidgey, 0x22222222, 51), 1);             // sin tocar
        save.SetBoxSlotAtIndex(Mon(save, Species.Rattata, 0x33333333, 50), 3, 7);           // en una caja
        save.SetBoxSlotAtIndex(Mon(save, Species.Caterpie, 0x44444444, 19, egg: true), 1, 1); // un huevo no se toca

        var intended = new Dictionary<uint, int>
        {
            [0xC6AA4BB0] = 234, [0x22222222] = 51, [0x33333333] = 300, [0x44444444] = 77, [0x99999999] = 10
        };

        Assert.Equal(2, SaveAbilityKeeper.Pending(save, intended).Count);
        Assert.Equal(2, SaveAbilityKeeper.Apply(save, intended));

        var heracross = (PK7)save.GetPartySlotAtIndex(0);
        Assert.Equal(234, PokemonAbility.Of(heracross));
        Assert.Equal(1, heracross.Data[0x15] & 7);       // el hueco de habilidad se queda como estaba
        Assert.True(heracross.ChecksumValid);

        // Una con el noveno bit: 300 necesita 256 + 44.
        Assert.Equal(300, PokemonAbility.Of((PK7)save.GetBoxSlotAtIndex(3, 7)));
        Assert.Equal(51, PokemonAbility.Of((PK7)save.GetPartySlotAtIndex(1)));
        Assert.Equal(19, PokemonAbility.Of((PK7)save.GetBoxSlotAtIndex(1, 1)));

        // Segunda vuelta: ya está todo en su sitio.
        Assert.Empty(SaveAbilityKeeper.Pending(save, intended));
        Assert.Equal(0, SaveAbilityKeeper.Apply(save, intended));
    }

    [Fact]
    public void The_ledger_remembers_across_restarts_never_replaces_what_was_written_and_forgives_a_broken_file()
    {
        var file = Path.Combine(_folder, "habilidades.json");
        AbilityLedger.Configure(file);

        AbilityLedger.Record(0xC6AA4BB0, 234);
        AbilityLedger.Record(0x11111111, 0);        // sin habilidad: no se apunta

        // Otra vuelta de la app: lee el fichero.
        AbilityLedger.Configure(file);
        Assert.Equal(234, AbilityLedger.Snapshot()[0xC6AA4BB0]);
        Assert.DoesNotContain(0x11111111u, AbilityLedger.Snapshot().Keys);

        // Lo deducido de la historia solo rellena lo que falta.
        var added = AbilityLedger.Merge(new Dictionary<uint, int> { [0xC6AA4BB0] = 56, [0x22222222] = 7 });
        Assert.Equal(1, added);
        Assert.Equal(234, AbilityLedger.Snapshot()[0xC6AA4BB0]);
        Assert.Equal(7, AbilityLedger.Snapshot()[0x22222222]);

        // Un fichero roto es un libro vacío, no una caída.
        File.WriteAllText(file, "{ esto no es json");
        AbilityLedger.Configure(file);
        Assert.Empty(AbilityLedger.Snapshot());

        // Sin configurar no se escribe nada.
        AbilityLedger.Configure(null);
        AbilityLedger.Record(0x55555555, 12);
        Assert.Empty(AbilityLedger.Snapshot());
    }

    [Fact]
    public void A_pokemon_PermaLocke_builds_is_written_down_with_the_ability_it_was_given()
    {
        AbilityLedger.Configure(Path.Combine(_folder, "habilidades.json"));
        var save = new SAV7USUM { OT = "Grenin", TID16 = 1, SID16 = 2, Language = (int)LanguageID.Spanish };

        var pokemon = PokemonBuilder.Build(new NewPokemon((int)Species.Heracross, 30, 0, 234, [31, 31, 31, 31, 31, 31], false), save);

        Assert.Equal(234, AbilityLedger.Snapshot()[pokemon.PID]);
    }
}
