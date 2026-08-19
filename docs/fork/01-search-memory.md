# Parche 1 para el fork de Azahar — búsqueda de memoria nativa

**Estado: escrito contra el código real del fork, SIN COMPILAR NI PROBAR.** Yo no puedo
compilar C++ aquí. Lo verificas tú, igual que hiciste con `NEW_LINEAR_HEAP`.

Repositorio: `github.com/Grenin430/azahar`. Ficheros que toca, los dos ya conocidos:
`src/core/rpc/packet.h` y `src/core/rpc/rpc_server.{h,cpp}`.

## Qué resuelve

Hoy PermaLocke localiza el equipo, la mochila y todo lo demás **barriendo memoria desde fuera**:
miles de peticiones UDP de 4 KB para leer 96 MB, unos 6 segundos por barrido. Además hay un
aviso en `docs/ARCHITECTURE.md` §6 de que una ráfaga ininterrumpida de decenas de miles de
peticiones **tumbó el emulador**, y por eso el buscador cede el hilo cada 256 peticiones.

Con este parche la búsqueda ocurre **dentro** del emulador, a velocidad de memoria, y viaja un
solo paquete de ida y otro de vuelta. De segundos a milisegundos, y desaparece la ráfaga que
tumbaba el emulador.

Lo que **no** resuelve: seguir sin saber la *estructura* de la mochila. Eso necesita enganchar
código del juego, que es el parche 2 y es harina de otro costal.

## 1. `src/core/rpc/packet.h`

Añadir el tipo nuevo al enum:

```cpp
enum class PacketType : u32 {
    Undefined = 0,
    ReadMemory = 1,
    WriteMemory = 2,
    ProcessList = 3,
    SetGetProcess = 4,
    SearchMemory = 5,
};
```

Y, junto a las demás constantes, el tope de resultados que caben en una respuesta:

```cpp
// Una respuesta lleva el número de aciertos y luego una dirección por acierto.
constexpr u32 MAX_SEARCH_HITS = (MAX_PACKET_DATA_SIZE - sizeof(u32)) / sizeof(u32);
```

## 2. `src/core/rpc/rpc_server.h`

Declarar el manejador junto a los otros cuatro:

```cpp
    void HandleSearchMemory(Packet& packet, u32 address, u32 region_size);
```

## 3. `src/core/rpc/rpc_server.cpp`

### Formato de la petición

Se respeta la convención que ya usa el servidor: los dos primeros u32 son `arg1` y `arg2`.

| Offset | Campo |
|---|---|
| 0x00 | `arg1` = dirección de inicio |
| 0x04 | `arg2` = cuántos bytes recorrer |
| 0x08 | `stride`: cada cuántos bytes se prueba (4 para valores alineados, 2 para barrer fino) |
| 0x0C | `pattern_size`: longitud del patrón, máximo 64 |
| 0x10 | el patrón, `pattern_size` bytes |
| 0x10 + n | la máscara, `pattern_size` bytes: 0xFF obliga a coincidir, 0x00 es comodín |

La máscara es lo que permite buscar «el objeto 50 con cualquier cantidad» en una sola pasada,
que es justo lo que hoy resuelvo con heurística desde C#.

### Respuesta

`u32` con el número de aciertos, y a continuación esa misma cantidad de `u32` con las
direcciones. Tope `MAX_SEARCH_HITS` (255). Si hay más, se devuelven los primeros y el cliente
puede continuar desde la última dirección.

### El manejador

