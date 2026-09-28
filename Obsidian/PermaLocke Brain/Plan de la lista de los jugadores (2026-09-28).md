# Plan de la lista de los jugadores (2026-09-28)

Decisión del organizador: **se hacen todas**. Es lo único que toca ahora. **No se publica ninguna actualización hasta
acabar la lista entera**, salvo que él diga expresamente que se publique. Lo del juego (randomizado) solo llega a runs nuevas.

## Fáciles (en curso)
- [x] 4. Parpadeo del panel del cap al morir: no esconderlo mientras `HpBarWatcher` mira la barra; que la lectura se salte
  la zona del panel. Y el parpadeo de la lluvia de sangre.
- [x] 10. Ruleta: quitar Revivir (28), Revivir Máximo (29), Hierba Revivir (37) y Ceniza Sagrada (44) de los curativos.
  `roulette.json` es regla oficial: el organizador la sube desde Admin › REGLAS.
- [x] 2. Tienda de combate de la app: interruptor en Admin (abierta/cerrada) y apertura por prueba; aviso en la app.
- [x] 11. Carta al álbum también para huevos, fósiles, regalos: cualquier Pokémon nuevo registrado.

## Medianas
- [x] 6. Subida de dificultad explicada en la app, gráfica, detallada, original (base: `subida_de_dificultad_explicada.txt`).
- [x] 12. MT/objetos repetidos: medir con RomTool (suelo + NPC + tiendas) y que cada MT salga una vez.
- [x] 13. Dinero: mover lo especial del Supermercado Ultraganga (cupón) a otra tienda, actualizar TIENDAS; precios a hablar.
- [x] 3. Reinicios para repetir (Cápsula Habilidad…): aviso en Admin si habilidad/naturaleza cambia y vuelve.

## Difíciles / investigar
- [x] 1. Montones de bayas que no se regeneren (reloj del juego; investigar código/datos).
- [x] 5. Rotombola (Roto Loto): bloquear o, si no se puede, aviso en Admin.
- [ ] 8. Banda Focus: en todos los juegos se gasta (la Cinta Focus no). Tocar el combate es arriesgado: decidir con él.
- [x] 7. Admin «a prueba de niños»: inicio con lo del día, explicación en cada botón, asistentes, confirmaciones.
- [x] 9. Motes votados en los emuladores de los demás: tabla, ventanas interactivas, mote con el juego ABIERTO (probar
  con cuidado, ver «Subir los PS actuales» en Trampas).

## Hecho (2026-09-28, sin publicar)
- 4: `CapBadge.Place` encoge el panel para no llegar a la barra de PS; fuera el esconderse de 1.0.5.10.
  `OverlayWindows.PlaceOver` solo llama a SetWindowPos si cambió la caja: las ventanas «encima» se peleaban cada
  0,1-0,5 s por ponerse delante (parpadeo del panel, de la lluvia y del fantasma).
- 10: roulette.json sin Revivir (28) ni Revivir Máximo (29) (no había Hierba Revivir ni Ceniza). Subirla desde REGLAS.
- 2: shop.json «abierta» y «abreEnPrueba»; `ShopService.ClosedReason`; Admin › TIENDA: CERRAR / ABRIR / ABRIR EN PRUEBA…
  (sube la regla oficial; se aplica al reiniciar PermaLocke).
- 11: `GameLinkMonitor.NewcomerArrived` al registrar solo un Pokémon nuevo del equipo; `CatchCeremony.CelebrateNewcomer`,
  sin repetir carta por PID si ya salió como captura.
