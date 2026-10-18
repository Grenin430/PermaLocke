# Traspaso: megas en el overlay (2026-10-16)

Prompt para empezar la sesión siguiente. Leer también `00 - Inicio.md` y el final de `Historial de conversaciones.md`.

---

Seguimos con **PermaLocke singleplayer** (`C:\Users\javie\Desktop\Permalocke singleplayer`, rama `singleplayer`). Lee el cerebro (`Obsidian/PermaLocke Brain`, empieza por `00 - Inicio.md` y el final de `Historial de conversaciones.md`) y esta nota.

## Qué estamos haciendo

Los Pokémon que cruzan el mar y el cielo del overlay (`Views/Cap/CapVisitors.cs`, dibujos en `Views/Cap/CapCritterArt.cs` + hojas PNG en `Views/Cap/Critters/`) van a tener su forma mega. Plan del usuario:

1. Mientras sale la animación del **6.º cristal Z** (al superar la prueba 6), van **entrando en el layout todos los Pokémon con mega** que pasan por él.
2. Cuando están todos, se **meten en una esfera blanca que se rompe como un huevo con destellos arcoíris** y se abre mostrando el **símbolo de la megaevolución**.
3. **A partir de ahí, para siempre**, esos Pokémon pasan por el overlay en su **forma mega**.

Se hace mega a mega, con aprobación del usuario en cada uno (vídeo + ampliación en `Desktop/imagenes/revision/prototipo_megas`).

## Megas hechas y aprobadas (id = 10000 + especie)

| Mega | Id | Hoja | Cómo se hizo |
|---|---|---|---|
| Mega-Gyarados | 10130 | `megagyarados` | `recon2.js` (patrón a 6 px) + `megagyarados_base.js` + `megagyarados_anim.js` |
| Mega-Sharpedo | 10319 | `megasharpedo` | `recon_plain.js` + `shrink_grid.js 2.4` + `megasharpedo_anim.js` |
| Kyogre primigenio | 10382 | `primalkyogre` | `recon2.js` (6,5 px) + `primalkyogre_anim.js` (inclinación 12) |
| Mega-Charizard X | 10006 | `megacharizardx` | vuelo aprobado de Charizard recoloreado: `recolor_sheet.js` + `megacharizardx.map.json`, `megacharizardx_flames.js` (ojo rojo, llamita en la boca), `megacharizardx_features.js` (cuernos cortos punta azul, púas de los hombros negras desde 2026-10-17), `megacharizardx_throat.js` (2026-10-17: el cian del cuello de la mandíbula al pecho, solo de la mega) — en ese orden; membrana de las alas `1E4E92`/`3A86D6` |
| Mega-Pidgeot | 10018 | `megapidgeot` | `flight_rig` del Pidgeot con piezas nuevas: `megapidgeot_flight_anim.js` (`megapidgeot.txt`) |
| Mega-Aerodactyl | 10142 | `megaaerodactyl` | `recon_plain.js ... 16 4` + `megaaerodactyl_anim.js` (aleteo amplio) |
| Mega-Altaria | 10334 | `megaaltaria` | `recon_plain.js ... 4` + `shrink_grid.js 1.35` + `megaaltaria_anim.js` (borde pizarra en la nube) |
| Mega-Dragonite | 10149 | `megadragonite` | `megadragonite_make.js` genera pose y vuelo desde el Dragonite aprobado (versión 2 aprobada: alas de plumas pequeñas en la cabeza en lugar de antenas, alitas turquesa normales en la espalda, cuerno blanco, bola turquesa con anillo violeta en la cola). No copiar la referencia (su autor no permite repostear). |
| Mega-Skarmory | 10227 | `megaskarmory` | vuelo aprobado de Skarmory recoloreado por piezas: `megaskarmory_flight_anim.js` (capas de `compose`: cabeza/cuerpo/cola dorados, cuello índigo, alas índigo con borde dorado, ojo rojo; patas plegadas quitadas y patas largas índigo con garras doradas colgando, dos cuchillas en la espalda; Height 66) |
| Mega-Staraptor | 10398 | `megastaraptor` | **el sprite de la referencia animado, no el vuelo del normal** (el usuario lo pidió así): `recon_plain.js ... 16 1` + fondo blanco inundado desde el borde (pecho blanco conservado) + `shrink_grid.js megastaraptor megastaraptor_small 1.35` (a 111 de alto no cabe en el cielo: altura máxima ~82 en Slim, ~93 normal) + `megastaraptor_anim.js` (alas giran desde el hombro con `C.swing`, cola se mece) |
| Mega-Latias | 10380 | `megalatias` | sprite de la referencia (arriba a la izquierda de `mega_latias_latios.png`, 1:1, 74x74) animado: `recon_plain.js <recorte> megalatias 16 1` + fondo inundado (`flood.js` del scratchpad: la A blanca que toca el borde pasa a fondo) + `megalatias_anim.js` (aleta grande bate desde la raíz, ala inferior y aleta derecha responden, aletas de la cola se mecen) |
| Mega-Latios | 10381 | `megalatios` | igual que Latias, pero **el verde** (abajo a la derecha de la referencia, lo pidió el usuario; 96x68), `megalatios_anim.js`. Para que las partes que se mueven en vertical no se despeguen: desplazamiento según x (no según y) y una «sombra» (`ghost`) con las celdas de la unión quietas debajo |

