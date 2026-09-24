---
tipo: historial
revisado: 2026-09-24
---
# Historial de conversaciones

La última sesión va arriba. Antes del 2026-09-23 solo hay un resumen por fechas sacado de `CLAUDE.md` y de `ARCHITECTURE.md`: el detalle está en el § citado ([[Índice de ARCHITECTURE]]). **Cada sesión nueva añade su entrada aquí.**

## 2026-09-24 — torneo: entrar con Discord (fase 1)
- Plan acordado: Supabase (proyecto `rnqpjvkonxjhrjsdvlpm`) con login de Discord; fase 1 solo identidad, fase 2 subir `RunSnapshot` + historial con RLS, fase 3 clasificación y auditoría en Admin con `SnapshotAudit`. La GPL impide impedir copias: el login es para el torneo, no contra la copia.
- `Data/torneo.json` (URL + clave anon, pública por diseño). `App/Services/DiscordLogin.cs`: OAuth PKCE, navegador + `HttpListener` en `127.0.0.1:47281/callback`, sesión en `Config/discord.json` cifrada con DPAPI. Panel TORNEO en CONFIGURACIÓN. Sin probar contra el servidor.
- Secretos que NO van al repo ni al chat: Client Secret de Discord, contraseña de la BD, `service_role`.
- Puerta al abrir (`LoginWindow` + `LoginViewModel`, en `App.OnStartup` antes de `MainWindow`): solo ENTRAR CON DISCORD; sin cuenta de la whitelist o sin conexión, no abre. Whitelist en Supabase: tabla `whitelist(discord_id)` con RLS sin políticas y función `permitido()` (`tools/supabase/01-whitelist.sql`), que la app llama con el token del jugador. Rechazado: enseña su ID de Discord para dárselo al organizador.
- Fase 2: `TournamentUpload` sube `RunSnapshot`+`RunHistory` (enums como texto) a `runs` al abrir y tras cada evento (máx. 1/min, solo si cambia `ChainHead`); trigger anota cada envío en `subidas` (eventos, huella, puntos). `02-runs.sql`.
- Amigos y COMPETICIÓN por el servidor (`03-amigos.sql`): tabla `presencia` (latido lo sella el servidor), vistas `amigos`, `logros` (del historial subido), `clasificacion`, `ultima_run`, todas `security_invoker`. `CommunityService` reescrito sobre `DiscordLogin.GetAsync/PostAsync`; se arranca también en la distribución local. COMPETICIÓN (`SyncViewModel`/`SyncView`) rehecha: solo TOP con podio. Todo cerrado a `anon` (comprobado con curl).
- Fotos de Discord (`04-fotos.sql`: `presencia.avatar_url`, vistas `amigos` y `clasificacion` con la foto al final): la app la escribe en cada latido; JUGAR (amigos y actividad) y el TOP la enseñan, con el Pokémon de la run o la inicial como reserva.
- Carpeta de amigos republicada con login, amigos, top y fotos; la de antes queda como `PermaLocke para amigos (sin login)`. Nueva guía `tools/ACTUALIZAR-local.txt` (se publica como «ACTUALIZAR DESDE LA VERSION ANTERIOR.txt»): copiar ROM, Saves, Randomized, Config y de `Emulatoruser` solo sdmc, nand, load y sysdata (no config). `LEEME-local.txt` ya dice que hace falta internet y Discord.
- Fase 3: ventana AUDITORÍA DEL TORNEO en Admin (`AuditWindow`, `AuditViewModel`): lee `runs` y `subidas`, pasa `SnapshotAudit.Check` a cada run y `SnapshotAudit.Rewinds` al registro de subidas (menos eventos = restaurada; mismos eventos y otra huella = reescrita). Admin enlaza `DiscordLogin.cs` de la app (sin copiarlo). `05-organizador.sql`: tabla `organizadores` y función `es_organizador()`; solo el organizador lee `subidas`. Prueba de ida y vuelta del JSON del servidor en `SnapshotAuditTests`.
- COMPETICIÓN ampliada: resumen (en el torneo, conectados, jugando, caídos), foto con marco del color de presencia, etapa, logros, vivos y caídos por jugador, y RÉCORDS (más capturas, caídos, logros, más avanzado). Todo de la última run subida (`06-clasificacion.sql` amplía la vista `clasificacion`). Carpeta de amigos republicada; la anterior queda como `(sin records)`.
- Una run por jugador (`07-una-run.sql`): `runs.activa` + índice único por jugador; `puedo_crear_run()` que la app pregunta en `CreateRunViewModel` antes de crear; `reiniciar_run(jugador)` solo organizador, archiva (no borra) y anota en `reinicios`. Botón REINICIAR en la auditoría de Admin, con confirmación.
- Admin en pixel: enlaza `Themes/Pixel.xaml` y los controles del kit desde App (`ToastKind` sacado a su fichero para poder enlazar `ToastPixels`); `App.xaml` de Admin aplica los estilos Px a los controles de siempre. Paneles JUGADORES y MANDAR UN REGALO como `PixelWindow`. JUGADORES y regalos siguen por la carpeta compartida (pendiente pasarlos al servidor).
- Regalos por el servidor (`08-regalos.sql`): tabla `regalos(id, para, regalo jsonb)`; el jugador ve los suyos y los de todos, solo el organizador manda y retira. `GiftInbox` (app, ya también en la distribución local) y `GiftDesk` (Admin) usan `DiscordLogin`; recogido = evento `AdminGiftClaimed` en el historial subido. Admin sin carpeta compartida: JUGADORES sale de `clasificacion`.
- Fallo de regalos arreglado: «A todos» venía marcado por defecto y elegir a alguien no lo quitaba, así que llegaba a todos. Ahora empieza sin marcar, la lista JUGADORES es de selección múltiple (clic marca/desmarca) y elegir a alguien quita «A todos»; se manda un regalo por destinatario. La app del organizador filtra por `para` = todos o su `user_id` (el organizador ve todos por RLS). `DiscordAccount.UserId` nuevo.
- Admin: quitado dar objetos en los regalos (a petición). Quedan puntos, tiradas y wonder trades. Acceso directo `EscritorioPermaLocke Admin.lnk` al `binRelease` del repo: recompilar Admin en Release tras cambiarlo.
- Menos tráfico para 20 jugadores: amigos y logros cada 60 s (antes 15); la run sube cada 5 min como mucho (antes 1) y al cerrar (`TournamentUpload.Flush` en `App.OnExit`, no con `--sin-juego`). Pendiente si hace falta: logros en tabla aparte en vez de sacarlos del historial en cada consulta.
- `tools/desplegar.ps1 -Prueba|-Amigos`: el despliegue de siempre en un comando (se niega con PermaLocke o Azahar abiertos; nunca borra; si ya hay un «(anterior)» se para). Probado `-Prueba`; `-Amigos` sin ejecutar todavía.
- `publicar.ps1`: el zip de código fuente ya no lleva `src/PermaLocke.Admin` ni `tools/supabase` (Admin no se reparte). Carpeta de amigos republicada y **congelada como versión para repartir** (el usuario la da por definitiva y se la pasa a 1 amigo para probar); ahora se trabaja en Admin.
- Admin: botón ENTRAR CON DISCORD en la ventana principal y ventana LISTA DEL TORNEO (`WhitelistWindow`/`WhitelistViewModel`): añadir (ID de 17-20 cifras) y quitar con confirmación. `09-whitelist-admin.sql`: solo el organizador ve y toca `whitelist`. Ventana principal a 1320 de ancho.
- Admin (`10-control.sql`): JUGADORES con estado (gris/azul/verde, relee cada minuto); auditoría con «Ver archivadas» y REACTIVAR (`reactivar_run`, se niega si ya tiene otra activa; queda en `reinicios.accion`); SUSPENDER/REACTIVAR en la lista (`whitelist.suspendido`, `permitido()` lo excluye); ventana ANUNCIOS (tabla `anuncios`). App: el anuncio más reciente sale en JUGAR y como aviso la primera vez que aparece uno nuevo (`CommunityService.ReadAnnouncementAsync`).
- `tools/desplegar.ps1 -Prueba|-Amigos`: el despliegue de siempre en un comando (se niega con PermaLocke o Azahar abiertos; nunca borra; si ya hay un «(anterior)» se para). Probado `-Prueba`; `-Amigos` sin ejecutar todavía.

