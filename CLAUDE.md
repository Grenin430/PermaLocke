# PermaLocke

> **Cerebro de Claude:** antes de explorar el código, leer `Obsidian/PermaLocke Brain/00 - Inicio.md` (mapa de código,
> flujos, conversaciones anteriores, trampas). Al acabar un trabajo, actualizar allí el historial y la nota afectada.

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

El diario completo (Fase 1 → §179) está en `Obsidian/PermaLocke Brain/Archivo - estado de CLAUDE.md (2026-09-24).md`, y cada
cambio tiene su § en `docs/ARCHITECTURE.md` (índice en `Obsidian/PermaLocke Brain/Índice de ARCHITECTURE.md`). Lo que se
hizo en cada conversación está en `Historial de conversaciones.md` del cerebro. Casi todo está hecho y visto en la app, pero
muchas cosas aún **no se han jugado**: no dar nada por verificado sin mirar su §.

Trampas que ya costaron caro y hay que tener siempre presentes:
- **Barrer memoria tumba Azahar.** Nunca buscar en bucle ni sin partida guardada. Los cierres del amigo eran del fork: los
  arregló el parche 4 (§173), confirmado por el jugador.
- **En el juego se escribe por identidad (PID), nunca por posición**: las copias del equipo tienen órdenes distintos (§96).
  Los PS de verdad están en `0x1E4 + 0x158`; el espejo `0x330128E4` el juego no lo lee (§98, §99).
- **Un campo solo vale donde la estructura está identificada** (un Ledyba evolucionó por leer `Stat_Level` donde no era, §53).
- **PKHeX descifra el array en el sitio**: darle siempre una copia (§97, Huevo Malo).
- **Aislar la run no aísla la partida**: `PlayerSave` encuentra el save de Azahar de verdad. Las pruebas visuales se hacen con
  `--sin-juego`, y nada destructivo en copias.
- **La carpeta de prueba `Desktop\PermaLocke prueba` tiene su propio `Data/`**: los cambios de JSON hay que copiarlos allí.
  Nunca desplegar con PermaLocke o Azahar abiertos.
- **Kit pixel:** `PixelPanel` y `PixelText` no reciben clics, así que toda plantilla interactiva necesita
  `Background="Transparent"` en la raíz. `PixelPanel` no pinta por debajo de 6×5 celdas. La fuente no tiene «≤», «→» ni «−» (§177).
- **WPF:** solo una `Application` por proceso en las pruebas (todo en `HomeViewTests`). Un `ComboBox` editable necesita
  `PART_EditableTextBox`. Los comentarios XML no admiten `--`.
- **Constantes de pk3DS clavadas** (807 especies, 233 habilidades, 728 movimientos): los límites salen de las tablas del mundo
  instalado (`WorldLimits`, `InstalledWorld`). Las sondas deben llamar a `InstalledWorld.ApplyQuietly` (§91).
- **Detectable no es detectado**: el vigilante empareja por PID contra lo registrado en la run (§56, §68).

