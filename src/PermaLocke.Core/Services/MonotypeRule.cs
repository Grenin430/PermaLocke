using System.Collections.Concurrent;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>
/// What a MONOTYPE role means (§220): which species it admits, and whether the run is owning only those.
/// </summary>
/// <remarks>
/// <para>
/// The role names a type (<see cref="Role.MonoType"/>). A Pokémon is <b>admitted</b> when that type is one of its one or
/// two, in the form it has: the cartridge's own types and, for the gen 8-9 expansion and the regional forms, the installed
/// world's (<see cref="ITypeLookup"/>). The list is therefore computed, not written down: a new species or a retyped one
/// cannot be missing from it.
/// </para>
/// <para>
/// Three things use it. The gacha and the wonder trade only hand out admitted species. And the gacha refuses to roll while
/// the player <b>holds</b> a Pokémon that is not admitted. What is held is what the save says (party and boxes, the last
/// thing the player saved): a released one is gone from it and stops counting, which the run's registry never learns. A fallen
/// one does not count either, by the PID the run recorded its death with. Without a readable save it falls back on the
/// registry's living ones. Nothing is checked about what the player fights with.
/// </para>
/// </remarks>
public sealed class MonotypeRule(
    IRoleCatalog roles,
    ITypeLookup types,
    ISpeciesStatsCatalog stats,
    IPokemonRepository pokemon,
    IBoxReader? boxes = null)
{
    private readonly ConcurrentDictionary<int, IReadOnlySet<int>> _species = new();

    /// <summary>The run's role, or null when it has none the catalogue knows.</summary>
    public Role? RoleOf(Run run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return roles.Find(run.RoleId);
    }

    /// <summary>The type this run is restricted to, or null when its role is not a MONOTYPE one (or has no role).</summary>
    public int? TypeOf(Run run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return roles.Find(run.RoleId)?.MonoType;
    }

    /// <summary>Whether a Pokémon of this species and form has the type.</summary>
    public bool Allows(int type, int species, int form = 0)
    {
        var pair = types.GetTypes(species, form);
        return pair.First == type || pair.Second == type;
    }

    /// <summary>
    /// Species with the type in their ordinary form or in any regional one: a Galarian Ponyta makes Ponyta admitted in a
    /// PSÍQUICO run, and <see cref="FormFor"/> then gives it in the form that qualifies.
    /// </summary>
    public IReadOnlySet<int> SpeciesOf(int type) => _species.GetOrAdd(type, t =>
        stats.All
            .Where(s => Allows(t, s.Id) || s.RegionalForms.Any(form => Allows(t, s.Id, form.Form)))
            .Select(s => s.Id)
            .ToHashSet());

    /// <summary>
    /// The form to hand a species out in: the one that was drawn when it has the type, otherwise the ordinary form when that
    /// does, otherwise the first regional form that does.
    /// </summary>
    public (int Form, string Name) FormFor(int type, SpeciesStats species, int drawn, string drawnName)
    {
        ArgumentNullException.ThrowIfNull(species);

        if (Allows(type, species.Id, drawn))
        {
            return (drawn, drawnName);
        }

        if (Allows(type, species.Id, 0))
        {
            return (0, string.Empty);
        }

        foreach (var form in species.RegionalForms)
        {
            if (Allows(type, species.Id, form.Form))
            {
                return (form.Form, form.Name);
            }
        }

        return (drawn, drawnName);
    }

    /// <summary>
    /// The names of the Pokémon the player holds that the type does not admit, in the order the save lists them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What is held</b> comes from the save (party and boxes), not from the run's registry: the registry learns of captures,
    /// deliveries, deaths and trades, but never of a release, so a Pokémon the player let go would stay «alive» in it for ever
    /// and the gacha would never open again. A Pokémon the run has recorded as fallen is skipped by its PID: it is a corpse in
    /// a box, not a Pokémon the player is keeping. One whose entry is damaged (what the game draws as a Huevo Malo) is skipped
    /// too: nobody can say what it is.
    /// </para>
    /// <para>
    /// The save is the last thing the player saved, so a release only counts once the game has saved it. With no save to read
    /// (no reader, no file, an unreadable one) the registry's living Pokémon are used instead, as the rule first did.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<string>> InvalidAsync(Run run, int type, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var registered = await pokemon.GetAllAsync(run.Id, ct).ConfigureAwait(false);

        if (boxes is not null && await boxes.ReadAsync(ct).ConfigureAwait(false) is { Available: true } held)
        {
            var fallen = registered
                .Where(p => p.Status == PokemonStatus.Dead && p.Pid is > 0)
                .Select(p => p.Pid!.Value)
                .ToHashSet();

            return
            [
                .. held.Boxes
                    .SelectMany(box => box.Pokemon)
                    .Where(p => p.IsIntact && !fallen.Contains(p.Pid) && !Allows(type, p.Species, p.Form))
                    .Select(p => p.DisplayName)
            ];
        }

        return
        [
            .. registered
                .Where(p => p.Status == PokemonStatus.Alive && p.Species > 0 && !Allows(type, p.Species, p.Form))
                .Select(p => p.Nickname ?? p.SpeciesName)
        ];
    }

    /// <summary>What the player is told when the gacha refuses to roll.</summary>
    public string BlockedMessage(Run run, IReadOnlyList<string> invalid)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(invalid);

        var name = roles.Find(run.RoleId)?.Name ?? run.RoleId;
        var rest = invalid.Count > 3 ? $" y {invalid.Count - 3} más" : string.Empty;

        return $"Equipo no válido para {name}: {string.Join(", ", invalid.Take(3))}{rest} no es de tu tipo. "
               + "Suéltalo y guarda la partida para volver a usar el gacha.";
    }
}
