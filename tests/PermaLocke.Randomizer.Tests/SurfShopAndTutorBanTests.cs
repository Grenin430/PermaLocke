using PermaLocke.Randomizer;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>The BP counter of the surf beaches sells Poké Balls and nobody learns from the BP tutors (2026-09-27).</summary>
public sealed class SurfShopAndTutorBanTests
{
    [Fact]
    public void The_beach_counter_turns_into_poke_balls_at_one_bp()
    {
        var cro = new byte[64];
        (int Id, int Price)[] shelf = [(43, 1), (27, 1), (50, 48)];
        for (var i = 0; i < shelf.Length; i++)
        {
            BitConverter.GetBytes((ushort)shelf[i].Id).CopyTo(cro, 20 + (i * 4));
            BitConverter.GetBytes((ushort)shelf[i].Price).CopyTo(cro, 22 + (i * 4));
        }

        var names = new string[60];
        names[4] = "Poké Ball"; names[43] = "Zumo de Baya"; names[27] = "Cura Total"; names[50] = "Caramelo Raro";
        var options = new RandomizerOptions
        {
            BattlePointShopReplaced = [new(43, "Zumo de Baya"), new(27, "Cura Total"), new(50, "Caramelo Raro")]
        };

        Assert.Equal(3, ShopRandomizer.ReplaceBattlePointItems(options, cro, names));
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(4, BitConverter.ToUInt16(cro, 20 + (i * 4)));
            Assert.Equal(1, BitConverter.ToUInt16(cro, 22 + (i * 4)));
        }

        // Ya no está: buscarla otra vez no escribe nada.
        Assert.Throws<InvalidDataException>(() => ShopRandomizer.ReplaceBattlePointItems(options, cro, names));
    }

    [Fact]
    public void Every_tutor_bit_goes_and_the_spare_bits_stay()
    {
        var row = new byte[PersonalEntry7.Size];
        for (var i = 0; i < 10; i++) row[MachineFlags.TutorOffset + i] = 0xFF;

        Assert.Equal(MachineFlags.TutorCount, TutorBan.Clear(row, 0));
        for (var i = 0; i < 8; i++) Assert.Equal(0, row[MachineFlags.TutorOffset + i]);
        Assert.Equal(0xF8, row[MachineFlags.TutorOffset + 8]);
        Assert.Equal(0xFF, row[MachineFlags.TutorOffset + 9]);
    }
}
