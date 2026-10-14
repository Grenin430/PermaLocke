---
tipo: ui
revisado: 2026-09-24
---
# UI y kit pixel

Todo `src/PermaLocke.App` está en estilo pixel desde §176–§176 quater, salvo algo suelto (`PendingView`, `DeathWindow`). Los avisos de `ToastWindow` son un pergamino (`ToastScroll`, §238). Lista de ficheros en [[Mapa de código/PermaLocke.App]].

## Temas (`App.xaml`: Palette, Icons, Controls, Pixel)
- `Themes/Pixel.xaml`. Cada color es un `Color` (no un Brush) salvo los `*Brush`:
  - `PxInk`, `PxShadow`, `PxSidebar`, `PxBase` `#120E20`;
  - `PxFace`, `PxFaceHigh`, `PxFaceLift`, `PxWell`;
  - `PxAccent` `#B07BF0`, `PxAccentLight`, `PxAccentDeep`;
  - `PxText`, `PxTextDim`, `PxTextFaint`;
  - `PxGood`/`PxGoodDeep`, `PxBad`/`PxBadDeep`, `PxWarn`/`PxWarnDeep`, `PxGold`;
  - `PxBaseBrush`, `PxInkBrush`, `PxAccentBrush`.
- Estilos: `PxButton`, `PxSubtleButton`, `PxDangerButton` (al pulsar: sin sombra y desplazados 6 px), `PxCaptionButton`, `PxCaptionClose`, `PxNavItem`, `PxTabItem`/`PxTabControl`, `PxScrollBar`, `PxTextBox` (`Tag` es el texto de ayuda; Consolas 15), `PxCheckBox`, `PxComboBox`/`PxComboBoxItem`, **`PxEditableComboBox`**, **`PxRadioCard`**, **`PxToggleButton`**.
- **Orden importa**: un `StaticResource` no ve claves definidas más abajo. Por eso `PxTextBox` y los que van detrás están después de `PxScrollBar`.
- `Palette.xaml`, `Controls.xaml` e `Icons.xaml` son del tema anterior (§74) y siguen cargados; algunas vistas usan todavía `AccentBrush`, `UiFont` y similares.

## Piezas (`Views/Pixel/`, espacio de nombres `PermaLocke.App.Views.Pixel`)
| Pieza | Qué es |
|---|---|
| `PixelFont` | fuente propia, glifos en tabla (incluye ▲▼◀▶·…ªº§~<>). **Sin «−» Unicode**: usar «-». |
| `PixelText` | `Text`, `Scale`, `Tight`, `Wrap`, `Trim`, `Shadow` (Color), `Colour` (Color), `TextAlignment` |
| `PixelIcon` | iconos de 12×12: IconHome, Randomizer, Gacha, Shop, Trophy, Grid, Dumbbell, Document, Wheel, Tools, Chart, Grave, Swords, Play, People, Gift, Points, Refresh, Check, Dot, Star, Cross, Tick, Away. Tiene `Bob`. |
| `PixelSprite` | `Source`, `Scale`, `Silhouette`, `Outline`, `Stone`, `TrimEmpty`, `Bob` |
| `PixelWindow` | ContentControl con `Title`, `Icon`, `HeaderContent`, `Fill`, `HeaderFill`, `HeaderAccent`, `IsSunken`; franja de 36 px |
| `PixelRule`, `PixelBackdrop` (`Base`) | separador y fondo de trama |

