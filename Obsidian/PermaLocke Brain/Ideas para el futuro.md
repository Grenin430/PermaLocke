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

## Hechas de las tandas de ideas
- 2026-09-26, sexta tanda («espectáculo encima del emulador» o «lucir la app»): **nueva carta al capturar** (§190),
  solo salvajes, confirmada por el jugador. Quedan propuestas y sin decidir de esa tanda: pantalla VS antes de un
  combate importante, alarma de variocolor salvaje, evolución de lujo (la carta se rompe y se recompone), el equipo en
  un campamento pixel, la TIENDA como mostrador con tendero, la clasificación como podio. Las cuatro primeras necesitan
  antes comprobar que la app detecta ese momento de forma fiable.
- Posible ampliación de la carta al capturar: también regalos, estáticos y huevos (el usuario dijo «de momento»
  solo salvajes).

## Rechazadas (no volver a proponer)
- 2026-09-26: ver el cementerio y las **killcams de los demás** (peso de las killcams en Supabase: ~2 MB por clip).
- 2026-09-26, «ninguna sorprende»: hitos en directo, epitafio del caído, coincidencia de ruta, muro de la vergüenza,
  apuestas, evento temporal «luna de sangre»; Soul Link automático entre PCs, manchas de sangre tipo Dark Souls,
  mausoleo (entrenador con los caídos), invasión con el equipo de un amigo; combates en directo en pixel, textos de la
  Colina del Recuerdo con los caídos reales, latido con PS bajos.
- 2026-09-26, sin interés (sin rechazo explícito, no insistir): conquista de rutas, pactos con el diablo, gran final por
  link, retos semanales, casa de subastas, préstamos, Diario de Alola, Pokédex del torneo, Salón de la Fama.
- Antes (§183): «tarjeta de entrenador» y listas de mecánicas.
- 2026-09-26, cuarta tanda (sin tocar la jugabilidad): de ella solo se hizo el estado de Discord (§185), y solo el
  nombre. Quedan sin decidir, no rechazadas: cromos de cada captura, álbum de fotos automático, «el viaje» en el MAPA,
  biografía de cada Pokémon, resumen de sesión tipo Wrapped, sonido chiptune.
- 2026-09-26, quinta tanda, «no me convence ninguna»: Presiona F sobre el fantasma, ver el álbum de los demás, carta que
  arde en directo, pantalla para retransmitir (OBS), compartir una carta como imagen, intro «DÍA X» al abrir JUGAR,
  vitrina de medallas y cristales Z en 3D, la app cambia de colores según la isla.
- 2026-09-26, ampliar el ÁLBUM (tres tandas, ninguna convence; lo aparca hasta que el uso real sugiera algo): carta al
  detalle con ficha, abrir sobre, cartas que evolucionan, Pokédex del álbum, filtros, cementerio de cartas, álbum de un
  amigo, sellos, portada, PNG; mesa de estrategia contra el próximo combate, sellos de aviso (evoluciona, cap, EV),
  ordenar cajas desde el álbum, equipos guardados, cicatriz de la carta, carta compañera en directo; duelo de cartas
  entre amigos.
- 2026-09-26, séptima tanda, «no me convence ninguna»: canal de Discord automático (webhook), cartel SE BUSCA del que
  más mata, mapa de calor de muertes, aviso de ruta libre/gastada al entrar, GIF de la sesión, marcador en directo.
- 2026-09-26, octava tanda (molestar/animar sobre el emulador ajeno), «no me gusta ninguna»: tomatazos, susto de
  Gengar, grada con ánimos/abucheos, maldición de Mimikyu, pintada, fantasma que persigue a un amigo elegido.
- 2026-09-26, novena tanda (dentro de la app), «no me convence ninguna»: dónde sale cada especie en el mundo
  randomizado, Pokédex del mod, máquina del tiempo de copias del save, mini-ventana siempre encima, buscador Ctrl+K,
  notas por ruta en el MAPA.
- 2026-09-26, décima tanda (con investigación del repo y fuentes externas, a petición suya), «no me gusta ninguna»:
  reencarnación (el caído renace como huevo en otro amigo), alijo para el que viene detrás, maldiciones cruzadas en la
  seed, el trono (el ganador es Campeón del siguiente torneo + veterano), intercambio por cable, mercader nocturno,
  tribunal de roles, padrinos (un amigo pone el mote), el grito (audio del micro con el fantasma), carrera de semilla
  hermana. Diez tandas seguidas rechazadas: no seguir proponiendo listas sin que traiga una pista propia.