Mega-Latias y Mega-Latios son **variocolor** (lo dice el usuario): llevan brillos pintados en la hoja (`tools/critters/sparkle.js`: cinco estrellas de cuatro puntas blancas con puntas doradas, cada una a su ritmo; paleta S/T). Latias también usa la «sombra» de unión (`ghost`) contra los desgarros.

Todo en `tools/critters/`. Cada una tiene su entrada en `META` de `build_batch.js` (y las de vuelo en el `FLIGHT`) y su línea en `CapCritterArt.cs` tras «The Mega forms».

## Qué queda

> **Actualizado 2026-10-17:** las cuatro megas de abajo y el evento de la prueba 6 (v2, §248) están hechos y aprobados. Lo único que falta es **verlo en el juego real** al superar la prueba 6. El resto de esta sección es el plan original, ya cumplido.


1. **Megas pendientes**, en este orden: **Skarmory** (10227), **Staraptor** (10398), **Latias** (10380) y **Latios** (10381). Referencias en `Desktop/imagenes/megas/` (`mega_skarmory.png`, `mega_staraptor.png`, `mega_latias_latios.png` — trae normales y variocolor, usar las normales). Charizard Y: «de momento no».
   - Skarmory, Staraptor y Dragonite/Charizard/Pidgeot/Pidgey/Spearow/Pikipek/Toucannon/Salamence tienen **vuelo aprobado propio** (`FLIGHT` en `build_batch.js`): para la mega, partir de ese vuelo y cambiar piezas o recolorear (como Pidgeot/Charizard/Dragonite), no reconstruir desde una referencia de pie.
   - Latias/Latios: `latias_anim.js`/`latios_anim.js` normales; referencia en sprite ampliado → `recon_plain.js` (probar tamaño de píxel fijo si la detección da uno raro) y animar como los normales.
2. **El evento del 6.º cristal** (entrada de todas las megas, esfera blanca que se rompe como un huevo con destellos arcoíris, símbolo mega). Prototipo primero, en el laboratorio.
3. **Formas mega permanentes** en los visitantes desde la prueba 6.
4. Compilar la App, tests, y registrar en el cerebro.

## Cómo hacer el evento y el cambio permanente (propuesta)

- **Cuándo:** la prueba 6 superada = `CapPlaqueData.Cleared >= 6`. No hace falta guardar nada: el «desbloqueo» se deduce de `Cleared`.
- **Formas mega para siempre:** en `CapVisitors.Paint`, si las megas están desbloqueadas, cambiar la especie elegida de la ruta por su mega cuando exista entrada `10000 + especie` en `CritterArt.All` (Charizard → 10006, Kyogre → 10382). Pasar el dato por `CapSeaContext` (añadir un campo, p. ej. `Megas`) desde `CapWorldView` (que ya construye `_context` con `data`).
- **El evento:** reutilizar la película del cristal (`CapTrial`: `CapTrialTurn`, `PaintCrystal`, `CapTrial.PaintBed`; `CapBadge` la lanza al ver la prueba nueva, con `_progress.Forget()` para que no tarde). Si la prueba que se acaba de superar es la 6, encadenar un `CapMegaParade` nuevo (clase pura en `Views/Cap`, función del momento, sin `Random`/`DateTime` — hay test que lo prohíbe):
  1. Durante la película del cristal, las megas (de momento en su forma normal) entran nadando/volando desde los bordes del mar y el cielo de las dos ventanas (usar el mundo común: `CapSeaContext.WorldX`/`WorldWidth`) y se reúnen hacia el centro del abismo.
  2. Con todas dentro, cada una se envuelve en una esfera blanca (pixel art, sin degradados ni resplandores), que se agrieta y se rompe como un huevo con destellos de los colores del símbolo mega.
  3. Sale cada una en su forma mega, y en el centro se abre el símbolo (la hélice ya trazada en `Helix` de `CapMega.cs`, colores `Hue`).
  4. Se dispersan y siguen pasando como visitantes normales, ya en mega.
