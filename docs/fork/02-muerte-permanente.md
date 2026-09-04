# Parche 2 para el fork de Azahar — la muerte se reaplica sola

**Estado: escrito contra el código real del fork (commit `cf46ecc`), SIN COMPILAR NI PROBAR.**
Yo no puedo compilar C++ aquí. Lo verificas tú, igual que hiciste con `NEW_LINEAR_HEAP` y con
`SearchMemory`.

Repositorio: `github.com/Grenin430/azahar`. Ficheros que toca, los tres de siempre:
`src/core/rpc/packet.h` y `src/core/rpc/rpc_server.{h,cpp}`.

## Qué resuelve

Hoy la marca de muerte se escribe **una vez**. PermaLocke la mete en las copias del equipo, las
relee, y ahí acaba su trabajo. Lo que pasa después no lo ve nadie:

- **El juego reescribe el hueco** y deshace la marca. Medido: el §90 escribió 7 PS en una copia,
  aguantaron quince segundos y el juego los pisó en cuanto tocó el equipo.
- **La aplicación no está delante.** El sondeo va a 1 Hz y solo mientras PermaLocke esté abierto.

Con este parche la lista de muertos vive **dentro del emulador** y se reaplica varias veces por
segundo, sin red de por medio. Es lo que hace la referencia: su módulo apunta el PID en una lista
de vigilancia, detecta que «el juego reescribió» y vuelve a convertir. Ver `ARCHITECTURE.md` §94.

## Qué NO resuelve, y conviene decirlo antes

**Sigue siendo memoria.** Si cierras el juego sin guardar, la marca se pierde igual — la de la
referencia también. Lo que cambia es que mientras juegas la marca **no se puede caer**, así que
cuando guardes estará puesta. Para lo permanente de verdad sigue estando MANTENIMIENTO, que
escribe la partida.

**No engancha el fin del combate.** No hace falta: no decidimos *quién* muere —eso ya lo hace
PermaLocke y funciona—, solo *que lo que está muerto siga estándolo*. Enganchar el combate es un
parche 3 y necesita entender las estructuras de combate del juego.

## El diseño, y por qué el emulador se queda tonto

El emulador **no sabe qué es un Pokémon**. No cifra, no calcula checksums, no conoce a Shedinja.
PermaLocke le da los bytes ya hechos —que es lo que hoy escribe de todas formas— y el emulador solo
tiene un trabajo:

> «En esta dirección, si los cuatro primeros bytes valen T, entonces los siguientes N tienen que
> ser exactamente estos. Si no lo son, escríbelos.»

Esos cuatro bytes son la **constante de encriptación**, que en un PK7 va en claro en el offset 0 y
es única por Pokémon. Es la misma disciplina del §53: **la identidad por delante**. Si el hueco ya
no lleva a ese Pokémon —el equipo se reordenó, o se guardó en una caja— la constante no coincide y
**no se toca nada**, en vez de machacar al vecino.

La referencia hace esto mismo con el PID; nosotros usamos la constante porque va en claro y ahorra
descifrar dentro del emulador.

## 1. `src/core/rpc/packet.h`

Añadir el tipo al enum, detrás del que ya metimos:

```cpp
enum class PacketType : u32 {
    Undefined = 0,
    ReadMemory = 1,
    WriteMemory = 2,
    ProcessList = 3,
    SetGetProcess = 4,
    SearchMemory = 5,
    WatchBlock = 6,
};
```

Y el tope de una entrada, junto a las demás constantes:

```cpp
// Una entrada de vigilancia: dirección, etiqueta y el bloque que debe haber ahí.
constexpr u32 MAX_WATCH_BLOCK = MAX_PACKET_DATA_SIZE - (sizeof(u32) * 4);
constexpr u32 MAX_WATCH_ENTRIES = 12;
```

`MAX_WATCH_ENTRIES` es un tope de cordura: seis Pokémon por dos estructuras. Que exista un tope es
lo que impide que un cliente con un fallo llene el emulador de trabajo por fotograma.

## 2. `src/core/rpc/rpc_server.h`

Cabeceras nuevas arriba:

```cpp
#include <mutex>
#include <vector>
```

El manejador, junto a los otros cinco:

```cpp
    void HandleWatchBlock(Packet& packet, u32 mode, u32 block_size);
```

