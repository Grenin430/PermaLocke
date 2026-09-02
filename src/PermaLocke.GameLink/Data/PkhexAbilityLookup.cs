using PKHeX.Core;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink.Data;

/// <summary>Ability names out of PKHeX, in the language the player reads.</summary>
/// <remarks>
/// The table is built once and kept: resolving sixty names one by one over a linear search of
/// three hundred strings is cheap, but it happens every time a wheel is built.
/// </remarks>
public sealed class PkhexAbilityLookup : IAbilityLookup
{
    /// <summary>
    /// Last ability Ultra Moon knows: 233, Fuerza Cerebral.
    /// </summary>
    /// <remarks>
    /// Not a guess. PKHeX's list is the current one and runs to well over three hundred, so it
    /// carries abilities that exist in gen 8 and 9 and not here. Written into a PK7 they are a
    /// number the game has no name for.
    /// </remarks>
    public const int LastGen7Ability = IAbilityLookup.LastUsableAbility;

    private readonly string[] _names;
    private readonly Dictionary<string, int> _ids;

    public PkhexAbilityLookup(string language = "es")
    {
        _names = GameInfo.GetStrings(language).abilitylist;
        _ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var id = 1; id < _names.Length; id++)
        {
            _ids.TryAdd(_names[id], id);
        }
    }

    public int LastAbility => LastGen7Ability;

    public int GetId(string name) =>
        !string.IsNullOrWhiteSpace(name) && _ids.TryGetValue(name.Trim(), out var id) ? id : 0;

    public string GetName(int abilityId) =>
        abilityId > 0 && abilityId < _names.Length ? _names[abilityId] : $"Habilidad {abilityId}";
}