- Tienda cerrada: velo negro translúcido sobre toda la TIENDA que se traga los clics, «TIENDA DE COMBATES NO DISPONIBLE».
- 6: INFORMACIÓN › DIFICULTAD (`DifficultyViewModel`, `DifficultyView`, `Data/dificultad.json`; caps de levelcaps.json).
- 3: `GameLinkMonitor.WatchRerollsAsync` cada 5 s: habilidad/naturaleza que cambia y vuelve en 6 h → `IntegrityKinds.Reroll`.
- 12: MT del suelo = las del cartucho barajadas; tiendas de MT = las que vendía el cartucho, una vez entre todas
  (`FillWithMachines` con bolsa compartida); `fieldItemsMaxRepeats` 1. Medido (semilla 111): 29 MT de tienda distintas,
  0 en suelo y tienda. Las MT que regalan NPC no se pueden leer: por eso solo se usan las de suelo/tienda del cartucho.
- 13: `specialMartItemPrice` 50000 → 30000 (lo pidió; «ya te iré contando» los demás precios). `specialMartOrder`
  [10,15,21,22] → [10,15,25,26]: fuera del Ultraganga (cupón); el 24 no (pk3DS lo pone en la Ruta 3, sin Centro). 25/26: objetos X, pueblo por ver
  jugando. informacion.json regenerado (RomTool informacion).
- 1 (sin probar jugando): `BerryPileKeeper` (GameLink/Field). Bloque 22 del save (BerrySpot) = 64 montones × 4 bytes
  (estado + 3 de tirada); en la partida del organizador todos tenían estado 2 → 2 = «crecido» (SUPUESTO, sin medir
  cogiendo uno). Se localiza en RAM una vez por las tiradas (búsqueda, no barrido en bucle), cada 5 s: montón con estado
  ≠ 2 se apunta en `Saves/backup/montones-cogidos-<run>.json`; si vuelve a 2 se le reescriben sus bytes de cogido. El
  juego regenera por su reloj, así que el fichero de partida no sirve: solo la copia viva. Medir: `Probe --campo antes.txt`,
  coger un montón, guardar, `Probe --campo despues.txt`, `Probe --campo-diff antes.txt despues.txt`.
- 5 (sin probar jugando): los Roto-poderes NO son objetos (ningún bolsillo acepta 1579; «Roto» no sale en la lista de
  objetos). `SaveRotoLoto.TurnOff` apaga `FieldMenu.RotomLoto1/2` al pulsar JUGAR (juego cerrado, copia antes, relee);
  `IntegrityKinds.RotoLoto` en AUDITORÍA. La partida del organizador: ambos apagados y afecto Rotom 0 (sin comprobar
  que PKHeX lea bien ese bloque en USUM). Si la Rotombola sigue saliendo, hay que medir con una partida que la tenga.
- 9: `tools/supabase/20-motes.sql` (motes + votos_mote, EJECUTAR en Supabase). Captura o recién llegado → votación de
  10 min (una por PID). A los demás: `NicknameVoteWindow` encima de todo (proponer o apoyar; un voto por jugador,
  cambiable). Al cerrar: `NicknameVote.Winner` (más repetido, empate al primero) y `RenameService` con el juego CERRADO
  (con el juego abierto no: la trampa de la 1.0.6.2); avisa «se pondrá al cerrar el juego». `--ensayar-mote` la enseña.
- 7 (primera fase): franja HOY en Admin (conectados, regalos sin recoger, buzón), ayuda en cada botón (ToolTip),
  confirmación en MANDAR, RETIRAR, borrar sugerencia y publicar/retirar anuncio. Asistentes paso a paso: no hechos.
- 8 (Banda Focus): pendiente de decidir con el organizador.
- 9 REHECHA como la describió el organizador: ventanas pequeñas arriba a la derecha del emulador con barra de 15 s
  (dorada, roja los últimos 5): 1) al que captura «¿Quieres que los otros pongan el mote…?» SÍ/NO; 2) a los demás
  «X quiere ponerle un mote a POKÉMON» + escribir; 3) votar entre las propuestas; 4) ganador. El servidor pone los
  plazos (`propuestas_hasta` +20 s, `cierra` +40 s; `votos_mote.voto`). Mote EN TIEMPO REAL: `AzaharGameWriter.SetNickname`
  escribe solo el bloque guardado (como el cap de nivel, nunca la cola de PS) en todas las copias del equipo por PID
  (`GameLinkMonitor.RenameLive`); si no está en el equipo, con el juego cerrado. SIN PROBAR en el juego.
