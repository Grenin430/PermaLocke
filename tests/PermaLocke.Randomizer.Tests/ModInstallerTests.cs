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

    private string Gen => Path.Combine(_root, "gen");
    private string Mod => Path.Combine(_root, "mod");
    private string BaseRomfs => Path.Combine(_root, "base", "romfs");
    private string BaseExefs => Path.Combine(_root, "base", "exefs");
    private string Shelf => Path.Combine(_root, "copias");

    /// <summary>
    /// The randomized code.bin is installed on top of the base mod's, not left behind.
    /// </summary>
    /// <remarks>
    /// This is the one that reached a player. The installer always did it; the ROM tool had its own
    /// copy of the install and only laid down the base layer's <c>exefs</c>, so two installs left the
    /// game with the expansion's own TM and tutor tables while the generation report said all hundred
    /// TMs taught something else. It lives here now because there is one installer.
    /// </remarks>
    [Fact]
    public void El_code_bin_randomizado_gana_al_del_mod_base()
    {
        Make("base/romfs/a/0/1/7", "especies");
        Make("base/exefs/code.bin", "MT del mod");
        Make("gen/romfs/a/0/8/3", "encuentros");
        Make("gen/exefs/code.bin", "MT barajadas");

        ModInstaller.Install(Gen, Mod, BaseRomfs, BaseExefs, backupRoot: Shelf);

        Assert.Equal("MT barajadas", Read("mod/exefs/code.bin"));
    }

    /// <summary>
    /// Installing over a world keeps a copy of what it was about to overwrite, byte for byte.
    /// </summary>
    [Fact]
    public void Instalar_encima_de_un_mundo_guarda_copia_de_lo_que_se_pierde()
    {
        Make("gen/romfs/a/0/8/3", "mundo nuevo");
        Make("gen/exefs/code.bin", "MT nuevas");
        Make("mod/romfs/a/0/8/3", "mundo que se estaba jugando");
        Make("mod/exefs/code.bin", "MT de ese mundo");

        var copy = ModInstaller.Install(Gen, Mod, null, null, backupRoot: Shelf);

        Assert.NotNull(copy);
        Assert.Equal("mundo que se estaba jugando", File.ReadAllText(Path.Combine(copy, "romfs", "a", "0", "8", "3")));
        Assert.Equal("MT de ese mundo", File.ReadAllText(Path.Combine(copy, "exefs", "code.bin")));
        Assert.True(File.Exists(Path.Combine(copy, "LEEME.txt")));

        // Y lo instalado es lo nuevo: la copia no se hace en lugar de instalar.
        Assert.Equal("mundo nuevo", Read("mod/romfs/a/0/8/3"));
    }

    /// <summary>The first install of all has nothing to lose, so it makes no copy.</summary>
    [Fact]
    public void La_primera_instalacion_no_hace_copia()
    {
        Make("gen/romfs/a/0/8/3", "mundo");

        Assert.Null(ModInstaller.Install(Gen, Mod, null, null, backupRoot: Shelf));
        Assert.False(Directory.Exists(Shelf));
    }

    /// <summary>
    /// Reinstalling exactly the same world keeps nothing, so it cannot push a real copy out.
    /// </summary>
    [Fact]
    public void Reinstalar_el_mismo_mundo_no_gasta_una_copia()
    {
        Make("gen/romfs/a/0/8/3", "mundo");

        ModInstaller.Install(Gen, Mod, null, null, backupRoot: Shelf);

        Assert.Null(ModInstaller.Install(Gen, Mod, null, null, backupRoot: Shelf));
    }

    /// <summary>
    /// What is still identical to the base mod is not copied: it can be rebuilt from Expansion.
    /// </summary>
    /// <remarks>
    /// Coming back from the battle mode is exactly this case — the installed files are the base
    /// mod's — and without the rule every return would spend one of the three slots on a copy of
    /// the unrandomized mod and push out a world somebody was playing.
    /// </remarks>
    [Fact]
    public void Lo_que_es_igual_que_el_mod_base_no_se_guarda()
    {
        Make("base/romfs/a/0/8/3", "encuentros del mod");
        ModInstaller.CopyTree(BaseRomfs, Path.Combine(Mod, "romfs"));
        Make("gen/romfs/a/0/8/3", "encuentros randomizados");

        Assert.Null(ModInstaller.Install(Gen, Mod, BaseRomfs, null, backupRoot: Shelf));
        Assert.Equal("encuentros randomizados", Read("mod/romfs/a/0/8/3"));
    }

    /// <summary>The battle mode's swap keeps the world it is about to overwrite.</summary>
    [Fact]
    public void Pasar_al_modo_combate_guarda_copia_del_mundo()
    {
        Make("base/romfs/a/0/8/3", "encuentros del mod");
        Make("mod/romfs/a/0/8/3", "encuentros de mi mundo");

        var copy = ModInstaller.SwitchToBase(Mod, BaseRomfs, null, backupRoot: Shelf);

        Assert.NotNull(copy);
        Assert.Equal("encuentros de mi mundo", File.ReadAllText(Path.Combine(copy, "romfs", "a", "0", "8", "3")));
        Assert.Equal("encuentros del mod", Read("mod/romfs/a/0/8/3"));
    }

    /// <summary>Only the last three copies are kept, and the one that goes is the oldest.</summary>
    [Fact]
    public void Se_guardan_las_tres_ultimas()
    {
        Make("mod/romfs/a/0/8/3", "mundo 0");

        for (var world = 1; world <= 4; world++)
        {
            Make("gen/romfs/a/0/8/3", $"mundo {world}");
            ModInstaller.Install(Gen, Mod, null, null, backupRoot: Shelf);
        }

        var kept = Directory.GetDirectories(Shelf)
            .Select(copy => File.ReadAllText(Path.Combine(copy, "romfs", "a", "0", "8", "3")))
            .Order()
            .ToList();

        Assert.Equal(ModInstaller.BackupsKept, kept.Count);
        Assert.Equal(["mundo 1", "mundo 2", "mundo 3"], kept);
    }

    /// <summary>
    /// If the copy cannot be made, nothing is installed and the world is left exactly as it was.
    /// </summary>
    /// <remarks>
    /// Forced by pointing the copies at a path that is a file, so no folder can be made there. The
    /// same rule as SaveEraser: without a copy it does not destroy.
    /// </remarks>
    [Fact]
    public void Sin_copia_no_se_instala_nada()
    {
        Make("gen/romfs/a/0/8/3", "mundo nuevo");
        Make("mod/romfs/a/0/8/3", "mundo que se estaba jugando");
        var blocked = Make("no-es-carpeta", "soy un fichero");

        Assert.Throws<IOException>(() => ModInstaller.Install(Gen, Mod, null, null, backupRoot: blocked));

        Assert.Equal("mundo que se estaba jugando", Read("mod/romfs/a/0/8/3"));
    }

    /// <summary>
    /// By default copies go beside the emulator's <c>mods</c> folder, where it does not load mods from.
    /// </summary>
    [Fact]
    public void Por_defecto_la_copia_va_al_lado_de_mods_y_no_dentro()
    {
        var mod = Path.Combine(_root, "load", "mods", "00040000001B5100");
        Make("gen/romfs/a/0/8/3", "mundo nuevo");
        Make("load/mods/00040000001B5100/romfs/a/0/8/3", "mundo viejo");

        var copy = ModInstaller.Install(Gen, mod, null, null);

        Assert.NotNull(copy);
        Assert.Equal(Path.Combine(_root, "load", "permalocke-copias"), Path.GetDirectoryName(copy));
    }
}
