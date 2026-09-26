---
tipo: ui
revisado: 2026-09-24
---
# UI y kit pixel

Todo `src/PermaLocke.App` está en estilo pixel desde §176–§176 quater, salvo COMPETICIÓN (`SyncView.xaml`, oculta con `LocalOnly`) y algo suelto (`PendingView`, `ToastWindow`, `DeathWindow`). Lista de ficheros en [[Mapa de código/PermaLocke.App]].

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
- ÁLBUM (§186, §187, §188): pinta con `PixelColour` (cuatro bytes; el `Color` de WPF cuesta `Math.Pow`), la mano calcula la luz por celda y va a 60 fps si puede, el álbum se para mientras se inspecciona (`IsPaused`). Acabados `TcgFinish` por región (`CellCanvas.Regions`, `Tint`); `HandScene` proyecta la carta en 3D por píxel (Pbgra32, rectángulo sucio); `AlbumScene` con tapa/pestañas opcional (`dressed`) y luz horneada. `TcgCardArt` dibuja cartas en un `CellCanvas` (BGRA con transparencia, `Stamp`, `StampColumns` para girar a columnas enteras, `Dither` Bayer 4×4, `Hash` estable); `AlbumScene` compone la doble página; `AlbumStage`/`CardStage` las ponen en pantalla a celdas enteras centradas en píxeles enteros.
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
