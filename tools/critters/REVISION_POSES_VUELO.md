# Revisión de poses de vuelo — piloto de Pidgey

Estado: Pidgey y tanda 1 aprobados e intactos. Tanda 2 de los cuatro restantes integrada y entregada para revisión; Salamence sin batido.
Fecha: 2026-10-14. El usuario autoriza redibujar la pose base cuando la referencia representa apoyo en tierra;
esta instrucción sustituye la restricción anterior de reconstruir siempre la misma pose. No autoriza rehacer las especies que ya funcionan.

## Inventario

Se han revisado los 28 voladores del catálogo mediante dos fases del ciclo y sus scripts anatómicos.
Los diez casos claros de pose terrestre se proponen para corrección; solo Pidgey tiene una propuesta nueva en esta entrega.

| Especie | Problema observado | Criterio propuesto |
|---|---|---|
| Pidgey | Torso erguido, patas de apoyo y ala plegada que solo oscila | Batido rápido, torso hacia delante, alas articuladas abiertas y patas flexionadas |
| Spearow | Cuerpo de ave posada y dedos de apoyo, aunque el ala está alzada | Batido rápido; cuello y torso hacia el avance, patas recogidas |
| Pikipek | Pose posada casi intacta, alas cerradas y pies extendidos | Batido y tramos de planeo breve; cuerpo inclinado y dedos recogidos |
| Pidgeot | Postura de pie, ala pegada al costado y pies bajo el peso | Batido amplio y planeo; cresta y cola acompañan la dirección |
| Toucannon | Torso vertical y postura de apoyo con alas cerradas | Batido de alas anchas; equilibrar el pico con torso y cola |
| Staraptor | Pose posada y ala cerrada; cambios mínimos de ala/patas | Batido firme y planeo con patas flexionadas y torso adelantado |
| Skarmory | Piernas extendidas y pies preparados para apoyar | Planeo dominante y golpes de ala articulados; recoger patas conservando placas metálicas |
| Dragonite | Caderas, piernas y pies de postura terrestre | Vuelo con batido; torso algo inclinado y rodillas flexionadas, sin aplanarlo como un ave |
| Charizard | Apoyo bípedo conservado y batido demasiado contenido sobre ese cuerpo | Batido de alas membranosas, caderas/patas recogidas y cola estabilizadora |
| Salamence | Pose de cuadrúpedo con patas extendidas bajo el tronco | Planeo y batido; retraer las cuatro patas y adaptar articulaciones de las alas |

Fuera de esta corrección por postura terrestre: Wingull, Butterfree, Swablu, Zubat, Pelipper, Fearow, Altaria, Crobat, Togekiss,
Aerodactyl, Latias, Latios, Rayquaza, Ho-Oh, Articuno, Zapdos, Moltres y Lugia. Sus poses de partida admiten vuelo, planeo o suspensión;
no se deben horizontalizar de forma uniforme. Butterfree conserva su postura de insecto; Altaria su sustentación entre nubes;
Rayquaza su levitación serpentina; Latias/Latios/Togekiss su silueta de planeo. Esto no es una auditoría de física real ni una orden de retocarlos.

## Piloto: qué ha cambiado

- Rostro, pico, cresta y máscara de Pidgey conservados: 212 píxeles opacos de la cabeza idénticos en los ocho fotogramas.
- Torso reconstruido horizontalmente, con espalda y vientre completos debajo de las alas. No se ha girado el sprite entero.
- Patas nuevas flexionadas, con tobillos y dedos hacia atrás; siguen siendo visibles durante el batido, con oclusión parcial natural por el ala.
- Cola nueva extendida desde la rabadilla, unida al torso y con un desfase pequeño respecto al golpe de ala.
- Dos alas nuevas abiertas, con coberteras, secundarias y primarias. Hombro, codo y muñeca cambian de posición; se pliegan más al recuperar.
- Cabeza y cuerpo fijos en el GIF de revisión. No se simula el batido trasladando el dibujo entero.
- Solo los 16 colores originales. Bordes nítidos, sin interpolación de color. Las partes nuevas requieren tu aprobación artística.

## Archivos para revisar

