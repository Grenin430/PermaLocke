using PermaLocke.Randomizer.Rom;
using pk3DS.Core;

namespace PermaLocke.Randomizer.Sprites;

/// <summary>
/// Which icon of <c>a/0/6/2</c> belongs to which species.
/// </summary>
/// <remarks>
/// <para>
/// The cartridge does not publish this anywhere: not in <c>personal</c>, not in the RomFS, not
/// in the uncompressed <c>code.bin</c>. It was worked out by looking at the container, and what
/// came out is this:
/// </para>
/// <list type="bullet">
/// <item>Icons 1 to 866 are species 1 to 649 in National Dex order, each species followed by
/// its own forms. Icon 0 is the egg.</item>
/// <item>From icon 867 on, the order is <b>not</b> the National Dex any more — Furfrou comes
/// first, then Phantump, then Litleo — so species 650 to 807 are left out until somebody
/// identifies those 287 icons one by one.</item>
/// <item>A species does not always take as many icons as it declares forms: Pikachu takes ten
/// and declares eight, Arceus takes one and declares eighteen. Those differences are listed in
/// <see cref="Adjustments"/>, every one of them checked by eye against the decoded icon.</item>
/// <item>In the Kanto species that have an Alolan form, <b>the Alolan icon comes first</b>, so
/// the ordinary form sits a slot or two later. That is what <see cref="NormalFormOffsets"/>
/// corrects, and getting it wrong would show an Alolan Raichu for a plain one.</item>
/// </list>
/// <para>
/// The whole thing is verified by a number that has to come out exactly right: species 1 to 649
/// must consume exactly 866 icons, which is where Genesect ends and the ordered block stops.
/// </para>
/// </remarks>
public static class PokemonIconIndex
{
    /// <summary>Last species whose icon is known. Beyond this the container changes order.</summary>
    public const int LastKnownSpecies = 649;

    /// <summary>Icons the ordered block holds, egg aside. Used as a self-check.</summary>
    public const int OrderedBlockIcons = 866;

    /// <summary>Index of the egg icon, which is the only one that needs no working out.</summary>
    public const int EggIcon = 0;

    /// <summary>
    /// Icons a species takes beyond what <c>personal</c> declares. Negative means fewer.
    /// Every entry was read off the decoded container.
    /// </summary>
    private static readonly Dictionary<int, int> Adjustments = new()
    {
        // Extra icons. Most are the female variant, which the cartridge stores as a separate
        // picture right after the male one even though it is not a form.
        [25] = 2,    // Pikachu: ten icons, eight forms
        [35] = 1,    // Clefairy
        [36] = 1,    // Clefable
        [39] = 1,    // Jigglypuff
        [40] = 1,    // Wigglytuff
        [51] = 1,    // Dugtrio
        [61] = 1,    // Poliwhirl
        [62] = 1,    // Poliwrath
        [89] = 1,    // Muk
        [99] = 1,    // Kingler
        [159] = 1,   // Croconaw
        [173] = 1,   // Cleffa
        [174] = 1,   // Igglybuff
        [186] = 1,   // Politoed
        [201] = 18,  // Unown: 46 icons for 28 forms
        [215] = 1,   // Sneasel
        [216] = 1,   // Teddiursa
        [315] = 1,   // Roselia
        [335] = 1,   // Zangoose
        [336] = 1,   // Seviper
        [351] = 1,   // Castform
        [359] = 2,   // Absol: both the plain and the mega are doubled
        [377] = 1,   // Regirock
        [389] = 1,   // Torterra
        [406] = 1,   // Budew
        [407] = 1,   // Roserade
        [467] = 1,   // Magmortar
        [468] = 1,   // Togekiss
        [479] = 1,   // Rotom: seven icons for six forms
        [492] = 1,   // Shaymin
        [500] = 1,   // Emboar
        [513] = 1,   // Pansear
        [514] = 1,   // Simisear
        [521] = 1,   // Unfezant
        [529] = 1,   // Drilbur
        [530] = 1,   // Excadrill
        [539] = 1,   // Sawk
        [549] = 1,   // Lilligant
        [569] = 1,   // Garbodor
        [577] = 1,   // Solosis
        [584] = 1,   // Vanilluxe
        [591] = 1,   // Amoonguss
        [592] = 1,   // Frillish
        [593] = 1,   // Jellicent
        [599] = 1,   // Klink
        [600] = 1,   // Klang
        [601] = 1,   // Klinklang
        [607] = 1,   // Litwick
        [622] = 1,   // Golett
        [623] = 1,   // Golurk
        [646] = 3,   // Kyurem: every form twice
        [647] = 1,   // Keldeo
        [648] = 2,   // Meloetta: both forms twice

        // Fewer icons than forms: the cartridge draws one picture for the lot.
        [414] = -2,  // Mothim: three forms, one icon
        [493] = -17, // Arceus: eighteen forms, one icon
    };

