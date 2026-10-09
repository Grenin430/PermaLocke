using System.IO;
using Microsoft.Extensions.Logging;
using PermaLocke.GameLink;
using PermaLocke.Infrastructure;
using PermaLocke.Randomizer.Modules;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;

namespace PermaLocke.App.Services;

/// <summary>
/// The <see cref="ItemCatalog"/> of the world the player is actually playing (2026-10-09), read from the item table
/// (<c>a/0/1/9</c>) and the mega evolution table (<c>a/0/1/5</c>) of the game Azahar loads.
/// </summary>
/// <remarks>
/// <para>
/// Where each file comes from is the order of <see cref="WorldEvolutionLines"/>: the installed mod first, because it may
/// carry items of its own; then the expansion base layer; then the player's cartridge. Looked at again every few seconds,
/// like <see cref="MachineItemLookup"/>, because the player installs a new world while PermaLocke is open.
/// </para>
/// <para>
/// When the item table cannot be read, the catalog is <see cref="ItemCatalog.Empty"/> and every item plays the plainest
/// animation, which is never the wrong one.
/// </para>
/// </remarks>
public sealed class WorldItemCatalog(AzaharInstallation azahar, AppPaths paths, ILogger<WorldItemCatalog> logger)
{
    private static readonly TimeSpan Recheck = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private ItemCatalog _catalog = ItemCatalog.Empty;
    private string _source = string.Empty;
    private DateTime _checked = DateTime.MinValue;

    /// <summary>What the installed world says. Never throws.</summary>
    public ItemCatalog Current
    {
        get
        {
            lock (_gate)
            {
                Refresh();
                return _catalog;
            }
        }
    }

    /// <summary>Reads the tables now instead of at the first item: the first time may mean taking them out of the ROM.</summary>
    public void Warm() => _ = Current;

    private void Refresh()
    {
        if (DateTime.UtcNow - _checked < Recheck) return;
        _checked = DateTime.UtcNow;

        try
        {
            var items = Find(GameFiles.Item, "a-0-1-9.garc");
            var megas = Find(GameFiles.MegaEvolution, "a-0-1-5.garc");
            var personal = Find(GameFiles.Personal, "a-0-1-7.garc");
            var source = $"{Stamp(items)}|{Stamp(megas)}|{Stamp(personal)}";
            if (source == _source) return;

            _source = source;
            _catalog = items is null ? ItemCatalog.Empty : Build(items, megas, personal);
            logger.LogInformation("Categorías de objetos leídas de {Items} (megas: {Megas})", items ?? "nada", megas ?? "nada");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo leer la tabla de objetos; todos irán con la animación sencilla");
        }
    }

    private static ItemCatalog Build(string items, string? megas, string? personal)
    {
        using var table = new GarcPatcher(items);
        var pockets = FieldItemRandomizer.ReadPockets([.. Enumerable.Range(0, table.FileCount).Select(table.Read)]);
        var stones = megas is null ? new HashSet<int>() : MegaTrainerRandomizer.ReadStones(megas).ToHashSet();
        return new ItemCatalog(pockets, stones, megas is null || personal is null ? null : StoneKinds(megas, personal));
    }

    /// <summary>
    /// The type of the Pokémon each Mega Stone is for, from the cartridge: the mega evolution table says which species a stone
    /// belongs to (the entry of that species names it), and the personal table says that species' first type. Nothing by hand,
    /// and the stones the mod adds are in the same tables. A species whose entries cannot be read has no type, and its stone
    /// takes the colour of its icon.
    /// </summary>
    private static Dictionary<int, int> StoneKinds(string megas, string personal)
    {
        var kinds = new Dictionary<int, int>();

        try
        {
            using var mega = new GarcPatcher(megas);
            using var people = new GarcPatcher(personal);

            for (var species = 0; species < mega.FileCount && species < people.FileCount; species++)
            {
                var entry = mega.Read(species);
                for (var at = 0; at + 8 <= entry.Length; at += 8)
                {
                    if (BitConverter.ToUInt16(entry, at) == 0 || BitConverter.ToUInt16(entry, at + 2) != 1) continue;
                    int stone = BitConverter.ToUInt16(entry, at + 4);
                    if (stone == 0 || kinds.ContainsKey(stone)) continue;

                    var row = people.Read(species);
                    var type = row.Length >= PersonalEntry7.Size ? PersonalEntry7.GetTypes(row, 0).First : -1;
                    if (type is >= 0 and < 18) kinds[stone] = type;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
        {
            kinds.Clear();
        }

        return kinds;
    }

    private static string Stamp(string? path) =>
        path is not null && new FileInfo(path) is { Exists: true } file ? $"{path},{file.Length},{file.LastWriteTimeUtc.Ticks}" : "-";

    private string? Find(string gameFile, string extractedName)
    {
        var relative = gameFile.Replace('/', Path.DirectorySeparatorChar);

        var installed = Path.Combine(
            AzaharInstallation.ModDirectory(azahar.Locate(AppContext.BaseDirectory), LayeredFsMod.UltraMoonProgramId),
            "romfs", relative);
        if (File.Exists(installed)) return installed;

        var expansion = Path.Combine(paths.Expansion, "romfs", relative);
        if (File.Exists(expansion)) return expansion;

        if (RomInspector.ScanFolder(paths.Rom).FirstOrDefault(rom => rom.IsSupported) is not { } cartridge) return null;

        var extracted = Path.Combine(Path.GetTempPath(), "permalocke-objetos", extractedName);
        Directory.CreateDirectory(Path.GetDirectoryName(extracted)!);

        return File.Exists(extracted) || new RomFsReader(cartridge.Path).ExtractTo(gameFile, extracted) ? extracted : null;
    }
}