- OJO sondas: sin carpeta leen el Azahar global (partida vieja del 26/09, 52 min). La del organizador está en
  `Desktop\PermaLocke prueba\Emulator\user\...`: `Probe --campo fichero "C:/Users/javie/Desktop/PermaLocke prueba"`.
  Leída bien (28/09): 63 montones en 02 y 1 en 00 (el que cogió) → 02 = con bayas, 00 = cogido (CONFIRMADO).
  Rotombola 1 = False, Rotombola 2 = True, afecto Rotom 120: PKHeX lee bien el bloque. Qué significa cada bandera y si
  apagarlas repite el tutorial de Rotom (la primera tirada es obligada): SIN PROBAR, hacerlo en una copia.
- Sincronía (2026-09-28): `DiscordLogin.ServerOffset` (cabecera Date, media de 20 respuestas). Motes: todos abren las
  propuestas a creado+3 s, la votación a `propuestas_hasta`, el ganador a `cierra`, en hora del servidor; lectura cada 1 s,
  votos en directo cada 1 s. Fantasmas y lluvias: lectura cada 2 s y salen a creado+4 s en todas las pantallas.
  Ventanas del mote: izquierda del emulador a media altura.
- 7 fase 2, Admin rehecho: menú lateral agrupado (`AdminViewModel.Sections`, `AdminSection`) y páginas en `Pages/` (las
  9 ventanas pasaron a UserControl con git mv). INICIO (cifras + botón a cada sitio), JUGADORES (lista + ficha al lado,
  `Sheet`), REGALOS (a quién / qué / mandados), CEMENTERIO (`FallenViewModel`, tabla caidos), CONTROL (pausa + tienda),
  REGLAS, ANUNCIOS, AUDITORÍA, INFORMES, BUZÓN, MOTES (`MotesViewModel`, ANULAR: política de borrado en 20-motes.sql,
  re-ejecutar), LISTA, CONSUMO, LIMPIEZA. ACTUALIZAR de arriba refresca también la página. Visto en pantalla.
- Motes, tercera vuelta: `RenameService.Allowed` (teclado latino del juego, aproximado, sin medir tecla a tecla) y 12
  caracteres, con aviso y contador al escribir; un solo voto (`HasVoted`, y en el servidor la fila solo se toca con
  voto nulo). COLA en el servidor: trigger `motes_en_cola` (bloqueo advisory) pone `empieza` = fin de la anterior + 9 s;
  tramos 3 s / 15 s proponer / 15 s votar / 8 s resultado. La pregunta al que captura va aparte, arriba a la izquierda.
  Re-ejecutar 20-motes.sql. Colas de muertes revisadas: ceremonia propia y fantasmas encolan bien.
- PENDIENTE (no hecho, lo pidió aparcar): velocidad del emulador. En `PermaLocke prueba\Emulator\user\config\qt-config.ini`
  vuelve `frame_limit=0` (sin límite) y el juego va al máximo; Azahar lo reescribe al cerrar. Arreglo pensado:
  `AzaharInstallation.EnsureNormalSpeed` (frame_limit=100, frame_limit\default=true en [Renderer]) llamado desde
  `EmulatorLauncher.Launch` junto a `DisableDiscordPresence`. A mano mientras: esos dos valores con Azahar cerrado.
- 1.0.7.3: quien captura ve su votación (proponer y votar como espectador, votos en directo) y el ganador a la vez que
  todos (`FollowAsync(row, mine)`, `NicknameVoteViewModel.IsWatching`). Mote en directo también en caja:
  `LiveBoxRenamer` busca UNA vez el EC en claro (+2 bytes a cero) en el heap, descifra, comprueba PID y checksum y
  escribe solo el bloque guardado; el EC se guarda al ofrecer (solo en esa sesión de la app). SIN PROBAR en juego.