Último: **plan del próximo torneo** en marcha (nota del cerebro «Plan del próximo torneo»). Paso 1 (§194): subir solo los eventos nuevos (`15-eventos-y-limpieza.sql`) y LIMPIEZA en Admin; SQL probados con `tools/supabase/pruebas/probar.sh`. Paso 2 (§195): traer la partida de otra carpeta. Paso 3 (§196): actualización automática desde GitHub Releases (`tools/publicar-actualizacion.ps1`). Antes: **Admin ampliado** (§193): FICHA de cada jugador y órdenes que aplica su app (revivir, marcar caído, rutas, pruebas, dar objeto/Pokémon, mensajes, cerrar el juego), PAUSAR TORNEO y REGLAS oficiales por el servidor (`14-reglas.sql`). Antes: **antitrampas de recarga** (§192): sin estados guardados, PermaLocke no se cierra con el juego abierto y Azahar muere con él, jugar fuera y cerrar en combate quedan anotados solo para Admin. Antes: **la app avisa del reinicio desde Admin** (§191): reiniciar solo archiva en el servidor; el jugador pulsa HOME →
EMPEZAR DE CERO. Antes: **la carta de cada captura vuela al álbum** (§190): al contar el juego una captura salvaje (al acabar el
combate), una ball pixel donde está el entrenador suelta la carta real del Pokémon y entra en un álbum pequeño que
llega y se va por la derecha; `--ensayar-captura`, ajuste «Carta al capturar». Antes: **GACHA con más golpe** (§189): racha exprés ×2,5 hasta abrirse, SALTAR (botón, máquina, ESPACIO), tensión,
«¡SUBE!», fogonazo, confeti, monedas y sellos LEGENDARIO/VARIOCOLOR; nada antes de abrirse depende del tier. Antes: **la
carta en la mano del ÁLBUM iba a trompicones** (§188): el `Color` de WPF calcula scRGB con `Math.Pow`, así
que el dibujo pixel usa `PixelColour`; la mano pasó de ~100 ms a 4 ms por fotograma y va a 60 fps si puede. Antes: **ÁLBUM
premium** (§187): carpeta de piel con pestañas y luz, página que gira en perspectiva, acabados por
rareza (holo, inversa, dorada, polícroma) y la carta en la mano en 3D (vuela, se inclina, se da la vuelta). Antes: **ÁLBUM** (§186): las cajas como carpeta de cartas TCG pixel (3×3 o 4×4), rareza por tier del gacha, variocolor
holográfica, caídas arrugadas y medio quemadas; solo lectura. Antes: **«Jugando a PermaLocke» en Discord** (§185): tubería local de Discord, id en `discordApp` de `Data/torneo.json`;
el estado propio de Azahar se apaga en su `qt-config.ini`. Antes: **lluvia de sangre** (§184): un wipe hace llover sangre 12 s en el emulador de quien lo sufre y de los
demás; tabla `13-lluvias.sql`. Antes: **fantasmas** (§183): la muerte de un Pokémon viaja por el servidor y cruza como fantasma el emulador de los
demás; tabla `12-fantasmas.sql`. Antes: **las pruebas salen en LOGROS sin guardar** (§182): miran también la mochila viva. Antes: **Pokémon que te sigue** (§181): plugin 3GX de otro autor en `Emulator/follower/`, instalado al pulsar JUGAR, con
interruptor en CONFIGURACIÓN. Antes: **iconos variocolor** (§180). El cartucho no los tiene: `Data/variocolor.json` (generado por `RomTool variocolor`
desde renders de Showdown) repinta el icono de la ROM del jugador. Aproximado; 1123 iconos con tabla, 6 sin referencia.

---

## Comandos