    /// <summary>
    /// How far past the first icon of the species its <b>ordinary</b> form sits.
    /// </summary>
    /// <remarks>
    /// The Kanto species with an Alolan form list the Alolan icon first. Dugtrio and Muk have
    /// two Alolan icons, so their ordinary form is two slots along. Every one checked by eye.
    /// </remarks>
    private static readonly Dictionary<int, int> NormalFormOffsets = new()
    {
        [19] = 1,  // Rattata
        [20] = 1,  // Raticate
        [26] = 1,  // Raichu
        [27] = 1,  // Sandshrew
        [28] = 1,  // Sandslash
        [37] = 1,  // Vulpix
        [38] = 1,  // Ninetales
        [50] = 1,  // Diglett
        [51] = 2,  // Dugtrio
        [52] = 1,  // Meowth
        [53] = 1,  // Persian
        [74] = 1,  // Geodude
        [75] = 1,  // Graveler
        [76] = 1,  // Golem
        [88] = 1,  // Grimer
        [89] = 2,  // Muk
        [103] = 1, // Exeggutor
        [105] = 1, // Marowak
    };

    /// <summary>
    /// Builds species → icon for every species the container puts in a known place.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// When the ordered block does not add up to <see cref="OrderedBlockIcons"/>. That number is
    /// the whole proof this table is right, so a mismatch means the table, the cartridge or the
    /// form counts changed and the result must not be used.
    /// </exception>
    public static IReadOnlyDictionary<int, int> Build(GameConfig config) =>
        Build(species => config.Personal[species].FormeCount);

    /// <inheritdoc cref="Build(GameConfig)"/>
    /// <param name="formCountOf">How many forms a species declares, as the cartridge says.</param>
    public static IReadOnlyDictionary<int, int> Build(Func<int, int> formCountOf)
    {
        var index = new Dictionary<int, int>(LastKnownSpecies);
        var cursor = 1;   // 0 is the egg

        for (var species = 1; species <= LastKnownSpecies; species++)
        {
            var offset = NormalFormOffsets.GetValueOrDefault(species);
            index[species] = cursor + offset;
            cursor += Math.Max(1, formCountOf(species) + Adjustments.GetValueOrDefault(species));
        }

        if (cursor - 1 != OrderedBlockIcons)
        {
            throw new InvalidDataException(
                $"Las especies 1-{LastKnownSpecies} deberían ocupar {OrderedBlockIcons} iconos y ocupan {cursor - 1}. " +
                "La tabla de iconos no cuadra con este cartucho: no se usa.");
        }

        return index;
    }

    /// <summary>Builds the table straight from a cartridge.</summary>
    public static async Task<IReadOnlyDictionary<int, int>> BuildAsync(string romPath, string scratchDirectory,
        CancellationToken ct = default)
    {
        using var workspace = await RomWorkspace.ExtractAsync(romPath, scratchDirectory, ct: ct);
        return Build(workspace.Config);
    }
}
