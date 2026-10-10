---
tipo: lecciones
revisado: 2026-09-24
---
# Trampas y lecciones

Errores ya cometidos en este proyecto, con el § donde se cuentan ([[Índice de ARCHITECTURE]]). Las de WPF están en [[UI y kit pixel]].

## Memoria y emulador
- **Barrer memoria tumba Azahar.** Tres accesos no válidos en `azahar.exe+0x781693` llegaron de 3 a 6 s después de un barrido; en un solo día hubo 226 barridos (§ en `AzaharGameStateProvider`). Por eso: nada de barrer sin partida guardada, enfriamiento exponencial y nada de buscar en bucle. Las búsquedas anchas del combate congelaron el emulador dos veces (§114).
- La causa de fondo de los cierres del amigo era el **volcado de la caché de la GPU** en las lecturas RPC con OpenGL. Lo arregla el parche 4 (§173).
- **Identidad, no posición:** escribir por número de hueco manda el cambio al Pokémon de al lado, porque las copias tienen órdenes distintos. PID siempre por delante (§96). La única excepción es `--huevo --arreglar`, que va por EC (§97).
- **Un campo solo vale donde la estructura está identificada.** `Stat_Level` en el salto `0x1E4` leyó 145 y evolucionó un Ledyba (§53).
- **PKHeX descifra el array en el sitio.** Hay que darle una copia (`BattlePokemon.Parse`, §97 Huevo Malo).
- Hay que llamar a `SetGetProcess` (`AttachTo`) antes de leer.
- El espejo `0x330128E4` no lo lee el juego: escribir PS ahí no hace nada (§98). Los PS de verdad están en `0x1E4+0x158` (§99).
- Una medida tomada en una sola situación no sostiene una regla general: la zona con el jugador quieto engañó (§55).
- `WorldLimits` es global: una sonda sin `InstalledWorld.ApplyQuietly` da diagnósticos falsos (§91).
- **Una verificación puede estar peor pensada que el código** (§65, §91).

## Kit pixel (§177)
- `PixelPanel` y `PixelText` tienen `IsHitTestVisible = false`. **Toda plantilla interactiva (Button, TabItem, ListBoxItem…) necesita `Background="Transparent"` en la raíz**, o no recibe clics. Así estaban JUGAR y las pestañas del visor.
- UI Automation `Invoke` se salta el hit-test: las capturas e2e no detectan este fallo.
- `PixelPanel` no pinta nada por debajo de 6×5 celdas (18×15 px a escala 1). Para marcas pequeñas se usa un `Border` liso.
- `PixelFont` no tiene «≤», «→» ni «−»: salen mal o como otro glifo.

## Datos y ROM
- Se parchean bytes en su sitio; no se regeneran estructuras con los escritores de pk3DS. Se relee lo escrito (§19).
- Constantes de pk3DS clavadas: 807 especies, 233 habilidades, 728 movimientos. Sale de las tablas del juego (§136, mod gen 8-9).
- Hay dos órdenes de IV en el repo, el de la tirada y el del lector: confundirlos dio 0 de 180 (§56).
- El icono de un objeto no es `id-1` a partir del 100: `ItemIconIndex` es una tabla comprobada y lanza si el objeto no está (§45).
- Un cruce de dos especies pasa todas las comprobaciones de la tabla de iconos. Solo se ve mirando (Volcanion/Hoopa, §88).
- Apagar un módulo del randomizador no quitaba su fichero del mod (§27).
- `Stage()` copiaba la vanilla encima de lo parcheado (§47).
- Renombrar con PKHeX subía los récords de capturas (§42).

## Proceso
- «Detectable no es detectado»: sin PID ni registro, la detección de muertes queda inerte y en silencio (§56, §68).
- Un comentario que avisa de un riesgo no lo impide: lo impide el código (§97).
- Una prueba destructiva en la partida de alguien deja por escrito qué destruyó (§59).
- Aislar la run no aísla la partida (`PlayerSave`).
- Un log filtrado no es un log vacío: Azahar va con `RPC_Server:Error` (§95).
- El binario instalado no siempre es el que se abre (`Emulator/` frente a `Nuevo_azahar/`, §95).
- Dos PermaLocke a la vez escriben dos veces en el mismo juego. Ahora hay un mutex (§159).

