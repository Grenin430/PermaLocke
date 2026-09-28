---
tipo: historial
revisado: 2026-09-26
---
# Historial de conversaciones

La última sesión va arriba. Antes del 2026-09-23 solo hay un resumen por fechas sacado de `CLAUDE.md` y de `ARCHITECTURE.md`: el detalle está en el § citado ([[Índice de ARCHITECTURE]]). **Cada sesión nueva añade su entrada aquí.**

## 2026-09-27 — 1.0.4.1 a 1.0.5
- 1.0.4.1 instalada sola: **funciona**. Su ventana se cortaba arriba y abajo, y la versión nueva no se veía sin reiniciar (se miraba cada 30 min): 1.0.4.2 (§206).
- Preguntó si el límite de GitHub es para todos: es 60/h por conexión. Pidió cada minuto: 1.0.4.3 (§207). Lección medida: sin sesión, un 304 con ETag cuenta igual.
- Iñigo: un caído (SaintPablo) entró vivo en un combate tras una cura de la historia. Mirado con sus eventos del servidor (consulta con la sesión de Admin desde un proyecto temporal en el scratchpad) y su log: la cura llegó 0,8 s antes del combate. 1.0.4.4 (§208). El reemplazo del aviso de actualización ya existía (`Notifier.Pin` con la misma clave y la franja atada a `Available`).
- 1.0.4.5 (§209): aviso pequeño «X está jugando a PermaLocke» como en Steam, solo sobre el emulador; `--ensayar-amigo`.
- 1.0.4.6 (§210): el gacha a veces iba x3: `Loaded` repetido apuntaba `OnFrame` varias veces.
- 1.0.4.7 (§211): wonder trade 2 por 1 en el ÁLBUM con animación nueva (media de los dos totales, ±8 %, nivel el mayor; el usuario dio el visto bueno a la media), panel del cap sobre Azahar, tutores de PB prohibidos y tienda de PB de surf a Poké Balls (solo al regenerar), gacha sin la racha exprés (era el «x3» de los amigos). Un `PermaLocke.App.exe` colgado (PID 39272, sin permiso para cerrarlo) bloqueaba `binDebug`: los ensayos salieron de `binnsayo`.
- 1.0.4.8 (§212): el equipo (icono, nivel, PS) bajo el panel del cap y la casilla para quitarlo en CONFIGURACIÓN. Explicado qué hacen PAUSAR/REANUDAR de Admin.
- 1.0.4.9 (§213): PS del panel en tiempo real en combate con las tablas que ya se leen. El proceso colgado 39272 ya no estaba: pruebas de la App otra vez en verde.
- 1.0.5 (§214): fantasma nuevo (ensayo `--ensayar-fantasma`), caído curado antes de un combate: juanito con la 1.0.4.9 tuvo tres; el combate toma los PS de otro sitio y ahora se corrige dentro del combate en el banquillo; PS del panel con 3 s de retraso (destripaba los golpes). Lección: los ensayos con overlay salen encima del Azahar del usuario si lo tiene abierto.
- Pedido: acceso directo de Admin al día (Release recompilado) y la 1.0.4.1 con el aviso de primer encuentro 6 s después (`FirstEncounterDelay`) y el variocolor del gacha del 10 % al 2 % (§205). Subida directa a main para que la Action la publique.

## 2026-09-26 — plan del próximo torneo, desde la nube (y su puesta en marcha)
**Pedido:** implementar [[Plan del próximo torneo]] en su orden, solo en el repo (torneo en marcha), servidor solo con SQL
nuevos numerados que ejecuta él, nada de borrar datos sin preguntar, un commit por paso, cerebro y § al acabar cada uno.

**Hecho en el repo (rama `claude/relaxed-sagan-9amy20`, commits `TORNEO paso N`):**
- **Paso 1 (§194):** `15-eventos-y-limpieza.sql` (tabla `eventos`, `subir_eventos`, vista `logros_todos`, `limpieza`);
  `TournamentUpload` sube solo lo nuevo y vuelve a la subida entera si falta la función; `CommunityService` lee
  `logros_todos`; Admin: `ServerHistory` y ventana LIMPIEZA. SQL probados en un Postgres local que imita a Supabase
  (`tools/supabase/pruebas/probar.sh`, cluster en `/var/tmp/permalocke-pg`, puerto 5433).
- **Paso 2 (§195):** `FolderTransfer` (GameLink, probado) copia Saves, partida, mundo instalado, jugador, sesión y la ROM si
  falta; aparta lo que hubiera; nunca toca la carpeta vieja; se hace al arrancar tras reiniciar (`--tras-traspaso`).
- **Paso 3 (§196):** `Infrastructure/AppUpdate` + `UpdateService` (GitHub Releases, SHA-256, respeta reglas oficiales).
- **Paso 4 (§197):** `FirstRunGuide`, PRIMEROS PASOS en JUGAR.
- **Paso 5 (§198):** `16-copias.sql`; `Data/ServerBackup` (copia en caliente de SQLite + json de la run + partida + LEEME);
  `ServerBackupService` cada 6 h si cambió; Admin: pestaña COPIAS; LIMPIEZA deja 5 por jugador.
