using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PermaLocke.GameLink.Rpc;

public enum RpcRequestType
{
    ReadMemory = 1,
    WriteMemory = 2,
    ProcessList = 3,
    SetGetProcess = 4,

    /// <summary>Only in PermaLocke's Azahar fork. The official build rejects it.</summary>
    SearchMemory = 5
}

/// <param name="ProcessId">Emulated process handle, as Azahar numbers them.</param>
/// <param name="TitleId">3DS title id, e.g. 0x00040000001B5100 for Ultra Moon (EUR).</param>
public sealed record EmulatedProcess(uint ProcessId, ulong TitleId, string Name);

/// <param name="NoReply">
/// True when nobody answered at all, which almost always means the emulator is not running.
/// </param>
/// <remarks>
/// The distinction exists because the two cases read very differently to a player: an emulator
/// that is closed is a thing they can fix, and the socket's own words for it — "se ha forzado la
/// interrupción de una conexión existente" — are not the way to tell them. A protocol answer that
/// makes no sense is worth showing as it is; silence is not.
/// </remarks>
public sealed class AzaharRpcException(string message, bool noReply = false) : Exception(message)
{
    public bool NoReply { get; } = noReply;
}

/// <summary>
/// Client for the RPC server Azahar inherits from Citra. It listens on UDP 127.0.0.1:45987
/// and exposes memory reads and writes against the emulated 3DS address space, which is
/// exactly what PermaLocke needs and the reason no fork of the emulator is required.
/// </summary>
/// <remarks>
/// Protocol taken from the reference client shipped with the emulator
/// (<c>Azahar/scripting/citra.py</c>, GPLv2+): a 16 byte header of four little-endian
/// uint32 values — version, request id, request type, payload size — followed by the payload.
/// Only the wire format is used here; no code from that file is reproduced.
/// </remarks>
public sealed class AzaharRpcClient : IDisposable
{
    public const int DefaultPort = 45987;

    /// <summary>The server rejects payloads larger than this, so reads and writes are chunked.</summary>
    private const int MaxPayloadSize = 1024;

    private const int HeaderSize = 16;
    private const uint ProtocolVersion = 1;

    /// <summary>
    /// How many times a request is repeated before giving up.
    /// </summary>
    /// <remarks>
    /// UDP loses datagrams, and an emulator in the middle of a battle answers late. One lost
    /// packet used to be reported to the player as "Azahar no responde".
    /// </remarks>
    private const int Attempts = 3;

    private readonly UdpClient _socket;
    private readonly IPEndPoint _endpoint;
    private readonly Random _requestIds = new();

    /// <summary>One request at a time: the socket is shared by every screen and the poller.</summary>
    private readonly Lock _gate = new();

    /// <summary>Requests that had to be sent again. Health of the link, for diagnosis.</summary>
    public int Retries { get; private set; }

    /// <summary>Late replies thrown away. Each one is a desynchronisation that did not happen.</summary>
    public int Discarded { get; private set; }

    public AzaharRpcClient(string host = "127.0.0.1", int port = DefaultPort, int timeoutMilliseconds = 1500)
    {
        _endpoint = new IPEndPoint(IPAddress.Parse(host), port);
        _socket = new UdpClient();
        _socket.Client.ReceiveTimeout = timeoutMilliseconds;
        _socket.Client.SendTimeout = timeoutMilliseconds;
    }

    /// <summary>
    /// True when something answers on the RPC port. A false result means the emulator is not
    /// running or was built without the scripting server.
    /// </summary>
    public bool TryPing(out string message)
    {
        try
        {
            var processes = ListProcesses();
            message = processes.Count == 0
                ? "El servidor RPC responde, pero no hay ningún proceso emulado (¿juego sin cargar?)."
                : $"El servidor RPC responde. Procesos emulados: {processes.Count}.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Sin respuesta en {_endpoint}: {ex.Message}";
            return false;
        }
    }

    public IReadOnlyList<EmulatedProcess> ListProcesses()
    {
        var processes = new List<EmulatedProcess>();
        uint alreadyRead = 0;

        while (true)
        {
            var payload = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(payload, alreadyRead);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), 0x7FFFFFFF);

            var reply = Send(RpcRequestType.ProcessList, payload);
            if (reply.Length < 4)
            {
                break;
            }

            var count = BinaryPrimitives.ReadUInt32LittleEndian(reply);
            if (count == 0)
            {
                break;
            }

            var entries = reply.AsSpan(4);
            for (var i = 0; i < count; i++)
            {
                // Each entry is 0x14 bytes: uint32 process id, uint64 title id, 8 byte name.
                var entry = entries.Slice(i * 0x14, 0x14);
                processes.Add(new EmulatedProcess(
                    BinaryPrimitives.ReadUInt32LittleEndian(entry),
                    BinaryPrimitives.ReadUInt64LittleEndian(entry[4..]),
                    Encoding.ASCII.GetString(entry[12..20]).TrimEnd('\0')));
            }