## 2026-09-24 — plugins y MISCELÁNEA sin mochila
- Instalados por el usuario los plugins **ponytail** (código mínimo) y **caveman** (respuestas cortas en el chat); ambos activos.
- MISCELÁNEA: quitada la ventana «MOCHILA DEL JUEGO» (`ReadBagCommand`, `BagContents`, `BagAddress`, `PocketName`). `BagService` sigue: DARSE OBJETOS usa `CapacityFor`. Premios ya no releen la mochila al entregar.
- Ventana: quitado el tamaño NORMAL (1180x760); quedan GRANDE (por defecto) y MUY GRANDE. Un `ventana.json` con "normal" cae en GRANDE. Se probó un `Viewbox` para escalar y se descartó: a escala no entera el pixel art sale borroso.
- GRANDE: escenas de GACHA y RULETA bajan `DesignWidth` de 430 a 350 celdas (cabe el escenario de GRANDE). Gacha: cartel GACHA a x=320 y alfombrilla (`RestX`) a 262. Ruleta: `PixelScene.Ox` pasa a tener `set`; la lista se queda, rueda `WheelShift` −40, marcador/neón/fichas `RightShift` −80 (el vuelo de la ficha lo compensa). Celdas de probabilidad: ball de 24 junto al %, y sin ball cuando «NO SALE». 1522 pruebas.
- Cabecera sin subtítulo (solo título). Gacha: la ball cae más arriba (`RestFloor` 156 a 142, alfombrilla con ella) porque en GRANDE el escenario enseña unas 145 filas y la cortaba.

