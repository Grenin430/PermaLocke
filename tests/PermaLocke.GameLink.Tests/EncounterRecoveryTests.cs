using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink.Battle;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Field;
using PermaLocke.GameLink.Rpc;
using PKHeX.Core;

namespace PermaLocke.GameLink.Tests;

public sealed class EncounterRecoveryTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PermaLockeEncounter-" + Guid.NewGuid().ToString("N"));
    private readonly UdpClient _server = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<uint, byte[]> _memory = [];
    private readonly ManualTime _time = new();
    private Task _serving = Task.CompletedTask;
    private AzaharRpcClient _client = null!;
    private SavedGameCache _saved = null!;
    private BagService _bag = null!;
    private int _searches;
    private string SavePath => Path.Combine(_root, "Emulator", "user", "sdmc", "Nintendo 3DS", "a", "b",
        "title", "00040000", "001b5100", "data", "00000001", "main");
    private string BagPath => Path.Combine(_root, "bag.txt");
    private const uint CounterAddress = 0x33080000;
    private const uint BagAddress = 0x33011934;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        File.WriteAllText(Path.Combine(_root, "Emulator", "azahar.exe"), "");
        _client = new("127.0.0.1", ((IPEndPoint)_server.Client.LocalEndPoint!).Port, 100);
        _saved = new(new PlayerSave(new AzaharInstallation(NullLogger<AzaharInstallation>.Instance), _client, _root));
        _bag = new(_client, null!, Path.Combine(_root, "ledger.json"), BagPath, NullLogger<BagService>.Instance);
        _serving = Task.Run(async () =>
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    var request = await _server.ReceiveAsync(_stop.Token);
                    byte[] response;
                    lock (_memory) response = Answer(request.Buffer);
                    await _server.SendAsync(response, request.RemoteEndPoint, _stop.Token);
                }
            }
            catch (OperationCanceledException) { }
        });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _stop.Cancel();
        await _serving;
        _client.Dispose();
        _server.Dispose();
        _stop.Dispose();
        Directory.Delete(_root, true);
    }

    private byte[] Answer(byte[] request)
    {
        var type = (RpcRequestType)BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(8));
        var address = BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(16));
        byte[] data;
        if (type == RpcRequestType.SetGetProcess) data = BitConverter.GetBytes(42u);
        else if (type == RpcRequestType.ReadMemory)
        {
            var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(20));
            var region = _memory.FirstOrDefault(p => address >= p.Key && (ulong)address + (uint)length <= (ulong)p.Key + (uint)p.Value.Length);
            data = region.Value is null ? [] : region.Value.AsSpan((int)(address - region.Key), length).ToArray();
        }
        else if (type == RpcRequestType.SearchMemory)
        {
            _searches++;
            var size = BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(20));
            var stride = (int)BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(24));
            var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(28));
            var hits = new List<uint>();
            foreach (var (start, bytes) in _memory.OrderBy(p => p.Key))
            {
                for (var offset = 0; offset + length <= bytes.Length; offset += stride)
                {
                    var candidate = start + (uint)offset;
                    if (candidate < address || (ulong)candidate + (uint)length > (ulong)address + size) continue;
                    var match = true;
                    for (var i = 0; i < length; i++)
                        if ((bytes[offset + i] & request[32 + length + i]) != (request[32 + i] & request[32 + length + i]))
                        { match = false; break; }
                    if (match) hits.Add(candidate);
                }
            }
            hits = [.. hits.Take(255)];
            data = new byte[4 + hits.Count * 4];
            BinaryPrimitives.WriteUInt32LittleEndian(data, (uint)hits.Count);
            for (var i = 0; i < hits.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4 + 4 * i), hits[i]);
        }
        else throw new InvalidOperationException("Unexpected RPC " + type);
        var reply = new byte[16 + data.Length];
        request.AsSpan(0, 12).CopyTo(reply);
        BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(12), (uint)data.Length);
        data.CopyTo(reply, 16);
        return reply;
    }

    private SAV7USUM Save(ushort world = 0, ushort map = 0)
    {
        var game = new SAV7USUM();
        for (var i = 0; i < 10; i++) game.Records.SetRecord(i, i == 1 ? 17 : i == 4 ? 3 : 0);
        var situation = game.Data.Slice(game.AllBlocks[1].Offset, 0x14);
        BinaryPrimitives.WriteUInt16LittleEndian(situation, world);
        BinaryPrimitives.WriteUInt16LittleEndian(situation[2..], map);
        BinaryPrimitives.WriteSingleLittleEndian(situation[8..], 12);
        BinaryPrimitives.WriteSingleLittleEndian(situation[12..], 20);
        BinaryPrimitives.WriteSingleLittleEndian(situation[16..], 30);
        // SaveUtil recognises a USUM file by its size and BEEF footer. Blank editor saves lack the footer.
        // https://github.com/kwsch/PKHeX/blob/master/PKHeX.Core/Saves/Util/SaveUtil.cs
        BinaryPrimitives.WriteUInt32LittleEndian(game.Data[^0x1F0..], 0x42454546);
        File.WriteAllBytes(SavePath, game.Write().ToArray());
        Assert.NotNull(_saved.Load());
        return game;
    }

    private void Memory(uint address, byte[] bytes) { lock (_memory) _memory[address] = bytes; }
    private void RemoveMemory(uint address) { lock (_memory) _memory.Remove(address); }

    private static byte[] CounterBlock(SAV7USUM game, int extraWild = 0)
    {
        var bytes = new byte[600];
        for (var i = 0; i < 100; i++) BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 4), game.GetRecord(i));
        for (var i = 100; i < 200; i++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(BattleCounterReader.OffsetOf(i)), (ushort)game.GetRecord(i));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), game.GetRecord(4) + extraWild);
        return bytes;
    }

    private void LocateBag()
    {
        var layout = BagLayout.UltraSunMoon;
        var bytes = new byte[layout.BlockSize + layout.PointerTableBytes];
        for (var i = 0; i < layout.Pockets.Count; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(layout.BlockSize + 4 * i), BagAddress + (uint)layout.Pockets[i].Offset);
        Memory(BagAddress, bytes);
        File.WriteAllText(BagPath, BagAddress.ToString("X8"));
        Assert.NotNull(_bag.Locate());
    }

    private FieldZoneReader Zones() => new(_client, _saved, new MapTable([new(0, 0, "ruta-1", "Ruta 1"), new(1, 4, "casa", "Casa")]),
        Path.Combine(_root, "zones.txt"), NullLogger<FieldZoneReader>.Instance, _time);
    private BattleCounterReader Counters() => new(_client, _bag, _saved, NullLogger<BattleCounterReader>.Instance, _time);

    private void FieldRecords()
    {
        var bytes = new byte[FieldRecord.Length];
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(4), 12);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(8), 20);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(12), 30);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(24), 0.6f);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(28), 0.8f);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), uint.MaxValue);
        Memory(0x30310000, bytes);
        Memory(0x30310200, (byte[])bytes.Clone());
    }

    [Fact]
    public void First_save_recovers_the_zone_without_waiting_two_minutes()
    {
        FieldRecords();
        var zones = Zones();
        Assert.Null(zones.CurrentZone());
        var firstSearches = _searches;
        Save();
        _time.Advance(19);
        Assert.Null(zones.CurrentZone());
        Assert.Equal(firstSearches, _searches);
        _time.Advance(1);
        Assert.Equal("ruta-1", zones.CurrentZone()?.LocationId);
        Assert.Equal("ruta-1", zones.LastConfirmed?.Zone.LocationId);
    }

    [Fact]
    public void An_unchanged_save_does_not_trigger_repeated_searches()
    {
        Save();
        var zones = Zones();
        Assert.Null(zones.CurrentZone());
        var searches = _searches;
        for (var i = 0; i < 23; i++)
        {
            _time.Advance(5);
            Assert.Null(zones.CurrentZone());
        }
        Assert.Equal(searches, _searches);
    }

    [Fact]
    public void Saving_during_a_battle_does_not_enable_zone_searches()
    {
        FieldRecords();
        var zones = Zones();
        Assert.Null(zones.CurrentZone());
        var searches = _searches;
        Save();
        _time.Advance(30);
        Assert.Null(zones.CurrentZone(allowSearch: false));
        Assert.Equal(searches, _searches);
        Assert.NotNull(zones.CurrentZone(allowSearch: true));
    }

    [Fact]
    public void No_save_is_explained_and_first_save_allows_future_encounters_to_be_read()
    {
        var counters = Counters();
        Assert.Null(counters.Read());
        Assert.Contains("Guarda", counters.Problem);
        Assert.Equal(0, _searches);
        var game = Save();
        Memory(CounterAddress, CounterBlock(game));
        var baseline = counters.Read();
        Assert.NotNull(baseline);
        Assert.Null(counters.Problem);
        Memory(CounterAddress, CounterBlock(game, 1));
        Assert.Equal(baseline.WildBattles + 1, counters.Read()?.WildBattles);
    }

    [Fact]
    public void A_bag_found_after_failed_search_recovers_counters_without_another_sweep()
    {
        var game = Save();
        var counters = Counters();
        Assert.Null(counters.Read());
        Assert.Equal(1, _searches);
        LocateBag();
        Memory(BagAddress + BattleCounterReader.DistanceFromBag, CounterBlock(game));
        _time.Advance(5);
        Assert.NotNull(counters.Read());
        Assert.Null(counters.Problem);
        Assert.Equal(1, _searches);
    }

    [Fact]
    public void A_dead_counter_address_is_replaced_after_three_failed_reads()
    {
        var game = Save();
        Memory(CounterAddress, CounterBlock(game));
        var counters = Counters();
        Assert.NotNull(counters.Read());
        RemoveMemory(CounterAddress);
        for (var i = 0; i < 3; i++) Assert.Null(counters.Read());
        LocateBag();
        Memory(BagAddress + BattleCounterReader.DistanceFromBag, CounterBlock(game, 1));
        _time.Advance(5);
        Assert.Equal(game.GetRecord(4) + 1, counters.Read()?.WildBattles);
        Assert.Equal(1, _searches);
    }

    [Fact]
    public void One_failed_read_does_not_discard_a_valid_counter_address()
    {
        var game = Save();
        Memory(CounterAddress, CounterBlock(game));
        var counters = Counters();
        Assert.NotNull(counters.Read());
        RemoveMemory(CounterAddress);
        Assert.Null(counters.Read());
        Memory(CounterAddress, CounterBlock(game, 1));
        Assert.NotNull(counters.Read());
        Assert.Null(counters.Problem);
        Assert.Equal(1, _searches);
    }

    [Fact]
    public void A_readable_but_stale_counter_copy_is_replaced_when_the_live_bag_appears()
    {
        var game = Save();
        Memory(CounterAddress, CounterBlock(game));
        var counters = Counters();
        Assert.Equal(3, counters.Read()?.WildBattles);
        LocateBag();
        var live = BagAddress + BattleCounterReader.DistanceFromBag;
        Memory(live, CounterBlock(game, 1));
        _time.Advance(5);
        Assert.Equal(4, counters.Read()?.WildBattles);
        Memory(live, CounterBlock(game, 2));
        Assert.Equal(5, counters.Read()?.WildBattles);
        Assert.Equal(1, _searches);
    }

    [Fact]
    public void An_invalid_bag_reference_does_not_replace_working_counters()
    {
        var game = Save();
        Memory(CounterAddress, CounterBlock(game));
        var counters = Counters();
        Assert.NotNull(counters.Read());
        LocateBag();
        RemoveMemory(BagAddress);
        Memory(BagAddress + BattleCounterReader.DistanceFromBag, CounterBlock(game, 30));
        _time.Advance(5);
        Assert.Equal(3, counters.Read()?.WildBattles);
        Assert.Equal(1, _searches);
    }

    private void BattleMemory(int hp, uint offset = 0)
    {
        foreach (var first in new uint[] { 0x30002738, 0x30009720 })
        foreach (var id in new[] { 0, 12 })
        {
            var bytes = new byte[BattleLayout.ReadLength];
            BattleLayout.SearchPattern.CopyTo(bytes);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x30), 0x3002E518);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x3C), (ushort)(id == 0 ? 25 : 19));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x3E), 40);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x40), (ushort)(id == 0 ? hp : 40));
            bytes[0x49] = (byte)id;
            Memory(first + offset + (uint)(id * BattleLayout.Stride), bytes);
        }
    }

    [Fact]
    public void One_missing_battle_read_preserves_a_faint_across_the_gap_without_searching_again()
    {
        BattleMemory(40);
        var reader = new BattleTableReader(_client, NullLogger<BattleTableReader>.Instance);
        var tracker = new BattleFaintTracker();
        Assert.Empty(tracker.Observe(reader.Read(_time.GetUtcNow())));
        Assert.True(tracker.InBattle);
        RemoveMemory(0x30002738);
        _time.Advance(1);
        // The monitor's existing exception path skips Observe, the encounter tick and Recording assignment.
        Assert.Throws<AzaharRpcException>(() => reader.Read(_time.GetUtcNow()));
        BattleMemory(0);
        _time.Advance(1);
        var faint = Assert.Single(tracker.Observe(reader.Read(_time.GetUtcNow())));
        Assert.True(faint.IsPlayers);
        Assert.Equal(0, faint.BattleId);
        Assert.Equal(1, _searches);
    }

    [Fact]
    public void Repeated_missing_battle_reads_allow_relocation_within_the_original_small_window()
    {
        BattleMemory(40);
        var reader = new BattleTableReader(_client, NullLogger<BattleTableReader>.Instance);
        Assert.Equal(2, reader.Read(_time.GetUtcNow()).Count);
        lock (_memory) _memory.Clear();
        for (var i = 0; i < 2; i++)
            Assert.Throws<AzaharRpcException>(() => reader.Read(_time.GetUtcNow()));
        Assert.Empty(reader.Read(_time.GetUtcNow()));
        BattleMemory(35, 0x10000);
        _time.Advance(3);
        Assert.Equal(2, reader.Read(_time.GetUtcNow()).Count);
        Assert.Equal(2, _searches);
    }

    [Fact]
    public void A_successful_read_of_a_freed_battle_block_still_ends_the_battle()
    {
        BattleMemory(40);
        var reader = new BattleTableReader(_client, NullLogger<BattleTableReader>.Instance);
        var tracker = new BattleFaintTracker();
        tracker.Observe(reader.Read(_time.GetUtcNow()));
        Memory(0x30002738, new byte[BattleLayout.ReadLength]);
        Assert.Empty(tracker.Observe(reader.Read(_time.GetUtcNow())));
        Assert.False(tracker.InBattle);
    }

    private static byte[] Position(ushort world, ushort map, float x)
    {
        var bytes = new byte[FieldRecord.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, world);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), map);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(4), x);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(8), 20);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(12), 30);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(24), 0.6f);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(28), 0.8f);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), uint.MaxValue);
        return bytes;
    }

    [Fact]
    public void A_stale_saved_position_can_lead_to_live_neighbours_of_a_different_map()
    {
        Save(4, 1);
        var page = new byte[4096];
        Position(4, 1, 12).CopyTo(page, 0x510);
        Position(0, 0, 50).CopyTo(page, 0x4C8);
        Position(0, 0, 60).CopyTo(page, 0x448);
        Memory(0x33F6E000, page);
        Assert.Equal("ruta-1", Zones().CurrentZone()?.LocationId);
        Assert.InRange(_searches, 1, 6);
    }

    private void SaturatedRouteSearch(int falseHits)
    {
        Save();
        var junk = new byte[falseHits * 64];
        for (var i = 0; i < falseHits; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(junk.AsSpan(i * 64 + 32), uint.MaxValue);
        Memory(0x30100000, junk);
        Memory(0x33F60000, Position(0, 0, 60));
        Memory(0x33F60200, Position(0, 0, 70));
    }

    [Fact]
    public void Route_records_beyond_the_255_hit_limit_are_not_silently_lost()
    {
        SaturatedRouteSearch(255);
        Assert.Equal("ruta-1", Zones().CurrentZone()?.LocationId);
        Assert.InRange(_searches, 1, 6);
    }

    [Fact]
    public void A_long_search_resumes_later_instead_of_restarting_or_exceeding_its_budget()
    {
        SaturatedRouteSearch(1300);
        var zones = Zones();
        Assert.Null(zones.CurrentZone());
        Assert.InRange(_searches, 1, 6);
        var searches = _searches;
        _time.Advance(119);
        Assert.Null(zones.CurrentZone());
        Assert.Equal(searches, _searches);
        _time.Advance(1);
        Assert.Equal("ruta-1", zones.CurrentZone()?.LocationId);
        Assert.InRange(_searches - searches, 1, 6);
    }

    [Fact]
    public void A_single_live_neighbour_and_an_old_map_do_not_become_a_guessed_zone()
    {
        Save(4, 1);
        var page = new byte[4096];
        Position(4, 1, 12).CopyTo(page, 0x510);
        Position(0, 0, 50).CopyTo(page, 0x4C8);
        Memory(0x33F6E000, page);
        Assert.Null(Zones().CurrentZone());
    }

    /// <summary>
    /// Ruta 1 on 2026-09-21: walking in from the outskirts, one record followed the player into the new map and the
    /// other kept the map they came from. The one that keeps moving is the player, and only once it has proved it.
    /// </summary>
    [Fact]
    public void The_record_that_keeps_moving_breaks_a_tie_between_two_maps()
    {
        Save(4, 1);
        var page = new byte[4096];
        Position(4, 1, 12).CopyTo(page, 0x510);
        Position(0, 0, 50).CopyTo(page, 0x4C8);
        Memory(0x33F6E000, (byte[])page.Clone());
        var zones = Zones();

        Assert.Null(zones.CurrentZone());

        _time.Advance(1);
        Position(0, 0, 51).CopyTo(page, 0x4C8);
        Memory(0x33F6E000, (byte[])page.Clone());
        Assert.Null(zones.CurrentZone()); // se ha movido una vez: todavía no

        _time.Advance(1);
        Position(0, 0, 52).CopyTo(page, 0x4C8);
        Memory(0x33F6E000, (byte[])page.Clone());
        Assert.Equal("ruta-1", zones.CurrentZone()?.LocationId);

        // Quieto más allá de la ventana, vuelve a no saberse: parado no se decide nada.
        _time.Advance(6);
        Assert.Null(zones.CurrentZone());
    }

    /// <summary>The record of where the player came in changes once, when they come in. Once is not walking.</summary>
    [Fact]
    public void A_record_that_jumped_once_is_not_taken_for_the_player()
    {
        Save(4, 1);
        var page = new byte[4096];
        Position(4, 1, 12).CopyTo(page, 0x510);
        Position(0, 0, 50).CopyTo(page, 0x4C8);
        Memory(0x33F6E000, (byte[])page.Clone());
        var zones = Zones();

        Assert.Null(zones.CurrentZone());

        _time.Advance(1);
        Position(4, 1, 99).CopyTo(page, 0x510);
        Memory(0x33F6E000, (byte[])page.Clone());
        Assert.Null(zones.CurrentZone());

        _time.Advance(1);
        Assert.Null(zones.CurrentZone());
    }

    /// <summary>Two records moving on different maps — a boundary being crossed — still decide nothing.</summary>
    [Fact]
    public void Two_moving_records_on_different_maps_decide_nothing()
    {
        Save(4, 1);
        var page = new byte[4096];
        Position(4, 1, 12).CopyTo(page, 0x510);
        Position(0, 0, 50).CopyTo(page, 0x4C8);
        Memory(0x33F6E000, (byte[])page.Clone());
        var zones = Zones();
        Assert.Null(zones.CurrentZone());

        for (var step = 1; step <= 3; step++)
        {
            _time.Advance(1);
            Position(4, 1, 12 + step).CopyTo(page, 0x510);
            Position(0, 0, 50 + step).CopyTo(page, 0x4C8);
            Memory(0x33F6E000, (byte[])page.Clone());
            Assert.Null(zones.CurrentZone());
        }
    }

    private static readonly int[] Following = [0x3C8, 0x448, 0x4C8];
    private static readonly int[] LeftBehind = [0x510, 0x590];

    private void Records(byte[] page, int[] offsets, ushort world, ushort map, float x)
    {
        foreach (var offset in offsets) Position(world, map, x).CopyTo(page, offset);
        Memory(0x33F6E000, (byte[])page.Clone());
    }

    private void Blank(byte[] page, int[] offsets)
    {
        foreach (var offset in offsets) Array.Clear(page, offset, FieldRecord.Length);
        Memory(0x33F6E000, (byte[])page.Clone());
    }

    /// <summary>
    /// Ciudad Hauoli on 2026-09-21: the bag opened, the records following the player went blank, and the ones holding
    /// the map they had come from were left alone to vote — balls taken on opening the bag, given back on closing it.
    /// </summary>
    [Fact]
    public void Opening_the_bag_does_not_take_the_player_back_to_the_map_they_left()
    {
        Save(4, 1);
        var page = new byte[4096];
        Records(page, LeftBehind, 4, 1, 12);
        Records(page, Following, 0, 0, 50);
        var zones = Zones();
        Assert.Equal("ruta-1", zones.CurrentZone()?.LocationId);

        _time.Advance(1);
        Blank(page, Following);
        Assert.Equal("ruta-1", zones.CurrentZone()?.LocationId);

        _time.Advance(30);   // una bolsa abierta un buen rato, búsqueda incluida
        Assert.Equal("ruta-1", zones.CurrentZone()?.LocationId);

        _time.Advance(1);
        Records(page, Following, 0, 0, 50);
        Assert.Equal("ruta-1", zones.CurrentZone()?.LocationId);
    }

    /// <summary>
    /// The Escuela de Entrenadores the same day: through a door, and then the bag inside. The door is followed at once
    /// because the records that said the street now say the house; the bag is not, because the ones saying the street
    /// then are the ones that were left there.
    /// </summary>
    [Fact]
    public void A_door_is_followed_and_the_bag_behind_it_is_not()
    {
        Save(4, 1);
        var page = new byte[4096];
        Records(page, LeftBehind, 4, 1, 12);
        Records(page, Following, 0, 0, 50);
        var zones = Zones();
        Assert.Equal("ruta-1", zones.CurrentZone()?.LocationId);

        _time.Advance(1);
        Records(page, Following, 4, 1, 80);
        Records(page, LeftBehind, 0, 0, 55);
        Assert.Equal("casa", zones.CurrentZone()?.LocationId);

        _time.Advance(1);
        Blank(page, Following);
        Assert.Equal("casa", zones.CurrentZone()?.LocationId);
    }

    /// <summary>
    /// Records that never said the current place can still take the player away, once one of them walks: that is the
    /// player, even when it was among the ones the reader took for left behind.
    /// </summary>
    [Fact]
    public void A_record_that_walks_into_another_place_takes_the_player_with_it()
    {
        Save(4, 1);
        var page = new byte[4096];
        Records(page, [.. LeftBehind, 0x610], 4, 1, 12);
        Records(page, [0x3C8, 0x448], 0, 0, 50);
        var zones = Zones();
        Assert.Equal("casa", zones.CurrentZone()?.LocationId);

        // Dos de la casa se apagan: mayoría para la ruta, pero de dos que ya decían la ruta y no se mueven.
        _time.Advance(1);
        Blank(page, [0x590, 0x610]);
        Assert.Equal("casa", zones.CurrentZone()?.LocationId);

        _time.Advance(1);
        Records(page, [0x448], 0, 0, 51);
        Assert.Equal("casa", zones.CurrentZone()?.LocationId);   // una vez no es andar

        _time.Advance(1);
        Records(page, [0x448], 0, 0, 52);
        Assert.Equal("ruta-1", zones.CurrentZone()?.LocationId);
    }

    /// <summary>
    /// Ciudad Hauoli to Ruta 2 and the outskirts to the Escuela on 2026-09-22, the same thing twice: across an edge with
    /// no door the record following the player changed map and kept walking, while three copies of the position that
    /// the game refreshes only now and then stayed on the map behind, frozen, and outvoted it. The first battle of the
    /// new route was counted in the old one, already spent, and when it ended the copies caught up and the balls came
    /// back. The one that walks is the player, even against a majority that does not move.
    /// </summary>
    [Fact]
    public void A_record_that_walks_across_an_edge_beats_a_frozen_majority_behind()
    {
        Save(4, 1);
        var page = new byte[4096];
        Records(page, [.. LeftBehind, 0x610], 4, 1, 12);
        Records(page, [0x3C8], 4, 1, 40);
        var zones = Zones();
        Assert.Equal("casa", zones.CurrentZone()?.LocationId);

        // Cruza el borde: el que le sigue cambia de mapa y anda; las tres copias se quedan quietas detrás.
        _time.Advance(1);
        Records(page, [0x3C8], 0, 0, 50);
        Assert.Equal("casa", zones.CurrentZone()?.LocationId);   // una vez no es andar

        _time.Advance(1);
        Records(page, [0x3C8], 0, 0, 51);
        Assert.Equal("ruta-1", zones.CurrentZone()?.LocationId);

        // Y parado, las copias quietas no se lo llevan de vuelta: ni dijeron la ruta ni se han movido.
        _time.Advance(10);
        Assert.Equal("ruta-1", zones.CurrentZone()?.LocationId);

        _time.Advance(40);
        Assert.Equal("ruta-1", zones.CurrentZone()?.LocationId);

        // Hasta que el combate las pone al día: entonces coinciden todas.
        _time.Advance(1);
        Records(page, [.. LeftBehind, 0x610], 0, 0, 51);
        Assert.Equal("ruta-1", zones.CurrentZone()?.LocationId);
    }

    /// <summary>A walking record on the same map as the majority changes nothing.</summary>
    [Fact]
    public void A_record_walking_on_the_majority_map_changes_nothing()
    {
        Save(4, 1);
        var page = new byte[4096];
        Records(page, [.. LeftBehind, 0x610], 4, 1, 12);
        Records(page, [0x3C8], 4, 1, 40);
        var zones = Zones();
        Assert.Equal("casa", zones.CurrentZone()?.LocationId);

        for (var step = 1; step <= 3; step++)
        {
            _time.Advance(1);
            Records(page, [0x3C8], 4, 1, 40 + step);
            Assert.Equal("casa", zones.CurrentZone()?.LocationId);
        }
    }

    /// <summary>
    /// Walking out of a building on 2026-09-21, the records disagreed while the game reloaded the field, the reader
    /// searched 64 MB three times inside those same milliseconds, and Azahar went down. A doubt is searched once it has
    /// lasted; a change of map clears up before that.
    /// </summary>
    [Fact]
    public void A_change_of_map_is_not_searched_while_it_loads()
    {
        Save(4, 1);
        var page = new byte[4096];
        Records(page, LeftBehind, 4, 1, 12);
        var zones = Zones();
        Assert.Equal("casa", zones.CurrentZone()?.LocationId);
        var searches = _searches;

        // Mucho después de la última búsqueda, uno de los dos cambia de mapa: uno contra uno, no se sabe.
        _time.Advance(30);
        Records(page, [0x590], 0, 0, 50);
        Assert.Null(zones.CurrentZone());
        Assert.Equal(searches, _searches);

        _time.Advance(4);
        Assert.Null(zones.CurrentZone());
        Assert.Equal(searches, _searches);

        // Si la duda dura, ya no es una carga: entonces sí se busca.
        _time.Advance(1);
        zones.CurrentZone();
        Assert.True(_searches > searches);
    }

    /// <summary>And a change of map that sorts itself out in time is never searched at all.</summary>
    [Fact]
    public void A_change_of_map_that_settles_is_never_searched()
    {
        Save(4, 1);
        var page = new byte[4096];
        Records(page, LeftBehind, 4, 1, 12);
        var zones = Zones();
        Assert.Equal("casa", zones.CurrentZone()?.LocationId);
        var searches = _searches;

        _time.Advance(30);
        Records(page, [0x590], 0, 0, 50);
        Assert.Null(zones.CurrentZone());

        _time.Advance(2);
        Records(page, [0x510], 0, 0, 51);
        Assert.Equal("ruta-1", zones.CurrentZone()?.LocationId);

        _time.Advance(10);
        Assert.Equal("ruta-1", zones.CurrentZone()?.LocationId);
        Assert.Equal(searches, _searches);
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(int seconds) => _now += TimeSpan.FromSeconds(seconds);
    }
}