## Añadidas 2026-09-26 (plan del torneo, releases)
- **El código que actualiza es el único que no se puede arreglar con una actualización.** Un fallo en la descarga (§204: `TimeSpan.MinValue` en el bucle, desborde en el primer trozo) deja a esas versiones sin poder actualizarse: hay que pasarlas a mano. Todo el camino de actualizar (descarga, comprobación, instalación) tiene que ejecutarse en una prueba antes de publicarlo; no basta con probar los números.
- **`TimeSpan.MinValue`/`MaxValue` como «nunca»: no restarlos.** Usar `TimeSpan?` nulo.
- **Un cambio en cómo se leen las versiones llega una versión tarde.** Lo que decide si se ofrece una actualización es el código de la app **instalada**, no el de la nueva: la 1.0.3 comparaba tres cifras, así que no podía salir una 1.0.3.1 (hubo que hacer la 1.0.4). Antes de cambiar el formato de versión, de las notas o del paquete, pensar qué hacen con ello las apps que ya están repartidas.
- **Rehacer la rama `claude/...` desde `origin/main` tira lo que no se fusionó.** Un commit del cerebro subido después de aceptar un PR se perdió así y hubo que recuperarlo con `cherry-pick`. Antes de `git checkout -B … origin/main`, mirar `git log origin/main..HEAD`.
- **Windows PowerShell 5.1: `$PSScriptRoot` está vacío en los valores por defecto de `param`.** Calcular rutas por defecto en el cuerpo del script.
- **En cadenas de PowerShell, `"$var:"` es una variable con ámbito** y no compila: `"${var}:"`. Tumbó la 1.ª ejecución de la Action.
- **Antes de subir un workflow o un `.ps1` desde la nube, parsearlo con `pwsh`** (`dotnet tool install --global PowerShell`); los `${{ }}` sustituidos por un valor.
- **SQL Editor de Supabase**: ejecuta solo lo seleccionado; un trozo de una función da «unterminated dollar-quoted string». Avisa de «destructive operations» y «tabla sin RLS» por palabras dentro de funciones: falso positivo, **Run without RLS** (el otro botón añade un `alter table` que falla).
- **Desde la nube no se llega a Supabase** (proxy 403) pero sí a `api.github.com`: lo del servidor se verifica con consultas que ejecuta el usuario.
- **Releases de GitHub**: el zip va en «Attach binaries», no en la caja de las notas; el paquete de actualización pesa ~70-80 MB (el exe autocontenido ~160 MB sin comprimir); si pasa de 2 GB es el zip de amigos.

## Añadidas 2026-09-26
- **Una sección con `Needs` distinto de `None` pierde ~74 px de alto** por la franja del juego: un dibujo a celdas enteras puede bajar de 2 a 1 px/celda. Medir el alto disponible antes de fijar el tamaño en celdas (§187).
- **Nada que aparezca y desaparezca junto a una escena pixel puede ocupar sitio:** le quita alto, la escena cambia de píxeles por celda y «se aleja». La línea de estado del gacha hacía eso al salir el Pokémon; ahora va encima del suelo (§189).
- **Reiniciar una run desde Admin no toca el PC del jugador**: solo la archiva en el servidor. El jugador tiene que pulsar HOME → EMPEZAR DE CERO; la app se lo dice desde el §191.
- **El juego cuenta una captura al acabar el combate**, no al cerrarse la ball (récord 6, §118): lo que reaccione a una captura sale al volver al campo. La carta del §190 usa por eso una ball propia donde está el entrenador.
- **Gacha: nada antes de abrirse puede depender del tier** (§171). La racha exprés, SALTAR, la tensión y los golpes de subida van igual para todos; lo que escala con la rareza (sacudida, confeti, sellos) empieza al abrirse, cuando la ball ya lo dice (§189).
- **El `Color` de WPF es caro:** `Color.FromRgb`/`FromArgb` calculan scRGB con `Math.Pow`. En un bucle por píxel tumba los fps (la carta en la mano iba a ~100 ms por fotograma). En dibujo pixel, `PixelColour` o bytes (§188).
  Se volvió a caer en `PixelTheme.MapPixels` (1.0.8, 9 ms por fotograma) y en la niebla del cementerio (§216).
