using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The locator against the real executable, when there is one on this machine.
/// </summary>
/// <remarks>
/// Skips itself when the mod is not installed, because <c>code.bin</c> is six megabytes of
/// Nintendo's and does not live in the repository. What it pins is the one thing the unit tests
/// cannot: that the rule written down here picks out, on a real 5.9 MB executable, exactly the
/// address that was measured by hand — <c>0x4E6860</c>, sixty-seven moves.
/// </remarks>
public sealed class TutorRealCodeTests
{
    private static string? RealCode()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        var path = Path.Combine(directory?.FullName ?? ".", "Expansion", "exefs", "code.bin");
        return File.Exists(path) ? path : null;
    }

    [Fact]
    public void It_lands_on_the_address_measured_by_hand()
    {
        if (RealCode() is not { } path)
        {
            return;
        }

        var code = File.ReadAllBytes(path);
        var at = TutorTable.Find(code);

        Assert.Equal(0x4E6860, at);

        var moves = TutorTable.Read(code, at);

        Assert.Equal(67, moves.Length);
        Assert.Equal(moves.Length, moves.Distinct().Count());

        foreach (var anchor in TutorTable.Anchors)
        {
            Assert.Contains(anchor, moves);
        }
    }
}