- 2026-10-08, aspectos de los avisos (§238): A–J en maquetas de píxeles (Rotom Dex, cartel de Alola, caja de texto del
  juego, medalla, cristal neón, y la tanda de objetos F–J), «bonitas pero no son mi estilo». De su lista se hicieron K–Q
  (menú de Alola, chat de Rotom, alarma del laboratorio, cinta de vídeo, pergamino, cartas en abanico, sprite que salta) y
  eligió **O, el pergamino** abriéndose a la izquierda. Panel del cap (§239): S–Y no elegidas (pergamino, tablón, cuaderno,
  etiquetas, mapa de la ruta, edicto de la Liga, pergamino horizontal); eligió **Z, placa de trofeo**. PNG en `prototipos/`.
- **Regla del usuario (2026-09-26): nada de ideas que alteren la jugabilidad.** Los objetos de Mario Kart la alteran;
  siguen apuntados porque los pidió guardar.

## Lista de los jugadores (2026-09-28), pendiente de decidir
1. Montones de bayas: que no se regeneren. 2. Tienda de combate de la app cerrada hasta el final/late game.
3. Detectar reinicios para repetir (Cápsula Habilidad…). 4. Parpadeo del panel del cap al morir (se esconde mientras
HpBarWatcher mira la barra, 1.0.5.10) y de la lluvia de sangre. 5. Bloquear la Rotombola (Roto Loto).
6. Subida de dificultad explicada en la app, gráfica (hay `subida_de_dificultad_explicada.txt` en la raíz).
7. Admin «a prueba de niños». 8. Banda Focus que no se gaste. 9. Motes votados por los demás en su emulador.
10. Ruleta de curativos sin revivir. 11. Carta al álbum también para huevos, fósiles y regalos.
12. MT y objetos repetidos en el mundo. 13. Balance del dinero y cupón del Supermercado Ultraganga.

## Reglas que se apilan por fase (apuntada el 2026-10-07, para verla más tarde)
Sacada de una búsqueda de formatos Nuzlocke multijugador (el Nuzlocke World Cup suma una regla en cada fase: semifinales,
estilo Fijo obligatorio; final, sin objetos en combate). Aquí: **después de cada prueba el torneo vota una regla más que
se suma a las anteriores** (Fijo obligatorio, sin objetos en combate, sin curar en combate…), con animación en cada app.
Apoyo ya hecho: las reglas viven dentro del juego (§217, `RuleBlock` + `Battle.cro`); «sin objetos en combate» y «estilo
Fijo» serían parches pequeños del mismo tipo. Sin hacer. Choca con la regla de arriba (nada que altere la jugabilidad):
el usuario la pidió guardar, así que se decide con él cuando toque.

## Rol «monotype» (HECHO el 2026-10-07 en §220 y §221 (guardería), sin publicar; investigación: [[Monotype de BxnnyLocke]])
Hecho: ocho roles monotype (agua, normal, planta, volador, psíquico, bicho, veneno, fuego) con la regla de BxnnyLocke. Ver §220
de `docs/ARCHITECTURE.md`. Roles en `Data/roles.json`: normal, cagoneta, experto, ludopata y los ocho `monotype_*`.

Lo que hace BxnnyLocke (2026-10-07, leído del ensamblado `Locke/data/BxnnyLocke.dll` con System.Reflection.Metadata, solo lectura; nada copiado). 14 roles: normal, eviolite, ludópata, cagoneta, experto, monotype (agua, normal, bicho, psíquico, volador) y temático (alto mando, villanos, Ash, feos). Puntos de logro: cagoneta ×0,5, experto ×1,75, eviolite ×1,5, monotype ×1,5, temático ×1,25, normal y ludópata ×1. Monotype y temático = lista blanca de especies por rol (GetLimitePorRol: agua 134, normal 113, bicho 81, psíquico 76, volador 95, alto mando 145, villanos 163, Ash 138, feos 187). `checkValidezEquipo` recorre equipo y cajas desbloqueadas: una especie fuera de la lista bloquea el gacha y la guardería con «Líbralo o usa wondertrade»; si es la última y no quedan tiradas de wondertrade, regala una. Gacha, wondertrade y huevos salen solo de esa lista, con BST parecido. En vez de la ruleta del ludópata, esos roles tienen **GUARDERÍA** (huevos de la lista). El rol «eviolite» solo abarata la Eviolite a la mitad en la tienda y suma ×1,5: no hay restricción de equipo en el código.

## Otras ideas de la búsqueda del 2026-10-07 (sin elegir; ninguna le hizo clic)
Mentor/pupilo (veterano con novato, el mentor gana según el pupilo), chasquido colectivo (votar qué tipos o especies
desaparecen), criador (recibe capturas de otros), relevo (jugar la partida de otro una prueba), traidor secreto, alma
gemela, necromante, rey, adivino, saboteador, benefactor, cazarrecompensas, espía, árbitro, parásito, mercader. Más las
listas de objetos y cambios de código de esa sesión (Caramelo Tope, Pañuelo Cariño, epitafio al caer, zonas que caducan,
caos por pasos…): **todas rechazadas.** Con esto van más de doce tandas seguidas rechazadas: no volver a proponer listas;
esperar una pista suya.
