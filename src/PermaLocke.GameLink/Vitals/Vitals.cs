using System.Buffers.Binary;
using PermaLocke.GameLink.Battle;
using PermaLocke.GameLink.Data;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink.Vitals;

/// <summary>One member of the party as the fast path sees it: only what changes while playing, read straight from the structure the game reads.</summary>
public readonly record struct PartySample(int Slot, int Species, int HeldItem, uint Exp, uint Pid, int Status, int Level, int Hp, int MaxHp);

/// <summary>One of the player's Pokémon as the battle's own block says it.</summary>
public readonly record struct BattleSample(int Table, int BattleId, int Species, int MaxHp, int Hp);

/// <summary>
/// What the fast path knows at an instant (2026-10-11): the HP and the status of the party, and the HP of the player's blocks of the battle in progress.
/// <see cref="ReadAt"/> is the moment the answers arrived (<see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>), the one the animations count from: the
/// delay of the reading is never added to the delay of the showing.
/// </summary>
public sealed record VitalsFrame(long ReadAt, IReadOnlyList<PartySample> Party, IReadOnlyList<BattleSample> Battle)
{
    /// <summary>
    /// Which of the battle's two tables is the one that changes first (the table of the calculation: its value is the final one at once, while the other
    /// walks to it through intermediate values for up to three seconds, §114), or -1 while that is not known. The life of a block is its value in that
    /// table, so that the panel never shows a hit in pieces.
    /// </summary>
    public int Lead { get; init; } = -1;

    /// <summary>The life of each player's block: the lead table's, or the first one seen while the lead is not known.</summary>
    public IReadOnlyList<BattleSample> Effective()
    {
        var result = new List<BattleSample>();
        foreach (var sample in Battle)
        {
            var at = result.FindIndex(r => r.BattleId == sample.BattleId);
            if (at < 0) result.Add(sample);
            else if (sample.Table == Lead) result[at] = sample;
        }

        return result;
    }

    /// <summary>Whether two frames say the same, whenever each was read: the party and the effective life of the battle (the other table's walk is not news).</summary>
    public bool SameAs(VitalsFrame? other) =>
        other is not null && Party.SequenceEqual(other.Party)
        && (Lead >= 0 ? Effective().SequenceEqual(other.Effective()) : Battle.SequenceEqual(other.Battle));

    /// <summary>The tables of the battle with the effective life in the player's blocks of every table: what the panel already knows how to read, only fresher and whole.</summary>
    public IReadOnlyList<BattleTable> ToTables(IReadOnlyList<BattleTable> cold)
    {
        if (Battle.Count == 0) return cold;
        var effective = Effective();
        return [.. cold.Select(table => new BattleTable(table.Origin, [.. table.Blocks.Select(block =>
        {
            foreach (var sample in effective)
            {
                if (sample.BattleId == block.BattleId) return block with { CurrentHp = sample.Hp, MaxHp = sample.MaxHp };
            }

            return block;
        })]))];
    }
}

/// <summary>
/// The party read straight from the structure the game reads (stride 0x1E4), not rebuilt: twenty bytes of identity and twenty-eight of stats for
/// each member, instead of two hundred and sixty and a decoding. The offsets are PK7's, checked against PKHeX in the tests; whether the game keeps
/// them in the clear (and not in PK7's shuffled and encrypted form) is not assumed: <see cref="Calibrate"/> compares them with what the full read
/// of the same moment decoded and turns this reader off for good when they do not agree.
/// </summary>
public sealed class PartyHotReader
{
    /// <summary>Species, held item, TID, SID, experience, ability... PID: from 0x08 of the stored block, twenty bytes.</summary>
    public const int IdentityOffset = 0x08, IdentitySize = 20;

    /// <summary>Inside the identity bytes.</summary>
    public const int SpeciesAt = 0, ItemAt = 2, ExpAt = 8, PidAt = 16;

    /// <summary>The twenty-eight bytes of battle stats: status (four bytes), level, HP and maximum HP.</summary>
    public const int TailSize = 28, StatusAt = 0, LevelAt = 4, HpAt = 8, MaxHpAt = 10;