- **Datos:** `CapPlaqueData` lleva ahora `Mega = CapMegaTurn?` (de la animación de grietas descartada). Sustituirla por el nuevo turno (p. ej. `CapMegaParadeTurn(double Start)`), lanzada desde `CapBadge` al detectar la prueba 6.
- **Limpieza:** `CapMega.cs` contiene la animación de **grietas, descartada** (y su escenario `mega` en `CapScenarios.cs`, y la llamada en `CapWorldView`). Quitarla al hacer la nueva; conservar la hélice del símbolo.
- **Laboratorio:** escenario nuevo en `CapScenarios.cs`; render con `--secuencia-ventana <escenario> 0 <segundos> 12 1920 <carpeta>`; vídeo con `C:\Program Files (x86)\Soundux\ffmpeg.exe`.

## Herramientas y trucos

- Laboratorio: compilar `tools/PermaLocke.PlaqueLab` con `--artifacts-path` en una carpeta **nueva del scratchpad** (en `.lab-art` y `lab2` quedaron procesos `PermaLocke.PlaqueLab.exe` colgados que no se dejan cerrar). **No usar `--bichos`** (falla con todos los Pokémon en hojas y deja el proceso colgado).
- Ver una especie en vuelo: `--secuencia-ventana completo 0 30 12 1920 <carpeta> --especies <id>` (acepta ya ids mega: `id % 10000`). `--especie <id>` solo la pone cuando sale su ruta.
- `tools/critters`: `recon2.js` (patrones con cuadrícula), `recon_plain.js` (sprites ampliados sin cuadrícula: `<img> <salida> 16 [píxel]`), `shrink_grid.js` (reducir por moda), `recolor_sheet.js` (recolorear una hoja final), `build_batch.js <nombre>` (genera la hoja, la copia a `Views/Cap/Critters/` y deja la entrada en `batch_entries.json`).
- Añadir la entrada C#: script `g3.js` del scratchpad (perdido entre sesiones) → hacerlo a mano tras la última `new(10...`.
- Desde PowerShell las comillas de un argumento se pierden: usar `\"` o editar con node.

## Estado general (sin commits en esta sesión)

Todo lo de esta sesión está **sin commit** (no se pidió). Hecho y probado por el usuario: Pokévial (recarga con el récord 10 de la enfermera, corcho y brillos), Repelente Infinito y Turbo en el overlay (`FieldItemsService`: repelente en partida +0x2016/+0x2030; turbo persistente con `RuleBlock.EggTurboKept` 0x13). La App compila (si PermaLocke está abierto, el `.exe` queda bloqueado: es solo la copia).

Reglas: responder en español, estilo caveman; código y comentarios en inglés; pixel art hecho a mano, sin degradados/resplandores/emojis; no desplegar singleplayer a «PermaLocke prueba» ni a la carpeta de amigos; `Locke/` solo lectura; nunca tocar la ROM vanilla; commits solo si se piden (correo de commits en la memoria); toda orden al usuario con su `cd "C:\Users\javie\Desktop\Permalocke singleplayer"`.

## Evento de la prueba 6 (v1 rechazada; ver la v2 abajo)

