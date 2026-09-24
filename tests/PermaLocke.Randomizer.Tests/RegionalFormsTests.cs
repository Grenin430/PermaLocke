using PermaLocke.Core.Services;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Regional forms: decided after the species, from their own draw, and only ones the world has (§138).
/// </summary>
public sealed class RegionalFormsTests
{
    private static readonly RegionalFormEntry[] Some =
    [
        new(37, [1], "Vulpix de Alola"),
        new(52, [1, 2], "Meowth de Alola y de Galar"),
        new(58, [1], "Growlithe de Hisui"),
    ];

    /// <summary>The cartridge declares Meowth's Alolan form but not its Galarian one, nor any Hisuian.</summary>
    [Fact]
    public void Only_the_forms_the_world_declares_are_kept()
    {
        var cartridge = RegionalForms.From(Some, species => species switch { 37 => 2, 52 => 2, _ => 1 });

        Assert.Equal([1], cartridge.Of(37));
        Assert.Equal([1], cartridge.Of(52));
        Assert.Empty(cartridge.Of(58));
        Assert.Equal(2, cartridge.SpeciesCount);
    }

    /// <summary>
    /// A species without regional forms draws nothing, so the stream is left exactly where it was.
    /// </summary>
    [Fact]
    public void A_species_without_forms_draws_nothing()
    {
        var forms = RegionalForms.From(Some, _ => 4);
        var used = new SeededRandomSource(99);
        var untouched = new SeededRandomSource(99);

        Assert.Equal(0, forms.Pick(used, 25));
        Assert.Equal(untouched.Next(1000), used.Next(1000));
    }

    /// <summary>Every form, the ordinary one included, is as likely as the others.</summary>
    [Fact]
    public void Every_form_comes_out_about_as_often()
    {
        var forms = RegionalForms.From(Some, _ => 3);
        var source = new SeededRandomSource(20260918);
        var seen = new int[3];

        for (var i = 0; i < 3000; i++)
        {
            seen[forms.Pick(source, 52)]++;
        }

        Assert.All(seen, count => Assert.InRange(count, 900, 1100));
    }

    [Fact]
    public void Without_a_list_everything_comes_out_in_its_ordinary_form()
    {
        Assert.Equal(0, RegionalForms.None.Pick(new SeededRandomSource(1), 37));
        Assert.Equal(0, RegionalForms.From([], _ => 4).FormCount);
    }

    /// <summary>
    /// The configured list: the 57 regional forms of the expansion, without Pikachu's cap or
    /// Galarian Darmanitan's Zen mode.
    /// </summary>
    [Fact]
    public void The_configured_list_is_the_measured_one()
    {
        var options = RandomizerOptionsLoader.Load(Path.Combine(FindRoot(), "Data", "randomizer.json"));
        var entries = options.RegionalForms;

        Assert.Equal(54, entries.Count);
        Assert.Equal(57, entries.Sum(entry => entry.Forms.Count));
        Assert.DoesNotContain(entries, entry => entry.Species == 25);
        Assert.Equal([2], entries.Single(entry => entry.Species == 555).Forms);
        Assert.Equal([2], entries.Single(entry => entry.Species == 80).Forms);
        Assert.Equal([1, 2, 3], entries.Single(entry => entry.Species == 128).Forms);
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PermaLocke.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new DirectoryNotFoundException("No se encuentra la raíz.");
    }
}
