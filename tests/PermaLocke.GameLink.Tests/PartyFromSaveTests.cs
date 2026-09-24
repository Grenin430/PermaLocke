using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// Finding the party from the saved party's encryption constants, with real Pokémon in a fake emulator's memory.
/// </summary>
/// <remarks>
/// Found getting the friends' folder ready on 2026-09-21: the sweep only confirms a party by a second Pokémon one stride
/// further, so on a fresh install — no address from last time — a player with only their starter was never found, and
/// with no party nothing else ran. This finds it from the save, with a handful of searches instead of a sweep.
/// </remarks>
public sealed class PartyFromSaveTests : IAsyncLifetime
{
    private readonly UdpClient _server = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<uint, byte[]> _memory = [];
    private readonly List<(uint Address, uint Size)> _searched = [];
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "PermaLockeFromSave-" + Guid.NewGuid().ToString("N"));
    private Task _serving = Task.CompletedTask;
    private uint _selected = uint.MaxValue;

    private const uint Base = 0x33F807C4;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_folder);
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
        _server.Dispose();
        _stop.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private byte[] Answer(byte[] request)
    {
        var type = (RpcRequestType)BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(8));
        var address = BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(16));
        byte[] data;

        switch (type)
        {
            case RpcRequestType.ProcessList:
                data = new byte[address == 0 ? 24 : 4];
                if (address == 0)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(data, 1);
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 42);
                    BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(8), AzaharGameStateProvider.UltraMoonTitleId);
                }
                break;
            case RpcRequestType.SetGetProcess:
                if (address == 1) _selected = BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(20));
                data = BitConverter.GetBytes(_selected);
                break;
            case RpcRequestType.ReadMemory:
            {
                var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(20));
                data = new byte[length];
                foreach (var (start, bytes) in _memory)
                {
                    for (var i = 0; i < length; i++)
                    {
                        var at = (long)address + i - start;
                        if (at >= 0 && at < bytes.Length) data[i] = bytes[at];
                    }
                }
                break;
            }
            case RpcRequestType.SearchMemory:
            {
                var size = BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(20));
                var stride = (int)BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(24));
                var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(28));
                var pattern = request.AsSpan(32, length).ToArray();
                var mask = request.AsSpan(32 + length, length).ToArray();
                var hits = new List<uint>();

                // El sondeo de SupportsSearch: un byte comodín en el código, que el fork siempre encuentra.
                if (mask.All(b => b == 0))
                {
                    hits.Add(address);
                }
                else
                {
                    _searched.Add((address, size));
                    foreach (var (start, bytes) in _memory)
                    {
                        for (var offset = 0; offset + length <= bytes.Length; offset += stride)
                        {
                            var candidate = start + (uint)offset;
                            if (candidate < address || candidate + (uint)length > address + size) continue;
                            var match = true;
                            for (var i = 0; i < length && match; i++)
                                match = (bytes[offset + i] & mask[i]) == (pattern[i] & mask[i]);
                            if (match) hits.Add(candidate);
                        }
                    }
                }

                data = new byte[4 + hits.Count * 4];
                BinaryPrimitives.WriteUInt32LittleEndian(data, (uint)hits.Count);
                for (var i = 0; i < hits.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4 + 4 * i), hits[i]);
                break;
            }
            default:
                throw new InvalidOperationException($"Unexpected RPC: {type}");
        }

        var reply = new byte[16 + data.Length];
        request.AsSpan(0, 12).CopyTo(reply);
        BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(12), (uint)data.Length);
        data.CopyTo(reply, 16);
        return reply;
    }

    private AzaharRpcClient Client() => new("127.0.0.1", ((IPEndPoint)_server.Client.LocalEndPoint!).Port, 500);

    /// <summary>A party Pokémon whose stats are coherent: the level in the tail is the level the experience gives.</summary>
    private static PK7 Pokemon(uint key, ushort species, int level)
    {
        var pokemon = new PK7
        {
            Species = species,
            EncryptionConstant = key,
            PID = key ^ 0x5A5A5A5A,
            OriginalTrainerName = "Grenin430",
            CurrentLevel = (byte)level
        };

        pokemon.Stat_Level = (byte)level;
        pokemon.Stat_HPMax = 24;
        pokemon.Stat_HPCurrent = 24;
        pokemon.Stat_ATK = 13;
        pokemon.Stat_DEF = 12;
        pokemon.Stat_SPE = 11;
        pokemon.Stat_SPA = 14;
        pokemon.Stat_SPD = 12;
        pokemon.RefreshChecksum();
        return pokemon;
    }

    private static byte[] Encrypted(PK7 pokemon)
    {
        var bytes = new byte[pokemon.SIZE_PARTY];
        pokemon.WriteEncryptedDataParty(bytes);
        return bytes;
    }

    /// <summary>The structure the game reads: 0x1E4 per entry, the stats at 0x158.</summary>
    private void Authoritative(uint address, params PK7[] party)
    {
        var bytes = new byte[PartyLayoutLocator.AuthoritativeStride * 6];
        for (var slot = 0; slot < party.Length; slot++)
        {
            var encrypted = Encrypted(party[slot]);
            var at = (int)(slot * PartyLayoutLocator.AuthoritativeStride);
            encrypted.AsSpan(0, 0xE8).CopyTo(bytes.AsSpan(at));
            encrypted.AsSpan(0xE8).CopyTo(bytes.AsSpan(at + (int)PartyLayoutLocator.AuthoritativeStatsOffset));
        }
        lock (_memory) _memory[address] = bytes;
    }

    /// <summary>A copy: 0x104 per entry, the stats right after the stored block.</summary>
    private void Copy(uint address, params PK7[] party)
    {
        var bytes = new byte[PartyLayoutLocator.CopyStride * 6];
        for (var slot = 0; slot < party.Length; slot++)
            Encrypted(party[slot]).CopyTo(bytes.AsSpan((int)(slot * PartyLayoutLocator.CopyStride)));
        lock (_memory) _memory[address] = bytes;
    }

    [Fact]
    public void A_lone_starter_is_found_in_the_structure_the_game_reads()
    {
        Authoritative(Base, Pokemon(0xA1B2C3D4, 722, 6));
        using var client = Client();
        client.AttachTo(AzaharGameStateProvider.UltraMoonTitleId);

        var found = new PartyLayoutLocator(client).LocateByKeys([0xA1B2C3D4]);

        Assert.Contains(found, layout => layout.Address == Base && layout.Stride == PartyLayoutLocator.AuthoritativeStride);

        // La misma dirección leída como copia toma la cola de otra cosa: no se acepta.
        Assert.DoesNotContain(found, layout => layout.Stride == PartyLayoutLocator.CopyStride);
    }

    [Fact]
    public void A_hit_on_the_third_slot_walks_back_to_the_first()
    {
        Copy(0x3301_28E4, Pokemon(0x11111111, 722, 6), Pokemon(0x22222222, 10, 3), Pokemon(0x33333333, 16, 4));
        using var client = Client();
        client.AttachTo(AzaharGameStateProvider.UltraMoonTitleId);

        var found = new PartyLayoutLocator(client).LocateByKeys([0x33333333]);

        Assert.Contains(found, layout => layout.Address == 0x3301_28E4 && layout.Stride == PartyLayoutLocator.CopyStride);
    }

    /// <summary>
    /// The emulator went down on 2026-09-21 while this searched the application heap at 0x08000000 during the game's
    /// intro, where that memory is not mapped yet: 28,871 unmapped reads, and a crash. Nothing has ever lived there.
    /// </summary>
    [Fact]
    public void The_search_stays_in_the_linear_heap()
    {
        Authoritative(Base, Pokemon(0xA1B2C3D4, 722, 6));
        using var client = Client();
        client.AttachTo(AzaharGameStateProvider.UltraMoonTitleId);

        new PartyLayoutLocator(client).LocateByKeys([0xA1B2C3D4, 0x0BADF00D]);

        lock (_memory)
        {
            Assert.NotEmpty(_searched);
            Assert.All(_searched, search => Assert.True(search.Address >= 0x30000000 && search.Address + search.Size <= 0x40000000,
                $"se buscó en 0x{search.Address:X8}+0x{search.Size:X}"));
        }
    }

    [Fact]
    public void Keys_that_are_not_in_memory_find_nothing()
    {
        Authoritative(Base, Pokemon(0xA1B2C3D4, 722, 6));
        using var client = Client();
        client.AttachTo(AzaharGameStateProvider.UltraMoonTitleId);

        Assert.Empty(new PartyLayoutLocator(client).LocateByKeys([0x0BADF00D]));
    }

    private sealed class NoSweep : IPartyLayoutLocator
    {
        private readonly PartyLayoutLocator _real;
        public NoSweep(AzaharRpcClient client) => _real = new PartyLayoutLocator(client);
        public int Sweeps { get; private set; }

        public IReadOnlyList<PartyLayout> LocateAll(string? trainer, CancellationToken ct = default)
        {
            Sweeps++;
            return [];
        }

        public IReadOnlyList<PartyLayout> LocateByKeys(IReadOnlyCollection<uint> keys, CancellationToken ct = default) =>
            _real.LocateByKeys(keys, ct);
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(int seconds) => _now += TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// A fresh install — no addresses from last time — with only the starter: connected from the save, and not one
    /// sweep. Before this it stayed «Carga tu partida» until a second Pokémon was caught.
    /// </summary>
    [Fact]
    public async Task A_fresh_install_with_only_its_starter_connects_without_sweeping()
    {
        Authoritative(Base, Pokemon(0xA1B2C3D4, 722, 6));
        using var client = Client();
        var clock = new ManualTime();
        var locator = new NoSweep(client);
        var provider = new AzaharGameStateProvider(client, new PkhexSpeciesLookup("es"), new NoLocations(),
            Path.Combine(_folder, "equipo.txt"), NullLogger<AzaharGameStateProvider>.Instance, locator, clock,
            () => [0xA1B2C3D4]);

        GameSnapshot? last = null;
        for (var second = 0; second < 25 && last?.Connected != true; second++)
        {
            last = await provider.ReadAsync();
            clock.Advance(1);
        }

        Assert.True(last?.Connected);
        Assert.Single(last!.Party);
        Assert.Equal(722, last.Party[0].Species);
        Assert.Equal(0, locator.Sweeps);
    }

    private sealed class NoLocations : ILocationLookup
    {
        public string GetName(int locationId) => string.Empty;
    }
}
