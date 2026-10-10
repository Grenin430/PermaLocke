using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Battle;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;
using PermaLocke.GameLink.Vitals;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// The fast reading of the game's life (2026-10-11): its offsets are the ones PKHeX and the real battle blocks have, what it reads is coherent or it is
/// dropped, a change needs a second reading, and it recovers when the emulator goes and comes back.
/// </summary>
public sealed class VitalsTests
{
    /// <summary>A memory made of regions; anything outside them is not there, as in the emulator. Counts the batches and the requests.</summary>
    private sealed class FakeMemory : IGameMemory
    {
        private readonly SortedDictionary<uint, byte[]> _regions = [];

        public int Batches { get; private set; }

        public int Requests { get; private set; }

        public bool Gone { get; set; }

        public void Put(uint address, byte[] bytes) => _regions[address] = bytes;

        public void Poke(uint address, ushort value)
        {
            foreach (var (start, bytes) in _regions)
            {
                if (address >= start && address + 2 <= start + bytes.Length)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan((int)(address - start)), value);
                    return;
                }
            }

            throw new InvalidOperationException($"0x{address:X8} is in no region");
        }

        public byte[]?[] ReadMany(IReadOnlyList<(uint Address, int Size)> ranges)
        {
            Batches++;
            Requests += ranges.Count;
            return [.. ranges.Select(range =>
            {
                if (Gone) return null;
                foreach (var (start, bytes) in _regions)
                {
                    if (range.Address >= start && range.Address + range.Size <= start + bytes.Length) return bytes.AsSpan((int)(range.Address - start), range.Size).ToArray();
                }

                return null;
            })];
        }
    }

    // ---------------------------------------------------------------- the party

    private static PK7 Pokemon(int species, uint pid, int hp, int max, int level = 42, int item = 234, int status = 0)
    {
        var pk = new PK7 { Species = (ushort)species, PID = pid, EXP = 12345, HeldItem = item, Stat_Level = (byte)level, Stat_HPMax = max, Stat_HPCurrent = hp, Status_Condition = status };
        return pk;
    }

    private static readonly PartyLayout Layout = new(0x08100000, PartyLayoutLocator.AuthoritativeStride, "Test");

    /// <summary>The authoritative structure of the game as the fast reader expects it: the stored block at the slot, the stats at +0x158.</summary>
    private static void PutParty(FakeMemory memory, params PK7[] party)
    {
        for (var slot = 0; slot < party.Length; slot++)
        {
            var bytes = new byte[PartyLayoutLocator.AuthoritativeStride];
            party[slot].Data.ToArray().AsSpan(0, Pk7Reader.StoredSize).CopyTo(bytes);
            party[slot].Data.ToArray().AsSpan(Pk7Reader.StoredSize, 28).CopyTo(bytes.AsSpan((int)PartyLayoutLocator.AuthoritativeStatsOffset));
            memory.Put(Layout.SlotAddress(slot), bytes);
        }
    }

    private static LivePartyMember Cold(int slot, PK7 pk) =>
        new(slot, pk.Species, "x", "x", pk.Stat_Level, pk.Stat_HPCurrent, pk.Stat_HPMax, false, pk.PID, 0, "", "Test");

    [Fact]
    public void The_offsets_of_the_party_reading_are_the_ones_of_PKHeX_for_every_field_it_takes()
    {
        var pk = Pokemon(130, 0xCAFEBABE, 77, 143, level: 42, item: 234, status: 0x10);
        var sample = PartyHotReader.Parse(2, pk.Data.ToArray().AsSpan(PartyHotReader.IdentityOffset, PartyHotReader.IdentitySize), pk.Data.ToArray().AsSpan(Pk7Reader.StoredSize, PartyHotReader.TailSize));

        Assert.NotNull(sample);
        Assert.Equal(new PartySample(2, 130, 234, 12345u, 0xCAFEBABE, 0x10, 42, 77, 143), sample);
    }

    [Fact]
    public void The_offsets_of_a_battle_block_are_the_ones_of_the_layout_and_of_a_real_block()
    {
        // Ferrocuello, read from a real battle: header and data, 74 bytes from the heap header.
        byte[] real =
        [
            0x44, 0x55, 0x00, 0x00, 0x20, 0x03, 0x00, 0x00, 0xFC, 0x26, 0x00, 0x30, 0x68, 0x2A, 0x00, 0x30,
            0xE7, 0xFF, 0xFF, 0xFF, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x18, 0xE5, 0x02, 0x30, 0x00, 0x00, 0x00, 0x00, 0x12, 0x04, 0x04, 0x00, 0xE1, 0x03, 0xB3, 0x00,
            0xAE, 0x00, 0x00, 0x00, 0x00, 0x00, 0x32, 0x00, 0x3B, 0x00, 0x50, 0x6C, 0x80, 0x00, 0x00, 0x00
        ];
        var block = BattleLayout.Parse(0x30002738, real)!;
        var sample = BattleHotReader.Parse(0, block.BattleId, block.Species, real.AsSpan(BattleLayout.HeaderSize + BattleHotReader.Offset, BattleHotReader.Length));

        Assert.Equal(new BattleSample(0, block.BattleId, block.Species, block.MaxHp, block.CurrentHp), sample);
        Assert.Equal(174, sample!.Value.Hp);
        Assert.Equal(179, sample.Value.MaxHp);
        Assert.Equal(BattleLayout.CurrentHpOffset, BattleHotReader.Offset + BattleHotReader.HpAt);
    }

    [Fact]
    public void The_party_is_read_in_the_clear_only_after_it_agreed_with_the_full_read_and_never_when_it_does_not()
    {
        var team = new[] { Pokemon(130, 11, 143, 143), Pokemon(748, 22, 100, 122), Pokemon(143, 33, 170, 170) };
        var memory = new FakeMemory();
        PutParty(memory, team);
        var cold = team.Select((pk, i) => Cold(i, pk)).ToList();

        var reader = new VitalsReader(memory);
        Assert.False(reader.PartyReady);
        Assert.True(reader.CalibrateParty(Layout, cold));
        var frame = reader.ReadOnce([], Layout, 3)!;
        Assert.Equal(3, frame.Party.Count);
        Assert.Equal(new[] { 143, 100, 170 }, frame.Party.Select(s => s.Hp));

        // The same structure as a cold read that disagrees (the game keeps it another way): the reader stays off and the party is left to the slow read.
        var other = new VitalsReader(memory);
        Assert.False(other.CalibrateParty(Layout, [Cold(0, Pokemon(130, 11, 99, 143)), Cold(1, team[1]), Cold(2, team[2])]));
        Assert.Empty(other.ReadOnce([], Layout, 3)!.Party);

        // Bytes that are not PK7's in the clear (shuffled and encrypted) do not calibrate either.
        var scrambled = new FakeMemory();
        PutParty(scrambled, team);
        for (var slot = 0; slot < 3; slot++) scrambled.Poke(Layout.SlotAddress(slot) + PartyLayoutLocator.AuthoritativeStatsOffset + 8, 0xFFFF);
        Assert.False(new VitalsReader(scrambled).CalibrateParty(Layout, cold));
    }

    [Fact]
    public void A_pair_of_life_and_maximum_that_cannot_be_is_not_a_reading()
    {
        var pk = Pokemon(130, 11, 143, 143);
        var identity = pk.Data.ToArray().AsSpan(PartyHotReader.IdentityOffset, PartyHotReader.IdentitySize).ToArray();
        var tail = pk.Data.ToArray().AsSpan(Pk7Reader.StoredSize, PartyHotReader.TailSize).ToArray();

        Assert.NotNull(PartyHotReader.Parse(0, identity, tail));
        BinaryPrimitives.WriteUInt16LittleEndian(tail.AsSpan(PartyHotReader.HpAt), 144);
        Assert.Null(PartyHotReader.Parse(0, identity, tail));
        BinaryPrimitives.WriteUInt16LittleEndian(tail.AsSpan(PartyHotReader.HpAt), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(tail.AsSpan(PartyHotReader.MaxHpAt), 0);
        Assert.Null(PartyHotReader.Parse(0, identity, tail));
    }

    // ---------------------------------------------------------------- the battle

    private const uint TableB = 0x30009730, TableA = 0x30002748;

    private static byte[] TableBytes(params (int Id, int Species, int Max, int Hp)[] blocks)
    {
        var bytes = new byte[18 * BattleLayout.Stride];
        foreach (var (id, species, max, hp) in blocks)
        {
            var data = (id * BattleLayout.Stride);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(data + 0x20), 0x08000000u + (uint)id);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(data + 0x2C), (ushort)species);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(data + 0x2E), (ushort)max);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(data + 0x30), (ushort)hp);
            bytes[data + 0x39] = (byte)id;
        }

        return bytes;
    }

    private static (FakeMemory Memory, IReadOnlyList<BattleTable> Cold) Battle(params (int Id, int Species, int Max, int Hp)[] players)
    {
        var memory = new FakeMemory();
        memory.Put(TableB, TableBytes(players));
        memory.Put(TableA, TableBytes(players));
        BattleBlock Block(uint origin, (int Id, int Species, int Max, int Hp) p) => new(origin + (uint)(p.Id * BattleLayout.Stride), 0, p.Species, p.Max, p.Hp, p.Id);
        IReadOnlyList<BattleTable> cold = [new BattleTable(TableA, [.. players.Select(p => Block(TableA, p))]), new BattleTable(TableB, [.. players.Select(p => Block(TableB, p))])];
        return (memory, cold);
    }

    [Fact]
    public void The_battle_is_read_with_one_batch_of_six_small_reads_for_two_tables_and_only_the_players_blocks()
    {
        var (memory, cold) = Battle((0, 130, 143, 90), (1, 748, 122, 122), (2, 143, 170, 0));
        var frame = new VitalsReader(memory).ReadOnce(cold, null, 0)!;

        Assert.Equal(1, memory.Batches);
        Assert.Equal(6, memory.Requests);
        Assert.Equal(6, frame.Battle.Count);
        Assert.Equal([90, 122, 0, 90, 122, 0], frame.Battle.Select(s => s.Hp));
    }

    [Fact]
    public void A_block_that_is_not_the_one_it_was_drops_the_whole_reading_instead_of_telling_a_hit()
    {
        var (memory, cold) = Battle((0, 130, 143, 143), (1, 748, 122, 122));
        var reader = new VitalsReader(memory);
        Assert.NotNull(reader.ReadOnce(cold, null, 0));

        // The battle ended and the block was freed and reused: another species, more life than maximum, another position.
        memory.Poke(TableB + 0x2C, 999);
        Assert.Null(reader.ReadOnce(cold, null, 0));
        memory.Poke(TableB + 0x2C, 130);
        memory.Poke(TableB + 0x30, 500);
        Assert.Null(reader.ReadOnce(cold, null, 0));
        memory.Poke(TableB + 0x30, 143);
        Assert.NotNull(reader.ReadOnce(cold, null, 0));
    }

    [Fact]
    public void The_frame_gives_the_tables_back_with_the_fresh_life_in_the_players_blocks_only()
    {
        var (memory, cold) = Battle((0, 130, 143, 143), (1, 748, 122, 122));
        var withWild = new List<BattleTable>(cold);
        withWild[1] = new BattleTable(TableB, [.. withWild[1].Blocks, new BattleBlock(TableB + (12u * BattleLayout.Stride), 0, 19, 20, 20, 12)]);
        memory.Poke(TableB + 0x30, 60);

        var tables = new VitalsReader(memory).ReadOnce(withWild, null, 0)!.ToTables(withWild);

        Assert.Equal(143, tables[0].Block(0)!.CurrentHp);
        Assert.Equal(60, tables[1].Block(0)!.CurrentHp);
        Assert.Equal(20, tables[1].Block(12)!.CurrentHp);
        Assert.Equal(122, tables[1].Block(1)!.CurrentHp);
    }

    // ---------------------------------------------------------------- the agreement of two readings

    [Fact]
    public void A_change_counts_when_a_second_reading_agrees_and_a_torn_one_changes_nothing()
    {
        var (memory, cold) = Battle((0, 130, 143, 143));
        var reader = new VitalsReader(memory);
        var accepted = reader.ReadConfirmed(null, cold, null, 0)!;

        // A steady value: no second reading is needed (one batch per call).
        var batches = memory.Batches;
        Assert.True(reader.ReadConfirmed(accepted, cold, null, 0)!.SameAs(accepted));
        Assert.Equal(batches + 1, memory.Batches);

        // A change that holds: two batches, and the new value is the accepted one.
        memory.Poke(TableB + 0x30, 100);
        memory.Poke(TableA + 0x30, 100);
        batches = memory.Batches;
        var changed = reader.ReadConfirmed(accepted, cold, null, 0)!;
        Assert.Equal(batches + 2, memory.Batches);
        Assert.Equal(100, changed.Battle[0].Hp);

        // A torn one: the second reading says something else, so the accepted value stays.
        var torn = new TornMemory(memory);
        var kept = new VitalsReader(torn).ReadConfirmed(changed, cold, null, 0)!;
        Assert.True(kept.SameAs(changed));
    }

    /// <summary>The first read of a pair sees a half-written value, the next ones see the right one.</summary>
    private sealed class TornMemory(FakeMemory inner) : IGameMemory
    {
        private int _calls;

        public byte[]?[] ReadMany(IReadOnlyList<(uint Address, int Size)> ranges)
        {
            var answers = inner.ReadMany(ranges);
            if (++_calls == 1) BinaryPrimitives.WriteUInt16LittleEndian(answers[0]!.AsSpan(BattleHotReader.HpAt), 7);
            else if (_calls == 2) BinaryPrimitives.WriteUInt16LittleEndian(answers[0]!.AsSpan(BattleHotReader.HpAt), 9);
            return answers;
        }
    }

    // ---------------------------------------------------------------- the emulator goes away

    [Fact]
    public void When_the_emulator_goes_the_reading_says_nothing_and_when_it_comes_back_it_reads_again_from_a_clean_start()
    {
        var team = new[] { Pokemon(130, 11, 143, 143) };
        var (memory, cold) = Battle((0, 130, 143, 143));
        PutParty(memory, team);
        var reader = new VitalsReader(memory);
        Assert.True(reader.CalibrateParty(Layout, [Cold(0, team[0])]));
        var first = reader.ReadConfirmed(null, cold, Layout, 1);
        Assert.NotNull(first);

        memory.Gone = true;
        Assert.Null(reader.ReadConfirmed(first, cold, Layout, 1));

        // It has to be told again what the party is: nothing is believed from before the emulator went.
        reader.ForgetParty();
        Assert.False(reader.PartyReady);
        memory.Gone = false;
        var back = reader.ReadConfirmed(null, cold, Layout, 1)!;
        Assert.Empty(back.Party);
        Assert.True(reader.CalibrateParty(Layout, [Cold(0, team[0])]));
        Assert.Single(reader.ReadConfirmed(null, cold, Layout, 1)!.Party);
    }

    // ---------------------------------------------------------------- the batch over the wire

    private sealed class FakeEmulator : IDisposable
    {
        private readonly UdpClient _server = new(new IPEndPoint(IPAddress.Loopback, 0));
        private readonly CancellationTokenSource _stop = new();

        public FakeEmulator(Func<uint, int, byte[]?> memory, bool reverse = false, bool silent = false)
        {
            Task.Run(() =>
            {
                var pending = new List<(IPEndPoint From, byte[] Reply)>();
                while (!_stop.IsCancellationRequested)
                {
                    try
                    {
                        IPEndPoint? from = null;
                        var request = _server.Receive(ref from);
                        if (silent) continue;
                        var id = BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(4));
                        var address = BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(16));
                        var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(20));
                        var data = memory(address, size) ?? [];
                        var reply = new byte[16 + data.Length];
                        BinaryPrimitives.WriteUInt32LittleEndian(reply, 1);
                        BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(4), id);
                        BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(8), 1);
                        BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(12), (uint)data.Length);
                        data.CopyTo(reply.AsSpan(16));

                        // Replies can come out of order and a stale one can come with them: the reader goes by the number.
                        pending.Add((from!, reply));
                        if (pending.Count < 3) continue;
                        if (reverse) pending.Reverse();
                        foreach (var (to, bytes) in pending) _server.Send(bytes, bytes.Length, to);
                        pending.Clear();
                    }
                    catch (Exception) when (_stop.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (SocketException)
                    {
                    }
                }
            });
        }

        public int Port => ((IPEndPoint)_server.Client.LocalEndPoint!).Port;

        public void Dispose()
        {
            _stop.Cancel();
            _server.Dispose();
        }
    }

    [Fact]
    public void The_batch_reader_sends_everything_first_and_matches_the_answers_by_number_even_out_of_order()
    {
        using var emulator = new FakeEmulator((address, size) => [.. Enumerable.Range(0, size).Select(i => (byte)((address >> 4) + i))], reverse: true);
        using var reader = new AzaharBatchReader(500, port: emulator.Port);

        var answers = reader.ReadMany([(0x100, 4), (0x200, 4), (0x300, 4)]);

        Assert.Equal([0x10, 0x11, 0x12, 0x13], answers[0]);
        Assert.Equal([0x20, 0x21, 0x22, 0x23], answers[1]);
        Assert.Equal([0x30, 0x31, 0x32, 0x33], answers[2]);
        Assert.Equal(0, reader.Incomplete);
    }

    [Fact]
    public void Unmapped_memory_and_a_silent_emulator_come_back_as_nothing_without_an_exception_and_within_the_timeout()
    {
        using (var emulator = new FakeEmulator((address, size) => address == 0x100 ? new byte[size] : null))
        using (var reader = new AzaharBatchReader(500, port: emulator.Port))
        {
            var answers = reader.ReadMany([(0x100, 4), (0x999, 4), (0x100, 2)]);
            Assert.NotNull(answers[0]);
            Assert.Null(answers[1]);
            Assert.NotNull(answers[2]);
        }

        using var silent = new FakeEmulator((_, _) => null, silent: true);
        using var mute = new AzaharBatchReader(120, port: silent.Port);
        var started = System.Diagnostics.Stopwatch.StartNew();
        var none = mute.ReadMany([(0x100, 4), (0x200, 4)]);
        Assert.All(none, Assert.Null);
        Assert.InRange(started.ElapsedMilliseconds, 100, 1500);
        Assert.Equal(1, mute.Incomplete);

        // Nobody listening at all: also nothing, and no exception.
        using var closed = new AzaharBatchReader(120, port: 1);
        Assert.All(closed.ReadMany([(0x100, 4)]), Assert.Null);
    }
}