- `Views/Cap/CapMega.cs` reescrito: fuera las grietas descartadas (y `CapMegaTurn`, `CapPlaqueData.Mega`, el escenario `mega`); se conservan hélice, `Symbol`, `Sign`, `Twinkle`, `Shard` (ahora de cascarón).
- Sin dato nuevo: `CapMega.Since(data, anim, t)` = segundos desde que sube el cap con `Cleared == 6` (`CapAnim.CapRise`), mientras dura (14,8 s, dentro de las películas de la prueba).
- Línea de tiempo: 0,4-7,7 entran las 12 (orden en `Megas`) desde los dos extremos del mundo, a escala 0,4, y giran en anillo en el centro del abismo; 7,8-9 espiral hacia dentro, se vuelven blancas, crece la esfera; 9-10,6 late, tiembla, grieta en zigzag con luz de los colores del símbolo y ramitas, destellos arcoíris; 10,6 fogonazo, casquete arriba y fondo abajo (tramado), esquirlas de cascarón y destellos hacia fuera; símbolo + «MEGAEVOLUCIÓN / DESBLOQUEADA»; salen en forma mega (blancas al principio, creciendo), voladores hacia arriba, nadadores a los lados; 13,3-14,5 se apaga el símbolo.
- La ventana del abismo (o una sola ventana) tiene la esfera; la del equipo ve pasar a los que entran y salen (centro supuesto a -Cols, detrás del juego).
- `CapSeaContext`: `Megas` (visitantes en forma mega: `Cleared >= 6` salvo durante el desfile antes de `Reveal`) y `Parade` (los visitantes no pasan durante el desfile). `CapVisitors.Centred` dibuja una hoja a cualquier escala (`Pick` con escala).
- Laboratorio: escenario `megas` (a los 2 s se supera la prueba 6). Vídeo `evento_megas.mp4` y `evento_megas_momentos.png`.
- Ojo: las escenas del laboratorio tienen `cleared: 7` por defecto, así que ahora salen ya en mega en todos los escenarios.
- Tests de la App: con `--artifacts-path` fuera del repo fallan 3 (buscan la raíz del repo); dentro del repo (`.testart-tmp`, borrarla después) pasan los 561.

### Versión 2 del evento (2026-10-16, APROBADA; §248)

El usuario rechazó la v1 (llegadas y salidas feas) y dio como guía la megaevolución de Pokémon Champions en https://www.youtube.com/watch?v=iqC8EmD-9IA (8:35-8:44): rayos de luz que convergen, destello, esfera iridiscente (franjas rosa/lima/turquesa), se oscurece a obsidiana con red de grietas de colores, grietas doradas que se ensanchan, destello, revelación con aurora morada/verde, polvo de estrellas en espiral y arcos dorados. `CapMega.cs` reescrito con eso (20 s):
- Llegada 0,6-8,8: cada mega por una curva Catmull-Rom propia (`WayIn`): voladores bajan del cielo desde un extremo del mar, rozan la superficie, se zambullen (salpicadura) y pasan por el centro; nadadores serpentean; Kyogre sube del abismo. Estela de polvo en sus dos colores (`Mega.Trail`), inclinación según la velocidad (`Centred` con `tilt`). Carrusel en perspectiva (cerca más grandes, lejos más pequeñas y hundidas en el agua).
- 7,9-10,2 rayos que convergen; el carrusel acelera y se cierra, se vuelven blancas; destello.
- 10,2 esfera iridiscente girando con rayos; 11,8-12,6 obsidiana desde abajo; 12,25 grietas (Voronoi sobre la esfera, 30 semillas) que se extienden desde un punto en los colores del símbolo; 13,0 doradas, tiembla, sale luz; 13,6 estallido (destello, dos ondas, esquirlas de obsidiana con borde dorado, destellos arcoíris).
- 13,75 revelación: mar oscurecido, aurora en cortinas sobre el agua (borde casi recto y rayos de distinta altura: con el borde ondulado parecía una montaña), polvo de estrellas en espiral, dos cintas doradas, símbolo en el centro, megas blancas que toman color; rótulo abajo, cerca del fondo.
- 16,8 se van una a una por curvas (`WayOut`) con estela y salpicadura; 17,6 se despeja.
- Todo en coordenadas del mar común: el centro del abismo en otra ventana se supone a `Cols/2 - WorldX` (anchos iguales); el abismo tiene aurora y destellos, la otra ventana medio destello.
- Vídeos `evento_megas.mp4`, `evento_megas_abismo_x2.mp4`, `evento_megas_momentos.png`. Ver vídeos de YouTube: con JS en el navegador integrado, `video.currentTime` + canvas en mosaico.
