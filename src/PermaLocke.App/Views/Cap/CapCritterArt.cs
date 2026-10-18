namespace PermaLocke.App.Views;

/// <summary>How a passer-by moves: a fish beats its tail, a serpent waves its whole body, a bird beats its wings.</summary>
public enum CritterMotion
{
    /// <summary>The back half waves; the head stays steady.</summary>
    Fish,

    /// <summary>The whole length waves, head to tail.</summary>
    Serpent,

    /// <summary>Upright (a seahorse, a round one): it bobs and sways, no wave along it.</summary>
    Bob
}

/// <summary>
/// The pixel art of the Pokémon that pass by the sea (singleplayer, 2026-10-13), drawn by hand in profile, head to the left. One letter a pixel
/// and a palette per Pokémon; a capital letter is the same colour but belongs to a wing (or a big fin) and beats round <see cref="Pivot"/>. The
/// outline and the light and shade along the top and bottom edges are added when it is painted, so the drawings are only the colours.
/// </summary>
/// <param name="Frames">Hand-posed frames, each a whole drawing with its own outline and shading, played at <paramref name="Fps"/> in a loop
/// (2026-10-13, the redrawing at the quality of the DS sprites, from the references in <c>Desktop/imagenes</c>). When there are frames they are the
/// animation and <paramref name="Rows"/>, <paramref name="Flap"/> and the wave are not used.</param>
/// <param name="Sheet">A sheet of hand-finished frames inside the app (<c>Views/Cap/Critters/&lt;name&gt;.png</c>): one row per tilt in <paramref name="Angles"/>
/// (degrees the nose is lifted, for a fish drawn facing left), one column per frame of the swim, <paramref name="Cell"/> square cells with the
/// body's pivot at their centre (2026-10-14). When it has a sheet the Pokémon swims a curving path and tilts with it.</param>
/// <param name="Height">The drawing's own height in its cells, for the room it needs in the sea or the sky.</param>
/// <param name="Drift">How far its path drifts up and down on the slow wave, in cells; <paramref name="Bob"/> the short sway on top; <paramref name="Surge"/>
/// how far each beat pushes it ahead.</param>
/// <param name="Pose">How far below the horizontal the drawing's own nose points, in degrees: the tilt that makes it swim level.</param>
/// <param name="Flap">How the wings beat: <c>up</c> (drawn raised, they swing down through the body line and back) or <c>sym</c> (drawn open
/// both ways, they close towards the pivot and open), or null for none.</param>
public sealed record CritterArt(int Species, string Name, CritterMotion Motion, string Palette, string Rows, string? Flap = null, int Pivot = 0,
    double Fps = 0, IReadOnlyList<string>? Frames = null, string? Sheet = null, int Cell = 0, int[]? Angles = null, double Pose = 0,
    int Height = 0, double Drift = 8, double Bob = 1.5, double Surge = 1.6)
{
    public static readonly IReadOnlyDictionary<int, CritterArt> All = Build();

    private static Dictionary<int, CritterArt> Build()
    {
        CritterArt[] all =
        [
            // ------------------------------------------------------------ the sea, common
            new(129, "Magikarp", CritterMotion.Fish, "", "", Fps: 8, Sheet: "magikarp", Cell: 64, Angles: [0, 4, 8, 12, 16, 20, 24], Pose: 15, Height: 57),
            new(118, "Goldeen", CritterMotion.Fish, "", "", Fps: 7, Sheet: "goldeen", Cell: 88, Angles: [0, 4, 8, 12, 16, 20], Pose: 10, Height: 40, Drift: 8, Bob: 1.5, Surge: 1.4),
            new(116, "Horsea", CritterMotion.Fish, "", "", Fps: 10, Sheet: "horsea", Cell: 48, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 35, Drift: 5, Bob: 3, Surge: 0.5),
            new(456, "Finneon", CritterMotion.Fish, "", "", Fps: 9, Sheet: "finneon", Cell: 62, Angles: [0, 4, 8, 12, 16, 20, 24], Pose: 12, Height: 33, Drift: 6, Bob: 1.5, Surge: 0.8),
            new(370, "Luvdisc", CritterMotion.Fish, "", "", Fps: 7, Sheet: "luvdisc", Cell: 50, Angles: [-8, -4, 0, 4, 8], Pose: 0, Height: 34, Drift: 5, Bob: 1.5, Surge: 0.5),
            new(223, "Remoraid", CritterMotion.Fish, "", "", Fps: 9, Sheet: "remoraid", Cell: 54, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 34, Drift: 6, Bob: 1.5, Surge: 1),
            new(211, "Qwilfish", CritterMotion.Fish, "", "", Fps: 8, Sheet: "qwilfish", Cell: 62, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 44, Drift: 5, Bob: 2.5, Surge: 0.5),
            new(746, "Wishiwashi", CritterMotion.Fish, "", "", Fps: 10, Sheet: "wishiwashi", Cell: 60, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 25, Drift: 7, Bob: 2, Surge: 1.1),
            new(170, "Chinchou", CritterMotion.Fish, "", "", Fps: 8, Sheet: "chinchou", Cell: 70, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 35, Drift: 4, Bob: 3, Surge: 0.4),
            new(318, "Carvanha", CritterMotion.Fish, "", "", Fps: 10, Sheet: "carvanha", Cell: 70, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 51, Drift: 6, Bob: 1.5, Surge: 1.3),
            new(779, "Bruxish", CritterMotion.Fish, "", "", Fps: 8, Sheet: "bruxish", Cell: 78, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 39, Drift: 5, Bob: 1.5, Surge: 0.9),

            // ------------------------------------------------------------ the sea, less common and rare
            new(119, "Seaking", CritterMotion.Fish, "", "", Fps: 7, Sheet: "seaking", Cell: 80, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 51, Drift: 5, Bob: 1.5, Surge: 0.8),
            new(226, "Mantine", CritterMotion.Fish, "", "", Fps: 6, Sheet: "mantine", Cell: 96, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 57, Drift: 7, Bob: 2, Surge: 0.9),
            new(320, "Wailmer", CritterMotion.Fish, "", "", Fps: 6, Sheet: "wailmer", Cell: 88, Angles: [-4, 0, 4], Pose: 0, Height: 47, Drift: 4, Bob: 1.5, Surge: 0.6),
            new(319, "Sharpedo", CritterMotion.Fish, "", "", Fps: 9, Sheet: "sharpedo", Cell: 90, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 70, Drift: 7, Bob: 1.5, Surge: 1.6),
            new(457, "Lumineon", CritterMotion.Fish, "", "", Fps: 8, Sheet: "lumineon", Cell: 80, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 57, Drift: 6, Bob: 1.5, Surge: 0.6),
            new(171, "Lanturn", CritterMotion.Fish, "", "", Fps: 7, Sheet: "lanturn", Cell: 86, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 46, Drift: 5, Bob: 2, Surge: 0.6),
            new(369, "Relicanth", CritterMotion.Fish, "", "", Fps: 6, Sheet: "relicanth", Cell: 92, Angles: [16, 22, 28, 34, 40], Pose: 28, Height: 49, Drift: 4, Bob: 1, Surge: 0.6),
            new(117, "Seadra", CritterMotion.Fish, "", "", Fps: 10, Sheet: "seadra", Cell: 68, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 47, Drift: 4, Bob: 2, Surge: 0.7),
            new(131, "Lapras", CritterMotion.Fish, "", "", Fps: 6, Sheet: "lapras", Cell: 88, Angles: [-4, 0, 4, 8], Pose: 0, Height: 66, Drift: 4, Bob: 1, Surge: 1),
            new(130, "Gyarados", CritterMotion.Fish, "", "", Fps: 7, Sheet: "gyarados", Cell: 100, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 76, Drift: 6, Bob: 1.5, Surge: 1.2),
            // The Mega forms (2026-10-16): 10000 + the species, for after the Mega Evolution is unlocked.
            new(10130, "Mega Gyarados", CritterMotion.Fish, "", "", Fps: 7, Sheet: "megagyarados", Cell: 114, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 90, Drift: 6, Bob: 1.5, Surge: 1.2),
            new(10319, "Mega Sharpedo", CritterMotion.Fish, "", "", Fps: 9, Sheet: "megasharpedo", Cell: 100, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 80, Drift: 7, Bob: 1.5, Surge: 1.6),
            new(10382, "Primal Kyogre", CritterMotion.Fish, "", "", Fps: 6, Sheet: "primalkyogre", Cell: 132, Angles: [0, 6, 12, 18, 24], Pose: 12, Height: 77, Drift: 5, Bob: 1.5, Surge: 0.7),
            new(10006, "Mega Charizard X", CritterMotion.Fish, "", "", Fps: 12, Sheet: "megacharizardx", Cell: 102, Angles: [-2, 0, 2], Pose: 0, Height: 60, Drift: 1, Bob: 0.3, Surge: 0.6),
            new(10018, "Mega Pidgeot", CritterMotion.Fish, "", "", Fps: 8, Sheet: "megapidgeot", Cell: 154, Angles: [-2, 0, 2], Pose: 0, Height: 60, Drift: 1, Bob: 0.3, Surge: 0.6),
            new(10142, "Mega Aerodactyl", CritterMotion.Fish, "", "", Fps: 9, Sheet: "megaaerodactyl", Cell: 100, Angles: [-3, 0, 3], Pose: 0, Height: 70, Drift: 3, Bob: 0.5, Surge: 0.9),
            new(10334, "Mega Altaria", CritterMotion.Fish, "", "", Fps: 7, Sheet: "megaaltaria", Cell: 90, Angles: [-2, 0, 2], Pose: 0, Height: 72, Drift: 2, Bob: 0.5, Surge: 0.5),
            new(10149, "Mega Dragonite", CritterMotion.Fish, "", "", Fps: 12, Sheet: "megadragonite", Cell: 80, Angles: [-2, 0, 2], Pose: 0, Height: 60, Drift: 1, Bob: 0.3, Surge: 0.6),
            new(10227, "Mega Skarmory", CritterMotion.Fish, "", "", Fps: 10, Sheet: "megaskarmory", Cell: 82, Angles: [-2, 0, 2], Pose: 0, Height: 66, Drift: 1, Bob: 0.3, Surge: 0.6),
            new(10398, "Mega Staraptor", CritterMotion.Fish, "", "", Fps: 9, Sheet: "megastaraptor", Cell: 98, Angles: [-2, 0, 2], Pose: 0, Height: 83, Drift: 2, Bob: 0.5, Surge: 0.7),
            new(10380, "Mega Latias", CritterMotion.Fish, "", "", Fps: 7, Sheet: "megalatias", Cell: 94, Angles: [-4, -2, 0, 2, 4], Pose: 0, Height: 74, Drift: 4, Bob: 1, Surge: 0.8),
            new(10381, "Mega Latios", CritterMotion.Fish, "", "", Fps: 7, Sheet: "megalatios", Cell: 114, Angles: [-2, 0, 2], Pose: 0, Height: 68, Drift: 2, Bob: 0.5, Surge: 0.7),
            // Shiny Tapu Koko (2026-10-18): it brings the first Z-Crystal to the Gimmighoul ruins (CapTapuKoko); not a visitor of any lane.
            new(785, "Tapu Koko variocolor", CritterMotion.Fish, "", "", Fps: 12, Sheet: "tapukoko", Cell: 168, Angles: [-30, -26, -22], Pose: -26, Height: 93, Drift: 0, Bob: 0, Surge: 0),
            // Machamp and Gigantamax Machamp (2026-10-18, prototype): they punch the second Z-Crystal into the Gimmighoul ruins (CapMachamp);
            // not visitors of any lane. Fps 1: the film picks each frame by its number.
            new(68, "Machamp", CritterMotion.Fish, "", "", Fps: 1, Sheet: "machamp", Cell: 104, Angles: [-40, -20, 0, 20, 40], Pose: 0, Height: 68, Drift: 0, Bob: 0, Surge: 0),
            new(20068, "Machamp Gigamax", CritterMotion.Fish, "", "", Fps: 1, Sheet: "gmaxmachamp", Cell: 96, Angles: [0], Pose: 0, Height: 64, Drift: 0, Bob: 0, Surge: 0),
            new(20069, "Setas Dinamax", CritterMotion.Fish, "", "", Fps: 1, Sheet: "setasdinamax", Cell: 58, Angles: [-60, -30, 0, 30, 60], Pose: 0, Height: 27, Drift: 0, Bob: 0, Surge: 0),
            // Mega Greninja (2026-10-18, prototype): it throws the third Z-Crystal into the ruins (CapGreninja); standing, and mid-flip turned right round.
            new(10658, "Mega Greninja", CritterMotion.Fish, "", "", Fps: 1, Sheet: "megagreninja", Cell: 104, Angles: [0], Pose: 0, Height: 96, Drift: 0, Bob: 0, Surge: 0),
            // Ho-Oh beating its wings (2026-10-18, prototype): it brings the fourth Z-Crystal in a rainbow feather (CapHoOh).
            // Shaymin (2026-10-18, prototype): it brings the fifth Z-Crystal (CapShaymin); Land Forme, Sky Forme and the Gracidea flower.
            new(492, "Shaymin forma Tierra", CritterMotion.Fish, "", "", Fps: 6, Sheet: "shaymintierra", Cell: 62, Angles: [-60, -40, -20, 0, 20, 40, 60], Pose: 0, Height: 31, Drift: 0, Bob: 0, Surge: 0),
            new(10492, "Shaymin forma Cielo", CritterMotion.Fish, "", "", Fps: 10, Sheet: "shaymincielo", Cell: 78, Angles: [-15, -8, 0, 8, 15], Pose: 0, Height: 43, Drift: 0, Bob: 0, Surge: 0),
            new(20492, "Gracídea", CritterMotion.Fish, "", "", Fps: 1, Sheet: "gracidea", Cell: 50, Angles: [0, 30, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330], Pose: 0, Height: 23, Drift: 0, Bob: 0, Surge: 0),
            // Terapagos and Miraidon (2026-10-18, prototype, not animated yet): the sixth Z-Crystal (CapAreaZero).
            new(1024, "Terápagos", CritterMotion.Fish, "", "", Fps: 8, Sheet: "terapagos", Cell: 90, Angles: [-30, -20, -10, 0, 10, 20, 30], Pose: 0, Height: 45, Drift: 0, Bob: 0, Surge: 0),
            new(1008, "Miraidon", CritterMotion.Fish, "", "", Fps: 10, Sheet: "miraidon", Cell: 128, Angles: [-20, -10, 0, 10, 20], Pose: 0, Height: 85, Drift: 0, Bob: 0, Surge: 0),
            new(20250, "Ho-Oh en vuelo", CritterMotion.Fish, "", "", Fps: 12, Sheet: "hoohsagrado", Cell: 154, Angles: [-10, -5, 0, 5, 10], Pose: 0, Height: 85, Drift: 0, Bob: 0, Surge: 0),
            new(20658, "Mega Greninja en el salto", CritterMotion.Fish, "", "", Fps: 1, Sheet: "megagreninjasalto", Cell: 148, Angles: [0, 30, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330], Pose: 0, Height: 96, Drift: 0, Bob: 0, Surge: 0),
            new(350, "Milotic", CritterMotion.Fish, "", "", Fps: 7, Sheet: "milotic", Cell: 106, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 76, Drift: 5, Bob: 1, Surge: 0.7),
            new(230, "Kingdra", CritterMotion.Fish, "", "", Fps: 9, Sheet: "kingdra", Cell: 88, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 66, Drift: 4, Bob: 1.5, Surge: 0.5),
            new(490, "Manaphy", CritterMotion.Fish, "", "", Fps: 8, Sheet: "manaphy", Cell: 72, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 39, Drift: 4, Bob: 2.5, Surge: 0.5),

            // ------------------------------------------------------------ the great ones of the sea
            new(382, "Kyogre", CritterMotion.Fish, "", "", Fps: 6, Sheet: "kyogre", Cell: 132, Angles: [16, 22, 28, 34, 40], Pose: 28, Height: 90, Drift: 5, Bob: 1.5, Surge: 0.7),
            new(321, "Wailord", CritterMotion.Fish, "", "", Fps: 5, Sheet: "wailord", Cell: 110, Angles: [-4, 0, 4], Pose: 0, Height: 84, Drift: 4, Bob: 1, Surge: 0.5),
            new(249, "Lugia", CritterMotion.Fish, "", "", Fps: 6, Sheet: "lugia", Cell: 76, Angles: [-2, 0, 2], Pose: 0, Height: 61, Drift: 2, Bob: 0.5, Surge: 0.6),

            // ------------------------------------------------------------ the sky, common
            new(278, "Wingull", CritterMotion.Fish, "", "", Fps: 9, Sheet: "wingull", Cell: 82, Angles: [-8, -4, 0, 4, 8], Pose: 0, Height: 39, Drift: 10, Bob: 1.5, Surge: 0.8),
            new(16, "Pidgey", CritterMotion.Fish, "", "", Fps: 12, Sheet: "pidgey", Cell: 78, Angles: [-4, 0, 4], Pose: 0, Height: 58, Drift: 1, Bob: 0.3, Surge: 0.6),
            new(21, "Spearow", CritterMotion.Fish, "", "", Fps: 12, Sheet: "spearow", Cell: 92, Angles: [-2, 0, 2], Pose: 0, Height: 60, Drift: 1, Bob: 0.3, Surge: 0.6),
            new(731, "Pikipek", CritterMotion.Fish, "", "", Fps: 14, Sheet: "pikipek", Cell: 84, Angles: [-2, 0, 2], Pose: 0, Height: 58, Drift: 1, Bob: 0.3, Surge: 0.6),
            new(12, "Butterfree", CritterMotion.Fish, "", "", Fps: 8, Sheet: "butterfree", Cell: 76, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 52, Drift: 8, Bob: 3.5, Surge: 0.6),
            new(333, "Swablu", CritterMotion.Fish, "", "", Fps: 8, Sheet: "swablu", Cell: 94, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 38, Drift: 8, Bob: 3, Surge: 0.6),
            new(41, "Zubat", CritterMotion.Fish, "", "", Fps: 12, Sheet: "zubat", Cell: 76, Angles: [-8, -4, 0, 4, 8], Pose: 0, Height: 44, Drift: 7, Bob: 3.5, Surge: 1),

            // ------------------------------------------------------------ the sky, less common and rare
            new(279, "Pelipper", CritterMotion.Fish, "", "", Fps: 7, Sheet: "pelipper", Cell: 88, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 61, Drift: 7, Bob: 2, Surge: 0.8),
            new(18, "Pidgeot", CritterMotion.Fish, "", "", Fps: 8, Sheet: "pidgeot", Cell: 108, Angles: [-2, 0, 2], Pose: 0, Height: 60, Drift: 1, Bob: 0.3, Surge: 0.6),
            new(22, "Fearow", CritterMotion.Fish, "", "", Fps: 8, Sheet: "fearow", Cell: 100, Angles: [-1, 0, 1], Pose: 0, Height: 61, Drift: 1, Bob: 0.3, Surge: 0.8),
            new(733, "Toucannon", CritterMotion.Fish, "", "", Fps: 10, Sheet: "toucannon", Cell: 120, Angles: [-2, 0, 2], Pose: 0, Height: 60, Drift: 1, Bob: 0.3, Surge: 0.6),
            new(334, "Altaria", CritterMotion.Fish, "", "", Fps: 7, Sheet: "altaria", Cell: 80, Angles: [-2, 0, 2], Pose: 0, Height: 61, Drift: 2, Bob: 0.5, Surge: 0.5),
            new(227, "Skarmory", CritterMotion.Fish, "", "", Fps: 10, Sheet: "skarmory", Cell: 82, Angles: [-2, 0, 2], Pose: 0, Height: 60, Drift: 1, Bob: 0.3, Surge: 0.6),
            new(398, "Staraptor", CritterMotion.Fish, "", "", Fps: 9, Sheet: "staraptor", Cell: 108, Angles: [-2, 0, 2], Pose: 0, Height: 60, Drift: 1, Bob: 0.3, Surge: 0.6),
            new(169, "Crobat", CritterMotion.Fish, "", "", Fps: 11, Sheet: "crobat", Cell: 112, Angles: [-8, -4, 0, 4, 8], Pose: 0, Height: 61, Drift: 9, Bob: 2.5, Surge: 1.2),
            new(468, "Togekiss", CritterMotion.Fish, "", "", Fps: 8, Sheet: "togekiss", Cell: 100, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 56, Drift: 5, Bob: 1, Surge: 0.7),
            new(149, "Dragonite", CritterMotion.Fish, "", "", Fps: 12, Sheet: "dragonite", Cell: 82, Angles: [-2, 0, 2], Pose: 0, Height: 60, Drift: 1, Bob: 0.3, Surge: 0.6),
            new(373, "Salamence", CritterMotion.Fish, "", "", Fps: 6, Sheet: "salamence", Cell: 148, Angles: [-2, 0, 2], Pose: 0, Height: 60, Drift: 1.2, Bob: 0, Surge: 0),
            new(6, "Charizard", CritterMotion.Fish, "", "", Fps: 12, Sheet: "charizard", Cell: 102, Angles: [-2, 0, 2], Pose: 0, Height: 60, Drift: 1, Bob: 0.3, Surge: 0.6),
            new(142, "Aerodactyl", CritterMotion.Fish, "", "", Fps: 9, Sheet: "aerodactyl", Cell: 100, Angles: [-3, 0, 3], Pose: 0, Height: 61, Drift: 3, Bob: 0.5, Surge: 0.9),
            new(380, "Latias", CritterMotion.Fish, "", "", Fps: 7, Sheet: "latias", Cell: 88, Angles: [-4, -2, 0, 2, 4], Pose: 0, Height: 59, Drift: 4, Bob: 1, Surge: 0.8),
            new(381, "Latios", CritterMotion.Fish, "", "", Fps: 7, Sheet: "latios", Cell: 78, Angles: [-2, 0, 2], Pose: 0, Height: 61, Drift: 2, Bob: 0.5, Surge: 0.7),

            // ------------------------------------------------------------ the legends of the sky
            new(384, "Rayquaza", CritterMotion.Fish, "", "", Fps: 7, Sheet: "rayquaza", Cell: 80, Angles: [-1, 0, 1], Pose: 0, Height: 61, Drift: 2, Bob: 0.5, Surge: 0.7),
            new(250, "Ho-Oh", CritterMotion.Fish, "", "", Fps: 6, Sheet: "hooh", Cell: 80, Angles: [-1, 0, 1], Pose: 0, Height: 61, Drift: 2, Bob: 0.5, Surge: 0.6),
            new(144, "Articuno", CritterMotion.Fish, "", "", Fps: 7, Sheet: "articuno", Cell: 76, Angles: [-2, 0, 2], Pose: 0, Height: 61, Drift: 2, Bob: 0.5, Surge: 0.6),
            new(145, "Zapdos", CritterMotion.Fish, "", "", Fps: 9, Sheet: "zapdos", Cell: 108, Angles: [-2, 0, 2], Pose: 0, Height: 61, Drift: 2, Bob: 0.5, Surge: 0.9),
            new(146, "Moltres", CritterMotion.Fish, "", "", Fps: 8, Sheet: "moltres", Cell: 90, Angles: [-2, 0, 2], Pose: 0, Height: 61, Drift: 2, Bob: 0.5, Surge: 0.8),
        ];

        return all.ToDictionary(a => a.Species);
    }
}