- En `Views/` (espacio `views`): `PixelPanel` (`Fill`, `HasShadow`, `IsSunken`, `HeaderHeight`/`Fill`/`Accent`), `PixelBar` (`Value` de 0 a 1, `Fill`, `Track`), `PixelCursor` (`Colour`).
- Escenas dibujadas celda a celda: `PixelScene.PaintRoom` (sala común del gacha, el wonder trade y la ruleta), `TradeMachine`/`TradeMachineScene`, la máquina de cápsulas, la ruleta de feria (§175), `CemeteryCanvas`/`CemeteryScene`, el cuarto del entrenador en JUGAR (§169) y el cielo de Alola (`AlolaSky`).
- Encima del emulador, en la pantalla de arriba (`GameWindow.TopScreen`): fantasmas y lluvia (`GhostWindow`, §183-184), escena de muerte (`DeathWindow`) y la carta de cada captura (`CatchWindow` + `CatchScene`, §190, en píxeles del juego: `Pixel` = tamaño de un píxel del 3DS en el monitor; el álbum a 2 y la ball a 2 píxeles del juego por celda). Las cartas se hacen siempre con `TcgCardFactory`, igual en el ÁLBUM y al capturar.
- Avisos y cap encima del juego (§238-§239, 1.0.14-1.0.15): `ToastScroll` (pergamino con rodillos de madera, manchas por hash,
  sello de cera; `Reveal` 0..1 recorta la hoja; anima con `CompositionTarget.Rendering`), `ToastPlate.Parchment` (pozo marrón,
  sprite gris si toca) y `ToastCountdown.Fuse` (mecha que se quema). Colores `InkBrown`, `WellBrown`, `WellFloorBrown`, `Ash` en
  `ToastPixels`. El cap es `CapPlaque` (placa de trofeo: escudo de nogal, copa y placas de latón, cuerda de progreso, una placa
  por miembro con medallón y chip «NV22»; dorado en el cap, apagado si cayó). Se pinta en un `Sheet` propio y pasa por el tema
  (`PixelTheme.Map`). Flujo y colocación: [[Flujos del vigilante]] §6 y §6 bis.
- **Prototipos de aspecto** en `tools/PermaLocke.PixelCheck/NotificationPrototypes*.cs` (lienzo `Pix` con ayudas: `Sheet`,
  `HRoll`/`VRoll`, `Wax`, `Rope`, `Chip`, `Medallion`, `Gauge`, `Brass`, `Trophy`…). Avisos A–Q (`Looks`) y cap S–Z
  (`CapLooks`), cada uno sobre un fotograma real del juego. Elegidos: **O pergamino** (avisos) y **Z placa de trofeo** (cap).
  PNG en `prototipos/`. Cómo generarlos: [[Comandos]].
