using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.GameLink.Battle;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink.Tests;

/// <summary>Everything that sets <see cref="WorldLimits"/>, which is global: one at a time.</summary>
[CollectionDefinition("WorldLimits", DisableParallelization = true)]
public sealed class WorldLimitsCollection;

/// <summary>
/// A fallen Pokémon on the bench of a battle, put down to zero in the battle itself, against a fake emulator holding a real,
/// encrypted Pokémon. (Until 1.0.9 this file also tested the level cap written into memory, which the patched game replaced.)
/// </summary>
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

    private const uint BlockHeader = 0x30010000;
    private const uint BlockPointer = 0x30020000;

    /// <summary>A battle block for the Froakie at <paramref name="position"/> with <paramref name="hp"/>, and its party structure.</summary>
    private BattleBlock Battle(int position, int hp)
    {
        var block = new byte[BattleLayout.HeaderSize + BattleLayout.BlockSize];
        BattleLayout.SearchPattern.CopyTo(block);
        var data = block.AsSpan(BattleLayout.HeaderSize);
        BinaryPrimitives.WriteUInt32LittleEndian(data[0x20..], BlockPointer);
        BinaryPrimitives.WriteUInt16LittleEndian(data[0x2C..], Froakie);
        BinaryPrimitives.WriteUInt16LittleEndian(data[0x2E..], (ushort)StatsAt(15)[0]);
        BinaryPrimitives.WriteUInt16LittleEndian(data[BattleLayout.CurrentHpOffset..], (ushort)hp);
        data[0x39] = (byte)position;

        var party = new byte[BattlePokemon.ReadLength];
        Encrypted(Pokemon(15)).AsSpan(0, BattlePokemon.StoredSize).CopyTo(party.AsSpan(BattlePokemon.Offset));

        lock (_memory)
        {
            _memory[BlockHeader] = block;
            _memory[BlockPointer] = party;
        }

        return BattleLayout.Parse(BlockHeader, block)!;
    }

    private int BattleHp()
    {
        lock (_memory) return BinaryPrimitives.ReadUInt16LittleEndian(_memory[BlockHeader].AsSpan(BattleLayout.HeaderSize + BattleLayout.CurrentHpOffset));
    }

    /// <summary>A fallen Pokémon on the bench goes to zero in the battle, read back (1.0.4.10).</summary>
    [Fact]
    public void A_fallen_one_on_the_bench_is_knocked_down_in_the_battle()
    {
        var block = Battle(position: 3, hp: 30);
        using var client = Client();
        var reader = new BattleTableReader(client, NullLogger<BattleTableReader>.Instance);

        Assert.True(reader.KnockDownOnBench(block, Pid));
        Assert.Equal(0, BattleHp());
    }

    /// <summary>On the field, with another Pokémon behind the block, or already at zero: nothing is written.</summary>
    [Fact]
    public void The_field_and_a_stranger_are_never_touched()
    {
        using var client = Client();
        var reader = new BattleTableReader(client, NullLogger<BattleTableReader>.Instance);

        Assert.False(reader.KnockDownOnBench(Battle(position: 0, hp: 30), Pid));
        Assert.Equal(30, BattleHp());
        Assert.False(reader.KnockDownOnBench(Battle(position: 1, hp: 30), Pid));
        Assert.Equal(30, BattleHp());
        Assert.False(reader.KnockDownOnBench(Battle(position: 4, hp: 30), 0xDEADBEEF));
        Assert.Equal(30, BattleHp());
    }
}
