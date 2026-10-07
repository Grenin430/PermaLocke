using System.Buffers.Binary;
using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// The eggs of the NURSERY of a MONOTYPE role (§221) go into the first free box slots of the save, in one write, and
/// really are eggs: read back, level 1, the species asked for, never in the party.
/// </summary>
[Collection("WorldLimits")]
public sealed class NurseryEggDeliveryTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "PermaLockeNursery-" + Guid.NewGuid().ToString("N"));

    public NurseryEggDeliveryTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static GachaPull Egg(int species, string name, int number) =>
        new("guarderia", "guarderia", species, name, false, 309, 1, false, [31, 20, 15, 10, 5, 0], 3, "Firme", 66, "Mar llamas",
            1UL, number);

    private string EmptySave(Action<SAV7USUM>? prepare = null)
    {
        var game = new SAV7USUM();
        prepare?.Invoke(game);

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
    public void Several_eggs_go_into_the_first_free_slots_in_one_write_with_one_backup()
    {
        var path = EmptySave();

        var results = Delivery().DeliverEggsTo(path,
            [Egg((int)Species.Charmander, "Charmander", 0), Egg((int)Species.Growlithe, "Growlithe", 1), Egg((int)Species.Ponyta, "Ponyta", 2)]);

        Assert.All(results, r => Assert.True(r.Delivered, r.Message));
        Assert.Equal([(1, 1), (1, 2), (1, 3)], results.Select(r => (r.Box, r.Slot)));
        Assert.Equal(3, results.Select(r => r.Pid).Distinct().Count());
        Assert.Single(Directory.GetFiles(Path.Combine(_folder, "backup")));

        var game = Reload(path);
        var first = game.GetBoxSlotAtIndex(0, 0);
        var third = game.GetBoxSlotAtIndex(0, 2);

        Assert.True(first.IsEgg);
        Assert.Equal((int)Species.Charmander, first.Species);
        Assert.Equal(1, first.CurrentLevel);
        Assert.Equal(results[0].Pid, first.PID);
        Assert.Equal((int)Species.Ponyta, third.Species);
        Assert.Equal(0, game.PartyCount);                 // nunca al equipo
        Assert.Equal(game.OT, first.OriginalTrainerName);
        Assert.Equal(game.TID16, first.TID16);
    }

    [Fact]
    public void An_egg_skips_the_slots_that_are_taken_and_says_so_when_the_boxes_are_full()
    {
        var path = EmptySave(game =>
        {
            for (var box = 0; box < game.BoxCount; box++)
            {
                for (var slot = 0; slot < game.BoxSlotCount; slot++)
                {
                    // Todo ocupado menos los dos últimos huecos de la última caja.
                    if (box == game.BoxCount - 1 && slot >= game.BoxSlotCount - 2)
                    {
                        continue;
                    }

                    var filler = new PK7 { Species = (ushort)Species.Pikipek, CurrentLevel = 5, PID = (uint)(0x1000 + (box * 30) + slot) };
                    filler.RefreshChecksum();
                    game.SetBoxSlotAtIndex(filler, box, slot);
                }
            }
        });

        var results = Delivery().DeliverEggsTo(path,
            [Egg((int)Species.Charmander, "Charmander", 0), Egg((int)Species.Growlithe, "Growlithe", 1), Egg((int)Species.Ponyta, "Ponyta", 2)]);

        // Caben dos: el tercero no se pierde ni se escribe, y queda como debido.
        Assert.True(results[0].Delivered);
        Assert.True(results[1].Delivered);
        Assert.Equal(DeliveryOutcome.BoxesFull, results[2].Outcome);
        Assert.Equal(0u, results[2].Pid);

        var game2 = Reload(path);
        Assert.True(game2.GetBoxSlotAtIndex(game2.BoxCount - 1, game2.BoxSlotCount - 2).IsEgg);
        Assert.True(game2.GetBoxSlotAtIndex(game2.BoxCount - 1, game2.BoxSlotCount - 1).IsEgg);
    }

    [Fact]
    public void With_every_box_full_nothing_is_written_and_no_backup_is_made()
    {
        var path = EmptySave(game =>
        {
            for (var box = 0; box < game.BoxCount; box++)
            {
                for (var slot = 0; slot < game.BoxSlotCount; slot++)
                {
                    var filler = new PK7 { Species = (ushort)Species.Pikipek, CurrentLevel = 5, PID = (uint)(0x1000 + (box * 30) + slot) };
                    filler.RefreshChecksum();
                    game.SetBoxSlotAtIndex(filler, box, slot);
                }
            }
        });
        var before = File.ReadAllBytes(path);

        var results = Delivery().DeliverEggsTo(path, [Egg((int)Species.Charmander, "Charmander", 0)]);

        Assert.Equal(DeliveryOutcome.BoxesFull, Assert.Single(results).Outcome);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(Directory.Exists(Path.Combine(_folder, "backup")));
    }

    [Fact]
    public void No_eggs_means_no_write_at_all()
    {
        var path = EmptySave();
        var before = File.ReadAllBytes(path);

        Assert.Empty(Delivery().DeliverEggsTo(path, []));
        Assert.Equal(before, File.ReadAllBytes(path));
    }
}