- Efectos de sala en `PixelScene` (§189), todos sobre los bytes: `Spotlight` (foco), `Wash` (tinte), `Flash` (fogonazo en tramado), `Shake` (mueve el fotograma entero), `FloorRing`, `ShoutText` (palabra con contorno). La máquina de cápsulas lleva el reloj en `CapsulePlay.Elapsed` (exprés y SALTAR, `CapsuleTimeline.Warp`).
- ÁLBUM (§186, §187, §188): pinta con `PixelColour` (cuatro bytes; el `Color` de WPF cuesta `Math.Pow`), la mano calcula la luz por celda y va a 60 fps si puede, el álbum se para mientras se inspecciona (`IsPaused`). Acabados `TcgFinish` por región (`CellCanvas.Regions`, `Tint`); `HandScene` proyecta la carta en 3D por píxel (Pbgra32, rectángulo sucio); `AlbumScene` con tapa/pestañas opcional (`dressed`) y luz horneada. `TcgCardArt` dibuja cartas en un `CellCanvas` (BGRA con transparencia, `Stamp`, `StampColumns` para girar a columnas enteras, `Dither` Bayer 4×4, `Hash` estable); `AlbumScene` compone la doble página; `AlbumStage`/`CardStage` las ponen en pantalla a celdas enteras centradas en píxeles enteros.
- COMPETICIÓN (2026-09-28): podio como estadio pixel de noche (`PodiumScene` + `PodiumStage`): público que salta, focos, marcador de bombillas, bloques oro/plata/bronce, foto de Discord pixelada a 24 celdas con marco del color de presencia, corona y confeti. Tooltip con etapa y equipo. Probado solo en PNG sin datos reales.
- TEMAS (2026-10-01, propuesta sin elegir ni publicar): `Views/Pixel/PixelTheme.cs` con CLÁSICO, ESMERALDA (GBA, claro, `PanelStyle.Rounded` + rayas), GAME BOY (4 verdes, todo pasa por `Map`/`MapPixels`, también escenas `PixelScene`, cuarto y cielo), ULTRAUMBRAL (neón cian/magenta + estrellas) y ROTOM DEX (cuerpo rojo, pantallas con bisel). Colores en `Themes/PixelColours.xaml` (fusionado dentro de `Pixel.xaml`; si los colores viven en el mismo fichero que los estilos, los estilos se quedan con los de siempre). Las escenas guardan sus colores salvo en GAME BOY.
- DISEÑOS en vivo (2026-10-02, §215, sin publicar ni commit): cada tema trae su marco (`ShellKind`: Rail/Tabs/Menu/Dock/Keys, `Shells/*Shell.xaml`, mismo ViewModel) y `WindowKind` (título de `PixelWindow`). `MainWindow` solo tiene `ShellSlot`; al cambiar de look `ApplyShell` construye un shell NUEVO (los ocultos se quedan con colores viejos) y se conservan sección y estado. Botón DISEÑO (barra y CONFIGURACIÓN) abre `DesignGalleryWindow`; la elección va a `Config/tema.json` (`ThemeStore`, `--tema` manda sin guardar). Colores de texto de acento: `PxAccentInk`/`PxAccentInkLight` (no `PxAccent`, que es relleno). Test de contraste en `PixelUiTests`. Admin enlaza `Views/Pixel/*.cs`: nada de `Shells.*` ahí (`ReducedMotion` vive en `PixelTheme`). Capturas: `--sin-juego --capturas <carpeta> [--pantallas A,B] [--vivo] [--foco] [--ruleta] --tamano grande|enorme|adaptar`. Trampas: ListBox con captura de ratón (`ShellSupport.ReleaseCapture`), WindowChrome oculto necesita `IsHitTestVisibleInChrome`, SelectedItem OneWay + SelectionChanged en páginas de grupo (TwoWay escribe null). Sin ver: DPI 125/150 %, jugar con cada uno.
- `EvHexagon` (`Values`, `Saved`, `IsOver`, `Plate`, `Guide`, `Fill`, `OverFill`).
- `PokemonPickerPanel.xaml`: el selector de equipo y cajas compartido por ENTRENAR EV y MOVIMIENTOS. Enlaza `Party`, `SelectedPartySlot`, `Slots`, `SelectedSlot`, `SelectedBox`, `Previous/NextBoxCommand` y `LoadCommand`.
- `ResourceKeyConverter` con `ConverterParameter=color` devuelve un Color.
- `TypeBadges.For(ITypeLookup, BoxedPokemon)` devuelve las placas `ViewerTypeBadge(Name, Colour)`.

## Verificación visual (cómo se ha hecho)
- Copia aislada `scratchpad\gacha-e2e-211902` (con la unión `Expansion`; ver [[Usuario y forma de trabajar]]). Se lanza con `--sin-juego`.
- Scripts en el scratchpad (hay que recrearlos si el scratchpad es nuevo):
  - `secciones-e2e.ps1 -App <exe> -Out <dir> [-Only patrón]` selecciona cada sección por UIAutomation (ListItem) y hace `PrintWindow`. En ENTRENAR y MOVIMIENTOS además selecciona un Pokémon y guarda la captura `-ficha`.
  - `visor-e2e.ps1`.
  - Trampa de PowerShell: las variables no distinguen mayúsculas, así que `$t` pisaba `$T`.
- Sin ventana: `HomeViewTests` con `PERMALOCKE_SNAP_DIR=<dir>` renderiza los diálogos a PNG (ver [[Pruebas]]).
- Publicar y desplegar: [[Comandos]].

