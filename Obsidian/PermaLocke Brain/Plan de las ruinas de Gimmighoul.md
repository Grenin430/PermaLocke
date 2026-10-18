---
tipo: plan
revisado: 2026-10-18
---
# Plan de las ruinas de Gimmighoul (singleplayer)

**Para la sesión siguiente: leer entero antes de tocar nada.** Pedido del jugador el 2026-10-18. Todo en `Desktop\Permalocke singleplayer` (rama `singleplayer`), **sin commit y sin desplegar** (ver «Estado del código» abajo). Nada de esto en `PermaLocke prueba` ni en `PermaLocke para amigos`.

## Qué es
Abajo a la izquierda del panel, en la ventana del fondo del mar (`CapPanelKind.Deep`), sobre la arena: las ruinas de Gimmighoul de Escarlata/Púrpura (ladrillo de piedra gastado con musgo), con un Gimmighoul cofre que lleva gafas de buceo y tubo. Sustituye a los cristales Z que antes salían repartidos por toda la arena (`CapTrial.PaintBed`, ya no se llama) y al altar (`CapAltar`, prototipo descartado, fichero aún en el repo sin usar).

## Fases (cada una con aprobación del jugador)
1. **Ruinas + Gimmighoul — APROBADA (2026-10-18).**
2. **Los 12 cristales Z repartidos por las ruinas, cada uno en un sitio distinto** (clavado medio incrustado en un muro, encima del cofre, alrededor, dentro…). **Uno a uno**: el jugador marca dónde va cada cristal en una imagen.
3. **Una animación propia para cada cristal** (cada uno diferente), de una en una, con prototipos y aprobación de cada una.

Orden de las pruebas y sus cristales (`CapAltar.Order`): 807 Normastal, 813 Lucharastal, 809 Hidrostal, 808 Pirostal, 811 Fitostal, 819 Litostal, 810 Electrostal, 820 Spectrostal, 822 Nictostal, 821 Dracostal, 824 Feeristal, 815 Geostal. Los iconos los da `item?.Invoke(id)` en el lab y `CapTrialCrystal.Of(item, icon)` dibuja el cristal (`Paint(p, x, y, scale, spin, burn, glint, cut, alpha)`).

## Lo que toca ahora (orden pedido por el jugador)
1. **Primero de todo**, pasarle la imagen de las ruinas con el Gimmighoul para que dibuje encima dónde va el **primer** cristal (Normastal): ya está hecha, `Desktop/imagenes/revision/prototipo_ruinas/v9/para_marcar_cristal.png` (ventana del fondo a ×3). Enviársela con `SendUserFile` y esperar su marca. Si hace falta regenerarla: `PlaqueLab --ventana altar 3 1920 <png>` y recortar `crop=640:250:0:620,scale=1920:750:flags=neighbor`.
2. Con el sitio marcado, **antes de integrar nada**, darle **prototipos de animación para ese primer cristal** (varias opciones distintas, en GIF, sobre el panel real) y que elija.
3. Integrar el cristal en su sitio con la animación elegida; luego el siguiente cristal, igual.

