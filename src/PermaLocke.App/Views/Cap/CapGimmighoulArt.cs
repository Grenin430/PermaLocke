namespace PermaLocke.App.Views;

/// <summary>
/// Gimmighoul in its chest, upright, with glasses and a snorkel (2026-10-18): the player's own pixel sprite (32 x 40 grid), read cell by cell,
/// with the glasses and the snorkel added (tools/clouds/gimmighoul_usuario.json, gimmighoul_cs.js; do not edit by hand).
/// </summary>
internal static class CapGimmighoulArt
{
    public const string Keys = "BDFGHKLMPRTWYmn";

    /// <summary>The row it stands on: the bottom of the box (the strap's end hangs lower).</summary>
    public const int Foot = 26;

    public static readonly uint[] Colours = [0x2C56B0, 0x383840, 0x1C2A50, 0xD8A63E, 0xFCE88C, 0x000000, 0xDEDEE2, 0xBEBEC4, 0xBA6866, 0x923C3E, 0x6EE2D4, 0xFFFFFF, 0xFAE84E, 0x9C9CA0, 0x6C6468];

    public static readonly string[] Rows =
    [
        "......KKK.............",
        "....KKGGGK............",
        "...KYGGGRDKKK..KKKK...",
        "..KHYYRRDnnnDKKGYYYK..",
        ".KHHHRRDnnnDPRHHYGGGK.",
        ".KHHRPDnnnDPRHHYGGGGGK",
        "KHHHRDnnnDPPRHHGGRnGGK",
        "KHHRPDnnnPPRHHYGRRRnGK",
        "KYYRPDnnDPPRHHGGRRRnGK",
        "KYYRnmLmDPPRHHGRRRRnGK",
        "KYYnmLLLnPRGHHGRRRRnGK",
        "KGYnLmmmLnRYHGGRRRnGK.",
        ".KYGmLLLmnRYHGGnnGGGK.",
        ".KGYnmmmnGYYHGGGGGBK..",
        ".KKKKnnnYYYYHGGGDKB...",
        ".....KDLLLLLLGDDnKB...",
        "......FTWFTTFDnnK.B...",
        ".....KGDGKBBBBBBBBB...",
        "...KKDYDYGmDYYnK......",
        "..KDMDmmGDDDGGGGKK....",
        ".KKDmMDmDDMMDYYnnGK...",
        ".KGGGDDDDDmMDnnGGGK...",
        ".KGRRGGGGGDDGGGRnGK...",
        ".KGRRRRRRGGGGRRRnGK...",
        ".KGGGRRRRRYYGRRRnGK...",
        ".KGGKKKGGGYYGGGKDGK...",
        "..KK...KKKnYGnK.KK....",
        "..........KGGK........",
        "...........KK.........",
    ];
}
