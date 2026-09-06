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
    ILogger<AzaharGameStateProvider> logger) : IGameStateProvider
{
    /// <summary>Ultra Moon (Europe). PermaLocke targets this title only.</summary>
    public const ulong UltraMoonTitleId = 0x00040000001B5100;

    private PartyLayout? _layout;

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
        if (DateTimeOffset.Now - _lastSweep >= SweepCooldown)
        {
            _sweepNext = true;
        }
    }

    public Task<GameSnapshot> ReadAsync(CancellationToken ct = default) =>
        Task.Run(() => Read(ct), ct);

    private GameSnapshot Read(CancellationToken ct)
    {
        var now = DateTimeOffset.Now;

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

        // El atajo de la dirección cacheada tiene que respetar el barrido pedido, y no lo hacía.
        // Salía por aquí antes de mirar _sweepNext, o sea que SweepAgain() no se consultaba nunca
        // mientras la dirección de siempre siguiera leyendo -- que es siempre. El cap pidió barrer
        // en cada corrección y no barrió ni una vez: «corregido y releído en 1 copias», con cinco
        // estructuras del equipo en memoria.
        if (!_sweepNext && _layout is { } cached
            && ReadParty(reader, cached) is { Count: > 0 } cachedParty)
        {
            return new GameSnapshot(true, null, cachedParty, now, TrainerNotice());
        }

        // Antes de barrer, se prueban las direcciones de la última sesión. Un barrido son 96 MB
        // y decenas de miles de peticiones, y hacerlo en cada arranque llegó a tumbar el
        // emulador. Fiarse de ellas es seguro porque no se confía: cada copia tiene que devolver
        // un equipo coherente de este entrenador antes de usarse.
        var remembered = _remembered = ReadRemembered();

        if (!_sweepNext && remembered is { Count: > 0 } && Choose(reader, remembered) is { } fromDisk)
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
            _gameTrainer = fromDisk.TrainerName;

            logger.LogInformation("Equipo en 0x{Address:X8}, el de la última vez, revalidado sin barrer",
                fromDisk.Address);

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
        if (!_sweepNext && _remembered.Count > 0 && ++_notReadyPolls < PollsBeforeSweeping)
        {
            // Y se suelta el enganche: si el equipo ha dejado de leerse puede ser que el juego se
            // haya cerrado y abierto, y entonces el proceso fijado ya no es el bueno. Reengancharse
            // cuesta tres peticiones una vez, no cuatro cada segundo.
            _attached = false;

            return GameSnapshot.Disconnected(
                "Azahar responde pero la partida todavía no está cargada. "
                + "Entra en ella y en unos segundos se engancha solo.", now);
        }

        logger.LogInformation("Localizando el equipo en memoria (barrido completo)...");

        var located = new PartyLayoutLocator(client).LocateAll(TrainerName, ct);
        var readable = Choose(reader, located);

        if (readable is null)


        {
            _layout = null;
            return GameSnapshot.Disconnected(
                "El juego responde, pero no encuentro el equipo en memoria. "
                + "¿Has empezado la partida y tienes algún Pokémon?", now);
        }

        _layout = readable;
        _notReadyPolls = 0;
        _sweepNext = false;
        _lastSweep = DateTimeOffset.Now;
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
    private PartyLayout? Choose(Pk7Reader reader, IReadOnlyList<PartyLayout> candidates) =>
        candidates
            .Select(layout => (Layout: layout, Party: ReadParty(reader, layout)))
            .Where(candidate => candidate.Party.Count > 0)
            .OrderByDescending(candidate => candidate.Party.Count)
            .ThenBy(candidate =>
                candidate.Layout.Stride == PartyLayoutLocator.AuthoritativeStride ? 0 : 1)
            .Select(candidate => candidate.Layout)
            .FirstOrDefault();

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
                member.TrainerName));
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
        _ => "Azahar no responde. Ábrelo, carga la ROM y activa "
             + "Configuración → Depuración → Activar servidor RPC."
    };
}