## 2026-09-24 — /doctor
- `CLAUDE.md` adelgazado de 147k a 13k caracteres: el «Estado actual» (el diario) y la tabla de estado pasaron tal cual a [[Archivo - estado de CLAUDE.md (2026-09-24)]]. En su lugar hay un resumen con las trampas clave.
- `~/.claude/settings.json` creado con `permissions.defaultMode: "auto"`.

## 2026-09-24 — pruebas 3 y 5: zona cerrada hasta el cristal Z (§179)
- En `Data/rules.json`, `ballControl.trialZones` queda así: `cueva-sotobosque-sala-de-la-prueba`/prueba-01, **`colina-saltagua`/prueba-03** (antes era la sala del dominante) y **`jungla-umbria`/prueba-05**.
- `colina-saltagua-sala-del-dominante` pasa de `marcadores` a `sinEncuentros` en `Data/marcadores.json`.
- Regla nueva: `EncounterSituation.PendingTrial` hace que `EncounterPolicy` retire las balls (menos a un variocolor) y no gaste la ruta hasta tener el cristal. `EncounterGuard` lo pasa desde `TrialZoneService.PendingAsync` en el tick de campo y en `Decide(battle)`.
- 1522 pruebas. Desplegados el exe y los dos JSON en `PermaLocke prueba`. Sin jugar.
- **Cuidado:** la carpeta de prueba tiene su propio `Data/`. Los cambios de JSON hay que copiarlos, comprobando con `diff --strip-trailing-cr` que no se pisa nada.