- pidgey_antes_y_despues.png: misma escala de píxel y cabeza alineada; pose de apoyo anterior frente a la propuesta.
- pidgey_antes_y_vuelo_fijo.gif: anterior estático junto al ciclo nuevo con el cuerpo fijo.
- pidgey_flight_bucle_fijo.gif: el nuevo ciclo por separado, ampliado ×6.
- pidgey_flight_fotogramas.png y pidgey_hoja_propuesta.png: fotogramas y hoja de inclinaciones.
- pidgey_direcciones.gif: espejo en ambos sentidos, como hace Swimmer.
- pidgey_trayectoria_simulada.gif: simulación externa de avance, inclinación y límites del cielo. No es una captura de la app.
- voladores_actuales_0.png y voladores_actuales_1.png: inventario visual de las 28 especies antes de esta revisión.
- pidgey_flight_costuras.png y pidgey_flight_rostros.png: ampliaciones de uniones y ojos.
- auditoria_pidgey.json y conservacion.json: comprobaciones reproducibles y conservación de las versiones anteriores.

## Comprobaciones y límites

8 fotogramas a 12 fps (ciclo de 2/3 de segundo), lienzo de 72×64, celda de 78, inclinaciones -4/0/4 grados.
24/24 regiones de ojos conservan negro y blanco. Iso 0 y holes 0 en fotogramas y hoja. Una sola componente conectada en cada uno de los
8 fotogramas y las 24 celdas: ni alas ni cola flotan separadas. Sin celdas tocando bordes. El cierre de ciclo queda dentro del máximo
de variación entre fotogramas, con la tolerancia previa del 5 %.
Reserva de altura propuesta: 58. Con las reglas del cielo de 69, los píxeles quedan en y=4..57 y hay cuatro unidades de margen de trayectoria.
Se comprueba también que bottom > top: caber por píxeles no basta. La base mira a la izquierda; el avance a la derecha usa el mismo espejo
horizontal del layout. La simulación externa no reemplaza la futura validación integrada en WPF.

Las 54 hojas de producción permanecen idénticas byte a byte. Los 53 scripts anatómicos previos y critter.js conservan su contenido;
la comparación con Git normaliza CRLF/LF únicamente para los textos. No se han ejecutado builds ni cambiado la app: para este piloto
se han realizado las auditorías de sprites y las revisiones visuales, no una nueva ejecución de las 555 pruebas de WPF.

La revisión automática bloqueó la integración antes de la aprobación del piloto. Esta entrega es la alternativa separada para revisión.
El siguiente paso, solo tras aprobar pose y ciclo, es integrar Pidgey, comprobarlo en el laboratorio aislado y extender el criterio a
los otros nueve afectados, respetando la anatomía y el tipo de vuelo de cada uno.

## Reproducción

Desde tools/critters: node review_flight_inventory.js y node review_pidgey_flight.js.
La propuesta vive en pidgey_flight_anim.js; pidgey_anim.js y pidgey.png siguen siendo las versiones anteriores.

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

### Correcciones de vuelo, tanda 1 (2026-10-14): cinco entregados para revisión
Pidgey corregido aprobado por el usuario: conservar exactamente tools/critters/pidgey_flight_anim.js (commit de detalle 8da0848)
y C:/Users/javie/Desktop/imagenes/revision/vuelo_piloto/detalle_pidgey/. Esta tanda no modifica su código, paleta, movimiento ni PNG.
Su propuesta aprobada sigue separada de la antigua hoja de producción; no se sustituye aquí para respetar la indicación de mantenerlo intacto.
El usuario autoriza continuar con los demás afectados en tandas de cinco y conservar los que ya funcionan.

Primera tanda: Spearow, Pikipek, Pidgeot, Toucannon y Staraptor. Se redibujan torso, patas flexionadas, cola y alas abiertas en sus paletas;
no se gira el personaje completo ni se conserva el cuerpo de pie. Cabezas y marcas conservadas por recortes explícitos de las referencias.
Pidgeot prolonga la cresta hacia atrás; las zonas descubiertas tienen pecho/vientre completos. Alas articuladas por hombro, codo y muñeca,
con textura propia por especie, muestreo inverso sin suavizado, coberteras que cubren la raíz y ala lejana sombreada.
La primera revisión corrigió uniones de rabadilla/patas y retiró cuatro píxeles del ala antigua incluidos en el recorte de cabeza de Spearow.

