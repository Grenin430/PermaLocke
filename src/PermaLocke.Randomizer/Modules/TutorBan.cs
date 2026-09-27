using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Modules;

/// <param name="Species">Rows of the table.</param>
/// <param name="Cleared">Tutor moves that were learnable before, over all species.</param>
public sealed record TutorBanResult(int Species, int Cleared);

/// <summary>
/// Nobody learns anything from the BP tutors (2026-09-27, <see cref="RandomizerOptions.BanTutors"/>).
/// </summary>
/// <remarks>
/// Ultra Moon keeps which tutor moves a species can learn as bits at <see cref="MachineFlags.TutorOffset"/> of its row of
/// the personal table, 67 of them. All go to zero, in the whole table and in each row's own copy, the way the TM
/// compatibility module writes both (§141). The four spare bits of the last byte are left as they were.
/// </remarks>
public sealed class TutorBan(RandomizerOptions options)
{
    public TutorBanResult Apply(LayeredFsMod mod)
    {
        ArgumentNullException.ThrowIfNull(mod);

        if (!options.BanTutors)
        {
            return new TutorBanResult(0, 0);
        }

        var path = mod.Stage(GameFiles.Personal);
        int rows, cleared = 0;

        using (var patcher = new GarcPatcher(path))
        {
            var index = patcher.FileCount - 1;
            var table = patcher.Read(index);
            rows = table.Length / PersonalEntry7.Size;

            for (var species = 0; species < rows; species++)
            {
                cleared += Clear(table, species * PersonalEntry7.Size);
            }

            patcher.Write(index, table);

            for (var row = 0; row < Math.Min(rows, index); row++)
            {
                var entry = patcher.Read(row);
                if (entry.Length != PersonalEntry7.Size) continue;
                Clear(entry, 0);
                patcher.Write(row, entry);
            }
        }

        // Releído: ni un bit de tutor en la tabla entera.
        using (var back = new GarcPatcher(path))
        {
            var written = back.Read(back.FileCount - 1);
            for (var species = 0; species < rows; species++)
            {
                if (Count(written, species * PersonalEntry7.Size) > 0)
                {
                    throw new InvalidDataException($"La especie {species} sigue pudiendo aprender movimientos de tutor.");
                }
            }
        }

        return new TutorBanResult(rows, cleared);
    }

    /// <summary>Zeroes the tutor bits of the row starting at <paramref name="start"/>; returns how many were set.</summary>
    public static int Clear(Span<byte> table, int start)
    {
        var was = Count(table, start);
        for (var i = 0; i < MachineFlags.TutorCount; i++)
        {
            table[start + MachineFlags.TutorOffset + (i / 8)] &= (byte)~(1 << (i % 8));
        }

        return was;
    }

    private static int Count(ReadOnlySpan<byte> table, int start)
    {
        var count = 0;
        for (var i = 0; i < MachineFlags.TutorCount; i++)
        {
            if ((table[start + MachineFlags.TutorOffset + (i / 8)] & (1 << (i % 8))) != 0) count++;
        }

        return count;
    }
}
