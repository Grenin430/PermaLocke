using System.Buffers.Binary;
using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// A gacha Pokémon joins the party when it has room, and goes to the PC when it does not (2026-09-21).
/// </summary>
/// <remarks>
/// Real saves built with PKHeX. The world's base stats here are deliberately NOT PKHeX's, so a party member whose stats
/// PKHeX quietly recalculated from its own table would fail the comparison.
/// </remarks>
[Collection("WorldLimits")]
public sealed class GachaPartyDeliveryTests : IDisposable
{
    private const ushort Froakie = 656;

    /// <summary>Not Froakie's real base stats, on purpose.</summary>
    private static readonly byte[] WorldBases = [50, 60, 70, 80, 90, 100];

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "PermaLockeGachaParty-" + Guid.NewGuid().ToString("N"));

    public GachaPartyDeliveryTests()
    {
        Directory.CreateDirectory(_folder);
        var table = new byte[(Froakie + 1) * 6];
        WorldBases.CopyTo(table, Froakie * 6);
        WorldLimits.BaseStats = table;
        WorldLimits.FormBaseStats = new Dictionary<(int Species, int Form), byte[]>();
    }

    public void Dispose()
    {
        WorldLimits.BaseStats = [];
        WorldLimits.FormBaseStats = new Dictionary<(int Species, int Form), byte[]>();
        Directory.Delete(_folder, recursive: true);
    }

    /// <summary>IVs as the roll gives them: PS, Ataque, Defensa, Velocidad, At. Esp., Def. Esp.</summary>
    private static readonly int[] RollIvs = [31, 20, 15, 10, 5, 0];

    private static GachaPull Pull() =>
        new("decente", "tier3", Froakie, "Froakie", false, 314, 12, false, RollIvs, 0, "Fuerte", 67, "Torrente", 1UL, 0);

    /// <summary>A save with <paramref name="partySize"/> Pokémon in the party.</summary>
    private string SaveWith(int partySize)
    {
        var game = new SAV7USUM();

        for (var slot = 0; slot < partySize; slot++)
        {
            var member = new PK7 { Species = (ushort)Species.Pikipek, CurrentLevel = 5, PID = (uint)(0x1000 + slot) };
            member.ResetPartyStats();
            member.RefreshChecksum();
            game.SetPartySlotAtIndex(member, slot);
        }

        // SaveUtil reconoce una partida de USUM por su tamaño y la marca BEEF del final.
        BinaryPrimitives.WriteUInt32LittleEndian(game.Data[^0x1F0..], 0x42454546);
        var path = Path.Combine(_folder, "main");
        File.WriteAllBytes(path, game.Write().ToArray());
        return path;
    }

    private SaveBoxDelivery Delivery() =>
        new(null!, Path.Combine(_folder, "backup"), NullLogger<SaveBoxDelivery>.Instance);

    private static SAV7USUM Reload(string path)
    {
        Assert.True(SaveUtil.TryGetSaveFile(path, out var loaded));
        return (SAV7USUM)loaded!;
    }

    [Fact]
    public void With_room_in_the_party_it_joins_the_party_with_the_world_s_stats()
    {
        var path = SaveWith(1);

        var result = Delivery().DeliverTo(path, Pull());

        Assert.True(result.Delivered);
        Assert.Equal(0, result.Box);     // 0 es el equipo
        Assert.Equal(2, result.Slot);

        var save = Reload(path);
        Assert.Equal(2, save.PartyCount);
        var joined = (PK7)save.GetPartySlotAtIndex(1);
        Assert.Equal(Froakie, joined.Species);
        Assert.Equal(result.Pid, joined.PID);
        Assert.Equal(12, joined.Stat_Level);

        int[] ivs = [RollIvs[0], RollIvs[1], RollIvs[2], RollIvs[4], RollIvs[5], RollIvs[3]];
        var expected = StatCalculator.Compute(WorldBases, ivs, [0, 0, 0, 0, 0, 0], 12, 0);
        int[] written = [joined.Stat_HPMax, joined.Stat_ATK, joined.Stat_DEF, joined.Stat_SPA, joined.Stat_SPD, joined.Stat_SPE];
        Assert.Equal(expected, written);

        // A plena salud: un cero aquí sería un muerto (§98).
        Assert.Equal(joined.Stat_HPMax, joined.Stat_HPCurrent);
    }

    [Fact]
    public void With_a_full_party_it_goes_to_the_pc()
    {
        var path = SaveWith(6);

        var result = Delivery().DeliverTo(path, Pull());

        Assert.True(result.Delivered);
        Assert.Equal(1, result.Box);
        Assert.Equal(1, result.Slot);

        var save = Reload(path);
        Assert.Equal(6, save.PartyCount);
        Assert.Equal(Froakie, save.GetBoxSlotAtIndex(0, 0).Species);
    }

    /// <summary>Without the installed world's base stats there are no honest party stats: the PC, as before.</summary>
    [Fact]
    public void Without_the_world_table_it_goes_to_the_pc()
    {
        WorldLimits.BaseStats = [];
        var path = SaveWith(1);

        var result = Delivery().DeliverTo(path, Pull());

        Assert.True(result.Delivered);
        Assert.Equal(1, result.Box);
        Assert.Equal(1, Reload(path).PartyCount);
    }

    /// <summary>The save is copied before it is touched, as the PC delivery always did.</summary>
    [Fact]
    public void The_save_is_backed_up_before_joining_the_party()
    {
        var path = SaveWith(1);

        Delivery().DeliverTo(path, Pull());

        Assert.Single(Directory.GetFiles(Path.Combine(_folder, "backup"), "main-*.sav"));
    }
}
