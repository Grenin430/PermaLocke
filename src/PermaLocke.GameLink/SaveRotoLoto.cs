using PKHeX.Core;

namespace PermaLocke.GameLink;

/// <summary>
/// Turns the Roto Loto off in the save (2026-09-28, players' list item 5): its two unlock flags in the field menu block,
/// which PKHeX names <c>RotomLoto1</c> and <c>RotomLoto2</c>.
/// </summary>
/// <remarks>
/// Only with the game closed, the whole save copied first and read back after, like every other save writer. The prizes
/// the Loto gives are not items (no bag pocket takes them), so they cannot be taken away afterwards: the only block is
/// the unlock. <b>Not seen in play yet</b>: the organiser's save had both flags off.
/// </remarks>
public static class SaveRotoLoto
{
    /// <summary>True when the save had the Loto on and now has it off; false when there was nothing to do.</summary>
    public static bool TurnOff(string path, string backupFolder)
    {
        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game
            || !(game.FieldMenu.RotomLoto1 || game.FieldMenu.RotomLoto2))
        {
            return false;
        }

        Directory.CreateDirectory(backupFolder);
        File.Copy(path, Path.Combine(backupFolder, $"main-{DateTime.Now:yyyyMMdd-HHmmss-fff}-rotombola.sav"), overwrite: false);

        game.FieldMenu.RotomLoto1 = false;
        game.FieldMenu.RotomLoto2 = false;
        File.WriteAllBytes(path, game.Write().ToArray());

        return SaveUtil.TryGetSaveFile(path, out var again) && again is SAV7USUM check
               && !check.FieldMenu.RotomLoto1 && !check.FieldMenu.RotomLoto2;
    }
}
