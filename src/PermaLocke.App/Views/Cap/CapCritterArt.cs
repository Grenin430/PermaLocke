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
            // Poliwag (2026-10-22, the player sends the sprites one by one): its fin beats on its root, its foot kicks
            new(60, "Poliwag", CritterMotion.Fish, "", "", Fps: 8, Sheet: "poliwag", Cell: 72, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 35, Drift: 5, Bob: 2, Surge: 0.8),

            // ------------------------------------------------------------ the sea, less common and rare
            // Tentacruel (2026-10-22): a jellyfish; its hood pulses, its flaps beat, its tentacles trail in a slow wave
            new(73, "Tentacruel", CritterMotion.Fish, "", "", Fps: 7, Sheet: "tentacruel", Cell: 88, Angles: [-3, 0, 3], Pose: 0, Height: 63, Drift: 4, Bob: 2.5, Surge: 0.4),
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
            // Mega Floette (2026-10-23): pixelized from the official artwork; it takes Floette's place once the Megas are unlocked
            new(10670, "Mega Floette", CritterMotion.Fish, "", "", Fps: 7, Sheet: "megafloette", Cell: 136, Angles: [-3, 0, 3], Pose: 0, Height: 75, Drift: 2, Bob: 0.8, Surge: 0.4),
            // Mega Dragalge (2026-10-23): takes Dragalge's place once the Megas are unlocked
            new(10691, "Mega Dragalge", CritterMotion.Fish, "", "", Fps: 7, Sheet: "megadragalge", Cell: 108, Angles: [-3, 0, 3], Pose: 0, Height: 87, Drift: 4, Bob: 1.5, Surge: 0.5),
            // Shiny Tapu Koko (2026-10-18): it brought the first Z-Crystal until Arceus took its place (2026-10-19); kept, nothing draws it now.
            new(785, "Tapu Koko variocolor", CritterMotion.Fish, "", "", Fps: 12, Sheet: "tapukoko", Cell: 168, Angles: [-30, -26, -22], Pose: -26, Height: 93, Drift: 0, Bob: 0, Surge: 0),
            // Arceus flying (2026-10-19): brings the first Z-Crystal now, in Tapu Koko's place (CapArceus).
            new(493, "Arceus", CritterMotion.Fish, "", "", Fps: 8, Sheet: "arceus", Cell: 128, Angles: [-8, -4, 0, 4, 8], Pose: 0, Height: 90, Drift: 0, Bob: 0, Surge: 0),
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
            // Raikou galloping (2026-10-18, prototype): the seventh Z-Crystal (CapRaikou).
            new(243, "Raikou", CritterMotion.Fish, "", "", Fps: 16, Sheet: "raikou", Cell: 132, Angles: [-20, -10, 0, 10, 20], Pose: 0, Height: 65, Drift: 0, Bob: 0, Surge: 0),
            new(20243, "Raikou de pie", CritterMotion.Fish, "", "", Fps: 1, Sheet: "raikoupie", Cell: 122, Angles: [-10, 0, 10], Pose: 0, Height: 65, Drift: 0, Bob: 0, Surge: 0),
            new(10487, "Giratina Origen", CritterMotion.Fish, "", "", Fps: 8, Sheet: "giratina", Cell: 98, Angles: [-10, 0, 10], Pose: 0, Height: 64, Drift: 0, Bob: 0, Surge: 0),
            new(717, "Yveltal", CritterMotion.Fish, "", "", Fps: 8, Sheet: "yveltal", Cell: 124, Angles: [-10, 0, 10], Pose: 0, Height: 84, Drift: 0, Bob: 0, Surge: 0),
            new(887, "Dragapult", CritterMotion.Fish, "", "", Fps: 8, Sheet: "dragapult", Cell: 94, Angles: [-10, 0, 10], Pose: 0, Height: 59, Drift: 0, Bob: 0, Surge: 0),
            new(885, "Dreepy", CritterMotion.Fish, "", "", Fps: 10, Sheet: "dreepy", Cell: 38, Angles: [-60, -30, 0, 30, 60], Pose: 0, Height: 17, Drift: 0, Bob: 0, Surge: 0),
            new(719, "Diancie", CritterMotion.Fish, "", "", Fps: 8, Sheet: "diancie", Cell: 80, Angles: [-10, 0, 10], Pose: 0, Height: 62, Drift: 0, Bob: 0, Surge: 0),
            new(10719, "Mega Diancie", CritterMotion.Fish, "", "", Fps: 8, Sheet: "megadiancie", Cell: 138, Angles: [-10, 0, 10], Pose: 0, Height: 108, Drift: 0, Bob: 0, Surge: 0),
            new(383, "Groudon", CritterMotion.Fish, "", "", Fps: 7, Sheet: "groudon", Cell: 102, Angles: [-10, 0, 10], Pose: 0, Height: 74, Drift: 0, Bob: 0, Surge: 0),
            new(10383, "Groudon Primigenio", CritterMotion.Fish, "", "", Fps: 8, Sheet: "primalgroudon", Cell: 124, Angles: [-10, 0, 10], Pose: 0, Height: 88, Drift: 0, Bob: 0, Surge: 0),
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
            // Gastly (2026-10-22): its gas swells round the ball; it leaves a trail of gas (CapVisitors.GasPuff)
            new(92, "Gastly", CritterMotion.Fish, "", "", Fps: 8, Sheet: "gastly", Cell: 58, Angles: [-3, 0, 3], Pose: 0, Height: 42, Drift: 5, Bob: 2, Surge: 0.4),
            // Haunter (2026-10-22): the body floats, each hand floats on its own beat, the wisp of its tail waves
            new(93, "Haunter", CritterMotion.Fish, "", "", Fps: 8, Sheet: "haunter", Cell: 78, Angles: [-3, 0, 3], Pose: 0, Height: 49, Drift: 4, Bob: 2, Surge: 0.5),
            // Whiscash (2026-10-22): tail fin, whiskers, dorsal and the three fins underneath swung whole on their roots
            new(340, "Whiscash", CritterMotion.Fish, "", "", Fps: 6, Sheet: "whiscash", Cell: 88, Angles: [-4, 0, 4], Pose: 0, Height: 49, Drift: 4, Bob: 1, Surge: 0.6),
            // Beldum (2026-10-22): it hovers; the head nods whole, the arm rocks on its joint, the claws twitch
            new(374, "Beldum", CritterMotion.Fish, "", "", Fps: 7, Sheet: "beldum", Cell: 58, Angles: [-4, 0, 4], Pose: 0, Height: 35, Drift: 3, Bob: 1.5, Surge: 0.3),
            // Jirachi (2026-10-22): it floats swaying, its two ribbons wave on their roots, the tags of its star rock
            new(385, "Jirachi", CritterMotion.Fish, "", "", Fps: 8, Sheet: "jirachi", Cell: 64, Angles: [-4, 0, 4], Pose: 0, Height: 45, Drift: 4, Bob: 2, Surge: 0.5),
            // Vespiquen (2026-10-22): it hovers; its wings buzz twice a cycle, its arms sway
            new(416, "Vespiquen", CritterMotion.Fish, "", "", Fps: 10, Sheet: "vespiquen", Cell: 72, Angles: [-3, 0, 3], Pose: 0, Height: 56, Drift: 3, Bob: 1.5, Surge: 0.4),
            // Drifloon (2026-10-23): the balloon bobs, its puff sways, its four strings swing like pendulums
            new(425, "Drifloon", CritterMotion.Fish, "", "", Fps: 7, Sheet: "drifloon", Cell: 78, Angles: [-3, 0, 3], Pose: 0, Height: 59, Drift: 4, Bob: 1.5, Surge: 0.2),
            // Mismagius (2026-10-23): hat and head float, frills and hat tip flutter, robe and tails swing
            new(429, "Mismagius", CritterMotion.Fish, "", "", Fps: 7, Sheet: "mismagius", Cell: 84, Angles: [-3, 0, 3], Pose: 0, Height: 64, Drift: 4, Bob: 1.5, Surge: 0.4),
            // Tympole (2026-10-23): its tail beats on its root, its head bobs, its sound-balls pulse
            new(535, "Tympole", CritterMotion.Fish, "", "", Fps: 9, Sheet: "tympole", Cell: 52, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 34, Drift: 5, Bob: 2, Surge: 0.9),
            // Basculin (2026-10-23): tail fin beats hard, dorsal, underside and jaw fins swung whole
            new(550, "Basculin", CritterMotion.Fish, "", "", Fps: 10, Sheet: "basculin", Cell: 68, Angles: [-6, -3, 0, 3, 6], Pose: 0, Height: 38, Drift: 5, Bob: 1.5, Surge: 1.2),
            // Basculegion (2026-10-23): tail and ghost wisps, dorsal and fins swung whole; drawn nose-down, Pose 18
            new(902, "Basculegion", CritterMotion.Fish, "", "", Fps: 9, Sheet: "basculegion", Cell: 96, Angles: [12, 15, 18, 21, 24], Pose: 18, Height: 45, Drift: 5, Bob: 1.5, Surge: 1.1),
            // Jellicent (2026-10-23), both forms: the male here, the female as 20000 + the species; head pulses, arms beat, tentacles sway
            new(593, "Jellicent", CritterMotion.Fish, "", "", Fps: 7, Sheet: "jellicent_macho", Cell: 76, Angles: [-3, 0, 3], Pose: 0, Height: 50, Drift: 5, Bob: 2.5, Surge: 0.4),
            new(20593, "Jellicent", CritterMotion.Fish, "", "", Fps: 7, Sheet: "jellicent_hembra", Cell: 96, Angles: [-3, 0, 3], Pose: 0, Height: 66, Drift: 5, Bob: 2.5, Surge: 0.4),
            // Tornadus (2026-10-23): rides its cloud; tail, mane and fist swung whole, the cloud drifting behind
            new(641, "Tornadus", CritterMotion.Fish, "", "", Fps: 7, Sheet: "tornadus", Cell: 96, Angles: [-3, 0, 3], Pose: 0, Height: 76, Drift: 3, Bob: 1, Surge: 0.8),
            // Thundurus (2026-10-23): riding its cloud like Tornadus (genie_rig.js)
            new(642, "Thundurus", CritterMotion.Fish, "", "", Fps: 7, Sheet: "thundurus", Cell: 96, Angles: [-3, 0, 3], Pose: 0, Height: 73, Drift: 3, Bob: 1, Surge: 0.8),
            // Landorus (2026-10-23): riding its cloud like Tornadus (genie_rig.js)
            new(645, "Landorus", CritterMotion.Fish, "", "", Fps: 7, Sheet: "landorus", Cell: 98, Angles: [-3, 0, 3], Pose: 0, Height: 75, Drift: 3, Bob: 1, Surge: 0.8),
            // Floette of the Eternal Flower (2026-10-23): hat brim, arms, stem and legs swung whole
            new(670, "Floette", CritterMotion.Fish, "", "", Fps: 8, Sheet: "floette", Cell: 86, Angles: [-3, 0, 3], Pose: 0, Height: 66, Drift: 4, Bob: 2, Surge: 0.4),
            // Hoopa (2026-10-23): floats; horns rock in their rings, hands drift on their own beat
            new(720, "Hoopa", CritterMotion.Fish, "", "", Fps: 8, Sheet: "hoopa", Cell: 62, Angles: [-3, 0, 3], Pose: 0, Height: 44, Drift: 4, Bob: 2, Surge: 0.4),
            // Dragalge (2026-10-23): crest, branch, leaf fins and tail leaf swung whole
            new(691, "Dragalge", CritterMotion.Fish, "", "", Fps: 7, Sheet: "dragalge", Cell: 106, Angles: [-4, 0, 4], Pose: 0, Height: 87, Drift: 5, Bob: 1.5, Surge: 0.6),
            // Phantump (2026-10-23): floating stump; branch arms, head curl and ghost tail swung whole
            new(708, "Phantump", CritterMotion.Fish, "", "", Fps: 8, Sheet: "phantump", Cell: 60, Angles: [-3, 0, 3], Pose: 0, Height: 40, Drift: 4, Bob: 2, Surge: 0.4),
            // Inkay (2026-10-23): swims upside down; its arms flap outward, mirrored
            new(686, "Inkay", CritterMotion.Fish, "", "", Fps: 8, Sheet: "inkay", Cell: 58, Angles: [-3, 0, 3], Pose: 0, Height: 43, Drift: 4, Bob: 2, Surge: 0.5),
            // Tapu Fini (2026-10-23): flota en su concha; el cuerno y las dos aletas de abajo se mecen enteros
            new(788, "Tapu Fini", CritterMotion.Fish, "", "", Fps: 7, Sheet: "tapufini", Cell: 116, Angles: [-3, 0, 3], Pose: 0, Height: 91, Drift: 4, Bob: 2, Surge: 0.4),
            // Tapu Lele (2026-10-23): flota en su concha; las dos cintas rizadas se mecen sobre los hombros a contratiempo
            new(786, "Tapu Lele", CritterMotion.Fish, "", "", Fps: 7, Sheet: "tapulele", Cell: 94, Angles: [-3, 0, 3], Pose: 0, Height: 75, Drift: 4, Bob: 2, Surge: 0.4),
            // Cosmog (2026-10-23): flota; los dos brazos con sus nubes de gas se mecen sobre su raíz a contratiempo
            new(789, "Cosmog", CritterMotion.Fish, "", "", Fps: 7, Sheet: "cosmog", Cell: 66, Angles: [-3, 0, 3], Pose: 0, Height: 46, Drift: 4, Bob: 2, Surge: 0.4),
            // Lunala (2026-10-23): de frente; las alas baten en espejo, inclinadas desde el costado (sin abrir hueco), bajando rápido y subiendo despacio; el cuerpo sube con el golpe y la media luna de abajo va con retraso
            new(792, "Lunala", CritterMotion.Fish, "", "", Fps: 10, Sheet: "lunala", Cell: 106, Angles: [-3, 0, 3], Pose: 0, Height: 86, Drift: 3, Bob: 1, Surge: 0.8),
            // Nihilego (2026-10-23): flota como una medusa; los lados cuelgan del sombrero y se mecen a contratiempo, las dos colas de abajo con el cuerpo
            new(793, "Nihilego", CritterMotion.Fish, "", "", Fps: 7, Sheet: "nihilego", Cell: 86, Angles: [-3, 0, 3], Pose: 0, Height: 68, Drift: 5, Bob: 2.5, Surge: 0.4),
            // Naganadel (2026-10-23): baten las dos alas enteras (la del fondo con retraso), el cuerpo sube con el golpe y el aguijón va detrás
            new(804, "Naganadel", CritterMotion.Fish, "", "", Fps: 9, Sheet: "naganadel", Cell: 100, Angles: [-3, 0, 3], Pose: 0, Height: 78, Drift: 3, Bob: 1.5, Surge: 0.8),
            // Poipole (2026-10-23): flota; los brazos se agitan a contratiempo, la cresta se mece y la cola se mueve
            new(803, "Poipole", CritterMotion.Fish, "", "", Fps: 8, Sheet: "poipole", Cell: 74, Angles: [-3, 0, 3], Pose: 0, Height: 56, Drift: 4, Bob: 2, Surge: 0.5),
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
            new(144, "Articuno", CritterMotion.Fish, "", "", Fps: 8, Sheet: "articuno", Cell: 82, Angles: [-2, 0, 2], Pose: 0, Height: 61, Drift: 2, Bob: 0.5, Surge: 0.6),
            new(145, "Zapdos", CritterMotion.Fish, "", "", Fps: 9, Sheet: "zapdos", Cell: 108, Angles: [-2, 0, 2], Pose: 0, Height: 61, Drift: 2, Bob: 0.5, Surge: 0.9),
            new(146, "Moltres", CritterMotion.Fish, "", "", Fps: 8, Sheet: "moltres", Cell: 90, Angles: [-2, 0, 2], Pose: 0, Height: 61, Drift: 2, Bob: 0.5, Surge: 0.8),
        ];

        return all.ToDictionary(a => a.Species);
    }
}
