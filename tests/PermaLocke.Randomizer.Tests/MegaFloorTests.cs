using System.Text.Json;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Megas in the important battles start right after the sixth trial, and not one battle sooner.
/// </summary>
/// <remarks>
/// Asked for on 2026-09-21, after the very first important battle — Ilima, in Hauoli — came out with a mega: the floor
/// was 1. The player gets megas after the sixth trial, so the bosses should too. The floor is a cartridge level and the
/// cap a screen level, already raised a fifth (§48); comparing the two directly is what cost a Larvitar in §85, so this
/// ties them through the same arithmetic the randomizer uses.
/// </remarks>
public sealed class MegaFloorTests
{
    internal static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }

    /// <summary>The competition's base edition raises every enemy a fifth, and no role goes below it.</summary>
    private const int BaseEnemyLevelPercent = 20;

    private static int SixthTrialCap()
    {
        using var stream = File.OpenRead(Path.Combine(Root(), "Data", "levelcaps.json"));
        using var document = JsonDocument.Parse(stream);

        return document.RootElement.GetProperty("caps").EnumerateArray()
            .Single(cap => cap.GetProperty("id").GetString() == "trial-06")
            .GetProperty("level").GetInt32();
    }

    [Fact]
    public void The_first_battle_with_a_mega_is_the_first_one_above_the_sixth_trial_cap()
    {
        var options = RandomizerOptionsLoader.Load(Path.Combine(Root(), "Data", "randomizer.json"));
        var cap = SixthTrialCap();
        var floor = options.MegaTrainerMinimumLevel;

        Assert.True(options.MegaTrainers);
        Assert.True(TrainerRandomizer.Raise(floor, BaseEnemyLevelPercent) > cap,
            $"el nivel {floor} de cartucho se ve a {TrainerRandomizer.Raise(floor, BaseEnemyLevelPercent)}: no pasa del cap {cap}");
        Assert.True(TrainerRandomizer.Raise(floor - 1, BaseEnemyLevelPercent) <= cap,
            $"el nivel {floor - 1} de cartucho, que se ve a {TrainerRandomizer.Raise(floor - 1, BaseEnemyLevelPercent)}, "
            + $"ya pasa del cap {cap}: las megas empezarian un combate tarde");
    }

    /// <summary>
    /// The sixth trial itself — Olivia's grand trial, a level 28 battle on the cartridge — has none: the megas start
    /// after it.
    /// </summary>
    /// <remarks>The 28 is measured: trainer 90, the Kahuna Mayla, in <c>RomTool importantes</c> over the base layer.</remarks>
    [Fact]
    public void The_sixth_trial_itself_has_no_mega()
    {
        var options = RandomizerOptionsLoader.Load(Path.Combine(Root(), "Data", "randomizer.json"));

        Assert.True(28 < options.MegaTrainerMinimumLevel);
    }
}