## 2026-09-24 — HOME, GACHA, stats del visor y textos de premio (§178)
Hecho, con 1521 pruebas correctas, desplegado en `PermaLocke prueba` y sin commit:
- HOME sin «IR A»: queda PARTIDA sola. `GoCommand` y `NavigateRequested` de HOME quitados.
- GACHA:
  - banners sin la línea de descripción;
  - barra de abajo en una fila (TIRAR | 5 probabilidades en `UniformGrid` | puntos + PREMIOS POR PRUEBA), así que la escena se ve entera;
  - el bote pasa a un overlay grande en la mitad izquierda (`Grid.RowSpan=3`, columnas 0.52/0.48), que no tapa la alfombrilla;
  - rangos «0-400 / 401-490 / 591+»;
  - la celda de un portal tiene `AutomationProperties.Name`=nombre del tier, y el script e2e lo pulsa («-bote»).
- VISOR: `PokemonViewerViewModel` usa `_forecast.With(pokemon, Evs)` en caja, como MOVIMIENTOS. Quitado el aviso amarillo.
- Ruleta: `SaveRouletteWorld` devuelve el nombre, «x N» o «Pierdes X», sin «antes → después». Gacha y wonder trade sin «Nv · naturaleza» ni «IV total».
- **Tiradas dadas en la run real** con `PERMALOCKE_ROOT="…\PermaLocke prueba" dotnet run --project tools/PermaLocke.Probe -- --credito <banner> <motivo>`, una por llamada: bueno +5, decente +5 y pocho +7, porque estaba en −2. Quedaron 5/5/5.

## 2026-09-24 — limpieza y dos fallos del kit (§177)
El usuario pidió quitar cosas y reportó botones que no iban. Hecho, con 1521 pruebas correctas, desplegado en `PermaLocke prueba` (md5 `2b0cbd91…`) y sin commit:
- CEMENTERIO sin el bloque ANTE QUIÉN / CUÁNDO / DE DÓNDE / CÓMO. Eso resuelve el «#822» porque ya no se enseña.
- COMBATES sin COMPROBAR. MISCELÁNEA sin +10 ESCAMAS CORAZÓN y sin el panel de avisos.
- HOME sin CAÍDOS, sin ÚLTIMAS ACCIONES y sin cambiar el rol. Nuevo panel «IR A» con 8 accesos (`HomeViewModel.GoCommand` → `NavigateRequested` → `MainViewModel`). El VM sigue rellenando `Fallen` y `RecentEvents`, que ya nadie pinta.
- MANTENIMIENTO borrada. Nueva **CONFIGURACIÓN** (`ViewModels/SettingsViewModel.cs`, `Views/SettingsView.xaml`, `Services/AppSettings.cs` → `Config/ajustes.json`):
  - tamaño de ventana;
  - avisos;
  - escena de muerte (`DeathCeremony.Enabled`);
  - killcam (`KillcamRecorder.Enabled`);
  - minimizar al abrir el juego;
  - VER UN AVISO;
  - carpetas.
- GACHA: la escalera de probabilidades en una fila propia a todo el ancho (`UniformGrid Rows=1`), con rangos «HASTA 400 / 401-490 / 591 O MÁS» y NO SALE.
- **Fallos del kit:** plantillas sin `Background` no reciben clics (JUGAR, pestañas del visor, portales, chinchetas, regalo, JUGAR de la cabecera), y la marca de `PxCheckBox` no se pintaba por el mínimo de 6×5 celdas de `PixelPanel`. Ver [[Trampas y lecciones]].

## 2026-09-24 — el cerebro
- El usuario pegó un encargo para crear un vault de Obsidian y aclaró que **es para Claude**: memoria para no releer el proyecto y para conocer las conversaciones anteriores. Se creó `Obsidian/PermaLocke Brain/`, con la memoria `cerebro-obsidian` apuntando a él.
- No se tocó código.
- A petición del usuario, `CLAUDE.md` lleva al principio una nota que apunta a `00 - Inicio.md`.
- Se le explicó cómo abrir el vault en Obsidian: abrir como bóveda la carpeta `PermaLocke Brain`, no el repo entero.
- El usuario confirmó dos cosas:
  - **al amigo ya no se le cierra Azahar**; anotado en §173 de ARCHITECTURE y en `CLAUDE.md`;
  - **Gimmighoul pide 999 monedas**; anotado en [[Preguntas abiertas]].