| Especie | Lienzo (sin reducción) | Celda | Ciclo / fps | Movimiento y rasgos |
|---|---|---|---|---|
| Spearow | 88×68 | 92 | 8 / 12 | Batido rápido y estrecho, pecho listado, plumas rosadas y cola naranja |
| Pikipek | 80×64 | 84 | 8 / 14 | Golpe rápido, recogida y dos fases de planeo breve; iris azul y cresta roja |
| Pidgeot | 104×68 | 108 | 8 / 8 | Batido amplio, recuperación plegada, cresta larga hacia atrás, pecho escalonado |
| Toucannon | 116×70 | 120 | 8 / 10 | Alas anchas, pico original equilibrado por torso horizontal y cola; patas azules recogidas |
| Staraptor | 104×70 | 108 | 8 / 9 | Batido firme, primarias abiertas; máscara, cresta y grupos blancos del pecho conservados |

Archivos definitivos del proyecto: C:/Users/javie/Desktop/Permalocke singleplayer/src/PermaLocke.App/Views/Cap/Critters/
{spearow,pikipek,pidgeot,toucannon,staraptor}.png. Integradas solo sus cinco entradas de CapCritterArt.cs.
Fuentes: tools/critters/flight_batch1_specs.js, flight_rig.js y los cinco <especie>_flight_anim.js.
Los <especie>_anim.js anteriores y los TXT reconstruidos permanecen intactos; <especie>_before_flight.json conserva el antes.
build_batch.js selecciona las nuevas fuentes solo para estos cinco nombres: regenerar el lote no recupera accidentalmente la pose de tierra.
Se mantienen las rutas del layout; inclinaciones -2/0/2, reserva 60 (Pikipek 58), Drift 1, Bob 0.3, Surge 0.6 para contener el avance vertical.

Entrega: C:/Users/javie/Desktop/imagenes/revision/vuelo_tanda1/
- tanda1_antes_y_despues.png: comparación a igual escala ×3.
- tanda1_ciclos_comparados.gif: antes estático y ciclos nuevos con cuerpo fijo; cada especie conserva su frecuencia.
- <especie>_antes_y_despues.png, <especie>_flight_bucle_fijo.gif, <especie>_flight_fotogramas.png.
- <especie>_hoja_definitiva.png, <especie>_rostros.png, <especie>_uniones.png.
- tanda1_juntos_en_el_layout_tamano_real.gif: laboratorio real, 336 fotogramas, 28 s, 12 fps, 1920×976, sin reescalado.
- auditoria.json, conservacion.json, rendimiento.txt, verificacion_gif.txt y mosaicos de ambos paneles.

Verificado: 40 fotogramas, 120 celdas, 120/120 regiones de ojos. Iso 0, holes 0 y una única componente conectada en los 160 dibujos.
Cabeza idéntica en cada fase: 247 píxeles Spearow, 351 Pikipek, 433 Pidgeot, 721 Toucannon y 590 Staraptor.
Sin celdas recortadas; cierres de ciclo dentro del máximo de las transiciones (tolerancia 5 %).
Sellado de huecos unitarios tras rotar: 10/0/8/1/0 respectivamente. Cielo y=5..56, 7..54, 2..55, 4..56 y 0..56; margen de trayectoria 2/4/2/2/2.
Las hojas integradas coinciden byte a byte con las auditadas. 107 archivos protegidos intactos: las otras 49 hojas, los 53 scripts
anatómicos anteriores, critter.js y cuatro archivos del piloto de Pidgey. Ninguna especie fuera de la tanda cambia.
Build aislado: 0 errores, dos advertencias CS0649 previas. Pruebas App: 555/555, 0 fallos y 0 omitidas (23 s);
advertencias CS8321/xUnit2030 previas. Laboratorio --medir: Party 178×321 media 1,48–1,71 ms, p95 2,14–3,23 ms;
Deep media 0,87–1,22 ms. Primera preparación 87,19 ms. Medidas de este PC, sin emulador; no son una comparación controlada de rendimiento.

