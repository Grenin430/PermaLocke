using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink;

namespace PermaLocke.GameLink.Tests;

/// <summary>Writing a nickname into a save built here: no emulator, no partida.</summary>
public class SaveRenamerTests
{
    [Fact]
    public void The_nickname_lands_and_empty_gives_the_species_name_back()
    {
        var save = new SAV7USUM();
        var pikachu = new PK7
        {
            Species = (ushort)Species.Pikachu, CurrentLevel = 50, OriginalTrainerName = "GRENIN",
            Language = save.Language, Version = save.Version, PID = 0x1234ABCD, Move1 = (ushort)Move.Thunderbolt
        };
        pikachu.ClearNickname();
        pikachu.RefreshChecksum();
        save.SetBoxSlotAtIndex(pikachu, 1, 3);
        var renamer = new SaveRenamer(null!, Path.GetTempPath(), NullLogger<SaveRenamer>.Instance);

        Assert.True(renamer.ApplyTo(save, new NicknameChange(1, 3, pikachu.PID, "Pikachu", "Chispas")).Delivered);
        var named = (PK7)save.GetBoxSlotAtIndex(1, 3)!;
        Assert.Equal("Chispas", named.Nickname);
        Assert.True(named.IsNicknamed);
        Assert.True(named.ChecksumValid);

        Assert.False(renamer.ApplyTo(save, new NicknameChange(1, 3, 0xDEADBEEF, "Otro", "X")).Delivered);

        Assert.True(renamer.ApplyTo(save, new NicknameChange(1, 3, pikachu.PID, "Chispas", "")).Delivered);
        Assert.False(((PK7)save.GetBoxSlotAtIndex(1, 3)!).IsNicknamed);
    }
}
