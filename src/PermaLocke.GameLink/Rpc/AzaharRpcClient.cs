using System.Buffers.Binary;
using System.Diagnostics;
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
    SearchMemory = 5,

    /// <summary>
    /// Del fork: mantener un bloque en su sitio cinco veces por segundo. NADA lo envía ya.
    /// </summary>
    /// <remarks>
    /// Se construyó para sostener el Shedinja en memoria y esa marca ya no existe: la muerte se
    /// escribe en el fichero de partida (§98 bis). El número se queda documentado porque el
    /// emulador sigue entendiéndolo, y el cliente que lo usaba está en el historial.
    /// </remarks>
    WatchBlock = 6,

    /// <summary>
    /// Del parche 3: apuntar quién escribe en un rango de memoria. Todavía no existe en el fork.
    /// </summary>
    /// <remarks>
    /// La última vía para averiguar de dónde saca el juego los PS. Todo lo que se puede preguntar
    /// desde fuera está agotado y medido en el §98: el espejo del equipo el juego lo rellena y no
    /// lo lee nunca —ni al abrir el menú ni al montar un combate— y los valores no están en claro
    /// en 400 MB. Pero el juego <b>escribe</b> los PS buenos en ese espejo, así que la instrucción
    /// que lo hace sabe de dónde vienen. Ver <c>docs/fork/03-donde-vive-el-ps.md</c>.
    /// </remarks>
    WatchWrites = 7,

    /// <summary>Del parche 3: recoger lo apuntado y vaciar el registro.</summary>
    ReadWriteLog = 8
}