Reproducción desde tools/critters: node review_flight_batch1.js; node assemble_flight_batch1.js;
node build_batch.js spearow pikipek pidgeot toucannon staraptor; node integrate_entries.js.
Desde raíz: dotnet build tools/PermaLocke.PlaqueLab --artifacts-path .lab-art;
dotnet test tests/PermaLocke.App.Tests --artifacts-path .lab-art; tools/critters/render_flight_batch1.ps1.
Limitaciones: amplitud contenida por los 69 píxeles de cielo; oclusión natural por alas y solapamientos entre visitantes en la vista forzada.
La frecuencia forzada no representa apariciones normales. El GIF del layout usa 12 fps, mientras los ciclos individuales conservan sus fps;
los tiempos GIF se cuantizan a centésimas. El reinicio del recorrido conjunto no es el bucle del batido. Pantallas del juego simuladas por el laboratorio.
Estado: tanda 1 entregada para revisión visual. Restan Skarmory, Dragonite, Charizard y Salamence para la siguiente tanda (cuatro restantes).
No se ha desplegado ni abierto la app o Azahar. Commit local, sin push.

### Correcciones de vuelo, tanda 2 (2026-10-14): cuatro restantes, entregados para revisión
El usuario respondió «ok» a la tanda 1, precisó que Salamence no debe batir las alas y pidió continuar.
Spearow, Pikipek, Pidgeot, Toucannon y Staraptor se conservan. Pidgey aprobado permanece intacto en su fuente y entrega separadas.
Se corrigen Skarmory, Dragonite, Charizard y Salamence; no se añade una quinta especie porque solo quedaban estas cuatro del inventario.

| Especie | Lienzo nativo | Celda | Fotogramas / fps | Anatomía y movimiento |
|---|---|---|---|---|
| Skarmory | 112×72 | 116 | 8 / 7 | Torso de placas horizontal, patas metálicas recogidas y planeo con ajustes angulares de láminas rígidas |
| Dragonite | 112×74 | 116 | 8 / 10 | Torso inclinado con vientre segmentado, rodillas flexionadas, brazos recogidos y batido de alas pequeñas |
| Charizard | 128×74 | 132 | 8 / 8 | Membranas amplias con nervaduras, torso inclinado, patas recogidas, cola extendida y llama conservada |
| Salamence | 144×74 | 148 | 8 / 6 | Planeo con dos alas extendidas fijas, cuatro patas recogidas, cuernos unidos al cráneo y cola estabilizadora |

Todas usan la resolución de la cuadrícula original y su paleta, sin reducción de estas nuevas poses: caben en el cielo por su orientación
reconstruida. Se conservan los antiguos TXT, scripts y fotogramas de referencia, incluidas sus antiguas reducciones de visualización.
Los tamaños de lienzo incluyen transparencia, no son cajas ajustadas al dibujo. No confundirlos con la altura real ocupada en el cielo.

Método nuevo: flight_rig2.js es independiente de flight_rig.js para no alterar la tanda 1. Recorta cabezas por rectángulo y máscara explícitos,
compone extremidades cercanas/lejanas, redibuja torso y rabadilla y usa texturas de alas diferentes: metal, membrana o superficie fija.
Las membranas de Charizard se ampliaron para conservar su carácter y nervaduras; Dragonite/Charizard recuperaron volumen abdominal.
Revisión de costuras: se corrigió la unión de la cola de Skarmory, una muestra aislada de membrana y la raíz del cuerno lejano de Salamence.
Se eliminaron restos del ala original que no pertenecían a la cabeza, sin borrar los ojos, dientes ni la boca.

Salamence: las capas de ambas alas son idénticas píxel a píxel en las ocho fases (fixedWings=true). No hay batido, plegado ni deformación
periódica de las alas. Solo la cola varía en el ciclo local; la inclinación global de la trayectoria afecta al animal completo. Su entrada usa
Drift 1.2, Bob 0 y Surge 0: sin impulso periódico que aparente un golpe de ala. Los otros tres conservan Drift 1, Bob 0.3, Surge 0.6.
Inclinaciones -2/0/2 y reserva 60 en los cuatro; trayectoria con dos unidades de margen dentro del cielo de 69.

