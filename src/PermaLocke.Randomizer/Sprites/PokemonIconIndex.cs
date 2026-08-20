using PermaLocke.Randomizer.Rom;
using pk3DS.Core;

namespace PermaLocke.Randomizer.Sprites;

/// <summary>
/// Which icon of <c>a/0/6/2</c> belongs to which species.
/// </summary>
/// <remarks>
/// <para>
/// The cartridge does not publish this anywhere: not in <c>personal</c>, not in the RomFS, not
/// in the uncompressed <c>code.bin</c>. It was worked out by looking at the container, and the
/// container turned out to be two blocks glued together.
/// </para>
/// <list type="bullet">
/// <item>Icons 1 to 866 are species 1 to 649 in National Dex order, each species followed by its
/// own forms. Icon 0 is the egg. That block is computed, not listed: see <see cref="Build(Func{int, int})"/>.</item>
/// <item>Icons 867 to 1153 hold species 650 to 807 in an order that is <b>not</b> the National
/// Dex — Furfrou first, then Phantump, then Litleo — so those 287 icons were identified one by
/// one and are listed in <see cref="UnorderedBlock"/>.</item>
/// <item>A species does not always take as many icons as it declares forms: Pikachu takes ten
/// and declares eight, Arceus takes one and declares eighteen. Those differences are listed in
/// <see cref="Adjustments"/>, every one of them checked by eye against the decoded icon.</item>
/// <item>In the Kanto species that have an Alolan form, <b>the Alolan icon comes first</b>, so
/// the ordinary form sits a slot or two later. That is what <see cref="NormalFormOffsets"/>
/// corrects, and getting it wrong would show an Alolan Raichu for a plain one. The second block
/// has the same trap: the plain Furfrou is the sixth of its eleven icons, not the first.</item>
/// </list>
/// <para>
/// Two numbers have to come out exactly right, and <see cref="Build(Func{int, int})"/> throws if
/// either does not. Species 1 to 649 must consume exactly 866 icons, which is where Genesect ends
/// and the ordered block stops; and species 650 to 807 must consume the remaining 287 with no icon
/// left over and none used twice. That second check is what caught the last mistake: Togedemaru
/// had been read into the pair that really belongs to Jangmo-o and Hakamo-o, and once every other
/// icon was spoken for the leftovers stopped adding up.
/// </para>
/// </remarks>
public static class PokemonIconIndex
{
    /// <summary>Last species of the ordered block. From 650 on the container changes order.</summary>
    public const int OrderedBlockSpecies = 649;

    /// <summary>Icons the ordered block holds, egg aside. Used as a self-check.</summary>
    public const int OrderedBlockIcons = 866;

    /// <summary>Last species the container draws at all.</summary>
    public const int LastSpecies = 807;

