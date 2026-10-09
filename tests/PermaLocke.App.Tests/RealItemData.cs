using System.IO;
using PKHeX.Core;
using PermaLocke.App.Services;
using PermaLocke.Randomizer.Modules;
using PermaLocke.Randomizer.Rom;

namespace PermaLocke.App.Tests;

/// <summary>
/// The item table, the mega table and the <c>code.bin</c> of the expansion that lives in this repository, when this machine
/// has them: where the project lives, not something the tests carry. Tests that need them do nothing without.
/// </summary>
internal sealed record RealItemData(ItemCatalog Catalog, string[] Names, byte[] Code)
{
    private static readonly Lazy<RealItemData?> Once = new(Read);

    /// <summary>Read once: two test classes asking at the same time made PKHeX's name tables fail now and then.</summary>
    public static RealItemData? Load() => Once.Value;

    private static RealItemData? Read()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var expansion = Path.Combine(folder.FullName, "Expansion");
            var tables = Path.Combine(expansion, "romfs", "a", "0", "1");
            var code = Path.Combine(expansion, "exefs", "code.bin");
            if (!File.Exists(Path.Combine(tables, "9")) || !File.Exists(Path.Combine(tables, "5")) || !File.Exists(code)) continue;

            using var table = new GarcPatcher(Path.Combine(tables, "9"));
            var pockets = FieldItemRandomizer.ReadPockets([.. Enumerable.Range(0, table.FileCount).Select(table.Read)]);
            var megas = MegaTrainerRandomizer.ReadStones(Path.Combine(tables, "5")).ToHashSet();
            return new RealItemData(new ItemCatalog(pockets, megas), GameInfo.GetStrings("en").itemlist, File.ReadAllBytes(code));
        }

        return null;
    }
}
