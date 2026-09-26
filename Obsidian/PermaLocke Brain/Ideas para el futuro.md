---
tipo: ideas
revisado: 2026-09-26
---
# Ideas para el futuro

Ideas que el usuario quiere **guardar para más adelante**, sin hacer. No se empiezan sin que lo pida. Abajo, las que ya
**rechazó**, para no volver a proponerlas. Contexto: [[Usuario y forma de trabajar]] · [[Historial de conversaciones]].

## Objetos de Mario Kart (apuntada el 2026-09-26, «me llama un poquito la atención»)
Durante la run se ganan **cajas ?**; el objeto que toca depende del **puesto en la clasificación** (los últimos, objetos
fuertes contra los de delante; los primeros, flojos o de defensa). Mantiene el torneo reñido.

- **Cómo se ganan las cajas (sin decidir):** superar prueba/Kahuna/combate importante; capturar en ruta nueva; cuando un
  amigo hace wipe, caja para todos los demás.
- **Uso:** desde la app o con una tecla jugando. A la víctima le sale el ataque **encima del emulador** en pixel (como
  la lluvia, §184: capa en `GhostWindow`), p. ej. el caparazón cruzando y reventando. Cada objeto lanzado/recibido es un
  evento en las dos runs (regla 4). Viaja por el servidor del torneo (tabla nueva, como `fantasmas`/`lluvias`).

| Objeto | Efecto | Pieza existente |
|---|---|---|
| Tinta | manchas pixel tapan el emulador de todos los de delante 15 s | sí: capa como `BloodRain` |
| Plátano | se deja en una ruta; el primer rival que entra pierde su primer encuentro (ruta gastada) | casi: zona (`FieldZoneReader`) y `ZoneEncounterSpent`; falta el viaje por servidor |
| Caparazón rojo | al de justo delante: sin Poké Balls 10 min | sí: `BallControlService` retira/devuelve con `WithheldLedger` (§147) |
| Caparazón azul | al líder: −50 puntos + explosión en pantalla | sí: evento de puntos + capa |
| Rayo | a todos los de delante: sin curas en la mochila 5 min | a medias: ampliar el retiro de balls al bolsillo de medicinas |
| Boo | roba un objeto al azar de la mochila de otro y te lo da | a medias: sacar (`WithheldLedger`) y meter (`BagItemDelivery`) existen por separado |
| Estrella | inmune a todo 1 h | fácil |
| Seta | tirada de gacha gratis o puntos dobles en la próxima captura | sí |

- **Límites propuestos:** ningún objeto mata ni toca PS/niveles (molestan, no deciden la run); un objeto por jugador cada
  X tiempo; el organizador apaga objetos desde Admin.
- **Primer paso sugerido:** Tinta, Caparazón rojo, Caparazón azul y Estrella (las piezas ya existen).
- **Pendiente de decidir por el usuario:** cómo se ganan las cajas y qué objetos entran.
- Cuidado al hacerlo: retirar objetos en vivo tiene que devolverse siempre, también tras un cierre (el libro de
  `objetos-retirados.txt` lleva el id de la run); nada de barrer memoria para buscarlos ([[Trampas y lecciones]]).

## Rechazadas (no volver a proponer)
- 2026-09-26: ver el cementerio y las **killcams de los demás** (peso de las killcams en Supabase: ~2 MB por clip).
- 2026-09-26, «ninguna sorprende»: hitos en directo, epitafio del caído, coincidencia de ruta, muro de la vergüenza,
  apuestas, evento temporal «luna de sangre»; Soul Link automático entre PCs, manchas de sangre tipo Dark Souls,
  mausoleo (entrenador con los caídos), invasión con el equipo de un amigo; combates en directo en pixel, textos de la
  Colina del Recuerdo con los caídos reales, latido con PS bajos.
- 2026-09-26, sin interés (sin rechazo explícito, no insistir): conquista de rutas, pactos con el diablo, gran final por
  link, retos semanales, casa de subastas, préstamos, Diario de Alola, Pokédex del torneo, Salón de la Fama.
- Antes (§183): «tarjeta de entrenador» y listas de mecánicas.