    /// <summary>Icons <c>a/0/6/2</c> holds in this cartridge, egg included.</summary>
    public const int ContainerIcons = 1154;

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
    /// Species 650 to 807 and the icon of their ordinary form, identified one at a time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This block keeps each evolution family together and in order — Chespin, Quilladin and
    /// Chesnaught land on three consecutive icons — so it was read family by family instead of
    /// species by species, which is what made it tractable. Families are not always neighbours
    /// though: fifty-six icons separate Noibat from Noivern, and Pancham from Pangoro.
    /// </para>
    /// <para>
    /// Where a species has several pictures the one listed is its ordinary form: Furfrou untrimmed,
    /// Oricorio in its red Baile style, Lycanroc at midday, Wishiwashi alone rather than schooling,
    /// Minior still inside its meteor, Necrozma before absorbing anything, and Magearna in white
    /// rather than its original red.
    /// </para>
    /// <para>
    /// Every entry was checked twice: once picking it out of the container, and again on a grid
    /// laid out in Dex order, where a wrong one stands out because the row stops reading like the
    /// Pokedex. The count in <see cref="Build(Func{int, int})"/> is the third check, and the only
    /// unforgiving one.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<int, int> UnorderedBlock = new()
    {
        [650] = 929,   [651] = 930,   [652] = 931,      // Chespin Quilladin Chesnaught
        [653] = 926,   [654] = 927,   [655] = 928,      // Fennekin Braixen Delphox
        [656] = 932,   [657] = 933,   [658] = 934,      // Froakie Frogadier Greninja
        [659] = 907,   [660] = 908,   [661] = 969,      // Bunnelby Diggersby Fletchling
        [662] = 970,   [663] = 971,   [664] = 883,      // Fletchinder Talonflame Scatterbug
        [665] = 884,   [666] = 885,   [667] = 880,      // Spewpa Vivillon Litleo
        [668] = 881,   [669] = 909,   [670] = 914,      // Pyroar Flabébé Floette
        [671] = 920,   [672] = 942,   [673] = 943,      // Florges Skiddo Gogoat
        [674] = 944,   [675] = 968,   [676] = 872,      // Pancham Pangoro Furfrou
        [677] = 947,   [678] = 948,   [679] = 959,      // Espurr Meowstic Honedge
        [680] = 960,   [681] = 961,   [682] = 976,      // Doublade Aegislash Spritzee
        [683] = 977,   [684] = 957,   [685] = 958,      // Aromatisse Swirlix Slurpuff
        [686] = 940,   [687] = 941,   [688] = 963,      // Inkay Malamar Binacle
        [689] = 964,   [690] = 905,   [691] = 906,      // Barbaracle Skrelp Dragalge
        [692] = 972,   [693] = 974,   [694] = 945,      // Clauncher Clawitzer Helioptile
        [695] = 946,   [696] = 952,   [697] = 953,      // Heliolisk Tyrunt Tyrantrum
        [698] = 954,   [699] = 955,   [700] = 987,      // Amaura Aurorus Sylveon
        [701] = 980,   [702] = 956,   [703] = 985,      // Hawlucha Dedenne Carbink
        [704] = 982,   [705] = 983,   [706] = 984,      // Goomy Sliggoo Goodra
        [707] = 978,   [708] = 878,   [709] = 879,      // Klefki Phantump Trevenant
        [710] = 950,   [711] = 951,   [712] = 966,      // Pumpkaboo Gourgeist Bergmite
        [713] = 967,   [714] = 925,   [715] = 981,      // Avalugg Noibat Noivern
        [716] = 988,   [717] = 990,   [718] = 991,      // Xerneas Yveltal Zygarde
        [719] = 1001,  [720] = 1003,  [721] = 1005,     // Diancie Hoopa Volcanion
        [722] = 1090,  [723] = 1091,  [724] = 1093,     // Rowlet Dartrix Decidueye
        [725] = 1094,  [726] = 1095,  [727] = 1096,     // Litten Torracat Incineroar
        [728] = 1097,  [729] = 1098,  [730] = 1099,     // Popplio Brionne Primarina
        [731] = 1013,  [732] = 1014,  [733] = 1015,     // Pikipek Trumbeak Toucannon
        [734] = 1069,  [735] = 1070,  [736] = 1084,     // Yungoos Gumshoos Grubbin
        [737] = 1085,  [738] = 1086,  [739] = 1112,     // Charjabug Vikavolt Crabrawler
        [740] = 1127,  [741] = 1066,  [742] = 1081,     // Crabominable Oricorio Cutiefly
        [743] = 1082,  [744] = 1072,  [745] = 1074,     // Ribombee Rockruff Lycanroc
        [746] = 1033,  [747] = 1006,  [748] = 1007,     // Wishiwashi Mareanie Toxapex
        [749] = 1088,  [750] = 1089,  [751] = 1100,     // Mudbray Mudsdale Dewpider
        [752] = 1101,  [753] = 1128,  [754] = 1129,     // Araquanid Fomantis Lurantis
        [755] = 1008,  [756] = 1009,  [757] = 1010,     // Morelull Shiinotic Salandit
        [758] = 1011,  [759] = 1103,  [760] = 1104,     // Salazzle Stufful Bewear
        [761] = 1018,  [762] = 1019,  [763] = 1021,     // Bounsweet Steenee Tsareena
        [764] = 1116,  [765] = 1016,  [766] = 1017,     // Comfey Oranguru Passimian
        [767] = 1113,  [768] = 1126,  [769] = 1035,     // Wimpod Golisopod Sandygast
        [770] = 1036,  [771] = 1034,  [772] = 1114,     // Palossand Pyukumuku Type: Null
        [773] = 1115,  [774] = 1043,  [775] = 1023,     // Silvally Minior Komala
        [776] = 1105,  [777] = 1110,  [778] = 1028,     // Turtonator Togedemaru Mimikyu
        [779] = 1106,  [780] = 1107,  [781] = 1108,     // Bruxish Drampa Dhelmise
        [782] = 1024,  [783] = 1025,  [784] = 1026,     // Jangmo-o Hakamo-o Kommo-o
        [785] = 1077,  [786] = 1078,  [787] = 1079,     // Tapu Koko Tapu Lele Tapu Bulu
        [788] = 1080,  [789] = 1131,  [790] = 1132,     // Tapu Fini Cosmog Cosmoem
        [791] = 1133,  [792] = 1134,  [793] = 1135,     // Solgaleo Lunala Nihilego
        [794] = 1137,  [795] = 1138,  [796] = 1139,     // Buzzwole Pheromosa Xurkitree
        [797] = 1141,  [798] = 1140,  [799] = 1136,     // Celesteela Kartana Guzzlord
        [800] = 1124,  [801] = 1143,  [802] = 1144,     // Necrozma Magearna Marshadow
        [803] = 1150,  [804] = 1151,  [805] = 1146,     // Poipole Naganadel Stakataka
        [806] = 1148,  [807] = 1152,                    // Blacephalon Zeraora
    };