- **Listar procesos cuesta ~6 ms de CPU** (`GetProcessesByName` recorre la tabla entera, 390 procesos): nunca en temporizadores. Para la ventana del juego, `GameWindow.Handle` (con caché de HWND+pid); para saber si está abierto, `EmulatorProcess.IsRunning` (§216).
- **`RunContext.CurrentChanged` llega ya en el hilo de la interfaz** (§216): no hace falta envolver cada oyente en `InvokeAsync`, pero lo que ya lo hace sigue valiendo.
- **Dibujo pixel probado en Linux:** `tools/PermaLocke.PixelCheck` compila los ficheros puros, que pintan con `PixelColour` (el `Color` de sustituto queda para `RoomSprite.At` y `PixelScene`), y desde el §189 también `PixelScene` y la máquina de cápsulas con sustitutos de `WriteableBitmap` (`WpfImaging.cs`). Para que un dibujo nuevo entre ahí, no debe tocar más WPF que eso (usar `SmallFont`, `TypeColours`, `RoomSprite`, `CellCanvas`).
- **Desde la nube sí se compila:** el SDK no se baja de builds.dotnet.microsoft.com (bloqueado), pero Ubuntu lo trae: `apt-get update` y `apt-get install -y dotnet-sdk-10.0`. WPF compila con `-p:EnableWindowsTargeting=true`; no se ejecuta. Para ver dibujos pixel, compilar los ficheros puros en una consola con un `Color` falso y escribir PNG.
- La letra pequeña de 3×5 tiene una N que se lee D en palabras largas; en las cartas va una N de 4 celdas (`TcgCardArt.WideN`).
- **Estado de Discord que no sale:** primero, Ajustes de Discord > Privacidad de la actividad > «Compartir tu actividad detectada». Apagado, Discord acepta el estado y no lo enseña; el log de PermaLocke no puede avisar.
- Un log sin ninguna línea de un servicio nuevo suele ser que el exe no lleva ese código (otra rama, sin compilar), no que falle.

## Añadidas 2026-09-25
- **No guardar ficheros del repo con `Get-Content | Set-Content` de PowerShell 5.1**: lee como ANSI y rompió todas las tildes de `MainWindow.xaml` (se restauró con git). Usar sed/Edit.
- `perl -0pi -e 's|…|…|'` con `|` de delimitador y `||` o `\|\|` en el patrón o la sustitución: metió el bloque al principio de `Program.cs`. Para eso, Edit.
- Escalado de Windows: una celda pixel que se redondea hacia arriba hace el texto más ancho que el diseño (125 % → +20 %). Siempre `Floor`.
- «No está en INFORMACIÓN» no era un fallo del generador: solo listaba lo que cambia el fixer. Preguntar qué se espera ver antes de dar una lista por completa.
- **WPF: `Loaded` puede repetirse sin `Unloaded`** (volver a una sección). Suscribirse a `CompositionTarget.Rendering` en `Loaded` sin quitar antes duplica el manejador y la animación va x2/x3 (§210). Siempre `-=` antes de `+=`.
- **Action: `LevelCapLiveTests` puede fallar una vez en GitHub** (servidor RPC falso, tiempos en la máquina de GitHub; ya va en la colección sin paralelo). Pasa en local: `gh run rerun <id> --failed` y publica (1.0.4.7, 2026-09-27).

## Estudio de fotos de Hauoli con Pokémon de gen 8-9 (2026-09-27)
El tutorial del Poké Finder en Ciudad Hauoli (CRO `FinderStudioCapture`/`FinderStudioViewer`) se queda en negro si el
equipo lleva Pokémon de la expansión (especie > 807): el estudio carga los modelos del equipo y no sabe con esos. Pasa
igual sin PermaLocke abierto. Solución confirmada por el jugador: dejar los de gen 8-9 en la caja, entrar, y volverlos
a meter. La cámara de Azahar (1.0.5.6 la pone en blanco) no era la causa.

## Tabla de megas (a/0/1/5): método 2 no es una piedra (2026-09-27)
Cada entrada: forma u16, método u16, argumento u16. Método 1 = megapiedra (objeto); método 2 = Rayquaza por movimiento,
y su argumento es un MOVIMIENTO. Leer el argumento como objeto sin mirar el método pone precio a otro objeto.
El mod reutiliza ids 505-520 y 1019-1023 para sus megapiedras nuevas.

## WDA_EXCLUDEFROMCAPTURE esconde la ventana (2026-09-27)
Sacar el panel del cap de las capturas hizo que dejara de verse encima del juego. Para que la copia de pantalla no lo
lea, se esconde mientras se mira la barra de PS.

## Subir los PS actuales en la partida la corrompe (2026-09-27)
ENTRENAR EV subió el EV de PS de Partecoños (6 → 252): máximos 98 → 117 y `Restat` subía también los actuales a 117.
Ultra Luna: «The saved game data is corrupted», aunque PKHeX leía todo bien (39/39) y las estadísticas eran las que el
juego calcula. Bisección con el jugador sobre las copias: EV sin recalcular carga; recalculado con los actuales en 98
carga; con los actuales a 117 no. `StatCalculator.Restat` ya no sube nunca los PS actuales (bajarlos con el cap sigue).
Copias de la prueba en `PermaLocke prueba\Saves\backup` (prueba-A, prueba-B, corrupta-apartada).

## Avisos y panel del cap (2026-10-08, §238-§239)
- **Un aviso que se dice «al cambiar algo» se pierde cuando ese algo ya estaba así.** Las noticias del combate solo salían
  si las balls cambiaban; un duplicado leído en la primera vuelta encontraba las balls ya permitidas por la ruta y no se
  avisaba nunca. Ahora se marca qué se ha dicho en cada combate (`WildBattle.NewsSaid`).
