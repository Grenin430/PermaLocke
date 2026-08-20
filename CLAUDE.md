# PermaLocke

Gestor/dinamizador de Nuzlockes para **Pokémon Ultra Luna** (3DS, jugado en el emulador
Azahar). Aplicación de escritorio Windows en C# / .NET 10 / WPF / MVVM.

El nombre del proyecto es **PermaLocke**, siempre y en todas partes: namespaces, assemblies,
carpetas, ejecutables, UI, documentación y comentarios. Nunca `BxnnyLocke` ni ninguna variante.

---

## Reglas absolutas de este repositorio

### 1. `Locke/` es SOLO LECTURA

La carpeta `Locke/` contiene una instalación ajena (BxnnyLocke) con **una partida en curso del
usuario**. Es material de referencia para analizar formatos de datos.

**Nunca escribir, mover, renombrar ni borrar nada dentro de `Locke/`.** Solo lectura.

No es software del usuario: participa en esa competición, no la desarrolla. Por tanto:
- Se pueden analizar sus **formatos de fichero** y su **concepto de arquitectura** (interoperar
  y aprender es legítimo).
- **No** se copia su código, ni se redistribuye su emulador instrumentado, ni sus assets.
  PermaLocke construye su propia instrumentación.

### 2. La ROM vanilla nunca se modifica

Se trabaja siempre sobre copias o, preferentemente, sobre capas de mods (LayeredFS).
Si la randomización falla: limpiar temporales, no tocar el original, registrar en `Logs/`.

### 3. Nada falso

Prohibido: randomización simulada, anti-cheat de mentira, botones que aparentan funcionar,
datos inventados. Si algo no se puede implementar de forma fiable todavía, se deja una
abstracción limpia y se documenta su estado en `docs/ARCHITECTURE.md`.

### 4. Nada de cambios silenciosos de estado

Ningún punto, Pokémon o regla cambia sin generar un `GameEvent` auditable. La UI nunca
escribe `points = X` directamente; pasa siempre por servicios de dominio.

---

## Licencia: PermaLocke es GPLv3

Decisión tomada y no negociable por dependencias: el proyecto usa **PKHeX.Core** (GPLv3) y
**pk3DS.Core** (GPLv3). La GPL es vírica, así que PermaLocke se distribuye bajo **GPLv3** con
el código fuente disponible. Para un proyecto entre amigos no supone ningún problema.

Consecuencia práctica: no se puede integrar código con licencia incompatible.

---

## Estructura

```
PermaLocke/
├── PermaLocke.slnx                 formato de solución XML de .NET 10
├── src/
│   ├── PermaLocke.Core/            dominio + servicios (sin WPF, sin IO concreto)
│   ├── PermaLocke.Rules/           motor de reglas (sin WPF)
│   ├── PermaLocke.Randomizer/      randomización de ROM (sin WPF)
│   ├── PermaLocke.GameLink/        lectura/escritura del juego (save, memoria)
│   ├── PermaLocke.Data/            persistencia: JSON + SQLite + sync
│   ├── PermaLocke.Infrastructure/  logging, hashes, rutas, DI
│   ├── PermaLocke.App/             WPF jugador
│   └── PermaLocke.Admin/           WPF administrador
├── third_party/pk3DS.Core/       pk3DS recortado a net10.0, GPLv3, ver su README
├── tools/PermaLocke.RomTool/     herramienta de desarrollo contra la ROM real
├── tests/
├── docs/ARCHITECTURE.md
├── Data/    Config/    Logs/    ROM/    Randomized/    Saves/
└── Locke/   ← SOLO LECTURA, material ajeno
```

Dependencias permitidas (dirección única):

```
App ─┐
     ├─→ Core ─→ (nada)
Admin┘     ↑
        Rules, Randomizer, GameLink, Data, Infrastructure ─→ Core
```

`Core`, `Rules`, `Randomizer` y `GameLink` **no referencian WPF** bajo ninguna circunstancia.
`Randomizer` depende además de `third_party/pk3DS.Core`, que se recortó a `net10.0` justamente
para que esa norma siga cumpliéndose: el original arrastra WinForms.
`Admin` no duplica lógica de negocio: consume los mismos servicios que `App`.

