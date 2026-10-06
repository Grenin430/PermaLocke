using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink;

/// <summary>
/// Live link to Ultra Moon running in Azahar, over the emulator's RPC server.
/// </summary>
/// <remarks>
/// Locating the party costs a full memory sweep, so the address is cached. Every read then
/// revalidates it: if slot zero stops holding a coherent Pokémon of this trainer the cache is
/// dropped and the sweep runs again. That way a moved party — new area, bigger team, different
/// emulator build — recovers by itself instead of silently reporting stale data.
/// </remarks>
public sealed class AzaharGameStateProvider(
    AzaharRpcClient client,
    ISpeciesLookup species,
    ILocationLookup locations,
    string knownLayoutPath,
    ILogger<AzaharGameStateProvider> logger,
    IPartyLayoutLocator? locator = null,
    TimeProvider? timeProvider = null,
    Func<IReadOnlyList<uint>?>? savedPartyKeys = null) : IGameStateProvider
{
    /// <summary>Ultra Moon (Europe). PermaLocke targets this title only.</summary>
    public const ulong UltraMoonTitleId = 0x00040000001B5100;

    private PartyLayout? _layout;
    private readonly IPartyLayoutLocator _locator = locator ?? new PartyLayoutLocator(client);
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    /// <summary>Cuántas lecturas seguidas se esperan antes de barrer los 96 MB.</summary>
    /// <remarks>
    /// A una lectura por segundo son unos veinte segundos, que es de sobra para cargar una partida
    /// y ridículo al lado de los diez minutos que cuesta un barrido completo.
    /// </remarks>
    private const int PollsBeforeSweeping = 20;

    private IReadOnlyList<PartyLayout> _remembered = [];
    private int _notReadyPolls;

    /// <summary>Set when something proves the remembered copies are not all of them.</summary>
    private bool _sweepNext;

    /// <summary>When the last full sweep ran, so a correction that never sticks cannot turn the
    /// monitor into a permanent memory scan while somebody is playing.</summary>
    private DateTimeOffset _lastSweep = DateTimeOffset.MinValue;

    private static readonly TimeSpan SweepCooldown = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long to wait after a sweep that found nothing, doubling each time up to
    /// <see cref="MaxSweepCooldown"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A sweep is the most dangerous thing PermaLocke does to the emulator, and the evidence is no longer
    /// circumstantial. Azahar crashed five times between the 19th and the 21st of September 2026, all of them
    /// access violations; the three at <c>azahar.exe+0x781693</c> each happened <b>three to six seconds after a
    /// full sweep</b>, and the emulator's own log for one of them ends mid-word inside an unbroken run of
    /// <c>ReadMemory</c> requests, eighteen microseconds apart, at emulator time 18.4 s — with the game still
    /// loading. The other two crashed with PermaLocke not even running, so the emulator has faults of its own; this
    /// is about not adding to them.
    /// </para>
    /// <para>
    /// A fixed cooldown did not stop it. While the save is not in memory the party cannot be found, so the sweep
    /// fails, so it runs again: 226 sweeps in one day, and 22 in a row at one point. Doubling turns that into a
    /// handful. The one thing it must not do is make a real reconnection slow, so <b>any</b> success resets it —
    /// the sweep that works, and the ordinary read that starts working on its own once the game finishes loading.
    /// </para>
    /// </remarks>
    private static readonly TimeSpan MaxSweepCooldown = TimeSpan.FromMinutes(4);

    /// <summary>Consecutive sweeps that found nothing.</summary>
    private int _fruitlessSweeps;

    /// <summary>How long to wait before sweeping again, after however many have come up empty.</summary>
    private TimeSpan SweepWait
    {
        get
        {
            var wait = SweepCooldown * Math.Pow(2, Math.Min(_fruitlessSweeps, 8));

            return wait > MaxSweepCooldown ? MaxSweepCooldown : wait;
        }
    }

    /// <summary>A sweep found nothing, so the next one waits twice as long.</summary>
    private void SweepCameUpEmpty()
    {
        _fruitlessSweeps++;

        logger.LogInformation(
            "El barrido no ha encontrado el equipo ({Veces} seguido(s)); el siguiente no será hasta dentro de {Espera:0} s",
            _fruitlessSweeps, SweepWait.TotalSeconds);
    }

    /// <summary>How often the cheap check for the party may run while it is not in memory.</summary>
    private static readonly TimeSpan PresenceCheckEvery = TimeSpan.FromSeconds(20);

    private DateTimeOffset _lastPresenceCheck = DateTimeOffset.MinValue;

    /// <summary>Why the party cannot be in memory yet, from the last check; null when it may be.</summary>
    private string? _absentBecause;

    /// <summary>
    /// The party found from the save, or why it cannot be found yet. Null reason and no layouts: the emulator cannot
    /// search, so the sweep is the only way, as it always was.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fourth crash in two days, on 2026-09-21, is what started this. The player began a new game, and while they
    /// typed their name the provider ran its first full sweep: the emulator's log stops mid-line at 21.8 s inside the
    /// burst of reads, the fourth time with the same signature. There was nothing to find — on the name screen no party
    /// exists — and the sweep could not have found a starter on its own anyway (see
    /// <see cref="PartyLayoutLocator.LocateByKeys"/>), which on a fresh install is every player's first half hour.
    /// </para>
    /// <para>
    /// So the save is asked first. Without one the game has never been saved: it is the intro, or a party the
    /// encounter rules cannot use yet either, and the player is asked to save. With one, its Pokémon are looked for
    /// by their encryption constants, a handful of requests where a sweep is a hundred thousand. On the title screen or
    /// while loading they are not in memory, and nothing is swept.
    /// </para>
    /// <para>
    /// An emulator that is not the fork answers every search empty, which would read as «not there» forever; then this
    /// stands aside and the sweep runs as before.
    /// </para>
    /// </remarks>
    private (IReadOnlyList<PartyLayout> Found, string? Why) LocateFromSave(Pk7Reader reader)
    {
        if (savedPartyKeys?.Invoke() is not { Count: > 0 } keys)
        {
            return ([], "Guarda la partida dentro del juego y PermaLocke encontrará tu equipo.");
        }

        try
        {
            if (!client.SupportsSearch())
            {
                return ([], null);
            }

            client.AttachTo(UltraMoonTitleId);

            // Solo lo que se deja leer como equipo: son las estructuras en las que luego se escribe.
            var found = _locator.LocateByKeys([.. keys])
                .Where(layout => ReadParty(reader, layout).Count > 0)
                .ToList();

            return found.Count > 0
                ? (found, null)
                : ([], "Tu equipo todavía no está cargado en el juego. Carga tu partida.");
        }
        catch (AzaharRpcException)
        {
            // Si ni una búsqueda contesta, un barrido de cien mil peticiones no es buena idea.
            return ([], "Esperando a que el juego responda.");
        }
    }

    /// <summary>Takes a located party as the one to read and write, however it was found.</summary>
    private GameSnapshot Adopt(Pk7Reader reader, IReadOnlyList<PartyLayout> located, PartyLayout readable,
        DateTimeOffset now, string how)
    {
        _layout = readable;
        _notReadyPolls = 0;
        _fruitlessSweeps = 0;
        _absentBecause = null;
        _allLayouts = PartyLayoutLocator.Distinct(located);
        _gameTrainer = readable.TrainerName;

        // Como con lo recordado: si lo que mejor se lee es el espejo, la estructura que lee el juego falta y se busca.
        _sweepNext = PartyLayoutLocator.NeedsLocatingAgain(readable);
        Remember(located);

        logger.LogInformation(
            "Equipo localizado {How} en 0x{Address:X8} (salto 0x{Stride:X}), {Copies} copias, entrenador «{Trainer}»",
            how, readable.Address, readable.Stride, located.Count, _gameTrainer);

        return new GameSnapshot(true, null, ReadParty(reader, readable), now, TrainerNotice());
    }

    /// <summary>Fallos seguidos antes de dar por perdido el equipo ya localizado.</summary>
    /// <remarks>
    /// Uno suelto es casi siempre un datagrama perdido. Tirar la dirección por eso obliga a
    /// revalidar, y si la revalidación tampoco cuaja, a barrer: diez minutos por un paquete.
    /// </remarks>
    private const int FailuresBeforeGivingUp = 5;

    private bool _attached;
    private int _failures;
    private IReadOnlyList<PartyLayout> _allLayouts = [];
    private string _gameTrainer = string.Empty;

    /// <summary>The copy used for reading: the one that carries battle stats.</summary>
    public PartyLayout? Layout => _layout;

    /// <summary>
    /// Every copy found. Writes go to all of them, because the one the game reads cannot be
    /// identified from memory alone and they all hold the same Pokémon.
    /// </summary>
    public IReadOnlyList<PartyLayout> AllLayouts => _allLayouts;

    /// <summary>
    /// Only the party for now. Boxes, bag, badges and wild encounters need their own locators
    /// and are not claimed until they exist.
    /// </summary>
    public GameLinkCapabilities Capabilities =>
        GameLinkCapabilities.Party | GameLinkCapabilities.LiveUpdates;

    /// <summary>Trainer name recorded in the run. Preferred when locating, never required.</summary>
    public string TrainerName { get; set; } = string.Empty;

    /// <summary>
    /// Forces a full sweep on the next read instead of trusting the remembered addresses.
    /// </summary>
    /// <remarks>
    /// The remembered set is revalidated for ever and <b>never widened</b>, and that is what made
    /// the level cap lose. Measured on the real run: at 21:45 there was one copy of the party, at
    /// 22:14 there were two and an hour later five. The cap wrote to the ones it remembered, read
    /// them back correct — and the game restored the level from a copy nobody had ever looked for.
    /// Three corrections in a minute, each one honestly reporting success.
    /// <para>
    /// A sweep used to be 96 MB and ten minutes of watching nothing. That number is <b>stale</b>:
    /// it was measured before §54 fixed the RPC client, which was losing replies and retrying.
    /// Timed again today against the running game, it is about five seconds — so paying for one
    /// whenever the cap has to correct somebody is cheap, and correcting is rare.
    /// </para>
    /// </remarks>
    public void SweepAgain()
    {
        if (_time.GetUtcNow() - _lastSweep >= SweepWait)
        {
            _sweepNext = true;
        }
    }

    public Task<GameSnapshot> ReadAsync(CancellationToken ct = default) =>
        Task.Run(() => Read(ct), ct);

    private GameSnapshot Read(CancellationToken ct)
    {
        var now = _time.GetUtcNow();

        // Engancharse cuesta TRES viajes de ida y vuelta -listar procesos, fijar el proceso y
        // comprobar cuál quedó fijado-, y antes se hacía en cada lectura. Con el equipo eso son
        // cuatro peticiones por segundo donde basta una, y cuatro ocasiones de que alguna expire:
        // cualquiera de las tres que se perdiera tiraba la conexión entera.
        //
        // El proceso no cambia mientras el juego está abierto, así que se engancha una vez y se
        // suelta solo cuando algo falla de verdad. Medido en una sesión real: el enlace se caía
        // solo cada pocos minutos, y en esos huecos no se vigila nada.
        try
        {
            if (!_attached)
            {
                client.AttachTo(UltraMoonTitleId);
                _attached = true;
            }
        }
        catch (Exception ex)
        {
            _attached = false;

            // El equipo localizado NO se tira a la primera. Un fallo suelto es casi siempre un
            // datagrama perdido, y volver a barrer por eso sería pagar diez minutos por un paquete.
            // Solo tras varios seguidos se admite que el juego se ha ido de verdad.
            if (++_failures >= FailuresBeforeGivingUp)
            {
                _layout = null;
            }

            return GameSnapshot.Disconnected(Explain(ex), now);
        }

        _failures = 0;

        var reader = new Pk7Reader(client);
        var sweepDue = _sweepNext && now - _lastSweep >= SweepWait;

        // El atajo de la dirección cacheada tiene que respetar el barrido pedido, y no lo hacía.
        // Salía por aquí antes de mirar _sweepNext, o sea que SweepAgain() no se consultaba nunca
        // mientras la dirección de siempre siguiera leyendo -- que es siempre. El cap pidió barrer
        // en cada corrección y no barrió ni una vez: «corregido y releído en 1 copias», con cinco
        // estructuras del equipo en memoria.
        if (!sweepDue && _layout is { } cached
            && ReadParty(reader, cached) is { Count: > 0 } cachedParty)
        {
            return new GameSnapshot(true, null, cachedParty, now, TrainerNotice());
        }

        // El equipo que el propio juego tiene en GameData (2026-10-06): sin barrer y siempre el bueno. Si la cadena no
        // lleva a un equipo legible (juego cargando, otro code.bin), se sigue como siempre.
        if (FromGame(reader) is { } own && ReadParty(reader, own) is { Count: > 0 } ownParty)
        {
            if (_layout != own)
            {
                logger.LogInformation("Equipo en 0x{Address:X8}, el que dice el juego, sin barrer", own.Address);
            }

            _allLayouts = PartyLayoutLocator.Distinct([own, .. _remembered ??= ReadRemembered()]);
            _layout = own;
            _notReadyPolls = 0;
            _fruitlessSweeps = 0;
            _sweepNext = false;
            _gameTrainer = own.TrainerName;
            return new GameSnapshot(true, null, ownParty, now, TrainerNotice());
        }

        // Antes de barrer, se prueban las direcciones de la última sesión. Un barrido son 96 MB
        // y decenas de miles de peticiones, y hacerlo en cada arranque llegó a tumbar el
        // emulador. Fiarse de ellas es seguro porque no se confía: cada copia tiene que devolver
        // un equipo coherente de este entrenador antes de usarse.
        var remembered = _remembered = ReadRemembered();

        if (!sweepDue && remembered is { Count: > 0 } && Choose(reader, remembered) is { } fromDisk)
        {
            // TODAS las recordadas, no solo las que se dejan leer.
            //
            // Aquí estaba el fallo que dejaba el cap de nivel sin efecto. La copia autoritativa
            // -la de salto 0x1E4, la que el juego lee de verdad- guarda las estadísticas de
            // combate en otro sitio, así que Pk7Reader la rechaza entera: exige unos PS máximos
            // coherentes y ahí no los encuentra. Filtrar por eso dejaba fuera de la lista de
            // escritura justo la copia que hay que corregir, y las correcciones iban solo a las
            // copias del bloque de partida, que el juego pisa.
            //
            // Poder leerse y poder escribirse son cosas distintas. Para escribir, la garantía la
            // pone el escritor: exige un checksum de PK7 válido antes de tocar nada -memoria al
            // azar no lo pasa- y relee después para comprobar que cuajó.
            _allLayouts = PartyLayoutLocator.Distinct(remembered);
            _layout = fromDisk;
            _notReadyPolls = 0;

            // El equipo se lee: la partida está cargada. Lo que hiciera fallar a los barridos de antes
            // ya no pasa, así que la espera vuelve a su mínimo y una reconexión de verdad no se retrasa.
            _fruitlessSweeps = 0;
            _gameTrainer = fromDisk.TrainerName;

            // Si de lo recordado solo lee el espejo, la estructura que lee el juego se ha movido: el
            // espejo está siempre en el mismo sitio y ella no. Se conecta ya con el espejo, para no
            // dejar de vigilar, y en la lectura siguiente se barre y se guarda la lista nueva. Sin
            // esto la lista del 10 de septiembre se aceptó durante ocho días (§135).
            if (PartyLayoutLocator.NeedsLocatingAgain(fromDisk))
            {
                _sweepNext = true;

                logger.LogInformation(
                    "Equipo en 0x{Address:X8}, el de la última vez, pero es el espejo: la copia que lee el"
                    + " juego ya no está donde estaba y se busca de nuevo", fromDisk.Address);
            }
            else
            {
                logger.LogInformation("Equipo en 0x{Address:X8}, el de la última vez, revalidado sin barrer",
                    fromDisk.Address);
            }

            return new GameSnapshot(true, null, ReadParty(reader, fromDisk), now, TrainerNotice());
        }

        // Las direcciones de la última vez existen pero todavía no tienen un equipo dentro. Casi
        // siempre eso no significa que se hayan movido: significa que Azahar está abierto y la
        // partida aún no está cargada, que es exactamente el minuto en el que uno abre las dos
        // cosas a la vez.
        //
        // Antes se barría en el acto, y un barrido son 96 MB. Medido en una sesión real: diez
        // minutos y nueve segundos, DURANTE LOS CUALES NO SE VIGILA NADA. En ese hueco murió un
        // Pokémon en una prueba y no se contó. Esperar unos segundos cuesta segundos; barrer
        // cuando no hacía falta cuesta diez minutos a ciegas.
        // A fresh installation has no saved addresses either. Give the game time to load there too.
        if (!_sweepNext && ++_notReadyPolls < PollsBeforeSweeping)
        {
            // Y se suelta el enganche: si el equipo ha dejado de leerse puede ser que el juego se
            // haya cerrado y abierto, y entonces el proceso fijado ya no es el bueno. Reengancharse
            // cuesta tres peticiones una vez, no cuatro cada segundo.
            _attached = false;

            return GameSnapshot.Disconnected(
                "Carga tu partida en Azahar.", now);
        }

        // Por la partida guardada antes que barrer (§152): unas pocas búsquedas en vez de cien mil lecturas, y
        // encuentra también a quien solo lleva su inicial. Cuesta poco, así que no espera a la pausa entre barridos.
        if (savedPartyKeys is not null && now - _lastPresenceCheck >= PresenceCheckEvery)
        {
            _lastPresenceCheck = now;
            var (fromSave, why) = LocateFromSave(reader);

            if (fromSave.Count > 0 && Choose(reader, fromSave) is { } chosen)
            {
                return Adopt(reader, fromSave, chosen, now, "por la partida guardada, sin barrer");
            }

            if (why is not null && why != _absentBecause)
            {
                logger.LogInformation("No se barre la memoria: {Motivo}", why);
            }

            _absentBecause = why;
        }

        // Failed searches used to bypass the cooldown and scan 96 MB again every six seconds.
        if (now - _lastSweep < SweepWait)
        {
            _attached = false;
            return GameSnapshot.Disconnected("Esperando a que el equipo esté disponible.", now);
        }

        // Nada que encontrar, nada que barrer. Un barrido pedido porque la estructura se ha movido sí va: el equipo
        // se acaba de leer, así que está, y la partida guardada no ha sabido llegar a la copia que falta.
        if (!_sweepNext && savedPartyKeys is not null && _absentBecause is { } absent)
        {
            _attached = false;
            return GameSnapshot.Disconnected(absent, now);
        }

        logger.LogInformation("Localizando el equipo en memoria (barrido completo)...");
        _lastSweep = _time.GetUtcNow();
        _sweepNext = false;
        IReadOnlyList<PartyLayout> located;
        try
        {
            // Reattach immediately before the expensive search, including after emulator restarts.
            client.AttachTo(UltraMoonTitleId);
            located = _locator.LocateAll(TrainerName, ct);
        }
        catch (AzaharRpcException ex)
        {
            _attached = false;
            _layout = null;
            _allLayouts = [];
            SweepCameUpEmpty();
            return GameSnapshot.Disconnected(Explain(ex), now);
        }
        finally
        {
            // Count unsuccessful and interrupted attempts too, from when they finish.
            _lastSweep = _time.GetUtcNow();
        }

        var readable = Choose(reader, located);
        if (readable is null)
        {
            _attached = false;
            _layout = null;
            _allLayouts = [];
            SweepCameUpEmpty();
            return GameSnapshot.Disconnected(
                "Todavía no se ve tu equipo. Carga la partida.", now);
        }

        _layout = readable;
        _notReadyPolls = 0;
        _fruitlessSweeps = 0;
        _sweepNext = false;
        _lastSweep = _time.GetUtcNow();
        _allLayouts = PartyLayoutLocator.Distinct(located);
        _gameTrainer = readable.TrainerName;
        Remember(located);

        logger.LogInformation(
            "Equipo localizado en 0x{Address:X8} (salto 0x{Stride:X}), {Copies} copias, entrenador «{Trainer}»",
            readable.Address, readable.Stride, located.Count, _gameTrainer);

        return new GameSnapshot(true, null, ReadParty(reader, readable), now, TrainerNotice());
    }

    /// <summary>
    /// The run is created before the game is ever read, so the name typed there may not match
    /// the trainer in the save. Saying so is more useful than silently using one or the other.
    /// </summary>
    private string? TrainerNotice() =>
        string.IsNullOrEmpty(_gameTrainer)
        || string.Equals(_gameTrainer, TrainerName, StringComparison.Ordinal)
            ? null
            : $"El entrenador del juego es «{_gameTrainer}» y la run está a nombre de «{TrainerName}».";

    /// <summary>
    /// Picks the copy worth reading from: the one the game itself reads, when it can be read.
    /// </summary>
    /// <remarks>
    /// The tie-break used to prefer the save-block mirror, and that was not a choice so much as
    /// the only thing that worked — the authoritative structure keeps its battle stats at
    /// <c>0x158</c> and nothing here knew that, so it never yielded a single member. Now that it
    /// does, it wins, and it has to: the mirror is a photograph the game refreshes when it saves,
    /// so reading HP from it meant a Pokémon could faint and go on looking healthy until the
    /// player saved. Most members still wins first, because a copy that only holds the lead is
    /// worse than a complete one whatever else it is. §99.
    /// </remarks>
    /// <summary>
    /// Picks the structure to read the party from. The rule lives in
    /// <see cref="PartyLayoutLocator.Preferred"/>, where it can be tested without a game.
    /// </summary>
    private PartyLayout? Choose(Pk7Reader reader, IReadOnlyList<PartyLayout> candidates) =>
        PartyLayoutLocator.Preferred(
            candidates.Select(layout => (Layout: layout, Read: ReadParty(reader, layout).Count)));

    private const uint GameManagerPointer = 0x006A3984, GameDataOffset = 0x24, PartyOffset = 0x0C;
    private const uint LinearHeap = 0x30000000, LinearHeapEnd = 0x34000000;

    /// <summary>
    /// The party the game itself holds (2026-10-06, measured on the running game): the GameManager the code.bin keeps at
    /// 0x6A3984, its GameData at +0x24 (what <c>GameData::GetNowZoneID</c> reads), and GameData +0xC the
    /// <c>PokeParty</c>: six pointers to the members in party order and their count at +0x18. Each member points at +4 to
    /// its stored block, an entry of the authoritative structure (stride 0x1E4), so the lowest one is its first slot.
    /// </summary>
    private PartyLayout? FromGame(Pk7Reader reader)
    {
        static bool InHeap(uint address) => address is >= LinearHeap and < LinearHeapEnd;

        try
        {
            var manager = BitConverter.ToUInt32(client.ReadMemory(GameManagerPointer, 4));
            if (!InHeap(manager)) return null;
            var data = BitConverter.ToUInt32(client.ReadMemory(manager + GameDataOffset, 4));
            if (!InHeap(data)) return null;
            var party = BitConverter.ToUInt32(client.ReadMemory(data + PartyOffset, 4));
            if (!InHeap(party)) return null;

            var head = client.ReadMemory(party, 0x1C);
            var count = head[0x18];
            if (count is 0 or > 6) return null;

            var entries = new List<uint>();
            for (var i = 0; i < count; i++)
            {
                var member = BitConverter.ToUInt32(head, i * 4);
                if (!InHeap(member)) return null;
                var entry = BitConverter.ToUInt32(client.ReadMemory(member + 4, 4));
                if (!InHeap(entry)) return null;
                entries.Add(entry);
            }

            var first = entries.Min();
            const uint Stride = PartyLayoutLocator.AuthoritativeStride;
            if (entries.Any(e => (e - first) % Stride != 0 || e - first >= 6 * Stride)) return null;

            var trainer = reader.TryRead(first, PartyLayoutLocator.AuthoritativeStatsOffset)?.TrainerName ?? string.Empty;
            return new PartyLayout(first, Stride, trainer);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }


    /// <summary>Layouts found last session, if any. Never used without revalidating them.</summary>
    private IReadOnlyList<PartyLayout> ReadRemembered()
    {
        try
        {
            if (!File.Exists(knownLayoutPath))
            {
                return [];
            }

            var layouts = new List<PartyLayout>();

            foreach (var line in File.ReadAllLines(knownLayoutPath))
            {
                var parts = line.Split('\t');

                if (parts.Length == 3
                    && uint.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out var address)
                    && uint.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out var stride))
                {
                    layouts.Add(new PartyLayout(address, stride, parts[2]));
                }
            }

            return layouts;
        }
        catch (IOException)
        {
            return [];
        }
    }

    private void Remember(IEnumerable<PartyLayout> layouts)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(knownLayoutPath)!);
            File.WriteAllLines(knownLayoutPath,
                layouts.Select(l => $"{l.Address:X8}\t{l.Stride:X}\t{l.TrainerName}"));
        }
        catch (IOException ex)
        {
            // Sin el fichero solo se pierde el atajo: la próxima vez se vuelve a barrer.
            logger.LogWarning(ex, "No se pudo recordar la dirección del equipo");
        }
    }

    /// <summary>
    /// Reads a whole party, asking each structure for its stats where that structure keeps them.
    /// </summary>
    /// <remarks>
    /// The offset is not cosmetic. Reading 260 contiguous bytes only ever succeeded on the mirror,
    /// so that is the copy every snapshot came from — and the mirror is a photograph the game
    /// refreshes when it saves. A Pokémon that fainted read as healthy until the player saved,
    /// which is exactly the kind of silence that makes a watcher look like it is working. §99.
    /// </remarks>
    private List<LivePartyMember> ReadParty(Pk7Reader reader, PartyLayout layout)
    {
        var party = new List<LivePartyMember>();

        var statsOffset = layout.Stride == PartyLayoutLocator.AuthoritativeStride
            ? PartyLayoutLocator.AuthoritativeStatsOffset
            : (uint?)null;

        for (var slot = 0; slot < 6; slot++)
        {
            var member = reader.TryRead(layout.SlotAddress(slot), statsOffset);

            // An empty slot ends the party; every slot after it is empty too.
            if (member is null)
            {
                break;
            }

            party.Add(new LivePartyMember(
                slot,
                member.Species,
                species.GetName(member.Species),
                member.Nickname,
                member.Level,
                member.CurrentHp,
                member.MaxHp,
                member.IsShiny,
                member.Pid,
                member.MetLocation,
                locations.GetName(member.MetLocation),
                member.TrainerName,
                member.Form,
                member.Moves));
        }

        return party;
    }


    /// <summary>
    /// What to tell the player, which is not always what the exception said.
    /// </summary>
    /// <remarks>
    /// Silence from the socket gets the actionable sentence, not the socket's own words for it:
    /// "se ha forzado la interrupción de una conexión existente" is true, unreadable, and does not
    /// tell anybody to open the emulator. A reply that arrived and made no sense is different —
    /// that one is worth showing as it is, because there is no better guess to make.
    /// </remarks>
    private static string Explain(Exception ex) => ex switch
    {
        AzaharRpcException { NoReply: false } => ex.Message,
        _ => "Azahar no está abierto."
    };
}