- **Pixel art encima del juego: píxeles enteros por celda.** Un tamaño de celda con decimales emborrona las líneas de 1
  celda. `CapBadge.Place` redondea hacia abajo y, si no cabe, esconde la ventana en vez de pisar la barra de PS (que
  la detección de la muerte lee por copia de pantalla).
- **Animación de cierre que empieza a medias:** guardar desde dónde sale (`_motionFrom`) y no saltar a abierto; si la
  ventana se recarga (cambio de tema), `Loaded` reanuda la apertura y vuelve a programar el cierre.
- **Prototipos:** medir el ancho del texto antes de maquetar (nombre + chip no cabían en 100 celdas, se pasó a 112) y
  dejar los chips oscuros con letras claras sobre latón: claro sobre claro no se lee.

## Un hueco no es un sitio: MT del suelo (2026-10-08, §241)
- El cartucho repite objetos en zonas distintas que son **el mismo sitio en Sol y Luna** (MT03, MT06, MT93: Lago Corosol/Coroluna, Cañón de Poni, Paraíso Æther). Barajar hueco a hueco las reparte por dos lugares. Barajar **objetos distintos** y dar el mismo a todos sus huecos (`FieldItemRandomizer.MachineMap`).
- Ante «me ha salido repetido»: mirar el log (`Objeto nuevo en la mochila: <id>`) y `RomTool fielditems --dir <mundo>` antes de culpar a tiendas o NPC. Tiendas (29 MT) y suelo (42) no comparten ninguna en el cartucho.
- La misma semilla da otro reparto tras el cambio; los mundos ya instalados no se arreglan solos.

## Animación del objeto por categorías (2026-10-09 y 10, §243, 1.0.16)
- **Un efecto «para que se vea sobre negro» ilumina el fondo.** La viñeta de la Megapiedra llevaba un contorno de puntos del color de la piedra para verse sobre la banda negra del emulador, y el jugador lo vio como «alumbra el fondo un poco del color de la mega». La escena vive sobre el juego: lo que no es del objeto, de la bolsa, de la placa o de los anillos y chispas que salen de ellos, no se dibuja. La prueba (`The_background_is_left_alone_...`) mira que la derecha de la escena queda vacía en todo instante.
- **Antes de decir «la app no conoce ese icono», seguir el camino real:** `PokemonSpriteService.GetItem` con la tabla del cartucho (`ItemIconIndex.UseCartridgeTable`, de `exefs/code.bin`). Contar PNG ya extraídos dio 175 donde la app tiene 944 (de 1001; los 57 restantes valen 768 en la tabla: el propio juego dibuja «?»).
- **Píxel no entero = huecos.** Cada píxel del juego va de `round(gx * Pixel)` a `round((gx + 1) * Pixel)`; con `round(Pixel)` fijo salían rectas negras a 1,4 y 2,4 (§242). Probar siempre a 1,0, 1,5, 2,3 y 3,7.
- **Cero asignaciones por fotograma, medido.** `ItemStyleTests.A_frame_of_any_style_allocates_nothing` (`GC.GetAllocatedBytesForCurrentThread`) cazó cuatro: textos de la placa (por objeto), enumerador por `IReadOnlyList<T>` (recorrer por índice), matrices de destellos (por índice) y el pie de las MT (por hilo, `ThreadStatic`). Un `foreach` sobre una interfaz asigna aunque la colección sea un array.
- **Destellos por tiempo, no por fotograma:** cada uno de los dos fotogramas de blanco dura `FlashPhase` = 35 ms, algo más que un fotograma a 30 por segundo; se prueba a 30, 45 y 60 con diez desfases.
- **PKHeX cargado en paralelo da fallos intermitentes** en las pruebas: `RealItemData` lo carga una vez con `Lazy`, y las pruebas que tocan `ItemIconIndex` están en `[Collection("ItemIconIndex")]`.
- **La Action no corre las pruebas de App** (solo Core, Rules, GameLink y Randomizer): antes de subir una versión con cambios de App, `dotnet test tests/PermaLocke.App.Tests` en local.
- **Un heredoc largo de Bash falla con ciertos textos y `perl -0pi` sobre un `.md` UTF-8 con cadenas pegadas desde la línea de órdenes lo doble-codifica entero:** para editar documentación, `Edit`, y mirar `git diff --stat` después.
- **Sin probar en el emulador real:** el corte por tecla, el tipo de MT y megapiedra con el mundo instalado y el encaje de los 112 de alto junto a la pantalla de abajo solo los dirá el juego (ver §243).