```cpp
void RPCServer::HandleSearchMemory(Packet& packet, u32 address, u32 region_size) {
    const auto request = packet.GetPacketData();

    u32 stride = 0;
    u32 pattern_size = 0;
    std::memcpy(&stride, request.data() + (sizeof(u32) * 2), sizeof(stride));
    std::memcpy(&pattern_size, request.data() + (sizeof(u32) * 3), sizeof(pattern_size));

    constexpr u32 MAX_PATTERN_SIZE = 64;
    if (stride == 0 || pattern_size == 0 || pattern_size > MAX_PATTERN_SIZE ||
        (sizeof(u32) * 4) + (pattern_size * 2) > MAX_PACKET_DATA_SIZE) {
        packet.SetPacketDataSize(0);
        packet.SendReply();
        return;
    }

    std::array<u8, MAX_PATTERN_SIZE> pattern{};
    std::array<u8, MAX_PATTERN_SIZE> mask{};
    std::memcpy(pattern.data(), request.data() + (sizeof(u32) * 4), pattern_size);
    std::memcpy(mask.data(), request.data() + (sizeof(u32) * 4) + pattern_size, pattern_size);

    // Se lee por trozos con solape, para no perder una coincidencia partida entre dos trozos.
    constexpr u32 CHUNK = 0x10000;
    std::vector<u8> chunk(CHUNK + MAX_PATTERN_SIZE);
    std::vector<u32> hits;

    Kernel::Process* process = nullptr;
    if (selected_pid != 0xFFFFFFFF) {
        auto found = system.Kernel().GetProcessById(selected_pid);
        if (!found) {
            LOG_ERROR(RPC_Server, "Selected process does not exist.");
            packet.SetPacketDataSize(0);
            packet.SendReply();
            return;
        }
        process = found.get();
    } else {
        LOG_ERROR(RPC_Server, "No target process selected, memory access may be invalid.");
    }

    for (u32 offset = 0; offset < region_size && hits.size() < MAX_SEARCH_HITS; offset += CHUNK) {
        const u32 to_read = std::min(CHUNK + pattern_size - 1, region_size - offset);

        if (process != nullptr) {
            system.Memory().ReadBlock(*process, address + offset, chunk.data(), to_read);
        } else {
            system.Memory().ReadBlock(address + offset, chunk.data(), to_read);
        }

        for (u32 i = 0; i + pattern_size <= to_read; i += stride) {
            bool match = true;
            for (u32 b = 0; b < pattern_size; ++b) {
                if ((chunk[i + b] & mask[b]) != (pattern[b] & mask[b])) {
                    match = false;
                    break;
                }
            }
            if (match) {
                hits.push_back(address + offset + i);
                if (hits.size() >= MAX_SEARCH_HITS) {
                    break;
                }
            }
        }
    }

    const u32 count = static_cast<u32>(hits.size());
    std::memcpy(packet.GetPacketData().data(), &count, sizeof(count));
    std::memcpy(packet.GetPacketData().data() + sizeof(count), hits.data(), count * sizeof(u32));

    packet.SetPacketDataSize(sizeof(count) + (count * sizeof(u32)));
    packet.SendReply();

    LOG_INFO(RPC_Server, "SearchMemory 0x{:08X}+0x{:X}: {} aciertos", address, region_size, count);
}
```

### Validación y despacho

En `ValidatePacket`, añadir el caso junto a los otros cuatro:

```cpp
        case PacketType::SearchMemory:
```

En `HandleSingleRequest`, dentro del `switch`:

```cpp
        case PacketType::SearchMemory:
            if (arg2 > 0) {
                HandleSearchMemory(*request_packet, arg1, arg2);
                success = true;
            }
            break;
```

`ValidatePacket` ya exige `packet_size >= sizeof(u32) * 2`; esta petición manda bastante más,
así que no hace falta tocar ese umbral.

## Cabeceras

El manejador usa `std::vector` y `std::array`, y `Kernel::Process`. `rpc_server.cpp` ya incluye
`core/hle/kernel/process.h` y `core/memory.h`. Si el compilador se queja, añadir `<vector>`,
`<array>` y `<algorithm>`.

## Cómo comprobar que funciona, sin la app

Con el juego cargado, buscar el nombre del entrenador en UTF-16LE en el heap. Tiene que
devolver aciertos y hacerlo al instante. Del análisis de `docs/ARCHITECTURE.md` §6 sabemos que
el nombre aparece **10 veces**, así que es una prueba con resultado esperado y no una impresión.

## Riesgo

Bajo, y acotado: es un tipo de paquete nuevo. Si algo falla, falla al usarlo, no al arrancar, y
el Azahar oficial sigue funcionando porque desconoce el tipo 5 y lo rechaza en `ValidatePacket`.

Lo único que puede doler es el tiempo de bloqueo: una búsqueda sobre 64 MB ocupa el hilo del RPC
mientras dura. Como es memoria local, deberían ser milisegundos.
