using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>
/// The regional form the gacha and the wonder trade hand a species out in.
/// </summary>
/// <remarks>
/// <para>
/// The same rule the randomizer uses for the world (§138): the ordinary form or any of its regional
/// ones, all equally likely, so a Meowth is Kantonian, Alolan or Galarian a third of the time each.
/// </para>
/// <para>
/// From a stream <b>derived</b> from the roll's and not from the roll's itself. Deriving does not
/// advance the source, so every other number of a roll — species, level, IVs, nature, ability —
/// comes out exactly as it did before §139, and only the form is new. A species without regional
/// forms draws nothing at all.
/// </para>
/// </remarks>
public static class FormDraw
{
    /// <summary>The salt of the derived stream. Changing it changes every form ever rolled.</summary>
    public const string Salt = "forma";

    /// <returns>The form, zero for the ordinary one, and its name, empty for the ordinary one.</returns>
    public static (int Form, string Name) Roll(IRandomSource source, SpeciesStats species)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(species);

        var forms = species.RegionalForms;

        if (forms.Count == 0)
        {
            return (0, string.Empty);
        }

        var pick = source.Derive(Salt).Next(forms.Count + 1);
        return pick == 0 ? (0, string.Empty) : (forms[pick - 1].Form, forms[pick - 1].Name);
    }
}