Y los miembros nuevos, al final de la clase:

```cpp
private:
    struct Watch {
        u32 address = 0;
        u32 tag = 0;
        std::vector<u8> block;
    };

    void EnforceLoop(std::stop_token stop_token);
    void EnforceOnce();
    bool WriteGuarded(u32 address, std::span<const u8> data);

    std::mutex watch_mutex;
    std::vector<Watch> watches;
    std::jthread enforce_thread;
```

## 3. `src/core/rpc/rpc_server.cpp`

### 3.1 Sacar la lista blanca de regiones a un sitio

`HandleWriteMemory` decide hoy dentro de sí mismo si una dirección se puede escribir. La
vigilancia necesita **la misma** decisión, y dos copias de una lista blanca acaban discrepando —
que es justo el fallo que costó el §68. Se extrae tal cual:

```cpp
namespace {
bool IsWritableRegion(u32 address) {
    return (address >= Memory::PROCESS_IMAGE_VADDR && address <= Memory::PROCESS_IMAGE_VADDR_END) ||
           (address >= Memory::HEAP_VADDR && address <= Memory::HEAP_VADDR_END) ||
           (address >= Memory::LINEAR_HEAP_VADDR && address <= Memory::LINEAR_HEAP_VADDR_END) ||
           (address >= Memory::NEW_LINEAR_HEAP_VADDR &&
            address <= Memory::NEW_LINEAR_HEAP_VADDR_END) ||
           (address >= Memory::N3DS_EXTRA_RAM_VADDR && address <= Memory::N3DS_EXTRA_RAM_VADDR_END);
}
} // namespace
```

Y el `if` gigante de `HandleWriteMemory` pasa a ser `if (IsWritableRegion(address)) {`. **Nada más
de ese método cambia.**

### 3.2 Arrancar el hilo de vigilancia

En el constructor, detrás del que ya hay:

```cpp
    enforce_thread =
        std::jthread([this](std::stop_token stop_token) { EnforceLoop(stop_token); });
```

### 3.3 El manejador

```cpp
// mode 0 vacía la lista; mode 1 añade una entrada. Vaciar y volver a llenar es la única forma
// honesta de actualizarla: una lista que solo crece acabaría reponiendo la marca de un Pokémon
// que ya no está muerto porque la run se corrigió.
void RPCServer::HandleWatchBlock(Packet& packet, u32 mode, u32 block_size) {
    const auto data = packet.GetPacketData();
    bool ok = false;

    if (mode == 0) {
        std::scoped_lock lock{watch_mutex};
        watches.clear();
        ok = true;
        LOG_INFO(RPC_Server, "WatchBlock: lista vaciada");
    } else if (mode == 1 && block_size > 0 && block_size <= MAX_WATCH_BLOCK &&
               data.size() >= (sizeof(u32) * 4) + block_size) {
        Watch watch;
        std::memcpy(&watch.address, data.data() + sizeof(u32) * 2, sizeof(u32));
        std::memcpy(&watch.tag, data.data() + sizeof(u32) * 3, sizeof(u32));
        watch.block.assign(data.begin() + (sizeof(u32) * 4),
                           data.begin() + (sizeof(u32) * 4) + block_size);

        std::scoped_lock lock{watch_mutex};
        if (watches.size() < MAX_WATCH_ENTRIES && IsWritableRegion(watch.address)) {
            LOG_INFO(RPC_Server, "WatchBlock: 0x{:08X} etiqueta {:08X}, {} bytes", watch.address,
                     watch.tag, block_size);
            watches.push_back(std::move(watch));
            ok = true;
        }
    }

    // La respuesta es un u32: 1 aceptada, 0 rechazada. El cliente tiene que poder distinguir
    // «no cabe» de «se ha perdido el paquete».
    const u32 result = ok ? 1u : 0u;
    std::memcpy(packet.GetPacketData().data(), &result, sizeof(result));
    packet.SetPacketDataSize(sizeof(result));
    packet.SendReply();
}
```

### 3.4 La escritura con guardia, compartida

```cpp
bool RPCServer::WriteGuarded(u32 address, std::span<const u8> data) {
    if (!IsWritableRegion(address) || selected_pid == 0xFFFFFFFF) {
        return false;
    }
    auto process = system.Kernel().GetProcessById(selected_pid);
    if (!process) {
        return false;
    }
    system.Memory().WriteBlock(*process, address, data.data(), data.size());
    return true;
}
```