## Trampas de WPF de este proyecto
- `Style` como atributo **y** como `<X.Style>` a la vez es error de compilación.
- Dentro de un Style trigger no se puede poner `Style`: se hacen dos elementos y se alternan por visibilidad.
- Los comentarios XML no admiten `--`.
- `OpacityMask` con `ImageBrush` no pinta: la silueta se cocina en un bitmap.
- Un `ItemsControl` dentro de un `Grid` se recorta: una tira larga va dentro de un `Canvas`.
- Un `ComboBox` editable sin `PART_EditableTextBox` deja de aceptar texto en silencio.
- Una pila horizontal mide con ancho infinito, así que el texto no ajusta línea.
- Un estilo con `x:Key` no hereda del implícito sin `BasedOn="{StaticResource {x:Type X}}"`.
- `PrintWindow` devuelve el dibujo anterior si WPF no ha repintado.
- Solo cabe **una `Application` por proceso** en las pruebas.
- Un `DataTrigger` sobre un tipo anónimo con enlace TwoWay lanza, porque es de solo lectura: en pruebas se usa `ExpandoObject`.
- `StaticResource Visible` no existe; se usa `BoolToVisibility` (fallo r3 de HOME).

## Sprites del mar: lote 4 singleplayer (2026-10-14)
Finneon, Milotic, Manaphy, Pelipper y Crobat están integrados y aprobados por el usuario el 2026-10-14.
Método, tamaños, comandos, limitaciones y comprobaciones: `CONTINUIDAD_SPRITES_POKEMON.md`, sección «Lote 4».
Entrega: `C:\Users\javie\Desktop\imagenes\revision\lote4\`.
Las bases se reconstruyen de las referencias; no se redibujan. Las 216 celdas inclinadas pasan `iso 0, holes 0` y las 256 regiones de ojos
conservan sus colores característicos. La revisión de costuras sigue siendo visual: un hueco grande no lo detecta `check`.
`seal_sheet.js` solo cierra huecos aislados de rotación en las dos especies que lo solicitan. `critter.js` y las hojas aprobadas no se modifican.
El laboratorio exporta recorridos continuos con `--secuencia-ventana`; compilación aislada en `.lab-art`. No desplegar. El usuario ha autorizado continuar con el lote 5; confirmar primero su selección de especies.

## Sprites del mar: lote 5 singleplayer (2026-10-14)
Luvdisc, Seadra, Lumineon, Pikipek y Skarmory: integrados y aprobados por el usuario el 2026-10-14.
Método y límites: `CONTINUIDAD_SPRITES_POKEMON.md`, sección «Lote 5». Entrega: `C:\Users\javie\Desktop\imagenes\revision\lote5\`.
40 fotogramas, 184 celdas inclinadas y 184 regiones de ojos comprobadas; 553 pruebas aprobadas. Skarmory usa periodo 3,24, trayectoria casi
plana e inclinaciones -2/0/2 para respetar su altura. Pikipek conserva la pose posada. Las 21 hojas anteriores permanecen intactas.
Compilar solo en `.lab-art`; no desplegar. Lote 5 aprobado; confirmar las especies del lote 6 antes de comenzar.

## Sprites del mar: lote 6 singleplayer (2026-10-14)
Remoraid, Lanturn, Kingdra, Toucannon y Altaria: integrados y aprobados por el usuario el 2026-10-14.
Método y límites: `CONTINUIDAD_SPRITES_POKEMON.md`, sección «Lote 6». Entrega: `C:\Users\javie\Desktop\imagenes\revision\lote6\`.
40 fotogramas base, 168 celdas inclinadas y 192 regiones de ojos comprobados; 553 pruebas aprobadas. Periodos explícitos 3,66 para Lanturn
y 3,61 para Toucannon. Toucannon conserva su pose posada; Altaria dobla la nube por columnas. Alturas y ángulos contenidos para caber en el cielo.
Las 26 hojas anteriores permanecen intactas. Solo `.lab-art`, sin desplegar ni tocar la app abierta. Lote 6 aprobado; confirmar las especies del lote 7 antes de comenzar.

## Sprites del mar: lote 7 singleplayer (2026-10-14)
Wishiwashi, Relicanth, Pidgeot, Togekiss y Aerodactyl: integrados y aprobados por el usuario el 2026-10-14.
Método: `CONTINUIDAD_SPRITES_POKEMON.md`, sección «Lote 7». Entrega: `C:\Users\javie\Desktop\imagenes\revision\lote7\`.
40 fotogramas, 168 celdas y 168 regiones de ojos comprobados; 553 pruebas aprobadas. Periodos explícitos 3,55 (Pidgeot) y 3,08 (Togekiss).
Relicanth usa Pose 28 y conserva su ojo cerrado. Pidgeot mantiene su pose posada, altura 61 e inclinaciones -1/0/1 para caber en el cielo.
La auditoría comprueba el espacio vertical con el redondeo real de C#. Las 31 hojas anteriores siguen intactas. Solo `.lab-art`, sin desplegar.
Lote 7 aprobado; confirmar la selección del lote 8 antes de empezar.

## Sprites del mar: lote 8 singleplayer (2026-10-14)
Carvanha, Seaking, Fearow, Dragonite y Latias: integrados y aprobados por el usuario el 2026-10-14.
Método: `CONTINUIDAD_SPRITES_POKEMON.md`, sección «Lote 8». Entrega: `C:\Users\javie\Desktop\imagenes\revision\lote8\`.
40 fotogramas, 168 celdas y 168 regiones de ojos comprobados; 553 pruebas aprobadas. Periodos explícitos 4,34 / 2,84 / 3,49 para Carvanha,
Fearow y Latias. Fearow mantiene las alas parcialmente plegadas, anclaje -3 e inclinaciones mínimas para caber en el cielo.
El usuario autorizó reducir solo Dragonite: visualización al 75 % por vecino más cercano; la base 64×80 y su animación nativa se conservan.
Los tres voladores caben en el cielo. Las 36 hojas anteriores permanecen intactas. Solo `.lab-art`, sin desplegar.
Lote 8 aprobado; confirmar la selección del lote 9 antes de empezar.

## Sprites del mar: lote 9 singleplayer (2026-10-14)
Bruxish, Staraptor, Salamence, Charizard y Latios: integrados y aprobados por el usuario el 2026-10-14.
Método y medidas: `CONTINUIDAD_SPRITES_POKEMON.md`, sección «Lote 9». Entrega: `C:\Users\javie\Desktop\imagenes\revision\lote9\`.
40 fotogramas, 24 fotogramas nativos adicionales, 136 celdas y 136 regiones de ojos correctas; 555 pruebas aprobadas.
Reducción de visualización autorizada: Salamence 83,3 %, Charizard 75 %, Latios 80 %; originales conservados.
`recon2.js` toma el centro cuando una cuadrícula diminuta no tiene muestra interior; ojo de Latios preservado al reducir.
El laboratorio ahora incluye los cuatro voladores en la vista forzada, con dos pruebas nuevas; las asignaciones anteriores se conservan.
41 hojas aprobadas intactas, 82 hashes sin cambios. Solo `.lab-art`, sin desplegar. Lote 9 aprobado; confirmar la selección del lote 10 antes de empezar.

## Sprites del mar: lote 10 singleplayer (2026-10-14)
Articuno, Zapdos, Moltres, Kyogre y Lugia: integrados y aprobados por el usuario el 2026-10-14 («ok a todo»).
El usuario confirmó incluir ahora Kyogre y Lugia; quedan Wailord, Rayquaza y Ho-Oh. Método en `CONTINUIDAD_SPRITES_POKEMON.md`, «Lote 10».
Entrega: `C:\Users\javie\Desktop\imagenes\revision\lote10\`. Articuno y Lugia al 75 %, Moltres a 6/7; solo visualización, originales conservados.
Zapdos usa periodo 7,37; Kyogre mantiene tamaño nativo, Pose 28 y reserva 90 en el mar. `batch10_display.js` preserva rasgos al reducir;
el iris rojo de Articuno se proyecta después del blanco para evitar que lo tape. Integración admite la entrada final sin coma.
136 celdas, 160 regiones de ojos y 555 pruebas correctas. Mar y cielo auditados. 92 hashes anteriores intactos; sin despliegue.
Lote 10 aprobado junto al lote 11; catálogo completo.

## Sprites del mar: lote 11 singleplayer (2026-10-14)
Wailord, Rayquaza y Ho-Oh: integrados y aprobados por el usuario el 2026-10-14. Tras autorizar el lote final con «acaba esos 3»,
aprobó los lotes 10 y 11 con «ok a todo». Las 54 especies del catálogo tienen hoja animada integrada y aprobada.
Entrega: C:\Users\javie\Desktop\imagenes\revision\lote11\. Método completo en CONTINUIDAD_SPRITES_POKEMON.md, «Lote 11».
Bases 87×74, 89×83 y 84×91; visualización 100 %, 75 % y 70 %. Se conservan fuentes nativas. Periodos explícitos 2,724 y 2,52 para Wailord y Ho-Oh.
Wailord conserva el punto negro del ojo protegiendo solo ese color; Rayquaza proyecta el iris amarillo después del contorno al reducir.
Ho-Oh dobla alas y cola por muestreo inverso. 72 celdas y 72 ojos correctos, iso/holes 0; mar/cielo auditados, 555 pruebas correctas.
102 archivos anteriores idénticos. Solo .lab-art, sin desplegar. Trabajo de sprites completado; no quedan especies pendientes.

Lección del lote 11: caber por píxeles no basta. Swimmer omite al visitante cuando bottom <= top; el cielo de 69 requiere reserva menor
de 62. Rayquaza/Ho-Oh usan 61. La auditoría añade trajectoryRange > 0 y la vista conjunta confirma que realmente aparecen.

## Revisión de poses de vuelo (2026-10-14)
El usuario permite ahora redibujar las poses terrestres para vuelo, conservando identidad y acabado. Diez afectados: Pidgey, Spearow, Pikipek,
Pidgeot, Toucannon, Staraptor, Skarmory, Dragonite, Charizard y Salamence. Piloto de Pidgey separado y pendiente de revisión: cabeza original,
torso horizontal reconstruido, patas flexionadas, cola unida y alas nuevas articuladas con batido completo y recuperación plegada.
Ver tools/critters/REVISION_POSES_VUELO.md y entrega imagenes/revision/vuelo_piloto. 8 fotogramas a 12 fps, 24 celdas/ojos correctos,
iso/holes 0 y una sola componente conectada: comprobar esto último detecta apéndices separados que no son píxeles aislados.
No se ha cambiado ninguna hoja de producción ni integrado el piloto. Esperar aprobación de pose/ciclo antes de integrar o extender.


### Fidelidad del piloto de Pidgey (2026-10-14)
El usuario aprueba la nueva pose y el movimiento, pero solicita recuperar detalle del plumaje antes de extender la corrección.
Se conservan exactamente articulaciones, alas, cola, patas, cabeza, lienzo 72×64, 8 fotogramas y 12 fps. Solo se retoca el torso:
garganta cálida, pecho claro con grupos de plumas escalonados y sombras que continúan hacia el vientre horizontal.
Cabeza/rostro/cresta cotejados: sus 212 píxeles siguen idénticos. Alas y cola mantienen los grupos de plumas y bordes del piloto;
no se añaden cambios en estas partes. Los retoques afectan 31–49 píxeles por fotograma, exclusivamente dentro de x=16..41, y=30..39.
La versión previa se conserva en tools/critters/pidgey_flight_before_detail.json para comparar vuelo contra vuelo, sin usar la pose de pie.
Comprobaciones: 49 coordenadas de detalle estables, 345 muestras visibles correctas, oclusión natural del ala excluida del cotejo;
iso 0, holes 0 en los 8 fotogramas y 24 celdas inclinadas; una componente conectada en todas; 24/24 ojos; sin recortes ni salto de cierre
según el criterio de variación previo. 108 archivos de producción/aprobados intactos. Misma paleta y escala de píxel.
Entrega separada en C:/Users/javie/Desktop/imagenes/revision/vuelo_piloto/detalle_pidgey/: comparación estática y animada sincronizada,
ciclo actualizado con cuerpo fijo, ampliaciones del pecho y los 24 rostros, hoja y auditorías JSON. Script: review_pidgey_detail.js.
Reproducción desde tools/critters: node review_pidgey_flight.js C:/Users/javie/Desktop/imagenes/revision/vuelo_piloto/detalle_pidgey
seguido de node review_pidgey_detail.js. La instantánea anterior no debe regenerarse al repetir la revisión.
Estado: pose/movimiento aprobados; pasada de detalle entregada para revisión. Aún no integrada ni extendida a los otros nueve.
No hay cambios de WPF ni ejecución nueva de sus pruebas; la validación integrada queda pendiente. Sin despliegue ni push.

## Correcciones de vuelo: tanda 1 (2026-10-14)
Pidgey corregido aprobado, fuente y entrega intactas; sigue separado de su hoja antigua de producción.
Spearow, Pikipek, Pidgeot, Toucannon y Staraptor: nuevas poses y ciclos, integrados solo en singleplayer y entregados para revisión.
Fuentes flight_batch1_specs.js/flight_rig.js y cinco *_flight_anim.js; build_batch.js los selecciona. Originales *_anim.js conservados.
40 fotogramas, 120 celdas/ojos correctos, iso/holes 0, 107 archivos protegidos intactos, 555 pruebas correctas.
Layout real 28 s a tamaño nativo en imagenes/revision/vuelo_tanda1; rutas definitivas y método en CONTINUIDAD_SPRITES_POKEMON.md.
Restan cuatro: Skarmory, Dragonite, Charizard y Salamence. Sin despliegue ni push.

## Correcciones de vuelo: tanda 2 (2026-10-14)
Usuario aprueba tanda 1 y precisa Salamence sin batido. Skarmory, Dragonite, Charizard y Salamence corregidos e integrados, entregados
para revisión. Nuevos flight_rig2.js/flight_batch2_specs.js independientes de la tanda 1; originales y Pidgey intactos.
Salamence: alas idénticas en ocho fases, cola suave, Drift 1.2/Bob 0/Surge 0. Los demás tienen articulación propia.
32 fotogramas, 96 celdas/ojos, iso/holes 0, 123 archivos protegidos idénticos y 555 pruebas correctas. Layout real exportado a tamaño nativo.
Fuentes y rutas definitivas en CONTINUIDAD_SPRITES_POKEMON.md, «Correcciones de vuelo, tanda 2»; entrega imagenes/revision/vuelo_tanda2.
No quedan especies del inventario sin propuesta; esta tanda está pendiente de aprobación artística. Sin despliegue ni push.

## Rechazo de la tanda 2 y revisión de bases (2026-10-14)
El usuario rechaza las cuatro correcciones por pérdida de detalle, volumen y anatomía. Primero requiere cuatro bases estáticas
junto a sus referencias; animación e integración esperarán su revisión. Nuevos tools/critters/flight_bases_v2.js y
review_flight_bases_v2.js, independientes de los rigs actuales, sin comprimir cuerpos. Pidgey y tanda 1 intactos.
Entrega: imagenes/revision/vuelo_tanda2_bases_rehechas. Comparaciones con originales ×3/×4, ampliaciones ×6, PNG nativos
y prueba estática de cielo. Bases 80×49, 102×69, 137×87 y 134×79; paletas originales, una componente cada una, iso/holes unitarios 0,
1722/1722 píxeles de cabeza y 135 archivos protegidos conservados. La prueba estática deja solo 4/3 px de margen en Charizard/Salamence.
No son animaciones aprobadas ni una captura del layout real. La app sigue con la entrega rechazada de 410e8d0; no se ha sustituido
ningún recurso. Sin build/tests nuevos (no cambia C# ni sprites integrados), sin abrir app/emulador ni desplegar. Sin push.
Estado vigente y rutas completas: sección «Tanda 2 rechazada» de CONTINUIDAD_SPRITES_POKEMON.md.

## Charizard: revisión de postura v3 (2026-10-14)
Las cuatro bases v2 también rechazadas. Ahora se trabaja uno por uno: SOLO Charizard está redibujado
desde una referencia de vuelo de Pokémon Ranger (archivo LaunchBox, copyright visible). Silueta primero,
detalle después; paleta y rostro del original. Entrega imagenes/revision/charizard_pose_v3:
charizard_pose_y_referencia.png, base nativa y ampliada, fuentes TXT/JSON y auditorías.
158×72, 4885 píxeles, una componente, iso/holes unitarios 0, cabeza 425/425 y 137 archivos previos intactos
respecto a 21c95bb. Los chequeos no equivalen a aprobación de la pose. Sin animación ni integración,
sin build/tests nuevos; app con la tanda 2 rechazada de 410e8d0. Pidgey y tanda 1 intactos.
Esperar aprobación de Charizard antes de otros tres o ciclos. La altura/layout queda pendiente.
Método, referencia y rutas completas: sección «Charizard: pose v3» en CONTINUIDAD_SPRITES_POKEMON.md.

### Charizard: ajuste localizado de mandíbula, cuello y pecho (2026-10-14)
El usuario aprueba la construcción de la pose v3 y pide conservar cuerpo, alas, extremidades, cola y acabado.
Solo solicita corregir la transición cabeza-cuello-pecho. Nueva propuesta estática pendiente de revisión de esa unión.
Se usa la misma referencia de vuelo de Pokémon Ranger, inspeccionada de nuevo junto al sprite v3.

tools/critters/charizard_neck_refinement.js deriva de charizard_flight_pose_v3.js sin modificarlo.
Elimina el escalón bajo la mandíbula, traza una garganta progresiva, une nuca y espalda y redistribuye
la sombra del cuello en grupos continuos de píxeles. No hay desenfoque ni nuevos colores.
tools/critters/review_charizard_neck.js genera las comparaciones y comprueba el alcance exacto.

Entrega: C:/Users/javie/Desktop/imagenes/revision/charizard_cuello/
- charizard_cuello_antes_despues.png: zona ampliada ×12, antes a la izquierda, después a la derecha.
- charizard_completo.png: sprite completo ×6 sin suavizado.
- charizard_base_nativa.png, charizard_base.json, charizard_base.txt: nueva base de revisión, 158×72.
- auditoria.json: 106 píxeles modificados; caja afectada x23–51, y29–43 del lienzo fuente 174×92.
[Verificado] Todos los píxeles fuera de la región autorizada x23–54/y29–43 idénticos a v3; paleta idéntica.
4842 píxeles opacos, iso 0, holes 0 (huecos unitarios). Ambos scripts pasan node --check.
La fidelidad fuera de la unión se comprueba comparando las cuadrículas completas, no solo la silueta.
Reproducir desde tools/critters: node review_charizard_neck.js.
Sin animaciones, integración, cambios de layout, build/tests de C# ni despliegue. Las pruebas de píxeles
no sustituyen la revisión artística. Esperar aprobación de esta unión antes de continuar.