## Cómo se hizo (fase 1) y trampas
- **Ruinas**: `src/PermaLocke.App/Views/Cap/CapGimmighoulRuins.cs`. Estilo del **primer prototipo** (el que eligió): muros de ladrillo 6x3 en cuatro tonos con la parte de arriba rota en escalones (`Top`: `step` por cada 3 columnas y caída a partir del 55 % del ancho), ladrillos que faltan (`Recess`), cara lateral en sombra, musgo arriba y en manchas, arco o ventana (`Piece`), un trozo de piso de arriba sobre el muro del arco (`Base: 43` + `Ledge`), losas en perspectiva delante, bloques caídos, escombros y algas que se mecen. Se apoyan en el lecho: `floor` = punto más alto de `CapSea.Relief` a lo largo + 3, y un montículo de arena hasta el lecho. Se pinta en `CapWorldView` donde iba `CapTrial.PaintBed` (detrás va un comentario del plan).
- **Rechazado por el jugador, no repetir**: ruinas como edificio de dos plantas entero (v2: «parece un edificio abandonado»); trozos sueltos con picos (v3); Gimmighoul grande o en tres cuartos inclinado («parece que flota»); girar el arte en 2D (la perspectiva no lo asienta); el Gimmighoul de frente dibujado por mí (v6).
- **Gimmighoul aprobado**: es **el sprite del propio jugador** (cuadrícula 32x40 que pasó, `Desktop/imagenes/revision/prototipo_ruinas/referencias/gimmighoul_cuadricula_usuario.png`), leído celda a celda (color del centro de cada celda), con gafas de buceo (cristales turquesa, brillo blanco, puente y marco) y tubo azul que sale de la boca y sube por la derecha: 27 celdas cambiadas respecto a su sprite. Datos: `tools/clouds/gimmighoul_usuario.json` (con `foot`: la fila de la base del cofre). Lo convierte a C# `tools/clouds/gimmighoul_cs.js` → `CapGimmighoulArt.cs` (`Keys`, `Colours`, `Rows`, `Foot`). **Ojo**: no reutilizar una letra de su paleta para otro color (pasó con `P`, el rosado, que se volvió azul).
- Animado: burbujas desde lo alto del tubo (busca la primera celda `B`), un destello por las monedas, algas. Sombra del cofre sobre las losas en el código.
- Los scripts de los intentos anteriores del Gimmighoul se borraron a petición del jugador (2026-10-18). Queda `gimmighoul_recto.json` (su sprite enderezado, versión anterior, sin usar).
- **Laboratorio**: escenarios `altar` (7 cristales ganados) y `altar-completo` (12) en `CapScenarios`; `--hora h`, `--muerte tipo`. Vista: `--ventana altar 3 1920 x.png`; secuencia: `--secuencia-ventana altar 1 6 12 1920 dir`. Cada celda del panel ≈ 3 px en el preset 1920. La prueba de memoria del panel (`Nothing_in_the_cap_drawing_..._almost_no_memory`) prohíbe lambdas, listas o `new[]` por fotograma: arrays `static readonly` y funciones locales.
- Revisión de imágenes en `Desktop/imagenes/revision/prototipo_ruinas/v1..v9`.

## Estado del código (2026-10-18)
Sin commit en el singleplayer: la tanda del 17-18 (§251), el ultraumbral (§252), las nubes y el cielo (§253, desplegados) y las ruinas (prototipo, **sin desplegar**). Pruebas de la App: 549 en verde. **Mientras las ruinas no lleven los cristales, los cristales ganados no se ven en el panel**: no desplegar las ruinas hasta la fase 2 o, si el jugador lo pide antes, avisarle de eso.

Relacionado: [[Historial de conversaciones]], [[Para pasar al torneo]], ARCHITECTURE §252-§253.

## Cristal 1, Normastal — INTEGRADO (2026-10-18)

- Marcas del jugador: rombo rojo (arriba del piso alto, columna 29 de las ruinas), verde (hombro del muro alto, 41), amarillo (ventana del muro bajo, alféizar en x 60, altura 11). Llega al rojo, se tambalea sobre su punta, vuelca al verde (media vuelta como mucho), se desliza tumbado sobre una cara por la rampa siguiendo los ladrillos (`CapGimmighoulRuins.Surface`, `CapCrystalFall.Support`: nunca entra en un muro) y cae por delante del muro a la ventana (sombra en los ladrillos). Escala 0,4 (pedida 1/3 más grande que 0,3).
- La llegada elegida: **Tapu Koko variocolor** (idea y sprite del jugador): `tools/critters/tapukoko_shiny.js` (colores del arte variocolor: conchas negras con dientes blancos, cresta salmón) + `tapukoko_anim.js` (conchas que baten desde la bisagra, cresta y cola, inclinado -26°); hoja `tapukoko`, `CritterArt` 785. `CapTapuKoko`: sale del ultraumbral con el cristal en el nacimiento de la película, cruza la ventana del equipo acelerando, el hueco del juego deprisa y el abismo frenando; rastro = fragmentos de rayo sueltos que se quedan en el aire (la cinta pegada no gustó), arcos, muchos destellos variocolor, imágenes residuales; carga y rayo fractal largo (1,2 s, núcleo blanco, borde amarillo, contorno dorado oscuro; uno pálido y gordo no se veía) al soltarlo en el impacto de la película. El cristal cae, salpica y se hunde al rojo (`CapCrystalFall.FromTapu`; la parte común empieza 1,38 s más tarde).
- Solo para el Normastal (`CapTrialCrystal.Item`, `CapCrystalFall.Normastal` = 807): `CapTrial.Land/Where/WaveAt` reciben el objeto; los demás cristales siguen con la película de antes. Después de la película queda en la ventana para siempre (`CapCrystalFall.PaintRest`, desde `CapWorldView` con `data.Crystals`).
- Laboratorio: escenario `normastal` (prueba 1 superada a los 2 s); `--variante 0..3` enseña los prototipos descartados (hoja, picado, espiral, anzuelo de Gimmighoul). El lab hay que compilarlo dentro del repo (`--artifacts-path .lab-art/ruinas`): fuera no encuentra la raíz y se queda sin iconos (sin cristal).
- Revisión en `Desktop/imagenes/revision/prototipo_ruinas/normastal`.

