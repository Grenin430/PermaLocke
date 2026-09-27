using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink;

namespace PermaLocke.GameLink.Tests;

/// <summary>The shop's nature herbs, into a save built here: no emulator, no partida.</summary>
public class SaveNatureChangerTests
{
    [Fact]
    public void The_nature_lands_on_the_right_pokemon_and_only_there()
    {
        var save = new SAV7USUM();
        var pikachu = new PK7
        {
            Species = (ushort)Species.Pikachu, CurrentLevel = 50, OriginalTrainerName = "GRENIN", Nature = Nature.Hardy,
            Language = save.Language, Version = save.Version, PID = 0x1234ABCD, Move1 = (ushort)Move.Thunderbolt
        };
        pikachu.RefreshChecksum();
        save.SetBoxSlotAtIndex(pikachu, 1, 3);
        var changer = new SaveNatureChanger(null!, Path.GetTempPath(), NullLogger<SaveNatureChanger>.Instance);

        Assert.True(changer.ApplyTo(save, new NatureChange(1, 3, pikachu.PID, "Pikachu", (int)Nature.Timid)).Delivered);
        var changed = (PK7)save.GetBoxSlotAtIndex(1, 3)!;
        Assert.Equal(Nature.Timid, changed.Nature);
        Assert.Equal(pikachu.PID, changed.PID);
        Assert.True(changed.ChecksumValid);

        Assert.False(changer.ApplyTo(save, new NatureChange(1, 3, 0xDEADBEEF, "Otro", (int)Nature.Bold)).Delivered);
        Assert.False(changer.ApplyTo(save, new NatureChange(1, 3, pikachu.PID, "Pikachu", 25)).Delivered);
        Assert.Equal(Nature.Timid, ((PK7)save.GetBoxSlotAtIndex(1, 3)!).Nature);
    }
}