## 2026-09-23 / 24 — todo en pixel art (§176 bis, ter, quater)
Hecho, compilando sin avisos, con **1521 pruebas correctas** (Rules 150, Randomizer 479, Core 404, GameLink 381, App 107):
- **§176 bis–ter.** El VISOR y las catorce secciones pasaron al estilo pixel: JUGAR, RANDOMIZADOR, GACHA, TIENDA, LOGROS, MAPA, ENTRENAR EV, MOVIMIENTOS, POKE PASTE, CEMENTERIO, COMBATES, RULETA, MISCELÁNEA y MANTENIMIENTO. En la cabecera, placas translúcidas `#C8120E20` detrás del título y de los puntos. En GACHA, los portales de tier pasan a la barra de abajo, junto a TIRAR.
- **ESTADÍSTICAS, borrada** con confirmación del usuario: `StatisticsService.cs`, `StatisticsViewModel.cs` y `StatisticsView.xaml(.cs)`, y quitada de `MainViewModel.Sections`, `MainWindow.xaml`, el registro de DI y el enlace de JUGAR.
- **§176 quater. ENTRENAR EV y MOVIMIENTOS** dejan la bolsa naranja del §143:
  - Panel común `Views/PokemonPickerPanel.xaml` (equipo y cajas).
  - Placas de **tipo** del Pokémon elegido: `ViewModels/TypeBadges.cs` es la fuente única (también la usa el visor), expuesta como `SelectedTypes` en los dos ViewModels, que reciben `ITypeLookup` por el constructor.
  - `ZeroToVisibleConverter` pasó a `Converters/`.
- **La bolsa, borrada** con confirmación: `Themes/Bag.xaml`, `Views/BagPocketPanel.xaml(.cs)` y `Views/BagPixels.cs`.
- **Diálogos en pixel:** `CreateRunWindow`, `ChangeRoleWindow` y `RegisterCaptureWindow`. En `Themes/Pixel.xaml` se añadieron `PxEditableComboBox` (con `PART_EditableTextBox`), `PxRadioCard` y `PxToggleButton`. La letra de `PxTextBox` pasó de 13 a 15.
- También en pixel:
  - la ficha de `WonderTradeOverlay`;
  - los controles de `KillcamPlayer.cs`: `PxSubtleButton`/`PxToggleButton`, `WrapPanel`, ◀ ▶, `PixelText` para el tiempo, y el signo menos pasa a ASCII porque la fuente no tiene «−»;
  - los nombres bajo las tumbas de `CemeteryCanvas.cs`, en `PixelText` a escala 1.
- `tests/PermaLocke.App.Tests/HomeViewTests.cs`, una sola prueba STA (solo cabe una `Application` por proceso):
  - carga HOME, el VISOR, las 14 pantallas y los 3 diálogos;
  - los diálogos se construyen sin ViewModel: `GetUninitializedObject`, el constructor de `Window` y luego `InitializeComponent`, con `ExpandoObject` como datos porque enlazan en dos sentidos;
  - con la variable `PERMALOCKE_SNAP_DIR` guarda un PNG de cada diálogo.
