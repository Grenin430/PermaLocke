using System.Text.Json;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The level from which every trainer carries final evolutions, against the table it came from.
/// </summary>
/// <remarks>
/// <para>
/// This exists because of a bug that reached the player's game. The rule is «from the sixth trial
/// on», the only story signal a static mod has is the level the cartridge gave each team, and the
/// number was copied out of <c>Data/levelcaps.json</c> — which is expressed in <b>raised</b>
/// levels, because a cap is the boss's level plus the competition's 20% (§48). The comparison, on
/// the other hand, is against the <b>cartridge</b> level, and the file's own comment said so.
/// </para>
/// <para>
/// Five levels of daylight between the two units, and nothing failed: a Team Skull grunt sat at
/// cartridge level 33 with a Larvitar, which is between the sixth trial and the seventh, and the
/// rule skipped it. The number that belongs there is the sixth trial's Totem, measured from the
/// static table: <b>Vikavolt at 29</b>. The eight Totems read 12, 20, 22, 24, 29, 33, 35 and 49,
/// and multiplying them by 1.2 lands on the caps, which is the cross-check nobody ran.
/// </para>
/// </remarks>
public sealed class FullyEvolvedThresholdTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }

    private static int Threshold()
    {
        using var stream = File.OpenRead(Path.Combine(Root(), "Data", "randomizer.json"));
        using var document = JsonDocument.Parse(stream);

        return document.RootElement.GetProperty("fullyEvolvedFromLevel").GetInt32();
    }

    private static int SixthTrialCap()
    {
        using var stream = File.OpenRead(Path.Combine(Root(), "Data", "levelcaps.json"));
        using var document = JsonDocument.Parse(stream);

        return document.RootElement.GetProperty("caps").EnumerateArray()
            .First(cap => cap.GetProperty("id").GetString() == "trial-06")
            .GetProperty("level").GetInt32();
    }

    /// <summary>
    /// 25, four under the sixth Totem (Vikavolt, 29), chosen by the player on 2026-09-24 so the route trainers after
    /// the sixth trial with 25-28 cartridge Pokémon evolve too. Some trainers before it evolve as well; that is the price.
    /// </summary>
    [Fact]
    public void The_threshold_is_the_one_the_player_chose()
    {
        Assert.Equal(25, Threshold());
    }

    /// <summary>
    /// The two numbers are in different units, so the threshold has to be the lower one.
    /// </summary>
    /// <remarks>
    /// This is the assertion that would have caught it. A cap is a level the <b>player</b> may
    /// reach, already carrying the competition's 20%; the threshold is a level the <b>cartridge</b>
    /// wrote. Whenever they are equal, somebody has copied one into the other.
    /// </remarks>
    [Fact]
    public void The_threshold_is_not_the_cap_of_that_stage()
    {
        var threshold = Threshold();
        var cap = SixthTrialCap();

        Assert.True(threshold < cap,
            $"El corte ({threshold}) no puede llegar al cap de la sexta prueba ({cap}): el cap ya "
            + "va subido un 20% y esto se compara contra el nivel del cartucho.");
    }

    /// <summary>It never goes above the sixth Totem (29), or trainers from the sixth trial on would be left out.</summary>
    [Fact]
    public void The_threshold_does_not_pass_the_sixth_totem()
    {
        Assert.True(Threshold() <= 29);
    }
}
