using PKHeX.Core;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink.Data;

/// <summary>Zone names from PKHeX's Ultra Sun / Ultra Moon location tables.</summary>
public sealed class PkhexLocationLookup(string language = "es") : ILocationLookup
{
    private readonly GameStrings _strings = GameInfo.GetStrings(language);

    public string GetName(int locationId)
    {
        var name = _strings.GetLocationName(false, (ushort)locationId, 7, 7, GameVersion.UM);
        return string.IsNullOrWhiteSpace(name) ? $"Zona {locationId}" : name;
    }
}