    /// <summary>
    /// Builds species → icon for every species the container draws.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// When either block stops adding up. Those two counts are the whole proof this table is
    /// right, so a mismatch means the table, the cartridge or the form counts changed, and the
    /// result must not be used.
    /// </exception>
    public static IReadOnlyDictionary<int, int> Build(GameConfig config) =>
        Build(species => config.Personal[species].FormeCount);

    /// <inheritdoc cref="Build(GameConfig)"/>
    /// <param name="formCountOf">How many forms a species declares, as the cartridge says.</param>
    public static IReadOnlyDictionary<int, int> Build(Func<int, int> formCountOf)
    {
        var index = new Dictionary<int, int>(LastSpecies);
        var cursor = 1;   // 0 is the egg

        for (var species = 1; species <= OrderedBlockSpecies; species++)
        {
            var offset = NormalFormOffsets.GetValueOrDefault(species);
            index[species] = cursor + offset;
            cursor += Math.Max(1, formCountOf(species) + Adjustments.GetValueOrDefault(species));
        }

        if (cursor - 1 != OrderedBlockIcons)
        {
            throw new InvalidDataException(
                $"Las especies 1-{OrderedBlockSpecies} deberían ocupar {OrderedBlockIcons} iconos y ocupan {cursor - 1}. " +
                "La tabla de iconos no cuadra con este cartucho: no se usa.");
        }

        var expected = LastSpecies - OrderedBlockSpecies;
        var outsideTheFirstBlock = UnorderedBlock.Values
            .Count(icon => icon > OrderedBlockIcons && icon < ContainerIcons);
        if (UnorderedBlock.Count != expected ||
            UnorderedBlock.Values.Distinct().Count() != expected ||
            outsideTheFirstBlock != expected)
        {
            throw new InvalidDataException(
                $"Las especies {OrderedBlockSpecies + 1}-{LastSpecies} deberían ser {expected}, cada una con un icono " +
                $"propio entre {OrderedBlockIcons + 1} y {ContainerIcons - 1}. La tabla de iconos está mal editada: no se usa.");
        }

        foreach (var (species, icon) in UnorderedBlock)
        {
            index[species] = icon;
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
