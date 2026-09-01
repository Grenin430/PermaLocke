using PermaLocke.Randomizer.Output;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Laying a randomization on top of a base mod. See <c>docs/MOD-EXPANSION.md</c>.
/// </summary>
public sealed class ModInstallerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "permalocke-install-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Make(string relative, string content)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private string Read(string relative) => File.ReadAllText(Path.Combine(_root, relative));

    /// <summary>
    /// The order that matters: what the randomizer wrote wins over the base mod's copy.
    /// </summary>
    /// <remarks>
    /// If it went the other way the install would silently undo the randomization: the encounter
    /// tables would be the mod's, the report would still say what was generated, and the world
    /// would be somebody else's.
    /// </remarks>
    [Fact]
    public void Lo_randomizado_manda_sobre_el_mod_base()
    {
        Make("base/romfs/a/0/1/7", "especies del mod");
        Make("base/romfs/a/0/8/3", "encuentros del mod");
        Make("gen/romfs/a/0/8/3", "encuentros randomizados");

        ModInstaller.Install(Path.Combine(_root, "gen"), Path.Combine(_root, "mod"),
            Path.Combine(_root, "base", "romfs"), null);

        Assert.Equal("encuentros randomizados", Read("mod/romfs/a/0/8/3"));
        Assert.Equal("especies del mod", Read("mod/romfs/a/0/1/7"));
    }

    /// <summary>Everything the base mod has arrives, not only what the randomizer touches.</summary>
    /// <remarks>
    /// The bug this replaces: only the seven generated files were installed, so the species table,
    /// the learnsets and the models never reached the emulator. The new Pokémon appeared in the
    /// encounter tables and the game had nothing to draw them with.
    /// </remarks>
    [Fact]
    public void Llega_todo_el_mod_base_y_no_solo_lo_que_tocamos()
    {
        Make("base/romfs/a/0/9/4", "modelos");
        Make("base/romfs/a/0/1/3", "aprendizajes");
        Make("base/romfs/a/0/6/2", "iconos");
        Make("gen/romfs/a/0/8/3", "encuentros");

        ModInstaller.Install(Path.Combine(_root, "gen"), Path.Combine(_root, "mod"),
            Path.Combine(_root, "base", "romfs"), null);

        Assert.Equal("modelos", Read("mod/romfs/a/0/9/4"));
        Assert.Equal("aprendizajes", Read("mod/romfs/a/0/1/3"));
        Assert.Equal("iconos", Read("mod/romfs/a/0/6/2"));
        Assert.Equal("encuentros", Read("mod/romfs/a/0/8/3"));
    }

    /// <summary>The exefs goes beside romfs, never inside it.</summary>
    [Fact]
    public void El_exefs_va_al_lado_de_romfs_y_no_dentro()
    {
        Make("base/romfs/a/0/1/7", "especies");
        Make("base/exefs/code.bin", "codigo parcheado");
        Make("gen/romfs/a/0/8/3", "encuentros");

        ModInstaller.Install(Path.Combine(_root, "gen"), Path.Combine(_root, "mod"),
            Path.Combine(_root, "base", "romfs"), Path.Combine(_root, "base", "exefs"));

        Assert.Equal("codigo parcheado", Read("mod/exefs/code.bin"));
        Assert.False(File.Exists(Path.Combine(_root, "mod/romfs/exefs/code.bin")));
    }

    /// <summary>Without a base layer, only the randomization is installed.</summary>
    [Fact]
    public void Sin_mod_base_se_instala_solo_lo_randomizado()
    {
        Make("gen/romfs/a/0/8/3", "encuentros");

        ModInstaller.Install(Path.Combine(_root, "gen"), Path.Combine(_root, "mod"), null, null);

        Assert.Equal("encuentros", Read("mod/romfs/a/0/8/3"));
        Assert.False(Directory.Exists(Path.Combine(_root, "mod", "exefs")));
    }

    [Fact]
    public void Lo_identico_se_salta_y_lo_distinto_no()
    {
        Make("src/grande", "aaaa");
        Make("src/otro", "bbbb");
        ModInstaller.CopyTree(Path.Combine(_root, "src"), Path.Combine(_root, "dst"));

        // Segunda pasada sin cambios: no se copia nada.
        Assert.Equal(0, ModInstaller.CopyTree(Path.Combine(_root, "src"),
            Path.Combine(_root, "dst"), skipUnchanged: true));

        // Uno cambia de contenido y de longitud: ese sí.
        File.WriteAllText(Path.Combine(_root, "src", "otro"), "bbbbbbbb");
        Assert.Equal(1, ModInstaller.CopyTree(Path.Combine(_root, "src"),
            Path.Combine(_root, "dst"), skipUnchanged: true));
        Assert.Equal("bbbbbbbb", Read("dst/otro"));
    }

    /// <summary>
    /// Without the skip, everything is copied even when it looks identical.
    /// </summary>
    /// <remarks>
    /// This is what protects the randomized half. Two generations of a world can produce files of
    /// exactly the same length — the tables are fixed size — so "same length and date" must never
    /// be allowed to decide anything about them.
    /// </remarks>
    [Fact]
    public void Sin_el_salto_se_copia_aunque_parezca_igual()
    {
        Make("src/a", "1234");
        ModInstaller.CopyTree(Path.Combine(_root, "src"), Path.Combine(_root, "dst"));

        Assert.Equal(1, ModInstaller.CopyTree(Path.Combine(_root, "src"), Path.Combine(_root, "dst")));
    }
}