Fuentes definitivas: C:/Users/javie/Desktop/Permalocke singleplayer/tools/critters/flight_batch2_specs.js, flight_rig2.js
y {skarmory,dragonite,charizard,salamence}_flight_anim.js. Cada <especie>_before_flight.json conserva su versión anterior.
Hojas definitivas integradas: C:/Users/javie/Desktop/Permalocke singleplayer/src/PermaLocke.App/Views/Cap/Critters/
{skarmory,dragonite,charizard,salamence}.png. Solo sus cuatro entradas cambian en CapCritterArt.cs.
build_batch.js selecciona las nuevas fuentes para las nueve especies de ambas tandas; los parámetros de las cinco anteriores no cambian.

Entrega: C:/Users/javie/Desktop/imagenes/revision/vuelo_tanda2/
- tanda2_antes_y_despues.png y tanda2_ciclos_comparados.gif: antes a resolución nativa frente a las nuevas poses, igual escala ×3.
- <especie>_antes_y_despues.png, <especie>_flight_bucle_fijo.gif y <especie>_flight_fotogramas.png.
- <especie>_hoja_definitiva.png, <especie>_rostros.png y <especie>_uniones.png.
- tanda2_juntos_en_el_layout_tamano_real.gif: laboratorio real, 1920×976, 336 fotogramas a 12 fps, 28 segundos, sin reescalado.
- auditoria.json, conservacion.json, rendimiento.txt, *_verificacion.txt y mosaicos de ambos paneles.

Comprobaciones [verificado]: 32 fotogramas y 96 celdas inclinadas, 96/96 ojos, iso 0, holes 0 y una componente conectada en los 128 dibujos.
Sin contacto con bordes de lienzos nativos ni celdas. Cabezas conservadas en todas las fases: Skarmory 283 píxeles, Dragonite 427,
Charizard 419, Salamence 472. Cierres de ciclo dentro del máximo de transiciones con tolerancia 5 %; algunas fases del planeo se repiten
porque no hay batido. Sellado de huecos unitarios al rotar: 13/0/3/0, respectivamente. Límites de cielo y=0..45, 1..52, 1..52 y 3..52.
Hojas integradas idénticas byte a byte a las auditadas. 123 archivos protegidos intactos: 50 hojas ajenas a la tanda, 53 scripts anatómicos
originales, critter.js, cuatro archivos de Pidgey y 15 archivos de la tanda 1. No se modifica la entrega aprobada de Pidgey.
Build del laboratorio en .lab-art: 0 errores, dos advertencias CS0649 previas. Pruebas App: 555/555, 0 fallos y 0 omitidas, 24 s;
advertencias CS8321 y xUnit2030 previas. --medir: Party 178×321 media 1,57–1,87 ms, p95 2,13–3,24 ms; Deep media 0,90–1,25 ms.
Primera preparación 91,99 ms. Medidas en este PC, sin emulador y sin comparación controlada con versiones anteriores.
GIF conjunto y ciclos individuales decodificados sin errores. Mosaicos de ambos paneles y una ventana completa revisados: aparecen los cuatro.

Reproducción desde tools/critters: node review_flight_batch2.js; node assemble_flight_batch2.js;
node build_batch.js skarmory dragonite charizard salamence; node integrate_entries.js.
Desde raíz: dotnet build tools/PermaLocke.PlaqueLab --artifacts-path .lab-art;
dotnet test tests/PermaLocke.App.Tests --artifacts-path .lab-art; tools/critters/render_flight_batch2.ps1.
Limitaciones: amplitud y balanceo contenidos por el cielo; solapamientos naturales entre visitantes y oclusión detrás del juego en modo forzado.
La frecuencia forzada no representa apariciones normales. El layout se exporta a 12 fps; los ciclos individuales mantienen sus frecuencias.
Los tiempos GIF se cuantizan a centésimas y el reinicio del recorrido no es el cierre del ciclo individual. Las pantallas centrales son simuladas
por el laboratorio. La aprobación artística de esta tanda sigue pendiente; no quedan especies del inventario sin una propuesta de corrección.
Sin despliegue, sin abrir la app ni Azahar, commit local sin push.