## Cristal 2, Lucharastal (813) — INTEGRADO (2026-10-18)

Clavado en diagonal en la cara izquierda de la columna estrecha (pieza 166, 22 sobre el suelo), punta dentro de la piedra, grietas. Llegada elegida por el jugador: **Machamp Gigamax** (descartado el «puño del cristal»). Todo en §254 de ARCHITECTURE: `CapMachamp`, sprites en `tools/critters` (machamp, gmaxmachamp, setasdinamax), escenario `lucharastal`, revisión en `prototipo_ruinas/lucharastal` (v1-v6). Le gustó: flotador al estilo de los juegos, ritmo lento (~19 s), tres nubes girando sobre la cabeza, **impact frames** (los quiere también en otros golpes fuertes). No le gustaron: las ondas de luz del impacto (cutres). Siguiente: cristal 3, Hidrostal (809): pedirle que marque el sitio.

## Regla para los cristales que quedan (2026-10-18, pedida por el jugador)

Cada cristal Z llega con una animación del **tipo de su cristal** (el Lucharastal fue Machamp Gigamax por ser lucha). Antes de cada uno: ideas relacionadas con su tipo, el jugador elige; marcar el sitio en la imagen de las ruinas con los cristales ya puestos.

## Cristal 3, Hidrostal (809) — INTEGRADO (2026-10-18)

Mega Greninja (§255 de ARCHITECTURE; `CapGreninja`, escenario `hidrostal`, revisión en prototipo_ruinas/hidrostal v1-v5). Clavado de momento en la cara del muro alto sobre el arco: **sitio provisional, sin marcar** (imagen para marcar: prototipo_ruinas/hidrostal/para_marcar_hidrostal.png). Le gustó: géiser, shuriken detallado y girando, humo blando, tronco Sustituto de Smash que flota y se va. No le gustó: Kyogre, escenas rápidas, humo con líneas interiores.

## Cristal 4, Pirostal (808) — INTEGRADO (2026-10-18)

Ho-Oh con su pluma arcoíris (§256; `CapHoOh`, escenario `pirostal`, revisión en prototipo_ruinas/pirostal v1-v3). Tumbado encima del muro pequeño de la derecha (sitio marcado por el jugador). Le gustó: Ho-Oh aleteando de verdad. No le gustó: pluma pequeña e ilegible, arcoíris de puntos, llamas de rayas.

## Cristal 5, Fitostal (811) — hecho (v2, 2026-10-18)

Shaymin (§257; `CapShaymin`, escenario `fitostal`). En las losas entre el cofre y la columna (sitio marcado), con jardín.

## Regla: el tipo cambia el entorno (2026-10-18)

Cada cristal deja su tipo en las ruinas para siempre (§257): Normastal estrellas, Lucharastal grietas rojas con humo, Hidrostal huella de Greninja, Pirostal magma, Fitostal jardín. Para los siguientes: proponer animación de llegada + efecto en el entorno.

## Cristal 6, Litostal (819) — prototipo simple (2026-10-18)

Terápagos hace brotar cristales del Área Cero, Miraidon los rompe; el Litostal queda encima del muro bajo de la ventana grande (§258, `CapAreaZero`). Por pulir; falta el efecto de roca en el entorno ; el choque con las megas de la prueba 6 está decidido: primero el Litostal y después las megas (`CapMega.Wait`). Traspaso: [[Traspaso Litostal]].

**Litostal APROBADO (2026-10-18, §259).** Le gustó: Terápagos aleteando y Miraidon con el pecho de energía; cristales como los del Área Cero (bloques pálidos iridiscentes medio enterrados), la torre y las nubes cristalizadas enteras, rayos con ramas, grietas, onda de choque y el rayo final. No le gustó: cristales de agujas de colores, cristalitos sueltos en la torre y puntitos en las nubes, cristales colgando sobre el lecho, el Litostal balanceándose como un sprite volando, destellos difuminados. Siguiente: cristal 7, Electrostal (810): ideas de su tipo y que marque el sitio.
