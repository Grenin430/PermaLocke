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

## Añadidas 2026-09-26
- **Desde la nube sí se compila:** el SDK no se baja de builds.dotnet.microsoft.com (bloqueado), pero Ubuntu lo trae: `apt-get update` y `apt-get install -y dotnet-sdk-10.0`. WPF compila con `-p:EnableWindowsTargeting=true`; no se ejecuta. Para ver dibujos pixel, compilar los ficheros puros en una consola con un `Color` falso y escribir PNG.
- La letra pequeña de 3×5 tiene una N que se lee D en palabras largas; en las cartas va una N de 4 celdas (`TcgCardArt.WideN`).
- **Estado de Discord que no sale:** primero, Ajustes de Discord > Privacidad de la actividad > «Compartir tu actividad detectada». Apagado, Discord acepta el estado y no lo enseña; el log de PermaLocke no puede avisar.
- Un log sin ninguna línea de un servicio nuevo suele ser que el exe no lleva ese código (otra rama, sin compilar), no que falle.

## Añadidas 2026-09-25
- **No guardar ficheros del repo con `Get-Content | Set-Content` de PowerShell 5.1**: lee como ANSI y rompió todas las tildes de `MainWindow.xaml` (se restauró con git). Usar sed/Edit.
- `perl -0pi -e 's|…|…|'` con `|` de delimitador y `||` o `\|\|` en el patrón o la sustitución: metió el bloque al principio de `Program.cs`. Para eso, Edit.
- Escalado de Windows: una celda pixel que se redondea hacia arriba hace el texto más ancho que el diseño (125 % → +20 %). Siempre `Floor`.
- «No está en INFORMACIÓN» no era un fallo del generador: solo listaba lo que cambia el fixer. Preguntar qué se espera ver antes de dar una lista por completa.