---

## Convenciones

- **Idioma**: código, tipos y comentarios en inglés. UI y documentación de usuario en español.
- **Configuración fuera del código**: puntos, precios, probabilidades, reglas y logros viven en
  JSON bajo `Data/`. Nada de valores de juego hardcodeados en la UI.
- **MVVM estricto**: cero lógica de negocio en XAML o code-behind. Los ViewModels llaman a
  servicios inyectados.
- **DI**: `Microsoft.Extensions.DependencyInjection` desde el arranque de cada app.
- **Logging**: `Microsoft.Extensions.Logging` a fichero en `Logs/`. Al usuario final no se le
  muestran stack traces salvo en modo avanzado.
- **Async**: toda IO (ROM, save, red, SQLite) es asíncrona. Nada de bloquear el hilo de UI.
- **Tests**: xUnit. Obligatorios en puntos, reglas, gacha (seed reproducible) y eventos.

---

## Estado actual

**Fase 1 completada.** Solución con 8 proyectos + 3 de tests, DI, logging a fichero, shell WPF
con sidebar navegable y HOME. Compila sin warnings y arranca. `Locke/` sigue en el disco como
material de referencia de solo lectura; la ROM vanilla está en `ROM/`.

ROM verificada: `Pokemon Ultra Moon (Europe)`, TitleID `00040000001B5100`, **desencriptada**
(NoCrypto = true), RomFS ~3,7 GB. Ese TitleID es el que necesita la carpeta LayeredFS de Azahar.

**Fase 2 completada.** Creación de runs con detección y validación de la ROM, seed reproducible,
log de eventos encadenado por hash en SQLite, `run.json` por run, servicio de puntos y HOME
conectado a datos reales. 22 tests en verde.

Azahar instalado en `Azahar/`. Trae `scripting/citra.py`, que documenta su **servidor RPC UDP
en 127.0.0.1:45987** con ReadMemory / WriteMemory / ProcessList / SetGetProcess. Es la vía de
integración con el juego: no hace falta forkear el emulador. Ver `docs/ARCHITECTURE.md` §6.

**Fase 3 completada (capa de dominio).** Rule Engine con 7 reglas, modos por regla y
excepciones que se registran en vez de borrar el veredicto que levantan. 59 tests en verde.

RPC de Azahar **verificado contra el juego real**: lectura de memoria funcionando, escritura
aceptada. Cliente en `PermaLocke.GameLink`, sonda en `tools/PermaLocke.Probe`.

**Fase 3 conectada a la interfaz.** `EncounterService` (en `PermaLocke.Rules/Services`, la única
capa que conoce legítimamente el dominio y las reglas a la vez) convierte un intento de captura
en veredictos, un Pokémon persistido y eventos de auditoría. Diálogo de registro en la App con
evaluación en vivo. Nombres de especie desde **PKHeX.Core** (807 especies, español). 73 tests.

Limitación conocida: `LevelCapRule` está implementada y testeada, pero `RuleContext.LevelCap`
es siempre null porque **no existe seguimiento de etapa** (pruebas superadas). Empezará a
disparar cuando se registre el progreso de pruebas.

**Lectura en vivo del juego resuelta y verificada.** El equipo se localiza barriendo heap y
linear y ofreciendo cada offset a PKHeX; se encontró en `0x330128E4`, estable tras reiniciar el
emulador. `AzaharGameStateProvider` cachea la dirección, revalida en cada lectura y vuelve a
barrer si deja de cuadrar. `GameLinkMonitor` consulta cada 3 s y HOME muestra el equipo real.

Trampa documentada: hay que llamar a `SetGetProcess` antes de leer. Sin eso el servidor responde
igual con datos que no son del juego, y solo se ve en el log del emulador.

