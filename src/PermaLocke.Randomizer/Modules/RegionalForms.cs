using PermaLocke.Core.Abstractions;

namespace PermaLocke.Randomizer.Modules;

/// <summary>A species and the regional forms it may appear in, as configured.</summary>
/// <param name="Forms">Form indexes other than zero: Alola, Galar, Hisui, Paldea.</param>
public sealed record RegionalFormEntry(int Species, IReadOnlyList<int> Forms, string Note = "");

/// <summary>
/// Which regional forms a randomized Pokémon may come out in, and the pick between them.
/// </summary>
/// <remarks>
/// <para>
/// Until §138 every module wrote form 0, so nothing randomized ever came out as an Alolan, Galarian,
/// Hisuian or Paldean form, although every table the game reads — wild slots, trainer teams,
/// statics and gifts — has room for the form next to the species.
/// </para>
/// <para>
/// The form is decided <b>after</b> the species, from a stream of its own, and only for species that
/// have regional forms. So the species in every slot is exactly what it was, draw for draw, and a
/// world already being played only changes in that some of its Vulpix are now Alolan. Each form is as
/// likely as the ordinary one: a Meowth is Kantonian, Alolan or Galarian a third of the time each.
/// </para>
/// <para>
/// Which forms are regional is configuration, not a guess: the list was measured against the
/// expansion mod — 59 forms named after a region, every one with the official types — minus Pikachu's
/// «Alola» cap, which is a costume, and Galarian Darmanitan's Zen mode, which only exists in battle.
/// A form the loaded world does not declare is dropped here, so the cartridge, which has only the
/// Alolan ones, gets only the Alolan ones.
/// </para>
/// </remarks>
public sealed class RegionalForms
{
    private readonly Dictionary<int, int[]> _forms;

    /// <summary>Species whose ordinary form is out of a restricted pool: only a regional form may come out.</summary>
    private readonly HashSet<int> _regionalOnly = [];

    private RegionalForms(Dictionary<int, int[]> forms) => _forms = forms;

    /// <summary>
    /// The same forms, kept only where the form passes the test. A species whose ordinary form fails it comes out in a
    /// regional one every time (a Galarian Ponyta in a PSÍQUICO world).
    /// </summary>
    public RegionalForms Restrict(Func<int, int, bool> keep)
    {
        var forms = new Dictionary<int, int[]>();
        var restricted = new RegionalForms(forms);

        foreach (var (species, all) in _forms)
        {
            int[] valid = [.. all.Where(form => keep(species, form))];
            var ordinary = keep(species, 0);

            if (valid.Length == 0)
            {
                continue;
            }

            forms[species] = valid;

            if (!ordinary)
            {
                restricted._regionalOnly.Add(species);
            }
        }

        return restricted;
    }

    /// <summary>No regional forms at all: everything comes out in its ordinary form, as before.</summary>
    public static RegionalForms None { get; } = new([]);

    /// <summary>Species that can come out in a regional form.</summary>
    public int SpeciesCount => _forms.Count;

    /// <summary>Regional forms in total.</summary>
    public int FormCount => _forms.Values.Sum(forms => forms.Length);

    /// <param name="configured">The list from the configuration.</param>
    /// <param name="formCountOf">How many forms the loaded world declares for a species.</param>
    public static RegionalForms From(IReadOnlyList<RegionalFormEntry> configured, Func<int, int> formCountOf)
    {
        ArgumentNullException.ThrowIfNull(configured);
        ArgumentNullException.ThrowIfNull(formCountOf);

        var forms = new Dictionary<int, int[]>();

        foreach (var entry in configured)
        {
            var declared = formCountOf(entry.Species);
            int[] valid = [.. entry.Forms.Where(form => form > 0 && form < declared).Distinct().Order()];

            if (valid.Length > 0)
            {
                forms[entry.Species] = valid;
            }
        }

        return new RegionalForms(forms);
    }

    /// <summary>
    /// The form this species comes out in: zero, or one of its regional forms, all equally likely.
    /// </summary>
    /// <remarks>
    /// A species without regional forms draws nothing, so adding or removing one from the list only
    /// moves the forms of the species that come after it, never a species.
    /// </remarks>
    public int Pick(IRandomSource source, int species)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!_forms.TryGetValue(species, out var forms))
        {
            return 0;
        }

        if (_regionalOnly.Contains(species))
        {
            return forms[source.Next(forms.Length)];
        }

        var pick = source.Next(forms.Length + 1);
        return pick == 0 ? 0 : forms[pick - 1];
    }

    /// <summary>The regional forms of a species, empty when it has none.</summary>
    public IReadOnlyList<int> Of(int species) => _forms.TryGetValue(species, out var forms) ? forms : [];
}
