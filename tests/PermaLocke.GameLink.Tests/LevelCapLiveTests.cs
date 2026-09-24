using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink.Tests;

/// <summary>Everything that sets <see cref="WorldLimits"/>, which is global: one at a time.</summary>
[CollectionDefinition("WorldLimits", DisableParallelization = true)]
public sealed class WorldLimitsCollection;

/// <summary>
/// The level cap brings down the level the game SHOWS, with the stats that go with it, in the structure it reads.
/// </summary>
/// <remarks>
/// Found on 2026-09-21: the log said «corregido y releído» and the party menu still said 15 with a cap of 14. The cap
/// predated §99 and only lowered the experience; the level the menu shows lives in the 28 bytes at <c>0x158</c>. These
/// run the real writer against a fake emulator holding a real, encrypted Pokémon.
/// </remarks>
[Collection("WorldLimits")]
public sealed class LevelCapLiveTests : IAsyncLifetime
{
    private readonly UdpClient _server = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<uint, byte[]> _memory = [];
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "PermaLockeCap-" + Guid.NewGuid().ToString("N"));
    private Task _serving = Task.CompletedTask;

    private const uint Base = 0x33F807C4;
    private const uint Pid = 0x74F7EF00;
    private const ushort Froakie = 656;

    /// <summary>Froakie's series base stats, in the summary screen's order.</summary>
    private static readonly byte[] FroakieBases = [41, 56, 40, 62, 44, 71];

    private static readonly int[] Ivs = [31, 20, 15, 10, 5, 0];

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_folder);

        var table = new byte[(Froakie + 1) * 6];
        FroakieBases.CopyTo(table, Froakie * 6);
        WorldLimits.BaseStats = table;
        WorldLimits.FormBaseStats = new Dictionary<(int Species, int Form), byte[]>();

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
        WorldLimits.BaseStats = [];
        WorldLimits.FormBaseStats = new Dictionary<(int Species, int Form), byte[]>();
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
        var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(20));
        byte[] data;

        switch (type)
        {
            case RpcRequestType.ReadMemory:
                data = new byte[length];
                foreach (var (start, bytes) in _memory)
                    for (var i = 0; i < length; i++)
                        if ((long)address + i - start is var at && at >= 0 && at < bytes.Length) data[i] = bytes[at];
                break;
            case RpcRequestType.WriteMemory:
                foreach (var (start, bytes) in _memory)
                    for (var i = 0; i < length; i++)
                        if ((long)address + i - start is var at && at >= 0 && at < bytes.Length) bytes[at] = request[24 + i];
                data = [];
                break;
            default:
                throw new InvalidOperationException($"Unexpected RPC: {type}");
        }

        var reply = new byte[16 + data.Length];
        request.AsSpan(0, 12).CopyTo(reply);
        BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(12), (uint)data.Length);
        data.CopyTo(reply, 16);
        return reply;
    }

    private AzaharGameWriter Writer(AzaharRpcClient client) =>
        new(client, Path.Combine(_folder, "backup"), NullLogger<AzaharGameWriter>.Instance);

    private AzaharRpcClient Client() => new("127.0.0.1", ((IPEndPoint)_server.Client.LocalEndPoint!).Port, 500);

    private static int[] StatsAt(int level) =>
        StatCalculator.Compute(FroakieBases, Ivs, [0, 0, 0, 0, 0, 0], level, nature: 0);

    /// <summary>A party Froakie as the game keeps it: level and stats in step, as the Froakie of the test folder was.</summary>
    private static PK7 Pokemon(int level, int damage = 5, bool dead = false)
    {
        var pokemon = new PK7
        {
            Species = Froakie,
            PID = Pid,
            EncryptionConstant = 0x1234ABCD,
            OriginalTrainerName = "AAA",
            Nature = 0,
            CurrentLevel = (byte)level,
            IV_HP = Ivs[0], IV_ATK = Ivs[1], IV_DEF = Ivs[2], IV_SPA = Ivs[3], IV_SPD = Ivs[4], IV_SPE = Ivs[5]
        };

        var stats = StatsAt(level);
        pokemon.Stat_Level = (byte)level;
        pokemon.Stat_HPMax = stats[0];
        pokemon.Stat_ATK = stats[1];
        pokemon.Stat_DEF = stats[2];
        pokemon.Stat_SPA = stats[3];
        pokemon.Stat_SPD = stats[4];
        pokemon.Stat_SPE = stats[5];
        pokemon.Stat_HPCurrent = dead ? 0 : stats[0] - damage;
        pokemon.RefreshChecksum();
        return pokemon;
    }

    private static byte[] Encrypted(PK7 pokemon)
    {
        var bytes = new byte[pokemon.SIZE_PARTY];
        pokemon.WriteEncryptedDataParty(bytes);
        return bytes;
    }

    /// <summary>The structure the game reads: the stored block, then its stats 0x158 bytes in.</summary>
    private void Authoritative(PK7 pokemon)
    {
        var bytes = new byte[PartyLayoutLocator.AuthoritativeStride];
        var encrypted = Encrypted(pokemon);
        encrypted.AsSpan(0, 0xE8).CopyTo(bytes);
        encrypted.AsSpan(0xE8).CopyTo(bytes.AsSpan((int)PartyLayoutLocator.AuthoritativeStatsOffset));
        lock (_memory) _memory[Base] = bytes;
    }

    [Fact]
    public void The_level_the_game_shows_comes_down_with_the_stats_of_that_level()
    {
        Authoritative(Pokemon(15));
        using var client = Client();
        var writer = Writer(client);

        var result = writer.EnforceLevelCap(Base, cap: 14, Pid);

        Assert.True(result.Applied);
        var after = writer.ReadAuthoritative(Base)!;
        Assert.Equal(14, after.Stat_Level);
        Assert.Equal(14, GameLevels.Of(after));

        var expected = StatsAt(14);
        int[] written = [after.Stat_HPMax, after.Stat_ATK, after.Stat_DEF, after.Stat_SPA, after.Stat_SPD, after.Stat_SPE];
        Assert.Equal(expected, written);

        // Los PS actuales bajan lo mismo que el máximo: el daño que llevaba lo sigue llevando.
        Assert.Equal(expected[0] - 5, after.Stat_HPCurrent);
        Assert.True(PartyStats.AreHere(after));
    }

    /// <summary>Twenty rare candies and a cap must not leave a level 14 with level 34 stats.</summary>
    [Fact]
    public void Candies_past_the_cap_do_not_keep_their_stats()
    {
        Authoritative(Pokemon(34));
        using var client = Client();
        var writer = Writer(client);

        writer.EnforceLevelCap(Base, cap: 14, Pid);

        var after = writer.ReadAuthoritative(Base)!;
        Assert.Equal(14, after.Stat_Level);
        Assert.Equal(StatsAt(14)[1], after.Stat_ATK);
    }

    /// <summary>Zero PS is a death in this project (§98): the cap must not revive anybody.</summary>
    [Fact]
    public void A_fallen_pokemon_stays_at_zero()
    {
        Authoritative(Pokemon(15, dead: true));
        using var client = Client();
        var writer = Writer(client);

        writer.EnforceLevelCap(Base, cap: 14, Pid);

        var after = writer.ReadAuthoritative(Base)!;
        Assert.Equal(14, after.Stat_Level);
        Assert.Equal(0, after.Stat_HPCurrent);
    }

    /// <summary>Somebody else in the slot is left alone, stats tail included.</summary>
    [Fact]
    public void Another_pokemon_in_the_slot_is_not_touched()
    {
        Authoritative(Pokemon(15));
        byte[] before;
        lock (_memory) before = (byte[])_memory[Base].Clone();
        using var client = Client();

        var result = Writer(client).EnforceLevelCap(Base, cap: 14, expectedPid: 0xDEADBEEF);

        Assert.False(result.Applied);
        lock (_memory) Assert.Equal(before, _memory[Base]);
    }

    /// <summary>Without the installed world's base stats there is no honest number: the level comes down, the stats stay.</summary>
    [Fact]
    public void Without_the_world_table_only_the_level_comes_down()
    {
        Authoritative(Pokemon(15));
        WorldLimits.BaseStats = [];
        using var client = Client();
        var writer = Writer(client);

        writer.EnforceLevelCap(Base, cap: 14, Pid);

        var after = writer.ReadAuthoritative(Base)!;
        Assert.Equal(14, after.Stat_Level);
        Assert.Equal(StatsAt(15)[0], after.Stat_HPMax);
    }
}