**Detección automática en marcha.** `GameWatcher` compara el equipo del juego con lo registrado
en la run, emparejando por **PID** (sobrevive a motes, subidas de nivel y evoluciones). Las
muertes se registran solas; los Pokémon sin registrar se anuncian en HOME y abren el diálogo de
captura prerrellenado, **zona incluida**.

Dos fallos que solo aparecieron probando contra el juego real, ambos corregidos y con test:
1. El buscador exigía igualdad exacta del nombre de entrenador y descartaba el equipo del propio
   jugador (la run decía `Grenin`, el juego `Grenin430`). Ahora es preferencia, no requisito, y
   HOME avisa de la discrepancia.
2. Basura del heap pasaba por Pokémon. El filtro definitivo es `PK7.ChecksumValid`; los límites
   de PS siguen haciendo falta aparte, porque las estadísticas de combate quedan **fuera** de la
   zona cubierta por el checksum.

**Fase 7-8: randomizador desbloqueado.** La randomización por LayeredFS está verificada en el
juego. pk3DS.Core se vendoriza recortado y compila como `net10.0` sin WinForms; el extractor de
RomFS es propio. Norma que salió de dos fallos reales: **se parchean bytes en su sitio, no se
regeneran estructuras con los escritores de pk3DS**, y todo fichero se relee y se verifica antes
de publicarse. Detalle completo en `docs/ARCHITECTURE.md` §19.

**Randomizador completo (fases 7-8).** Seis módulos: salvajes, objetos del suelo, iniciales y
fósiles, entrenadores, datos de Pokémon y tiendas. Motor en `PermaLocke.Randomizer`, herramienta
en `tools/PermaLocke.RomTool` y sección en la App. Salvajes y datos de Pokémon verificados en el
juego; el resto genera y se relee bien, pero falta probarlo jugando. Detalle en
`docs/ARCHITECTURE.md` §19-21.

**Mochila localizada por su estructura y verificada (2026-08-19).** El botón de Caramelos Raros
de MISCELÁNEA no era de fiar: buscaba el valor empaquetado del objeto por toda la memoria y daba
falsos positivos —con cero caramelos encontraba uno—, además de ser incapaz de añadir un objeto
que el jugador no lleva, porque entonces no hay hueco que encontrar.

Ahora se localiza **el bloque**: los siete bolsillos van seguidos en 0xE28 bytes y justo detrás
hay una tabla con un puntero a cada uno, así que una candidata se acepta solo si esos siete
punteros apuntan cada uno a su propio bolsillo. Sobre los 96 MB de estado vivo sale **un bloque y
cero falsos positivos**, en `0x33011934`, la misma dirección del §18. Verificado en el juego:
la lectura coincide con la mochila real, la dirección aguanta reiniciar el emulador, y escribir
999 Caramelos Raros en un bolsillo que **no tenía entrada de caramelos** se ve en pantalla.
Toda escritura se relee antes de darla por buena. Detalle en `docs/ARCHITECTURE.md` §22.

**El emulador viaja con la app.** `Emulator/` lleva el fork propio; la app lo detecta junto a su
ejecutable, lo usa en modo portátil y le activa el servidor RPC solo. Verificado sobre el
publicado. Los binarios no se versionan: se reconstruyen desde `github.com/Grenin430/azahar`, y
solo se versiona `Emulator/LEEME.md` con su procedencia y su licencia. Ver §25.

**Randomizador: tipos y evoluciones desactivados (2026-08-19).** A petición del usuario, las
líneas evolutivas y los tipos se quedan como el cartucho; el resto sigue randomizado. Apagar
esos dos interruptores destapó dos fallos que nadie había visto porque **nunca se había apagado
un módulo**: la carpeta del mod no se vaciaba, así que el fichero de la generación anterior
seguía activo y el informe cantaba «0 evoluciones» mientras el juego las tenía randomizadas; y
los cinco aspectos de los datos de Pokémon compartían una sola fuente aleatoria, de modo que
apagar uno desplazaba a los demás. Ambos corregidos. Detalle en `docs/ARCHITECTURE.md` §27.