            alreadyRead += count;
        }

        return processes;
    }

    public uint GetProcess()
    {
        var payload = new byte[8];
        var reply = Send(RpcRequestType.SetGetProcess, payload);
        return BinaryPrimitives.ReadUInt32LittleEndian(reply);
    }

    /// <summary>
    /// Selects the emulated process to read from, and fails loudly if the title is not loaded.
    /// </summary>
    /// <remarks>
    /// Skipping this is a silent trap: the server answers reads anyway, logging
    /// "No target process selected, memory access may be invalid", and returns bytes that are
    /// not reliably the game's. Always attach before reading.
    /// </remarks>
    public EmulatedProcess AttachTo(ulong titleId)
    {
        var processes = ListProcesses();

        var target = processes.FirstOrDefault(p => p.TitleId == titleId)
                     ?? throw new AzaharRpcException(
                         $"El título {titleId:X16} no está cargado. Procesos: "
                         + string.Join(", ", processes.Select(p => $"{p.Name} ({p.TitleId:X16})")));

        SetProcess(target.ProcessId);

        var active = GetProcess();
        if (active != target.ProcessId)
        {
            throw new AzaharRpcException(
                $"No se pudo fijar el proceso {target.ProcessId}; el activo sigue siendo {active}.");
        }

        return target;
    }

    public void SetProcess(uint processId)
    {
        var payload = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), processId);
        Send(RpcRequestType.SetGetProcess, payload);
    }

    /// <param name="address">3DS virtual address. No host translation is needed.</param>
    public byte[] ReadMemory(uint address, int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        var result = new byte[size];
        var written = 0;

        while (written < size)
        {
            var chunk = Math.Min(size - written, MaxPayloadSize);

            var payload = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(payload, (uint)(address + written));
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), (uint)chunk);

            var reply = Send(RpcRequestType.ReadMemory, payload);
            if (reply.Length == 0)
            {
                throw new AzaharRpcException(
                    $"Lectura vacía en 0x{address + written:X8}: dirección no mapeada en el proceso actual.");
            }

            reply.CopyTo(result.AsSpan(written));
            written += reply.Length;
        }

        return result;
    }

    /// <summary>
    /// Read that reports failure instead of throwing. Scanning walks over unmapped pages all
    /// the time, and an exception per page would make it unusable.
    /// </summary>
    public bool TryReadMemory(uint address, int size, out byte[] data)
    {
        try
        {
            data = ReadMemory(address, size);
            return true;
        }
        catch (Exception)
        {
            data = [];
            return false;
        }
    }

    /// <summary>Shorter timeout while scanning: unmapped pages must fail fast, not stall.</summary>
    public int TimeoutMilliseconds
    {
        get => _socket.Client.ReceiveTimeout;
        set
        {
            _socket.Client.ReceiveTimeout = value;
            _socket.Client.SendTimeout = value;
        }
    }

    public void WriteMemory(uint address, ReadOnlySpan<byte> contents)
    {
        var written = 0;

        while (written < contents.Length)
        {
            // Eight bytes of the payload are taken by the address and length fields.
            var chunk = Math.Min(contents.Length - written, MaxPayloadSize - 8);

            var payload = new byte[8 + chunk];
            BinaryPrimitives.WriteUInt32LittleEndian(payload, (uint)(address + written));
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), (uint)chunk);
            contents.Slice(written, chunk).CopyTo(payload.AsSpan(8));

            Send(RpcRequestType.WriteMemory, payload);
            written += chunk;
        }
    }


    /// <summary>Longest pattern the fork accepts.</summary>
    public const int MaxSearchPattern = 64;

    /// <summary>
    /// Asks the emulator to search its own memory, which it does at memory speed instead of the
    /// thousands of round trips a scan from here costs.
    /// <para>
    /// Only PermaLocke's fork of Azahar answers this. The official build rejects the request
    /// type and replies empty, which callers should read as "not supported" and fall back to
    /// scanning by hand.
    /// </para>
    /// </summary>
    /// <param name="mask">One byte per pattern byte: 0xFF must match, 0x00 is a wildcard.</param>
    /// <param name="stride">How often to test, in bytes. Four for aligned values.</param>
    /// <returns>
    /// The addresses found, <b>truncated to the 255 that fit in one reply</b>. There is no
    /// paging here, so a loose pattern silently returns only the first matches of the region:
    /// use this for patterns specific enough to yield few hits, and never to prove that
    /// something is absent.
    /// </returns>
    public IReadOnlyList<uint> SearchMemory(uint address, uint regionSize, ReadOnlySpan<byte> pattern,
        ReadOnlySpan<byte> mask, int stride = 4)
    {
        if (pattern.Length == 0 || pattern.Length > MaxSearchPattern || mask.Length != pattern.Length)
        {
            throw new ArgumentException(
                $"El patrón debe medir entre 1 y {MaxSearchPattern} bytes, y la máscara lo mismo.",
                nameof(pattern));
        }

        var payload = new byte[(sizeof(uint) * 4) + (pattern.Length * 2)];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, address);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), regionSize);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), (uint)stride);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12), (uint)pattern.Length);
        pattern.CopyTo(payload.AsSpan(16));
        mask.CopyTo(payload.AsSpan(16 + pattern.Length));

        var reply = Send(RpcRequestType.SearchMemory, payload);
        if (reply.Length < sizeof(uint))
        {
            return []; // el Azahar oficial contesta vacío: no lo soporta
        }

        var count = (int)BinaryPrimitives.ReadUInt32LittleEndian(reply);
        var hits = new uint[Math.Min(count, (reply.Length - sizeof(uint)) / sizeof(uint))];
        for (var i = 0; i < hits.Length; i++)
        {
            hits[i] = BinaryPrimitives.ReadUInt32LittleEndian(reply.AsSpan(4 + (i * 4)));
        }
        return hits;
    }

    /// <summary>True when the emulator answers the search request, i.e. it is the fork.</summary>
    public bool SupportsSearch()
    {
        try
        {
            return SearchMemory(0x00100000, 0x1000, [0x00], [0x00]).Count > 0;
        }
        catch (AzaharRpcException)
        {
            return false;
        }
    }
    public void Dispose() => _socket.Dispose();

    /// <summary>
    /// Sends one request and waits for the reply that belongs to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two things here are not decoration, and their absence is what made the link die mid-session
    /// and never come back.
    /// </para>
    /// <para>
    /// <b>One conversation at a time.</b> The client is a singleton over a single socket, and the
    /// poller asks every second while the shop, the bag tools and the viewer ask from their own
    /// threads. Two overlapping requests and each one reads the other's answer: both fail, and the
    /// datagram each needed has already been consumed.
    /// </para>
    /// <para>
    /// <b>A late answer is discarded, not mistaken for this one.</b> UDP keeps whatever arrives
    /// after a timeout. Reading it as the current reply leaves the socket permanently one behind:
    /// every later call sees somebody else's id and throws, so a single hiccup while the emulator
    /// was busy turned into "Azahar no responde" until the application was restarted. Now the
    /// backlog is drained until the id matches.
    /// </para>
    /// </remarks>
    private byte[] Send(RpcRequestType type, byte[] payload)
    {
        var requestId = (uint)_requestIds.Next(int.MinValue, int.MaxValue);

        var request = new byte[HeaderSize + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(request, ProtocolVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), requestId);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(8), (uint)type);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(12), (uint)payload.Length);
        payload.CopyTo(request.AsSpan(HeaderSize));

        lock (_gate)
        {
            Exception? last = null;

            for (var attempt = 1; attempt <= Attempts; attempt++)
            {
                try
                {
                    _socket.Send(request, request.Length, _endpoint);
                    return Receive(requestId, type);
                }
                catch (SocketException ex)
                {
                    // Se perdió el datagrama, o el emulador estaba ocupado. Se vuelve a pedir con
                    // el MISMO id, así que si la respuesta anterior llega tarde todavía sirve.
                    last = ex;
                    Retries++;
                }
            }

            throw new AzaharRpcException(
                $"Sin respuesta de {_endpoint} tras {Attempts} intentos: {last?.Message}", noReply: true);
        }
    }

    /// <summary>Waits for the reply with this id, throwing away anything older that is queued.</summary>
    private byte[] Receive(uint requestId, RpcRequestType type)
    {
        while (true)
        {
            IPEndPoint? from = null;
            var reply = _socket.Receive(ref from);

            if (reply.Length < HeaderSize)
            {
                throw new AzaharRpcException($"Respuesta truncada ({reply.Length} bytes).");
            }

            var replyVersion = BinaryPrimitives.ReadUInt32LittleEndian(reply);
            var replyId = BinaryPrimitives.ReadUInt32LittleEndian(reply.AsSpan(4));
            var replyType = BinaryPrimitives.ReadUInt32LittleEndian(reply.AsSpan(8));
            var replySize = BinaryPrimitives.ReadUInt32LittleEndian(reply.AsSpan(12));

            if (replyId != requestId)
            {
                // La respuesta a una petición anterior que llegó tarde. Se tira y se sigue
                // esperando: leerla como si fuera esta es lo que desincronizaba el socket.
                Discarded++;
                continue;
            }

            if (replyVersion != ProtocolVersion || replyType != (uint)type)
            {
                throw new AzaharRpcException(
                    $"Respuesta inesperada: versión {replyVersion}, id {replyId}, tipo {replyType}.");
            }

            if (replySize != reply.Length - HeaderSize)
            {
                throw new AzaharRpcException(
                    $"El tamaño declarado ({replySize}) no coincide con el recibido ({reply.Length - HeaderSize}).");
            }

            return reply[HeaderSize..];
        }
    }
}