- **Sprites que «no se veían»:** en las capturas de Claude faltaban los de gen 8-9 porque la copia aislada tenía `Expansion` vacía. Se arregló con la unión. En la carpeta del usuario están indexadas las 1025 especies y el arnés `scratchpad/spritecheck` dio 0 sin sprite. Si el usuario sigue viendo huecos, **hay que preguntarle qué Pokémon y en qué pantalla**.
- **Visto de pasada y sin arreglar:** en el texto del cementerio sale «#822» en lugar de Corvisquire. Ver [[Preguntas abiertas]].
- **Desplegado** en `Desktop\PermaLocke prueba\PermaLocke.exe` (md5 `900ab0b7…`). La carpeta de amigos no se tocó.
- **Pregunta: cómo evoluciona Gimmighoul.** En el mundo instalado es la especie 999, que pasa a 1000 con el **método 8 (usar objeto), arg 994 = Moneda de Gimmighoul** (`RomTool evo-dump`). La moneda cuesta 30 y, por `specialMartOrder [10,15,21,22]` y su posición 18ª en `specialMartItems`, debería estar en el mostrador 22 (Supermercado Ultraganga, el del centro), sin verlo jugando. El usuario confirmó el 2026-09-24 que pide 999.
- Documentado en `ARCHITECTURE.md` §176 quater y en `CLAUDE.md`. **Sin commit.**

## Antes (resumen por fechas; el detalle está en el §)
- **2026-08-17 a 08-19:**
  - Base, RPC de Azahar y equipo en vivo (§6, §16–18).
  - Randomizador por LayeredFS (§19–21, §27).
  - Mochila localizada por su estructura (§22).
  - El emulador viaja con la app (§25).
  - Gacha (§26).
- **08-20 a 08-21:**
  - Sprites de la ROM y tabla especie→icono (§28–30 bis).
  - Visor (§32) y wonder trade (§33, §44).
  - Logros detectados solos: récords, cristal Z por prueba y Dominsignias (§36–43).
  - Tienda e iconos de objeto (§45).
- **08-22:**
  - Roles, Pokémon extra y +20 % a los enemigos (§46–48).
  - Cap de nivel (§49, §53, §154).
  - Interfaz (§50) y EV (§51).
  - El enlace se caía (§54).
  - Ancla de zona muerta (§55).
- **08-23 a 08-28:**
  - PID y detección de muertes (§56).
  - POKE PASTE (§57) y premios (§60).
  - Cristales Z (§61).
  - LUDÓPATA y ruleta (§62–65).
  - Empezar de cero (§66–67).
  - Registro automático (§68).
  - Iniciales de línea de tres (§69) y evoluciones por intercambio (§70).
- **09-01 a 09-07:**
  - Interfaz con identidad (§74), `publicar.ps1` (§75), copia de la run (§76), MANTENIMIENTO (§77), COMPETICIÓN (§79) y combate por link (§80).
  - MAPA (§81–83), ruleta rehecha (§84, §86) y gacha (§87, §103–105).
  - Los PS del equipo y los parches del fork (§90–99).
  - Mod de gen 8-9 (`docs/MOD-EXPANSION.md`).
- **09-13 a 09-19:**
  - Muerte en el momento por las tablas del combate (§114), cementerio y killcam (§115) y cielo de Alola (§116).
  - Regla de primer encuentro con zona (§117–119) y avisos en pixel (§120).
  - Jugadores, carpeta por jugador y regalos del admin (§123, §129), JUGAR (§125–126).
  - Mod 1.4, habilidades de nueve bits, formas regionales (§131–140).
  - Recuerda-movimientos (§142–144), tiendas de evolución (§145), texto de los iniciales (§146).
- **09-20 a 09-23:**
  - **Distribución local** (`docs/DISTRIBUCION-LOCAL.md`): se abandona Drive.
  - Informes del PC del amigo y revisiones r2 y r3 (ver [[Incidencias del PC del amigo]]).
  - Mundo revisado (§151).
  - Arreglos jugando (§152–166).
  - Log (§167), Visual C++ junto al emulador (§168).
  - Cuarto del entrenador en JUGAR (§169), máquina de cápsulas del gacha (§171), zip (§172).
  - **Parche 4 del fork** que arregla los cierres (§173).
  - Cabina del wonder trade (§174) y ruleta de feria (§175).
  - Muestra pixel (§176).