    public bool Calibrated { get; private set; }

    private static uint TailOffset(PartyLayout layout) =>
        layout.Stride == PartyLayoutLocator.AuthoritativeStride ? PartyLayoutLocator.AuthoritativeStatsOffset : (uint)Pk7Reader.StoredSize;

    public static PartySample? Parse(int slot, ReadOnlySpan<byte> identity, ReadOnlySpan<byte> tail)
    {
        if (identity.Length < IdentitySize || tail.Length < TailSize) return null;
        var species = BinaryPrimitives.ReadUInt16LittleEndian(identity[SpeciesAt..]);
        var hp = BinaryPrimitives.ReadUInt16LittleEndian(tail[HpAt..]);
        var max = BinaryPrimitives.ReadUInt16LittleEndian(tail[MaxHpAt..]);

        // HP and maximum come from the same twenty-eight bytes, so they are one reading; a pair that cannot be (more life than maximum, no maximum) is not.
        if (species == 0 || max == 0 || hp > max) return null;
        return new PartySample(slot, species, BinaryPrimitives.ReadUInt16LittleEndian(identity[ItemAt..]), BinaryPrimitives.ReadUInt32LittleEndian(identity[ExpAt..]),
            BinaryPrimitives.ReadUInt32LittleEndian(identity[PidAt..]), BinaryPrimitives.ReadInt32LittleEndian(tail[StatusAt..]), tail[LevelAt], hp, max);
    }

    /// <summary>Turns the reader on when what it reads agrees, for every member, with what the full read decoded.</summary>
    public bool Calibrate(IGameMemory memory, PartyLayout layout, IReadOnlyList<LivePartyMember> cold)
    {
        Calibrated = false;
        if (cold.Count == 0) return false;
        var samples = Read(memory, layout, cold.Count);
        if (samples is null || samples.Length != cold.Count) return false;

        foreach (var member in cold)
        {
            var sample = samples.FirstOrDefault(s => s.Slot == member.Slot);
            if (sample.Pid == 0 || sample.Pid != member.Pid || sample.Species != member.Species || sample.Hp != member.CurrentHp || sample.MaxHp != member.MaxHp) return false;
        }

        Calibrated = true;
        return true;
    }

    /// <summary>The first <paramref name="count"/> members, or null when one of them reads as nothing coherent.</summary>
    public PartySample[]? Read(IGameMemory memory, PartyLayout layout, int count)
    {
        if (count is <= 0 or > 6) return null;
        var ranges = new List<(uint, int)>(count * 2);
        for (var slot = 0; slot < count; slot++)
        {
            ranges.Add((layout.SlotAddress(slot) + IdentityOffset, IdentitySize));
            ranges.Add((layout.SlotAddress(slot) + TailOffset(layout), TailSize));
        }

        var answers = memory.ReadMany(ranges);
        var result = new PartySample[count];
        for (var slot = 0; slot < count; slot++)
        {
            var identity = answers[slot * 2];
            var tail = answers[(slot * 2) + 1];
            if (identity is null || tail is null || Parse(slot, identity, tail) is not { } sample) return null;
            result[slot] = sample;
        }

        return result;
    }
}

/// <summary>
/// The HP of the player's blocks of the battle, fourteen bytes each (species, maximum HP, HP, ..., position), from the blocks the monitor already
/// found. A block that is not the one it was (another species, another position, an HP over its maximum) is not read as a change: it is the block
/// being freed at the end of the battle, and the whole read is dropped.
/// </summary>
public static class BattleHotReader
{
    public const int Offset = 0x2C, Length = 14, MaxAt = 2, HpAt = 4, IdAt = 13;