/// <summary>
/// Una escritura que el emulador vio, con el estado de la CPU en ese instante.
/// </summary>
/// <param name="Pc">La instrucción que la hizo.</param>
/// <param name="Registers">Los dieciséis registros ARM. Cualquiera puede apuntar al origen.</param>
/// <remarks>
/// Los registros vienen enteros y sin interpretar a propósito: quien busca el origen prueba los
/// dieciséis como dirección y se queda con el que lleve a un Pokémon legible. Elegir uno dentro del
/// emulador sería adivinar, y adivinar con seguridad es exactamente como se pierde una noche.
/// </remarks>
public sealed record MemoryWrite(uint Pc, uint Address, uint Size, uint Value, uint[] Registers);

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
    private int _nextRequestId = Random.Shared.Next();

    /// <summary>One request at a time: the socket is shared by every screen and the poller.</summary>
    private readonly Lock _gate = new();

    /// <summary>Requests that had to be sent again. Health of the link, for diagnosis.</summary>
    public int Retries { get; private set; }

    /// <summary>Late replies thrown away. Each one is a desynchronisation that did not happen.</summary>
    public int Discarded { get; private set; }

    /// <summary>The last requests, for the report written when the emulator goes down (§168).</summary>
    public RpcTrace Trace { get; } = new();

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
        get { lock (_gate) return _socket.Client.ReceiveTimeout; }
        set
        {
            lock (_gate)
            {
                _socket.Client.ReceiveTimeout = value;
                _socket.Client.SendTimeout = value;
            }
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

    /// <summary>How many 32 bit registers an entry of the write log carries.</summary>
    public const int WriteLogRegisters = 16;

    /// <summary>Bytes one entry of the write log takes on the wire.</summary>
    public const int WriteLogEntrySize = (sizeof(uint) * 4) + (sizeof(uint) * WriteLogRegisters);

    /// <summary>
    /// Asks the emulator to note down whoever writes inside a range. Size zero stops watching.
    /// </summary>
    /// <returns>False when the emulator is not the fork, or refused the range.</returns>
    /// <remarks>
    /// Keep the range small. It is watched by taking those pages off the fast path, so a wide one
    /// slows the whole emulator down and buries the answer in writes nobody asked about.
    /// </remarks>
    public bool WatchWrites(uint address, uint size)
    {
        var payload = new byte[sizeof(uint) * 2];

        BinaryPrimitives.WriteUInt32LittleEndian(payload, address);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), size);

        var reply = Send(RpcRequestType.WatchWrites, payload);

        return reply.Length >= sizeof(uint) && BinaryPrimitives.ReadUInt32LittleEndian(reply) == 1;
    }

    /// <summary>
    /// Collects what the emulator noted down and empties its log.
    /// </summary>
    /// <param name="remaining">
    /// Entries still waiting after this batch. It is answered explicitly rather than left to be
    /// inferred from a full page, because a reply that silently drops the rest is how a truncated
    /// search got read as a measurement and cost a night (§98).
    /// </param>
    public IReadOnlyList<MemoryWrite> ReadWriteLog(out int remaining)
    {
        // Ocho bytes aunque no lleve argumentos: el servidor rechaza cualquier paquete con menos,
        // porque todos los tipos leen dos u32 de cabecera. Mandar cero lo tiraba en la puerta.
        var reply = Send(RpcRequestType.ReadWriteLog, new byte[sizeof(uint) * 2]);

        remaining = 0;

        if (reply.Length < sizeof(uint) * 2)
        {
            return [];
        }

        var count = (int)BinaryPrimitives.ReadUInt32LittleEndian(reply);

        remaining = (int)BinaryPrimitives.ReadUInt32LittleEndian(reply.AsSpan(4));

        var writes = new List<MemoryWrite>(count);

        for (var i = 0; i < count; i++)
        {
            var at = (sizeof(uint) * 2) + (i * WriteLogEntrySize);

            if (at + WriteLogEntrySize > reply.Length)
            {
                // El emulador dijo más de las que mandó. Se devuelven las enteras y se deja dicho
                // que faltan, en vez de inventar una entrada a medias.
                remaining += count - i;
                break;
            }

            var registers = new uint[WriteLogRegisters];

            for (var r = 0; r < WriteLogRegisters; r++)
            {
                registers[r] = BinaryPrimitives.ReadUInt32LittleEndian(
                    reply.AsSpan(at + (sizeof(uint) * 4) + (r * sizeof(uint))));
            }

            writes.Add(new MemoryWrite(
                BinaryPrimitives.ReadUInt32LittleEndian(reply.AsSpan(at)),
                BinaryPrimitives.ReadUInt32LittleEndian(reply.AsSpan(at + 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(reply.AsSpan(at + 8)),
                BinaryPrimitives.ReadUInt32LittleEndian(reply.AsSpan(at + 12)),
                registers));
        }

        return writes;
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
        var requestId = unchecked((uint)Interlocked.Increment(ref _nextRequestId));

        var request = new byte[HeaderSize + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(request, ProtocolVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), requestId);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(8), (uint)type);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(12), (uint)payload.Length);
        payload.CopyTo(request.AsSpan(HeaderSize));

        lock (_gate)
        {
            Exception? last = null;
            var clock = Stopwatch.StartNew();

            try
            {
                for (var attempt = 1; attempt <= Attempts; attempt++)
                {
                    try
                    {
                        _socket.Send(request, request.Length, _endpoint);
                        var reply = Receive(requestId, type);
                        Trace.Record(type, payload, answered: true, clock.Elapsed.TotalMilliseconds);
                        return reply;
                    }
                    catch (SocketException ex)
                    {
                        // Se perdió el datagrama, o el emulador estaba ocupado. Se vuelve a pedir con
                        // el MISMO id, así que si la respuesta anterior llega tarde todavía sirve.
                        last = ex;
                        Retries++;
                    }
                }
            }
            catch
            {
                Trace.Record(type, payload, answered: false, clock.Elapsed.TotalMilliseconds);
                throw;
            }

            Trace.Record(type, payload, answered: false, clock.Elapsed.TotalMilliseconds);
            throw new AzaharRpcException(
                $"Sin respuesta de {_endpoint} tras {Attempts} intentos: {last?.Message}", noReply: true);
        }
    }

    /// <summary>Waits for the reply with this id, throwing away anything older that is queued.</summary>
    private byte[] Receive(uint requestId, RpcRequestType type)
    {
        var clock = Stopwatch.StartNew();
        var timeout = _socket.Client.ReceiveTimeout;
        while (true)
        {
            // A stream of stale replies must not restart the timeout and hold every caller forever.
            if (timeout > 0)
            {
                var remaining = timeout - clock.Elapsed.TotalMilliseconds;
                if (remaining <= 0 || !_socket.Client.Poll(
                        (int)Math.Min(int.MaxValue, Math.Ceiling(remaining * 1000)), SelectMode.SelectRead))
                    throw new SocketException((int)SocketError.TimedOut);
            }

            IPEndPoint? from = null;
            var reply = _socket.Receive(ref from);
            if (!Equals(from, _endpoint))
            {
                Discarded++;
                continue;
            }

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