**Fase 4 empezada: el gacha (2026-08-19).** Tres banners con rarezas por **rango de total de
estadísticas base**, no por listas de Pokémon, así que ninguna especie se queda fuera y sigue
valiendo con la ROM randomizada (el módulo de datos baraja las estadísticas pero conserva el
total). `RomTool species` genera `Data/species.json` desde el cartucho. Cada tirada se calcula
con la seed de la run y su número, así que **se puede recomputar y auditar**.

**El Pokémon va al PC del juego**, escribiendo en el fichero de partida con PKHeX: copia previa,
y se relee para confirmarlo. Exige el juego cerrado, igual que la referencia. Cómo se supo que
esa era la vía: analizando los backups de BxnnyLocke, que son saves completos de 445.440 bytes.
Eso ahorró localizar las cajas en memoria. Detalle en `docs/ARCHITECTURE.md` §26.

Pantalla con animación abstracta del ultraespacio: cinco portales de color, uno por tier, sin
ningún asset con copyright. **Precios a cero mientras se prueba**; los reales (100/225/300) se
ponen cuando existan los logros, que son la fuente de puntos y hoy no existe ninguna.

**Siguiente.** Logros (la fuente de puntos), tienda, y probar en partida nueva iniciales,
entrenadores y tiendas.

Lo que NO está resuelto todavía y no debe darse por hecho (detalle en `docs/ARCHITECTURE.md`):

| Tema | Estado |
|---|---|
| Lectura del equipo en vivo | **RESUELTA Y VERIFICADA** contra el juego real |
| Detección automática de capturas y muertes | **Sin validar** — falta una captura real |
| Randomización vía LayeredFS | **RESUELTA Y VERIFICADA** en el juego — salvajes y textos |
| Escritura en el juego (Shedinja, cap, objetos) | **RESUELTA Y VERIFICADA** — exige el fork propio de Azahar |
| Mochila del juego (leer, poner cantidad, añadir lo que no llevas) | **RESUELTA Y VERIFICADA** en el juego — ver `ARCHITECTURE.md` §22 |
| Zona actual del jugador (regla de las Poké Balls) | **RESUELTA Y VERIFICADA** — es el área de `encdata`, leída anclando a la mochila; ver `ARCHITECTURE.md` §23 |
| Regla de las Poké Balls (impedir la captura, no solo registrarla) | **IMPLEMENTADA Y APAGADA** — cableada y con tests, sin probar en el juego; ver `ARCHITECTURE.md` §24 |
| Gacha (motor, probabilidades, reproducible) | **HECHO** — 194 tests; sin verse funcionando en la app |
| Entrega del Pokémon al PC del juego | **VERIFICADA sobre copia del save**; falta hacerlo en la partida real. Exige el juego cerrado |
| Logros, tienda y sincronización | **SIN EMPEZAR** — hoy no hay forma de ganar puntos |
| API concreta de pk3DS.Core | **VERIFICADA** contra la ROM real — ver `ARCHITECTURE.md` §19 |

---

## Comandos

```bash
dotnet build
dotnet test
dotnet publish src/PermaLocke.App -c Release -r win-x64 --self-contained true

# randomizador contra la ROM real (escribe en Randomized/, no en Azahar)
dotnet run --project tools/PermaLocke.RomTool -- inspect
dotnet run --project tools/PermaLocke.RomTool -- randomize 20260818
dotnet run --project tools/PermaLocke.RomTool -- dump 20260818 "Ruta 1"
dotnet run --project tools/PermaLocke.RomTool -- statics 20260818
dotnet run --project tools/PermaLocke.RomTool -- trainers 20260818
dotnet run --project tools/PermaLocke.RomTool -- pokemon 20260818
dotnet run --project tools/PermaLocke.RomTool -- shops --gen
dotnet run --project tools/PermaLocke.RomTool -- zones
dotnet run --project tools/PermaLocke.RomTool -- species
dotnet run --project tools/PermaLocke.RomTool -- randomize 20260818 --install
```

SDK requerido: .NET 10 (instalado: 10.0.400).