```bash
dotnet build
dotnet test
# la carpeta que se le pasa a otro jugador: exe autocontenido + Data + Emulator.
# Comprueba lo que acaba de escribir y lanza si falta algo. NO incluye ROM ni tu partida.
pwsh -File tools/publicar.ps1   # o powershell -File, funciona en los dos
pwsh -File tools/publicar.ps1 -Destino "otra/ruta"
# deja tambien <destino>.zip listo para compartir; -SinZip para no hacerlo. Empaquetar una carpeta ya publicada:
powershell -File tools/empaquetar.ps1 -Carpeta "ruta/publicada" -Zip "PermaLocke.zip"
# las carpetas del Escritorio: exe nuevo en "PermaLocke prueba" y carpeta+zip nuevos en "PermaLocke para amigos".
# Sin -ExecutionPolicy Bypass Windows bloquea el script (politica de ejecucion); Bypass solo vale para esa ejecucion.
powershell -ExecutionPolicy Bypass -File tools/desplegar.ps1 -Prueba -Amigos

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
dotnet run --project tools/PermaLocke.RomTool -- sprites --sheets
# colores variocolor de cada icono (Data/variocolor.json) desde renders de Showdown; hojas de revisión en %TEMP%/permalocke-variocolor
dotnet run --project tools/PermaLocke.RomTool -- variocolor

# los tres iniciales de un mod ya generado o instalado; sin carpeta mira todo Randomized/
dotnet run --project tools/PermaLocke.RomTool -- iniciales
dotnet run --project tools/PermaLocke.RomTool -- iniciales "ruta/al/mod"

# pone en espanol los nombres que un mod base solo trae en ingles. Sin --escribir solo mide.
dotnet run --project tools/PermaLocke.RomTool -- traducir
dotnet run --project tools/PermaLocke.RomTool -- traducir --escribir

# especies que son primera etapa de una linea de tres, contra la tabla del cartucho
dotnet run --project tools/PermaLocke.RomTool -- evoluciones "ruta/a/a/0/1/4"

# la tabla de evoluciones en crudo, agrupada por metodo; con un metodo, solo ese
dotnet run --project tools/PermaLocke.RomTool -- evo-dump "ruta/a/a/0/1/4"
dotnet run --project tools/PermaLocke.RomTool -- evo-dump "ruta/a/a/0/1/4" 19
dotnet run --project tools/PermaLocke.RomTool -- randomize 20260818 --install

# EV de la partida real; --probar demuestra la escritura SOBRE UNA COPIA
dotnet run --project tools/PermaLocke.Probe -- --ev
dotnet run --project tools/PermaLocke.Probe -- --ev --probar

# lo que el recuerda-movimientos ofrece a cada uno del equipo; --probar escribe SOBRE UNA COPIA
dotnet run --project tools/PermaLocke.Probe -- --recordar
dotnet run --project tools/PermaLocke.Probe -- --recordar --probar

# quien lleva una especie, o que lleva un entrenador, en un mod GENERADO O INSTALADO
dotnet run --project tools/PermaLocke.RomTool -- quien-lleva 246 "ruta/al/mod/romfs/a/1/0/7"
dotnet run --project tools/PermaLocke.RomTool -- entrenador 473 "ruta/al/mod/romfs/a/1/0/7"

# id de un objeto por su nombre, para no teclearlo de memoria
dotnet run --project tools/PermaLocke.Probe -- --objeto-find "Amuleto Iris"

# auditoría de la run: puntos, muertes, cap por etapa y cobertura de PID
dotnet run --project tools/PermaLocke.Probe -- --run

# PID que faltan; --probar lo demuestra SOBRE UNA COPIA, --arreglar escribe de verdad
dotnet run --project tools/PermaLocke.Probe -- --pids
dotnet run --project tools/PermaLocke.Probe -- --pids --probar
dotnet run --project tools/PermaLocke.Probe -- --pids --arreglar

# etapas marcadas a mano; con un número las corrige, dejando su evento
dotnet run --project tools/PermaLocke.Probe -- --etapas
dotnet run --project tools/PermaLocke.Probe -- --etapas 0

# cada cara de la ruleta contra una COPIA; la partida no se toca nunca
dotnet run --project tools/PermaLocke.Probe -- --ruleta
dotnet run --project tools/PermaLocke.Probe -- --ruleta --probar

# un hueco que el juego dibuja como huevo: dice si es un huevo o un Huevo Malo
dotnet run --project tools/PermaLocke.Probe -- --huevo
dotnet run --project tools/PermaLocke.Probe -- --huevo-copias
dotnet run --project tools/PermaLocke.Probe -- --huevo-diff "Saves/backup/<captura>.bin" 1
# reconstruye el hueco desde la captura previa; --probar lo hace SOBRE UNA COPIA
dotnet run --project tools/PermaLocke.Probe -- --huevo --arreglar "Saves/backup/<captura>.bin" --probar
dotnet run --project tools/PermaLocke.Probe -- --huevo --arreglar "Saves/backup/<captura>.bin"

# la tabla de mapas del juego (mapa -> mundo -> zona) que usa la regla de primer encuentro
dotnet run --project tools/PermaLocke.RomTool -- mapas 7 13 28
dotnet run --project tools/PermaLocke.RomTool -- mapas --escribir

# dónde guarda el juego el mapa y la posición; con la partida recién guardada y sin moverse
dotnet run --project tools/PermaLocke.Probe -- --situacion
dotnet run --project tools/PermaLocke.Probe -- --situacion cargada
dotnet run --project tools/PermaLocke.Probe -- --situacion firma

# PRUEBA: todo lo que genera el juego sale variocolor (code.ips junto al code.bin del mod). Con Azahar cerrado.
dotnet run --project tools/PermaLocke.Probe -- --shiny-siempre
dotnet run --project tools/PermaLocke.Probe -- --shiny-siempre quitar

# un Pokémon con habilidad y movimientos del mod de gen 8-9, para probarlos (§134). Azahar cerrado;
# copia la partida, relee y lo registra en la run como dado por el admin. --probar lo hace SOBRE UNA COPIA
dotnet run --project tools/PermaLocke.Probe -- --dar-mod 964 60 278 857,834,812,776 --probar
dotnet run --project tools/PermaLocke.Probe -- --dar-mod 984 60 281 838,861,915,866 --objeto 960

# el dibujo del ÁLBUM y del GACHA sin Windows (también en la nube); con la variable deja PNG para mirarlos
dotnet test tools/PermaLocke.PixelCheck
PERMALOCKE_PIXEL_DIR=/ruta dotnet test tools/PermaLocke.PixelCheck

# entregados en un wonder trade y contados todavía como vivos
dotnet run --project tools/PermaLocke.Probe -- --intercambiados
dotnet run --project tools/PermaLocke.Probe -- --intercambiados --arreglar
```

SDK requerido: .NET 10 (instalado: 10.0.400).

## Distribución vigente — 2026-09-20

El usuario descarta el reparto por Drive: cada amigo juega en una copia local independiente. Ver docs/DISTRIBUCION-LOCAL.md. PermaLocke.local activa este perfil; no copiar Config, Saves ni Emulator/user de una partida. La distribución nueva incluye la expansión y preserva sus créditos. Las notas de Drive anteriores son históricas.