- **Paso 6 (§199):** `17-informes.sql`; `CrashReportUpload` + `Infrastructure/CrashReportQueue`; Admin: ventana INFORMES;
  LIMPIEZA quita los de más de 30 días.

**Puesta en marcha con el usuario (mismo día):**
- SQL: el aviso «destructive operations / _limpiar sin RLS» del SQL Editor es falso positivo (tabla temporal dentro de la
  función): **Run without RLS**. El 15 falló una vez con «unterminated dollar-quoted string»: había ejecutado solo un
  trozo (el editor ejecuta la selección). Entero, bien. 15, 16 y 17 verificados con una consulta de 13 comprobaciones,
  todas `true`. Desde la nube **no se llega a Supabase** (proxy 403): las comprobaciones las ejecuta él.
- Carpetas: desplegó solo `-Amigos`, descomprimió el zip en una carpeta nueva, usó TRAER MI PARTIDA desde
  `PermaLocke prueba` (**observado: bien en Windows**), borró la vieja y renombró la nueva a `PermaLocke prueba`. Los
  amigos ya tienen instrucciones para hacer lo mismo cuando puedan.
- PR [Grenin430/PermaLocke#1](https://github.com/Grenin430/PermaLocke/pull/1) (plan entero) aceptado. **Repo puesto
  público** (ver [[Actualizaciones y publicación]]).
- **§200, 1.0.1:** versión visible en CONFIGURACIÓN y `<Version>` del csproj como única fuente. Al hacer el paquete,
  `publicar-actualizacion.ps1` falló en Windows PowerShell 5.1 (`$PSScriptRoot` vacío en `param`); arreglado en PR #2.
  Al subir el zip arrastró primero otro fichero (>2 GB, el de amigos) a la caja de las notas: el zip va en «Attach
  binaries». **1.0.1 publicada a mano y entró** (captura: PERMALOCKE 1.0.1).
- **§201, 1.0.2 (PR #3):** ventana de descarga (barra, MB, %, velocidad, tiempo, CANCELAR) pedida por el usuario, y
  **GitHub Action** que publica sola la release al aceptar un PR que sube la versión (respuesta a «¿no se podría
  automatizar y las lanzas tú?»). 1.ª ejecución falló por `"$t:"` en el workflow, nada publicado; arreglo en PR #4, que
  además relanza la Action al cambiar el propio workflow. **En verde: la 1.0.2 se publicó sola y el usuario la instaló.**
- **§202 (1.0.3):** la 1.0.2 salió por la Action y el usuario la instaló. Pidió «1.0.2.1» (se hizo 1.0.3: la comparación es de tres cifras) sin la pregunta de sí o no: franja ámbar con `IconWarning` y ACTUALIZAR en todas las secciones, aviso fijo sobre el juego mientras haya versión esperando, y comprobación cada 30 min.
- **§203 (1.0.4):** pidió versiones de cuatro números y que la siguiente fuera 1.0.3.1. La 1.0.3 ya se publicaba y compara tres: una 1.0.3.1 no le llegaría. Se hizo la 1.0.4 con soporte de cuatro números; desde ahí, 1.0.4.1, 1.0.4.2…
- La 1.0.3 se publicó sola (v1.0.1, v1.0.2 y v1.0.3 en Releases). PR #6 (1.0.4) abierto. El usuario pidió dejarlo todo apuntado; tabla de versiones en [[Actualizaciones y publicación]].
- **§204:** al instalar la 1.0.3 desde la 1.0.2 la ventana se abría medio segundo y se cerraba. Log del usuario: `OverflowException` en `DownloadAsync` (`TimeSpan.MinValue`). La 1.0.2 y la 1.0.3 no pueden actualizarse solas; arreglado en la 1.0.4 (bucle en `Infrastructure/DownloadCopy`, con pruebas) y quien las tenga cambia el exe a mano una vez.

## 2026-09-26 — motes desde el VISOR, diez tandas de ideas y antitrampas de recarga
- **Torneo en curso:** desde hoy solo se toca el repo; nada de `desplegar.ps1` ni copiar a `PermaLocke prueba` ([[Usuario y forma de trabajar]]).
- **Mote desde VISOR › DATOS** (commit `4de0aa4`): `RenameService` (Core) + `IPokemonRenamer` → `SaveRenamer` (GameLink). Juego cerrado, copia `main-…-mote.sav`, PID, `PokemonBuilder.InPlace`, relectura; evento `PokemonRenamed`; vacío = nombre de especie; 12 letras. Probado en `SaveRenamerTests`; **sin probar en partida real**. Gimmighoul: «reuniendo 999 Monedas de Gimmighoul».
- Ampliar el ÁLBUM y nueve tandas más de ideas (una con investigación externa): todas rechazadas ([[Ideas para el futuro]]). No proponer más listas sin una pista suya.
- **Antitrampas de recarga (§192), hecho:** teclas de estados y reinicio fuera + estados movidos a `Saves/estados-retirados` y anotados; la ventana no se cierra con el juego abierto; `EmulatorJob` mata Azahar si PermaLocke muere; tiempo de juego del save contra `partida-vista.json` (fuera/retrocedida); cierre (no fallo) en los 6 s tras un combate = abandono. Evento `IntegrityFlag`, solo en AUDITORÍA de Admin (decisión del usuario). Sin probar jugando.
- **Admin ampliado (§193):** órdenes (`AdminOrder`, esquema 3 del canal de regalos) que aplica `OrderService` en la app: revivir, marcar caído, revocar wipe, liberar ruta, pruebas, dar objeto/Pokémon, mensaje, cerrar/abrir juego (`PlayLock`). FICHA por jugador, PAUSAR/REANUDAR TORNEO, REGLAS oficiales (`14-reglas.sql` + `RulesSync`). Falta que el usuario ejecute el SQL; sin probar con servidor.
- **Admin rediseñado** (commit `6713e89`): tema plano propio en `PermaLocke.Admin/App.xaml` (pestañas, campos, desplegables con `PART_EditableTextBox`, casillas y tablas oscuros y legibles; `Faint` aclarado) y cabecera en dos filas (TORNEO / CONTROL). Visto en capturas por UI Automation. El usuario ya ejecutó `14-reglas.sql`. El acceso directo del escritorio apunta a `src/PermaLocke.Admin/bin/Release`: recompilar Release tras cambiar Admin.
- **Plan del próximo torneo** decidido y escrito en [[Plan del próximo torneo]]: orden 1 subir solo lo nuevo + LIMPIEZA en Admin, 2 traspaso de carpeta, 3 actualización (GitHub Releases), 4 primera vez, 5 copias, 6 informes. Sin empezar.
- Antes de hacerlo: ya cubierto que un muerto sigue muerto (evento + `SaveDeathEnforcer` al cerrar + `KeepFallenDownAsync` en vivo) y que una ruta gastada sigue gastada (`ZoneEncounterSpent` en la run). Huecos: estados guardados de Azahar, cerrar/resetear en mitad de un combate antes de que se detecte la muerte, jugar sin PermaLocke abierto y restaurar la partida desde una copia. Locke (`Desktop\Locke\data`) solo tiene `AntiTamperUSUM`: hash del emulador y aviso por correo; no bloquea estados (hay un `.cst` en su `user/states`).

## 2026-09-26 — reiniciar desde Admin no se veía en la app (§191), desde la nube
- El usuario reinició las dos runs desde Admin para empezar ya de verdad y su app seguía igual. Es lo diseñado: `reiniciar_run` solo archiva en el servidor; en el PC hay que pulsar HOME → EMPEZAR DE CERO (borra run y partida, con copia). Faltaba que la app lo dijera: ahora lo detecta (`runs.activa`) y avisa en JUGAR y encima del juego.

## 2026-09-26 — la carta de cada captura vuela al álbum (§190), desde la nube
- Sexta tanda de ideas, enfocada en «espectáculo encima del emulador» o «lucir la app» (la quinta, rechazada entera). Eligió «nueva carta al capturar», solo salvajes: álbum pequeño que entra por la derecha, la carta sale de la Poké Ball, se mete en el álbum y el álbum se va.
- Hecho: `EncounterGuard.Caught` (récord de capturas, al acabar el combate), el salvaje leído entero y guardado, `SaveBoxReader.Describe(PK7)`, `TcgCardFactory` (sacada del álbum), `CatchScene` + `CatchWindow` + `CatchCeremony`, ajuste «Carta al capturar», `--ensayar-captura`. La ball es nuestra, donde está el entrenador, porque el juego cuenta la captura al acabar el combate.
- PixelCheck 69 correctas; `captura.png`.
- **El usuario lo probó: «todo perfecto».** Quedan confirmados también el gacha del §189 (con la línea de estado sobre el suelo) y el álbum fluido del §188.

## 2026-09-26 — GACHA con más golpe y cincuenta seguidas sin cansar (§189), desde la nube
- Tras desplegar prueba y amigos con el §188 (el comando necesita `-ExecutionPolicy Bypass`, ya apuntado en `desplegar.ps1` y `CLAUDE.md`), el usuario pidió «potenciar» el gacha y que 50 tiradas seguidas no cansen.
- Decidido por mí: la primera tirada entera; las seguidas (≤20 s tras la anterior) en **exprés ×2,5 hasta que se abre** la ball; **SALTAR** en el mismo botón, en la máquina y con ESPACIO/INTRO; «RACHA ×N» en el neón. Nada depende del tier antes de abrirse (regla del §171).
- Golpe: foco que se cierra en los meneos, luz por la junta, tinte + sacudida + aro + «¡SUBE!» al subir, fogonazo en tramado y sacudida por rareza al abrir, Pokémon que sale al triple, confeti (tiers altos y variocolor), monedas y «¡LEGENDARIO!», «¡VARIOCOLOR!» arcoíris.
- PixelCheck compila ya `PixelScene` y la máquina (sustitutos en `WpfImaging.cs`) y saca `gacha-*.png`. 66 correctas. ~2 ms por fotograma.
- El usuario lo probó: «todo correcto» salvo que al salir el Pokémon la sala se alejaba y volvía. Era la línea de estado bajo TIRAR, que le quitaba alto a la máquina (3→2 px por celda) hasta que se borraba a los 5 s. Movida encima del suelo de la sala.

## 2026-09-26 — la carta en la mano iba a trompicones (§188), desde la nube
- El usuario: «está increíble, pero al inspeccionar la carta va muy muy muy bajo en fps».
- **Causa:** el `Color` de WPF convierte a scRGB con `Math.Pow` al construirse; la mano hacía varios por píxel de pantalla. Medido con un sustituto que imita ese coste: ~100 ms por fotograma.
- Hecho: `PixelColour` (cuatro bytes) en todo el dibujo del álbum vía `using Color = ...PixelColour;`; `HandScene.Card` con luz y reflejo por celda y proyección incremental; `AlbumStage.IsPaused` mientras se inspecciona; `CardStage` a 60 fps si pintar cuesta < 6 ms.
- Medido: mano ~100 → 4 ms, página girando 19 → 5 ms. Prueba nueva `A_frame_in_the_hand_is_cheap`. PixelCheck 46, Core 380, Rules 151, Randomizer 485, GameLink 389; imágenes iguales. Sin ver en Windows.

## 2026-09-26 — ÁLBUM premium (§187), desde la nube
- Petición: hacerlo «muchísimo más impresionante», acabado premium, sin perder pixel art ni funciones, decidiendo yo. Dirección: «colección nocturna del ultraespacio» (carpeta de piel bajo lámpara, acabados de tirada real, carta en la mano tipo Balatro).
- **Fallo encontrado:** con `GameNeed.Either` la franja «ABIERTO O CERRADO» dejaba el álbum a 1 px/celda en GRANDE. Ahora `None` + aviso pequeño en la barra + carpeta sin tapa si no cabe.
- Hecho: `TcgFinish`/`TcgRegion` (holo, inversa, dorada, polícroma; `CellCanvas.Regions`), `Animate(..., light)`, `HandScene` (homografía por píxel, vuelo, muelle, vuelta, rayos, polvo, sombra real), `AlbumScene` reescrita (piel, pespunte, cantoneras, pestañas, cabeceras, emblema, lámpara, destello, página en perspectiva), `CardStage` y `AlbumStage` nuevos, `OpenTabCommand`, `TitleOf`.
- Nuevo `tools/PermaLocke.PixelCheck` (fuera del slnx): las `AlbumTests` en Linux y PNG con `PERMALOCKE_PIXEL_DIR`. Sacados a ficheros puros: `SmallFont`, `TypeColours`, `RoomSprite` (partial), `AlbumPaging`.
- Verificado: solución compilada sin avisos; Core 380, Rules 151, Randomizer 485, GameLink 389, PixelCheck 45. Sin ver en la app.

## 2026-09-26 — ÁLBUM de cartas TCG (§186), desde la nube
- Idea del usuario a partir de los cromos: el visor como álbum de cartas TCG. Decisiones suyas: **sección nueva, solo ver** (sin wonder trade ni nada), **probar 3×3 y 4×4**, rareza = **tier del gacha** (y los que no salieron del gacha, por su BST/tier), caídos **arrugados o medio quemados**, «lo más detallado y pulido posible».
- Hecho: `GachaService.TierOf/TierIndexOf` (+pruebas), `TcgCard`/`CellCanvas`, `TcgCardArt` (completa 70×96, pequeña 50×68, reverso ficha, dorso/huevo, holo animada, quemada con brasas), `AlbumScene` (fundas, anillas, vuelta de hoja), `AlbumStage`, `CardStage` (carta en grande que se da la vuelta), `AlbumView`, `AlbumViewModel` en EQUIPO tras VISOR. `AlbumTests`.
- **Descubierto:** en la nube se puede compilar: `apt-get update && apt-get install -y dotnet-sdk-10.0`, y `dotnet build src/PermaLocke.App -p:EnableWindowsTargeting=true` compila WPF en Linux (0 avisos). Tests no-WPF (Core) se ejecutan: 380 correctas. Las de App (WPF) solo compilan. El dibujo se verificó con un programa de consola en el scratchpad (`cardpreview`, stub de `Color`) que saca PNG.
- Sin ver en la app ni con iconos reales. Pendiente de que el usuario lo compile y diga qué cambiar.

## 2026-09-26 — «Jugando a PermaLocke» en Discord (§185), desde la nube
- Cuarta tanda de ideas, ya sin tocar la jugabilidad (el usuario **no quiere ideas que alteren la jugabilidad**): estado en Discord, cromos, álbum de fotos automático, el viaje en el MAPA, biografía de cada Pokémon, resumen de sesión, sonido chiptune. Eligió solo el estado de Discord, y **solo** «Jugando a PermaLocke» con el icono, también con el emulador abierto.
- `DiscordPresence` (tubería `discord-ipc-N`, sin NuGet), `AzaharInstallation.DisableDiscordPresence` (`enable_discord_presence=false` en `[UI]`), claves `discordApp`/`discordImagen` en `Data/torneo.json` (vacías: **el usuario tiene que poner el id de la aplicación de Discord** llamada PermaLocke, con su icono en el Developer Portal). Sin compilar ni ver.
- Recordar: la carpeta de prueba y la de amigos tienen su propio `Data/torneo.json`: hay que copiarles las claves nuevas.
- El usuario creó la aplicación de Discord «PermaLocke»: id `1552637220228169770`, ya en `discordApp`. Sin `discordImagen` (se usa el icono de la aplicación).
- **Funciona** en el PC del usuario (lo juntó en su `main` local con `git merge origin/claude/relaxed-sagan-9amy20`). No salía porque tenía apagado en Discord «Compartir tu actividad detectada»: sin eso no sale nada y el log no dice nada. Sin probar con Azahar abierto a la vez.
- El usuario desplegó con `desplegar.ps1 -Prueba` (+ `torneo.json` copiado a mano a `PermaLocke prueba\Data`) y `-Amigos`: la carpeta de amigos lleva fantasmas, lluvia (12 s) y estado de Discord. Pendiente de confirmar: si ejecutó `13-lluvias.sql`. **Los amigos aún no juegan** (2026-09-26): el usuario sigue probando él solo; la carpeta de amigos es solo lo preparado para repartir.

## 2026-09-26 — la lluvia de sangre (§184), desde la nube
- Sesión en un **contenedor Linux en la nube** (rama `claude/relaxed-sagan-9amy20`): sin Azahar, sin el Escritorio del usuario y **sin .NET** (la red bloquea `builds.dotnet.microsoft.com`). Nada compilado ni visto: lo compila y prueba el usuario.
- Petición: al hacer wipe, lluvia roja «como sangre» ~30 s en todos los demás y en quien lo hace, como los fantasmas.
- `Views/BloodRain.cs` (pura, probada en `BloodRainTests`; umbrales comprobados con la simulación en Python), `GhostWindow.RainAsync`, `GhostService.Rain`/`ReadRainAsync`/`RehearseRainAsync`, `PlayNotifications` en `TeamWiped`. Misma casilla «Fantasmas». Ensayo `--ensayar-lluvia`.
- Tabla `tools/supabase/13-lluvias.sql` aparte de `fantasmas` (una app vieja pintaría un fantasma vacío). **La ejecuta el usuario.**
- El usuario la vio con `--ensayar-lluvia`: «va perfecto». Luego pidió **12 s en vez de 30**: parada a 9 s, apagado 10-12 s, charco al doble de ritmo. Descartadas: cementerios y killcams de los demás (peso de las killcams en Supabase) y dos tandas de ideas (hitos, epitafio, ruta compartida, muro de la vergüenza, apuestas, evento temporal; Soul Link automático, manchas de sangre, mausoleo, invasión): «ninguna sorprende».
- Tercera tanda más amplia (reglas, economía, crónica): le llamó la atención **objetos de Mario Kart**; la dejó apuntada para el futuro en [[Ideas para el futuro]], sin hacer. Allí están también las rechazadas.

## 2026-09-26 — los fantasmas (§183)
- Idea del jugador tras rechazar dos tandas de ideas mías (quiere que se le propongan ideas, no que se haga nada sin pedirlo; «tarjeta de entrenador» y listas de mecánicas descartadas). Decisiones suyas: el fantasma sale por la IZQUIERDA; varios en cola; casilla en CONFIGURACIÓN.
- `GhostService` (envía al momento, lee cada 10 s solo con Azahar abierto, cola), `GhostWindow` (aviso arriba a la izquierda + cruce derecha→izquierda), `GhostArt`, `DeathWindow.AddGhost`, `ToastKind.Ghost`, ajuste `Ghosts` (vive en `DeathCeremony.Ghosts` para no crear ciclo con `EmulatorLauncher`→`AppSettings`). Ensayo `--ensayar-fantasma`.
- Tabla `tools/supabase/12-fantasmas.sql`: **la ejecuta el usuario en Supabase**; sin ella no viaja nada (fallo registrado una vez).
- Visto en capturas de los ensayos. La trama de ajedrez se descartó (tablero a 8 px/celda). Sin probar entre dos PCs.
- El usuario ejecutó `12-fantasmas.sql`: comprobado con la clave pública (401 «permission denied for table fantasmas», frente a 404 de una tabla inexistente). Carpeta de amigos rehecha con los fantasmas; la anterior queda como `(anterior)`.

## 2026-09-25 — la 7ª prueba no salía en LOGROS (§182)
- Reporte: superada la 7ª (Chris, Electrostal Z 810) y no sale. El jugador sospechó del seguidor: no era. PKHeX sobre copia del save: 810 presente; save guardado 23:31:29, leído 23:31:40.
- Causa: los logros `item` solo miraban el fichero (`SaveRecordReader`); `TrialZoneService` mira la mochila viva. Arreglo: `AchievementService` recibe `IItemDelivery?` (opcional) y une fichero + mochila viva. +2 pruebas. 1501 pruebas. Desplegado en `PermaLocke prueba`. Carpeta de amigos rehecha con el arreglo; la anterior queda como `(anterior)`.
- La run de prueba tiene los logros 1-6 cobrados a mano (eventos AchievementUnlocked de 22/09 a 25/09). `Probe --run` con `PERMALOCKE_ROOT` enseñó OTRA run (50 eventos): no respeta esa variable; la real tiene 1562 eventos.

## 2026-09-25 — Pokémon que te sigue (§181, integrado)
- No viene en el mod de gen 8-9: es otro mod del mismo autor, «Pokemon Follower Mod (Includes SM)», gamebanana.com/mods/694400, CC BY-NC-ND. Plugin 3GX `Gen7FieldFollower.3gx` en `sdmc/luma/plugins/00040000001B5100/`, zip `ultra_moon_1e982.zip` (sha256 `827aa023…0485`). Menú con Start+Select.
- Probado en copia aislada `%TEMP%plf` (emulador + partida copiados; `load` copiado entero): el Pokémon de cabeza (Dragonite) sale detrás del jugador; `Probe --equipo` lee las dos estructuras del equipo (espejo en `0x330128E4` como siempre); `FieldZoneReader` real da Ciudad Malíe con 3 registros, también andando. Partida real sin tocar.
- **Trampas:** Azahar ignora `plugin_loader=true` si `plugin_loaderdefault=true` (hay que poner `default=false`, como con el RPC). La ruta del scratchpad pasa de 260 caracteres con la ruta de la partida: el emulador no la abre y la copia falla sin avisar; usar rutas cortas.
- El jugador lo probó en la copia (gen 8-9 de cabeza, hierba, combates): sin fallos. Copia borrada con su permiso.
- Integrado: `AzaharInstallation.SetFollower` (+3 pruebas), llamado en `EmulatorLauncher.Launch`; ajuste `Follower` en `AppSettingsData` y casilla en CONFIGURACIÓN; plugin en `Emulator/follower/` con `LEEME.txt` (créditos; solo el LEEME se versiona); `publicar.ps1` lo exige.
- Commit `afa03fa`. Desplegado en `PermaLocke prueba`: exe nuevo y `Emulatorollower` copiado (sha256 `0345866b…`). Se instala en su emulador la primera vez que pulse JUGAR. Carpeta de amigos rehecha con `desplegar.ps1 -Amigos` (171 ficheros, zip 2250 MB); la anterior queda como `(anterior)`.

## 2026-09-25 — iconos variocolor (§180)
- Pedido: sprites variocolor. El cartucho no los tiene. Por indicación del usuario, colores sacados de los renders de Showdown (`dex`/`dex-shiny`, y `home`/`home-shiny` si faltan o no cambian): comparando normal y variocolor píxel a píxel se sabe en qué se convierte cada color, y se aplica al icono de la ROM.
- Afinado en prototipo (scratchpad) con Charizard, Pikachu, Gyarados, Gengar, Umbreon y Rayquaza: RGB aditivo → HSL → LCh con **mediana** de 16 vecinos y peso de luz 1,5 (el que separa cuerpo y llama de Charizard).
- `Randomizer/Sprites/ShinyPalette.cs` (cálculo, sin WPF) y `PngImage.Decode` (nuevo). `RomTool variocolor` → `Data/variocolor.json` (tabla de colores por índice de icono, no dibujos) y 12 hojas de revisión en `%TEMP%/permalocke-variocolor` con `leyenda.txt`. Descargas en `%TEMP%/permalocke-showdown`.
- Medido: 16 de 1246 parejas de Showdown en otra pose (Ogerpon, Naganadel…), solape de siluetas < 0,84; el resto ≥ 0,92 → umbral 0,9. Resultado 1123 iconos con tabla, 6 sin referencia (Basculin blanca, Eiscue sin hielo, Maushold x4, Armarouge, Ceruledge, Terapagos astral).
- App: `PokemonSpriteService.Get(especie, forma, variocolor)`; enganchado en VISOR, EV, MOVIMIENTOS, HOME, JUGAR, GACHA, WONDER TRADE, CEMENTERIO, escena de muerte y aviso de caída (`DeathNotice.Shiny` nuevo). 1496 pruebas. Comprobado el servicio real con la ROM. **Desplegado en `PermaLocke prueba`** (exe md5 `eb8e8da7…` + `Data/variocolor.json` copiado, era nuevo). Sin ver en la app con un variocolor real.
- Commit `093b03e`. Carpeta de amigos republicada con `desplegar.ps1 -Amigos` (primera vez que se ejecuta, funcionó): la de antes y su zip quedan como `PermaLocke para amigos (anterior)`. Zip 2250 MB, carpeta 2738 MB, `variocolor.json` dentro.

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
- Limpieza (confirmada por el usuario): borrados `SnapshotStore`, `GiftStore`, `SharedFolderSettings`, `SeenMarksStore`, `OfficialRules`, `OfficialRulesService` y sus pruebas (también `RunSnapshotBattleTests`, que solo probaba la carpeta). `SyncService` queda solo con `PlayerAsync` y `BuildAsync` para la subida. 1483 pruebas.
- Admin: al entrar con Discord la cabecera enseña tu foto, nombre y SALIR, y el botón ENTRAR desaparece; la auditoría ya no tiene su propio ENTRAR.
- Apuntes del amigo: bandeja de REGALOS de la app en pixel (`MainWindow.xaml`); Admin sin «A todos», con SELECCIONAR TODOS encima de JUGADORES (un regalo por jugador marcado). Supercaramelo descartado por el usuario (un objeto nuevo en el juego no es viable). Baya Tamate (174) en la tienda de incienso de Konikoni con `price` 10 (pidió 1; 10 es el mínimo): solo en mundos generados después. `Tiendas especiales.txt` al repo, con la TIENDA de PermaLocke, y se reparte como `TIENDAS.txt`.
- Sección INFORMACIÓN (`InformationViewModel`/`InformationView`, antes de CONFIGURACIÓN): pestañas EVOLUCIONES (agrupadas por cómo evolucionan ahora, con sprites de especie, forma y objeto) y TIENDAS DEL JUEGO (mostradores especiales con icono y precio). Datos de `Data/informacion.json`, que genera `RomTool informacion` desde el cartucho + mod y `randomizer.json` pasando por `ImpossibleEvolutionFixer` (`Partners` ahora público). 45 evoluciones, 7 mostradores.
- INFORMACIÓN: buscador por pestaña (sin tildes ni mayúsculas), sin textos de explicación ni «antes:», flecha verde en las que evolucionan al subir de nivel. `ItemIconIndex` medido para piedras 107-110, Baya Tamate, objetos de intercambio (221-252, 321-327), 537, 646-647 y Piedra Hielo 849→698.
- Barra lateral agrupada: `GroupSectionViewModel` (pestañas; el aviso de juego es el de la pestaña visible). EQUIPO = VISOR, ENTRENAR EV, MOVIMIENTOS, POKE PASTE; TORNEO = COMPETICIÓN, LOGROS, COMBATES, CEMENTERIO. `MainViewModel.Navigate(título)` encuentra también pestañas (JUGAR, `--seccion`, visor→EV). Las pestañas cuelgan en la barra como subdesplegable (PxNavItem + NavChildrenTemplate); arrastrar con el botón pulsado ya no cambia de sección (se suelta la captura del ListBox). Luego: JUGAR = JUGAR, RANDOMIZADOR; PUNTOS = TIENDA (+ RULETA si la run juega con ella, en `_points.Pages`); INFORMACIÓN = EVOLUCIONES, TIENDAS (dos `InformationPageViewModel` sobre el mismo `InformationViewModel`), MISCELÁNEA. Barra: 9 entradas.
- Megapiedras de Z-A: `BagLayout.ExpansionMegaStones` (505-520, 961, 995-1023) al bolsillo de Objetos; PKHeX no las conocía y la tienda no las entregaba. Mapa: Konikoni a sinEncuentros, Colina del Recuerdo con marcador (0.334, 0.927). Wonder trade ±9% (BxnnyLocke, leído con ILSpy: ±9% sobre BST+1%, se ensancha de 5 en 5, 28 especies prohibidas, sin pesos). Franja grande de juego abierto/cerrado (`NeedHint`). Subida cada 2 min. Avisos encima del juego para regalos (`ToastKind.Gift`) y anuncios. Admin: AJUSTAR PUNTOS (AdminGift.Adjustment, esquema 2, la app lo aplica sola) y CONSUMO (`11-consumo.sql`, función `consumo()` solo organizador). `Fleeting`: mensajes de resultado se borran a los 5 s. Pokémon caído que luchó tras curación de historia: el usuario lo descartó (posible hueco: curación e inicio de combate en el mismo segundo).
- 2026-09-24/25, misma conversación (commits 2c72315 → 7aa6bd5):
  - Subida al torneo cada 2 min. Avisos encima del juego para REGALO y ANUNCIO (`ToastKind.Gift/Announcement`).
  - Admin: AJUSTAR PUNTOS en panel propio (AdminGift.Adjustment, esquema 2; `GiftInbox` lo aplica solo, sin recoger) y CONSUMO (`11-consumo.sql`, `UsageWindow`). El usuario confirmó que el ajuste funciona.
  - `Fleeting.Fade`: mensajes de resultado se borran a los 5 s (Status/TrainStatus/Problem en las secciones de acción).
  - Kit pixel: celda con `Math.Floor` (no redondeo) en PixelText/Icon/Sprite; con Windows al 125 % el texto salía un 20 % más ancho y se cortaba en el PC del amigo.
  - Dupes clause: `EncounterGuard` cuenta también el equipo en memoria (regalo del juego sin guardar: Zweilous de las dominsignias).
  - Randomizador: `fullyEvolvedFromLevel` 29 → 25; Dominantes y sus aliados (filas tipo 0 seguidas, nivel hasta 10 por debajo) pasan a evolución final desde el corte (`StaticEncounterRandomizer.EvolveTotems`). Megas enemigas: todas las especies con mega, legendarios de `bannedSpecies` y Rayquaza incluidos (86). Sina (clase 85) es combate importante.
  - Tienda: megapiedras de legendarios a 600; «Ascenso Draco (Rayquaza)» es un DESBLOQUEO (`ShopItem.UnlockMove/UnlockSpecies`, id falso 90620, evento `ShopPurchase` con `desbloqueo`): se paga una vez y MOVIMIENTOS lo ofrece gratis a Rayquaza (`RememberedFrom.Unlocked`, etiqueta MEGA, borde multicolor).
  - Barra: RULETA suelta bajo GACHA, TIENDA vuelve a sección (sin PUNTOS). Franja grande de juego abierto/cerrado (`NeedHint`). Textos de la app acortados (el usuario no quiere «descripciones de IA»).
  - ENTRENAR EV y MOVIMIENTOS rehechos (ficha en franja, gráfica REPARTO con colores por estadística `EvHexagon.StatColours`, filas de una línea, órdenes fijas abajo; MÁX y + no pasan de 510: `EvSpread.RoomFor`). App más compacta (barra 226, márgenes, `PixelWindow.BandHeight` 32, `PxNavItem` 31).
  - Gacha: `CapsuleMachine` a celda 2 si a 3 no cabe (pantallas bajas). Variocolor del gacha al 10 % en todos los tiers.
  - INFORMACIÓN: 230 evoluciones (todas las que no son «subir al nivel N» a secas, grupo OTRAS FORMAS, formas regionales con nombre); `RomTool informacion` describe todos los métodos 1-42.
  - Iconos: Pheromosa/Xurkitree estaban cruzados en `PokemonIconIndex`; revisadas a ojo las 1025 especies (`RomTool iconos-nombres` + hoja montada con PowerShell). Formas alternativas sin revisar.
  - Wonder trade ±9 % (leído BxnnyLocke con ILSpy, instalado `ilspycmd` con permiso). Mapa: Konikoni fuera, Colina del Recuerdo dentro (marcador 0.334, 0.927).
  - Descartado por el usuario: el caído que luchó tras una curación de historia; auto-actualizador (explicado, no hecho). Los sprites variocolor se hicieron después en otra sesión (§180, desde Showdown).
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

## 2026-09-27 (tarde): 1.0.5.1 → 1.0.5.9
Panel del cap (vida al subir de nivel, fuera de las capturas de pantalla, tamaño adaptable), ventana SE ADAPTA, fantasmas
sin emulador y aviso con los demás, segundo plano con icono junto al reloj, buzón de sugerencias (18-sugerencias.sql),
cementerios de todos con killcams en H.264 por Media Foundation (19-caidos.sql, `FallenShare`, `KillcamVideo`, `Mf`),
pestañas de jugadores en el CEMENTERIO, cámaras de Azahar en blanco, `.old` bloqueado en la actualización. Pantalla en
negro del estudio de fotos: gen 8-9 en el equipo (ver Trampas y lecciones).

## 2026-09-27 (noche): 1.0.5.10 → 1.0.6
- 1.0.5.10: panel del cap otra vez ventana normal (WDA_EXCLUDEFROMCAPTURE lo escondía); se esconde solo mientras
  `HpBarWatcher` mira la barra. WinForms aislado en `BackgroundMode.ShowIcon` (el cierre de una actualización, desde el
  `.old`, no podía cargarlo) y `BackgroundMode.Leaving` para que actualizar cierre de verdad.
- 1.0.5.11: los estáticos (Dominantes, aliados, fijos) borran los 4 movimientos del cartucho (0x0C) al cambiar de especie.
- 1.0.5.12: la comprobación de estáticos deja pasar legendarios mega donde los puso la regla Mega (seed 111: Darkrai mega
  en el Nihilego paraba todo el randomizado, también en la app).
- 1.0.6: `megaStonePrice` 20000 en randomizer.json: las megapiedras (método 1 de la tabla de megas a/0/1/5, 94 con el mod)
  se venden por 10000. Solo runs nuevas: no se toca el mundo instalado del torneo.
- Pendiente: devolver balls en Centro Pokémon (se explicó que no arregla la libreta a 0; falta el log del amigo),
  aviso del estudio de fotos con gen 8-9, seed elegible al crear run.
- 1.0.6.1: TIENDA › HIERBAS, las 25 naturalezas a 500 (Data/shop.json, `categoria: hierbas`, `naturaleza` 0-24, id
  91000+n). Las Mentas no existen en el juego: la hierba escribe la naturaleza en la partida con el juego CERRADO
  (`SaveNatureChanger`, guardas del mote, `StatCalculator.Restat` en el equipo) y luego cobra (`ShopService.ChangeNatureAsync`).
  Icono pixel propio (`HerbArt`). shop.json es regla oficial: si está en el servidor, publicarla desde Admin › REGLAS.
- 2026-09-28 (sin publicar): lista de los jugadores casi entera. Hechas 4, 10, 2, 11, 6, 3, 12, 13 y ahora 1 (montones de
  bayas de una vez, `BerryPileKeeper`), 5 (Rotombola apagada al pulsar JUGAR, `SaveRotoLoto`), 9 (motes votados,
  `NicknameVoteService` + `20-motes.sql`) y 7 primera fase (HOY, ayudas y confirmaciones en Admin). Falta 8 (Banda Focus).
  Detalle y lo que queda por medir jugando en «Plan de la lista de los jugadores (2026-09-28)».