    public static BattleSample? Parse(int table, int expectedId, int expectedSpecies, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < Length) return null;
        var species = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        var max = BinaryPrimitives.ReadUInt16LittleEndian(bytes[MaxAt..]);
        var hp = BinaryPrimitives.ReadUInt16LittleEndian(bytes[HpAt..]);
        if (species != expectedSpecies || bytes[IdAt] != expectedId || max == 0 || hp > max) return null;
        return new BattleSample(table, expectedId, species, max, hp);
    }

    public static BattleSample[]? Read(IGameMemory memory, IReadOnlyList<BattleTable> cold)
    {
        var wanted = new List<(int Table, BattleBlock Block)>();
        for (var t = 0; t < cold.Count; t++)
        {
            foreach (var block in cold[t].Blocks.Where(b => b.IsPlayers)) wanted.Add((t, block));
        }

        if (wanted.Count == 0) return [];
        var answers = memory.ReadMany([.. wanted.Select(w => (cold[w.Table].Origin + (uint)(w.Block.BattleId * BattleLayout.Stride) + Offset, Length))]);
        var result = new BattleSample[wanted.Count];
        for (var i = 0; i < wanted.Count; i++)
        {
            if (answers[i] is not { } bytes || Parse(wanted[i].Table, wanted[i].Block.BattleId, wanted[i].Block.Species, bytes) is not { } sample) return null;
            result[i] = sample;
        }

        return result;
    }
}

/// <summary>
/// The fast reading as a whole: the party and the battle in one batch, and a change counts only when a second reading agrees (a read can be caught
/// between two writes of the game; one more round trip, a quarter of a millisecond, buys never telling a hit that did not happen).
/// </summary>
public sealed class VitalsReader(IGameMemory memory, Func<long>? clock = null)
{
    private readonly Func<long> _clock = clock ?? System.Diagnostics.Stopwatch.GetTimestamp;
    private readonly PartyHotReader _party = new();
    private int _lead = -1;
    private string _origins = string.Empty;

    public bool PartyReady => _party.Calibrated;

    public bool CalibrateParty(PartyLayout layout, IReadOnlyList<LivePartyMember> cold) => _party.Calibrate(memory, layout, cold);

    public void ForgetParty() => _party.Calibrate(memory, new PartyLayout(0, 0, string.Empty), []);

    /// <summary>One reading; null when nothing coherent could be read (the emulator is gone, the blocks changed under it).</summary>
    public VitalsFrame? ReadOnce(IReadOnlyList<BattleTable> cold, PartyLayout? layout, int partyCount)
    {
        PartySample[] party = [];
        if (layout is not null && _party.Calibrated)
        {
            if (_party.Read(memory, layout, partyCount) is not { } read) return null;
            party = read;
        }

        var battle = BattleHotReader.Read(memory, cold);
        if (battle is null) return null;
        return new VitalsFrame(_clock(), party, battle);
    }

    /// <summary>A reading that differs from the last accepted one is read once more; if the two disagree the last accepted one stays and nothing changed.</summary>
    public VitalsFrame? ReadConfirmed(VitalsFrame? accepted, IReadOnlyList<BattleTable> cold, PartyLayout? layout, int partyCount)
    {
        // Another battle, another pair of tables: which one leads is found again.
        var origins = string.Join(",", cold.Select(table => table.Origin));
        if (origins != _origins)
        {
            _origins = origins;
            _lead = -1;
        }

        var first = ReadOnce(cold, layout, partyCount);
        if (first is null) return null;
        first = first with { Lead = _lead };
        if (accepted is null || first.SameAs(accepted)) return first;

        var second = ReadOnce(cold, layout, partyCount);
        if (second is null || !second.SameAs(first)) return accepted;

        // The first time the battle's life moves, the table that moved first is the one that leads from then on.
        if (_lead < 0 && FindLead(accepted, second) is { } found) _lead = found;
        return second with { Lead = _lead };
    }

    /// <summary>The table whose life differs from the accepted frame's while the others still agree with it: the one ahead. Null when it cannot be said.</summary>
    private static int? FindLead(VitalsFrame accepted, VitalsFrame now)
    {
        var moved = new HashSet<int>();
        foreach (var sample in now.Battle)
        {
            var before = accepted.Battle.FirstOrDefault(b => b.Table == sample.Table && b.BattleId == sample.BattleId);
            if (before.Species != 0 && before.Hp != sample.Hp) moved.Add(sample.Table);
        }

        var tables = now.Battle.Select(s => s.Table).Distinct().Count();
        return tables >= 2 && moved.Count == 1 ? moved.First() : null;
    }
}
