using PermaLocke.Core.Abstractions;

using pk3DS.Core;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Modules;

/// <param name="AreasChanged">Areas whose tables were rewritten.</param>
/// <param name="SlotsChanged">Individual encounter slots rewritten.</param>
public sealed record WildEncounterResult(int AreasChanged, int SlotsChanged);

/// <summary>
/// Rewrites the wild encounter tables of Ultra Moon.
/// <para>
/// The encdata GARC is ~460 MB because each area ships eleven subfiles and only one of them is
/// encounters; the rest is map data. Its subfiles are LZ11 compressed, so the container has to
/// be repacked — which the game accepts, verified on a real save. The decompressed payload, on
/// the other hand, is never rebuilt: only the four bytes of each occupied slot change.
/// </para>
/// </summary>
public sealed class WildEncounterRandomizer(RandomizerOptions options)
{
    /// <summary>Subfiles per area inside encdata; the encounter block is the tenth.</summary>
    private const int SubfilesPerArea = 11;
    private const int EncounterSubfile = 9;

    public WildEncounterResult Apply(IRandomSource random, SpeciesPool pool,
        GARC.LazyGARC garc, CancellationToken ct = default)
    {
        var areas = garc.FileCount / SubfilesPerArea;
        var areasChanged = 0;
        var slotsChanged = 0;

        for (var area = 0; area < areas; area++)
        {
            ct.ThrowIfCancellationRequested();

            var index = (area * SubfilesPerArea) + EncounterSubfile;
            var payload = garc[index];
            if (payload.Length < 4 || payload[0] != (byte)'E' || payload[1] != (byte)'A')
            {
                continue; // area without encounter tables
            }

            var changed = RandomizeArea(payload, random, pool);
            if (changed == 0)
            {
                continue;
            }

            garc[index] = payload;
            slotsChanged += changed;
            areasChanged++;
        }

        return new WildEncounterResult(areasChanged, slotsChanged);
    }

    /// <summary>Rewrites every table of one area's encounter block, in place.</summary>
    private int RandomizeArea(byte[] payload, IRandomSource random, SpeciesPool pool)
    {
        var entries = BitConverter.ToUInt16(payload, 2);
        var changed = 0;

        for (var entry = 0; entry < entries; entry++)
        {
            var start = BitConverter.ToInt32(payload, 4 + (entry * 4));
            var end = BitConverter.ToInt32(payload, 8 + (entry * 4));
            if (end - start < EncounterTable7.MinimumEntrySize)
            {
                continue; // no room for a day and a night table
            }

            foreach (var tableOffset in (int[])[EncounterTable7.DayTableOffset, EncounterTable7.NightTableOffset])
            {
                changed += RandomizeTable(new EncounterTable7(payload, start + tableOffset), random, pool);
            }
        }

        return changed;
    }

    private int RandomizeTable(EncounterTable7 table, IRandomSource random, SpeciesPool pool)
    {
        var offsets = EncounterTable7.SlotOffsets().ToArray();
        var baseSetLength = EncounterTable7.SlotsPerSet;
        var changed = 0;

        for (var i = 0; i < offsets.Length; i++)
        {
            var slotOffset = offsets[i];
            var original = table.GetSpecies(slotOffset);
            if (original == 0)
            {
                continue; // an empty slot stays empty: filling it would add encounters
            }

            // Mirroring makes an SOS call bring whatever appeared in the matching base slot.
            if (options.MirrorSosSlots && i >= baseSetLength)
            {
                table.SetSpecies(slotOffset, table.GetSpecies(offsets[i % baseSetLength]));
                changed++;
                continue;
            }

            table.SetSpecies(slotOffset, pool.Pick(random, original));
            changed++;
        }

        return changed;
    }

}