### 3.5 El bucle

```cpp
// Doscientos milisegundos: cinco pasadas por segundo. Suficiente para que la marca no se vea caer
// y lo bastante espaciado para que leer unos pocos cientos de bytes no le cueste nada al emulador.
// A cada fotograma sería gratis igual, pero no aporta y ata este código al ritmo de la GPU.
void RPCServer::EnforceLoop(std::stop_token stop_token) {
    LOG_INFO(RPC_Server, "Vigilancia de bloques iniciada.");

    while (!stop_token.stop_requested()) {
        EnforceOnce();
        std::this_thread::sleep_for(std::chrono::milliseconds(200));
    }
}

void RPCServer::EnforceOnce() {
    std::scoped_lock lock{watch_mutex};

    if (watches.empty() || selected_pid == 0xFFFFFFFF) {
        return;
    }

    auto process = system.Kernel().GetProcessById(selected_pid);
    if (!process) {
        return;
    }

    std::vector<u8> current;

    for (const auto& watch : watches) {
        current.resize(watch.block.size());
        system.Memory().ReadBlock(*process, watch.address, current.data(), current.size());

        // LA IDENTIDAD POR DELANTE. Si la etiqueta no cuadra, este hueco ya no lleva a ese
        // Pokemon -- el equipo se reordena, y se ha visto reordenarse en medio de una sesion --
        // asi que escribir aqui seria machacar a otro. Se deja en paz.
        u32 tag = 0;
        std::memcpy(&tag, current.data(), sizeof(tag));
        if (tag != watch.tag) {
            continue;
        }

        if (std::equal(current.begin(), current.end(), watch.block.begin())) {
            continue;
        }

        if (WriteGuarded(watch.address, watch.block)) {
            LOG_INFO(RPC_Server, "El juego reescribio 0x{:08X}; marca repuesta", watch.address);
        }
    }
}
```

### 3.6 El despacho

En `ValidatePacket`, añadir `case PacketType::WatchBlock:` a la lista de los que valen.

En `HandleSingleRequest`:

```cpp
        case PacketType::WatchBlock:
            HandleWatchBlock(*request_packet, arg1, arg2);
            success = true;
            break;
```

## Lo que hace falta del lado de PermaLocke

Poco, porque los bytes ya se construyen hoy:

1. `AzaharRpcClient` gana `WatchBlock(mode, address, tag, block)`, con el mismo cerrojo y los mismos
   tres intentos del §54.
2. Después de aplicar una muerte, `GameLinkMonitor` manda **la lista entera**: primero `mode 0` y
   luego una entrada por cada copia del equipo donde vive ese Pokémon.
3. Cuando el localizador vuelve a barrer porque el equipo se ha movido, se reenvía la lista con las
   direcciones nuevas. Las viejas se descartan solas al vaciar.
4. Si el emulador no es el fork, `WatchBlock` contesta vacío y **no pasa nada**: se sigue como hoy.
   Eso hay que tratarlo como el caso normal, no como un error.

## Cómo se comprueba que funciona

Sin esto, la comprobación es una opinión. Con el juego abierto y un Pokémon marcado:

1. `Probe --peek <hueco>` enseña el Shedinja.
2. Entrar y salir de un combate, que es lo que hace al juego reescribir el equipo.
3. `Probe --peek <hueco>` **tiene que seguir enseñando el Shedinja**, y el log del emulador tiene
   que llevar al menos un `El juego reescribio 0x...; marca repuesta`.

Ese renglón del log es la prueba de que el parche está haciendo algo, y no de que el juego no haya
tocado nada esta vez. Sin él, la prueba no vale.

## El riesgo, dicho

Este parche **escribe en la memoria del juego desde un hilo propio, cinco veces por segundo**, sin
que nadie se lo pida en ese momento. Es más de lo que el fork hacía hasta ahora, donde toda
escritura venía de una petición explícita. Las tres cosas que lo acotan:

- solo escribe en las regiones de la lista blanca que ya existía;
- solo escribe si la etiqueta cuadra, o sea si el hueco sigue llevando a quien creemos;
- y la lista está limitada a doce entradas y se vacía entera cada vez que se actualiza.

Aun así, es la parte que hay que mirar con lupa al revisarlo.
