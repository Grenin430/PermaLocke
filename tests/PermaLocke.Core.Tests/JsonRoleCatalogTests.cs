using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The role file the competition actually ships. Read from disk rather than from a fixture,
/// because a typo in <c>Data/roles.json</c> would change what everybody plays and nothing else
/// would catch it.
/// </summary>
public sealed class JsonRoleCatalogTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"permalocke-roles-{Guid.NewGuid()}.json");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }

        GC.SuppressFinalize(this);
    }

    private JsonRoleCatalog Load(string json)
    {
        File.WriteAllText(_path, json);
        return JsonRoleCatalog.Load(_path);
    }

    [Fact]
    public void A_missing_file_gives_no_roles_at_all()
    {
        var catalog = JsonRoleCatalog.Load(Path.Combine(Path.GetTempPath(), "no-existe-permalocke.json"));

        Assert.Empty(catalog.All);
        Assert.Empty(catalog.ImportantTrainerClasses);
        Assert.Null(catalog.Find("normal"));
    }

    [Fact]
    public void The_important_classes_come_through_as_a_set()
    {
        var catalog = Load("""
            {
              "clasesImportantes": [31, 49, 31, 206],
              "roles": [ { "id": "normal", "ganancia": 1, "perdida": 1 } ]
            }
            """);

        Assert.Equal(3, catalog.ImportantTrainerClasses.Count);
        Assert.Contains(206, catalog.ImportantTrainerClasses);
        Assert.DoesNotContain(208, catalog.ImportantTrainerClasses);
    }

    /// <summary>Looking a role up is case insensitive; an unknown one is null, never a default.</summary>
    [Fact]
    public void Finding_a_role_is_forgiving_about_case_but_not_about_existing()
    {
        var catalog = Load("""
            {
              "roles": [ { "id": "cagoneta", "name": "CAGONETA", "ganancia": 0.5, "perdida": 0 } ]
            }
            """);

        Assert.NotNull(catalog.Find("CAGONETA"));
        Assert.NotNull(catalog.Find("cagoneta"));
        Assert.Null(catalog.Find("experto"));
        Assert.Null(catalog.Find(null));
    }

    /// <summary>
    /// A multiplier is clamped to something defensible. A role that paid a hundred times would not
    /// be a difficulty, it would be a typo nobody noticed until the standings were wrong.
    /// </summary>
    [Fact]
    public void Absurd_numbers_are_clamped()
    {
        var catalog = Load("""
            {
              "roles": [
                { "id": "roto", "ganancia": 1000, "perdida": -5,
                  "nivelEntrenadores": 999, "pokemonExtra": 99 }
              ]
            }
            """);

        var role = catalog.Find("roto")!;

        Assert.Equal(10, role.Earn);
        Assert.Equal(0, role.Lose);
        Assert.Equal(200, role.EnemyLevelPercent);
        Assert.Equal(5, role.ExtraTrainerPokemon);
    }

    /// <summary>The three the competition ships, checked against what it says they do.</summary>
    [Fact]
    public void The_shipped_file_says_what_the_competition_says()
    {
        var shipped = Path.Combine(Root(), "Data", "roles.json");
        Assert.True(File.Exists(shipped), $"No está {shipped}");

        var catalog = JsonRoleCatalog.Load(shipped);

        Assert.Equal(12, catalog.All.Count);
        Assert.NotEmpty(catalog.ImportantTrainerClasses);

        var normal = catalog.Find("normal")!;
        var cagoneta = catalog.Find("cagoneta")!;
        var experto = catalog.Find("experto")!;
        var ludopata = catalog.Find("ludopata")!;

        // El ludopata no toca los puntos: lo que cambia es que la ruleta decide por el, y eso
        // es un interruptor y no un multiplicador. Es el unico rol que lo lleva.
        Assert.Equal(1.0, ludopata.Earn);
        Assert.Equal(1.0, ludopata.Lose);
        Assert.True(ludopata.Roulette);
        Assert.False(normal.Roulette);
        Assert.False(cagoneta.Roulette);
        Assert.False(experto.Roulette);

        Assert.Equal(1.0, normal.Earn);
        Assert.Equal(1.0, normal.Lose);

        Assert.Equal(0.5, cagoneta.Earn);
        Assert.Equal(0.0, cagoneta.Lose);

        Assert.Equal(1.5, experto.Earn);
        Assert.Equal(2.0, experto.Lose);

        // Los entrenadores suben; el cap del jugador es el de la tabla y no se toca.
        Assert.Equal(20, normal.EnemyLevelPercent);
        Assert.Equal(20, cagoneta.EnemyLevelPercent);
        Assert.Equal(27, experto.EnemyLevelPercent);
        Assert.All(catalog.All, role => Assert.Equal(0, role.PlayerCapPercent));

        // El extra lo llevan todos; el experto uno más.
        Assert.Equal(1, normal.ExtraTrainerPokemon);
        Assert.Equal(1, cagoneta.ExtraTrainerPokemon);
        Assert.Equal(2, experto.ExtraTrainerPokemon);
    }

    /// <summary>The eight MONOTYPE roles (§220): their type, and that each plays like normal except for the points.</summary>
    [Fact]
    public void The_shipped_file_has_the_eight_monotype_roles()
    {
        var catalog = JsonRoleCatalog.Load(Path.Combine(Root(), "Data", "roles.json"));
        var normal = catalog.Find("normal")!;

        var expected = new Dictionary<string, (int Type, string Name)>
        {
            ["monotype_agua"] = (10, "Agua"), ["monotype_normal"] = (0, "Normal"), ["monotype_planta"] = (11, "Planta"),
            ["monotype_volador"] = (2, "Volador"), ["monotype_psiquico"] = (13, "Psíquico"), ["monotype_bicho"] = (6, "Bicho"),
            ["monotype_veneno"] = (3, "Veneno"), ["monotype_fuego"] = (9, "Fuego")
        };

        Assert.Equal(expected.Count, catalog.All.Count(r => r.IsMonotype));

        foreach (var (id, (type, name)) in expected)
        {
            var role = catalog.Find(id)!;
            Assert.Equal(type, role.MonoType);
            Assert.Equal(name, role.MonoTypeName);
            Assert.Equal(1.5, role.Earn);
            Assert.Equal(1.0, role.Lose);
            Assert.False(role.Roulette);

            // Lo mismo que el normal en lo que se cuece en la ROM: cambiar entre ellos no pide randomizar otra vez.
            Assert.Equal(normal.EnemyLevelPercent, role.EnemyLevelPercent);
            Assert.Equal(normal.PlayerCapPercent, role.PlayerCapPercent);
            Assert.Equal(normal.ExtraTrainerPokemon, role.ExtraTrainerPokemon);
        }

        Assert.All(catalog.All.Where(r => !r.IsMonotype), r => Assert.Equal(string.Empty, r.MonoTypeName));
    }

    [Fact]
    public void A_type_outside_the_eighteen_leaves_the_role_without_one()
    {
        var catalog = Load("""
            { "roles": [ { "id": "a", "monotipo": 18 }, { "id": "b", "monotipo": -1 }, { "id": "c", "monotipo": 9 } ] }
            """);

        Assert.Null(catalog.Find("a")!.MonoType);
        Assert.Null(catalog.Find("b")!.MonoType);
        Assert.Equal(9, catalog.Find("c")!.MonoType);
    }

    /// <summary>
    /// The grunt classes must never be in the important list: Giovanni and his mooks share a class
    /// name, and letting 208 in would hand an extra Pokémon to every recruit in the game.
    /// </summary>
    [Fact]
    public void The_shipped_file_keeps_the_grunts_out()
    {
        var catalog = JsonRoleCatalog.Load(Path.Combine(Root(), "Data", "roles.json"));

        Assert.Contains(206, catalog.ImportantTrainerClasses);   // Giovanni
        Assert.DoesNotContain(208, catalog.ImportantTrainerClasses);
        Assert.DoesNotContain(209, catalog.ImportantTrainerClasses);
        Assert.DoesNotContain(28, catalog.ImportantTrainerClasses);  // reclutas del Team Skull
        Assert.DoesNotContain(29, catalog.ImportantTrainerClasses);
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
