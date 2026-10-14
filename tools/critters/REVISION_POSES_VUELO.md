# Revisión de poses de vuelo — piloto de Pidgey

Estado: Pidgey y tanda 1 aprobados e intactos. Tanda 2 RECHAZADA. Cuatro nuevas bases estáticas pendientes de aprobación; Salamence sin batido.
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
| Salamence | Pose de cuadrúpedo con patas extendidas bajo el tronco | Planeo con alas fijas, SIN batido; retraer las cuatro patas y conservar las uniones de las alas |

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

### Tanda 2 rechazada: cuatro bases rehechas, también RECHAZADAS posteriormente (2026-10-14)

La tanda de vuelo de Skarmory, Dragonite, Charizard y Salamence NO está aprobada: el usuario señala pérdida de anatomía,
volumen y detalle en los cuatro. Las comprobaciones técnicas anteriores no validan esa calidad artística. Pidgey y tanda 1
siguen aprobados e intactos. La nueva instrucción es mostrar primero LAS CUATRO BASES, antes de modificar animaciones.

Se preparan propuestas estáticas separadas, sin cambiar las hojas ya integradas, sus ciclos ni CapCritterArt.cs. Por tanto,
la app todavía contiene la tanda 2 rechazada del commit 410e8d0. No confundir estas bases con una corrección publicada.
Salamence mantiene el criterio de planeo con alas fijas; no se ha generado ningún ciclo nuevo en esta revisión.

Método: tools/critters/flight_bases_v2.js lee los cuatro *_before_flight.json originales. Las piezas aprovechables se
reorientan por separado a escala 1, conservando el área nativa y la paleta; se reconstruyen superficies, articulaciones,
membranas, patas y uniones. No se gira el animal completo ni se comprime el torso para caber en el panel. Se conservan
cabezas, ojos y bocas por superposición verificable, se separan los brazos de las alas y se reconstruyen las zonas ocultas.
Los torsos y colas de Dragonite/Charizard recuperan grosor, las placas de Skarmory siguen la articulación y Salamence
presenta cuatro extremidades diferenciadas y cinco placas abdominales. Son decisiones de dibujo pendientes de revisión visual.

| Base | Caja nativa ocupada | Píxeles opacos | Cabeza conservada | Prueba de visualización |
|---|---|---|---|---|
| Skarmory | 80×49 | 1669 | 283/283 | 100 %, 80×49 |
| Dragonite | 102×69 | 2726 | 433/433 | 75 %, 77×52 |
| Charizard | 137×87 | 4935 | 419/419 | 75 %, 103×65 |
| Salamence | 134×79 | 5295 | 587/587 | 5/6, 112×66 |

No se igualan las cantidades de píxeles opacos: la perspectiva, las alas desplegadas y las oclusiones cambian la silueta.
La caja de trabajo es 170×120; las medidas de la tabla excluyen transparencia. Ninguna base fuente se reduce.
Las escalas de prueba son las de las reconstrucciones originales y SOLO se aplican en una imagen de evaluación.

[Verificado] Cuatro bases con iso 0, holes 0 en critter.check (huecos unitarios), una componente de 8 vecinos por base,
paleta original sin colores nuevos y 1722/1722 píxeles de las máscaras de cabeza conservados tras componer. Se registran
también las regiones transparentes cerradas mayores: espacios de aire entre alas, cuerpo, patas y láminas; no se declara
que toda transparencia cerrada sea un error ni que la prueba de huecos unitarios demuestre ausencia de todos los defectos.
135 archivos protegidos sin cambios respecto a 410e8d0: incluye las 54 hojas PNG, catálogo, generador, rigs y fuentes anteriores.
PNG comparados byte a byte; texto comparado normalizando CRLF a LF por Git. No se ejecutan build/test/medir: no hay cambios
en la app ni en animaciones. Los 555 tests anteriores pertenecen a la tanda anterior, no a una integración de estas bases.

Entrega estática: C:/Users/javie/Desktop/imagenes/revision/vuelo_tanda2_bases_rehechas/
- cuatro_referencias_y_bases.png: referencia real, sprite reconstruido original y nueva base, escala de celda comparable ×3.
- <especie>_referencia_y_base.png y <especie>_original_y_base.png: comparaciones individuales ×3 y ×4.
- <especie>_base_ampliada.png: ampliación ×6 sin suavizado; <especie>_base_nativa.png: PNG transparente a resolución nativa.
- <especie>_base.txt / _base.json: propuestas exportadas reproducibles, NO fuentes de producción aprobadas.
- bases_tamano_nativo.png: los cuatro dibujos a 1:1; prueba_cielo_69px.png: prueba estática de cuatro cielos de 178×69.
- auditoria_bases.json, conservacion.json y LEEME.md: alcance, datos y limitaciones.

Reproducción desde tools/critters: node review_flight_bases_v2.js. No llamar build_batch.js ni integrate_entries.js para esta revisión.
Limitaciones: la prueba de cielo no es una captura del laboratorio ni verifica trayectorias, inclinaciones o ciclos. Charizard
ocupa 65 de 69 píxeles y Salamence 66 de 69: el margen estático es de 4 y 3 píxeles; habrá que resolver el margen de movimiento
al animarlos, conservando las bases y sin comprimir su anatomía. No hay fps nuevos ni ciclo de vuelo que aprobar todavía.
Siguiente paso: esperar la revisión de estas cuatro bases. Solo tras ella, preparar ciclos y revisar cada fotograma y el layout real.

### Charizard: pose v3 desde una referencia de vuelo (2026-10-14), pendiente de aprobación

Las cuatro bases v2 también han sido RECHAZADAS por construcción de pose. La instrucción vigente sustituye
la revisión conjunta: trabajar uno por uno, empezar por Charizard y mostrar SOLO su nueva base junto a la
referencia de vuelo. Esperar aprobación antes de tocar los otros tres o construir animaciones.
Pidgey y tanda 1 siguen aprobados e intactos. Salamence deberá conservar planeo sin batido.

Referencia de postura: ilustración de Pokémon Ranger con copyright 2006 visible, conservada en LaunchBox
https://gamesdb.launchbox-app.com/games/images/13003-pokmon-ranger
Imagen: https://images.launchbox-app.com/d4b1eb5d-8a70-48ef-845f-a9572b83e701.png
El anfitrión es un archivo externo, no una web oficial de Pokémon. Se inspeccionó la imagen antes de dibujar.
Las descripciones del buscador confundían otras dos imágenes con Charizard: una lámina oficial de Aerodactyl
y un fotograma de Dragonite. Ambas descartadas. Un fotograma real de Charizard en Horizons EP18 sirvió
de contraste secundario; la postura dibujada se basa en Ranger.

Método: primero silueta, luego piezas con color plano, después detalle y sombras usando exclusivamente la
paleta del sprite original. Tórax por delante de pelvis, muslos y patas hacia atrás, brazos flexionados,
cola gruesa continuando desde las caderas, dos alas diferenciadas. Se reconstruyen las piezas del cuerpo,
sin comprimir ni girar el sprite completo. Cabeza original reorientada como pieza independiente 20 grados,
escala 1; rostro, cuernos y llama proceden del original. La referencia oculta el extremo de la cola tras el
entrenador: esa parte se completa con el sprite original y no se presenta como anatomía totalmente visible.

Fuentes reproducibles, separadas de producción:
- tools/critters/charizard_flight_pose_v3.js: construcción de la base.
- tools/critters/review_charizard_pose_v3.js: comparación, PNG y auditorías.
Ejecutar node review_charizard_pose_v3.js desde tools/critters, con referencias/ranger.png en la entrega.
No ejecutar build_batch.js ni integrate_entries.js para esta propuesta.

Entrega: C:/Users/javie/Desktop/imagenes/revision/charizard_pose_v3/
- charizard_pose_y_referencia.png: única comparación destinada a esta revisión.
- charizard_base_nativa.png: 158×72, transparente; charizard_base_ampliada.png: ampliación ×6 sin suavizado.
- charizard_base.txt y charizard_base.json: propuesta fuente, NO aprobada ni integrada.
- 01_silueta.png, 02_construccion_plana.png y 03_base_detallada.png: etapas de desarrollo.
  La etapa plana es histórica; la cara original se recuperó en la etapa detallada.
- auditoria.json, conservacion.json, LEEME.md y referencias/fuentes.json: datos y procedencia.

[Verificado] 4885 píxeles opacos, una componente de 8 vecinos, iso 0 y holes 0 (solo huecos unitarios).
425/425 píxeles de la pieza de cabeza conservados en la composición. Paleta original, sin colores añadidos.
137 archivos protegidos intactos respecto a 21c95bb: 54 PNG byte a byte y texto normalizando CRLF.
Ambos scripts pasan node --check. Estas comprobaciones NO demuestran que la pose sea artísticamente correcta.
No hay animación, integración ni nueva medición del layout. No se repiten build/tests de C# sin cambios
en la aplicación. La altura nativa de 72 supera el cielo de 69: la futura visualización y sus márgenes
siguen pendientes, sin reducir esta base para resolverlos artificialmente. La app conserva la tanda 2
rechazada de 410e8d0. Sin despliegue, sin tocar la app/emulador, commit local sin push.

Siguiente paso: esperar aprobación visual de esta base de Charizard. Skarmory, Dragonite y Salamence esperan.

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

### Charizard definitivo aprobado: animación e integración (2026-10-14)
El usuario aprueba la pose v3 con la corrección de mandíbula-cuello-pecho de charizard_cuello.
Esa base queda definitiva y congelada en tools/critters/charizard_flight_approved.json (paleta y lienzo 174×92;
caja ocupada 158×72). SHA256: 8717a80a6862970eba54867220219bdace421c16144af95d55aed6a5a47efb89.
Los generadores de pose y cuello anteriores permanecen intactos. No volver a redibujar esta anatomía.

Nuevo tools/critters/charizard_approved_flight.js: 16 fases a 12 fps, ciclo de 1,333 s. El primer fotograma
reproduce exactamente la base aprobada, 0 píxeles distintos. Alas dobladas por columnas desde sus raíces,
muñeca y extremo con desplazamientos diferentes y leve acortamiento en la recuperación para la perspectiva.
Se reconstruye la superficie dorsal del ala lejana oculta por la cabeza: mover solo sus fragmentos visibles
dejaba piezas desconectadas. Cuatro píxeles de su borde, superpuestos en el antiguo recorte de cabeza,
pertenecen al ala y se mueven con ella. La cara y el cuello no se reconstruyen ni se deforman.
Torso con acompañamiento vertical de hasta un píxel, patas con desplazamiento mínimo, cola con flexión
suave y llama con oscilación local. El ala cercana oculta naturalmente partes del torso/patas al bajar.
El torso oculto por la raíz del ala se reconstruye antes de componer; los brazos se dibujan delante.

Fuente nativa de animación: lienzo común 165×96, conservado sin reducir. Visualización de layout: 60 %,
vecino más cercano, lienzo 99×58. La reducción permite el batido completo dentro del cielo de 69 píxeles;
no es una compresión de la anatomía base ni modifica el tamaño del panel. Hoja 1632×306, celdas 102×102,
16 columnas y tres inclinaciones [-2,0,2]. Height 60, Drift 1, Bob 0.3, Surge 0.6.
La fila inclinada protege el rostro y el extremo fino del ala para evitar separar dos píxeles de su punta.

Integración limitada a charizard_flight_anim.js, Critters/charizard.png y la entrada 6 de CapCritterArt.cs.
Skarmory, Dragonite, Salamence, Pidgey y todas las demás hojas permanecen intactos.
[Verificado] 136 archivos protegidos sin cambios respecto a c20806c, incluyendo 53 hojas ajenas.
16/16 fases nativas y de visualización, y 48/48 celdas de hoja: una componente conectada, iso 0, holes 0
(huecos unitarios). 568/568 píxeles del rostro y unión del cuello conservados en cada fase nativa.
Ojos comprobados en las 48 celdas y mosaico de rostro revisado. Paleta original. Cierre periódico exacto:
generar f=16 coincide con f=0; diferencia de última a primera 1372 píxeles, dentro del rango de transiciones
interiores (646–3562), sin salto extra de reinicio. Recorrido vertical previsto dentro de y6–62 del cielo.
Las comprobaciones geométricas no sustituyen la aprobación artística del ciclo: pendiente de revisión.

[Verificado] dotnet build tools/PermaLocke.PlaqueLab --artifacts-path .lab-art: 0 errores, 2 CS0649 previas.
dotnet test tests/PermaLocke.App.Tests --artifacts-path .lab-art: 555 correctas, 0 fallos, 0 omitidas, 25 s.
Advertencias previas CS8321 y xUnit2030. --especies 6 --medir: Party 178×321 media 1,38–1,71 ms,
p95 1,91–3,19 ms; Deep media 0,78–1,04 ms. Primera preparación 84,15 ms. Son medidas locales sin emulador;
no demuestran una mejora frente a versiones anteriores ni equivalen a FPS de juego.
Layout real del laboratorio: 336 fotogramas, 28 s a 12 fps, preset 1920, sin reescalado de la exportación.
Revisados ambos cielos, trayectoria en ambas direcciones y ampliaciones. GIF fijo y layout decodifican sin errores.

Entrega definitiva: C:/Users/javie/Desktop/imagenes/revision/charizard_vuelo_definitivo/
- charizard_base_aprobada.png / .json: base aprobada congelada.
- charizard_bucle_fijo.gif: animación nativa ampliada ×3, personaje sin recorrido horizontal.
- charizard_layout_bucle_fijo.gif: ciclo a escala del sprite de layout, ×1.
- charizard_fotogramas.png y charizard_layout_fotogramas.png: fases de ambos tamaños.
- charizard_hoja_definitiva.png: hoja integrada.
- charizard_rostros_48_celdas.png y charizard_uniones.png: revisión de rasgos y articulaciones.
- charizard_en_el_layout_tamano_real.gif: laboratorio a tamaño real; apariciones forzadas de Charizard.
- layout_cielos_revision.png, auditoria.json, rendimiento.txt y LEEME.md: comprobaciones y alcance.

Reproducción desde tools/critters: node build_batch.js charizard; node integrate_entries.js;
node review_charizard_flight.js. Desde raíz: build y tests con --artifacts-path .lab-art;
tools/critters/render_charizard_flight.ps1. No usar el antiguo review_flight_batch2.js para esta versión:
su contrato espera el rig rechazado de ocho fases. Usar la auditoría específica nueva.
Limitaciones: el GIF cuantiza tiempos a centésimas; la frecuencia forzada del laboratorio no representa
apariciones normales, puede haber solapamientos y oclusión detrás de las pantallas centrales simuladas.
El reinicio del recorrido de 28 s no es el cierre del ciclo individual. No se desplegó ni se abrió la app
o Azahar; solo se ejecutó el laboratorio aislado. Commit local, sin push.

Pendiente, uno por uno: Skarmory → Dragonite → Salamence. Buscar e inspeccionar referencias de vuelo
específicas (preferiblemente oficiales o fotogramas), construir primero silueta y pose base, recuperar
paleta/rostro/detalle del original y mostrar base junto a referencia. Esperar aprobación de cada pose
antes de animarla. Las cuatro bases v2 rechazadas no son punto de partida aprobado. Salamence planea,
sin agitar las alas. Mantener Pidgey y todas las especies aprobadas intactas.

### Charizard: segunda revisión del batido (2026-10-14), pendiente de aprobación
El usuario conserva la base aprobada y rechaza el batido anterior por rigidez del ala cercana y poca
participación de la lejana. charizard_vuelo_definitivo queda como histórico del primer ciclo, no como
animación aprobada. La base charizard_flight_approved.json mantiene su hash y su anatomía.

Nuevo tools/critters/charizard_wing_poses.js: poses clave explícitas con hombro, codo, muñeca, dedos
y centro de membrana independientes. Ambas alas comparten las fases 0/3/7/11/14/16, con máximo descenso
en 7. Bajada extendida y recuperación flexionada con otra perspectiva. La membrana se reconstruye
por sectores y los nervios se redibujan continuos. No se gira el ala como un bloque.
El brazo del ala conserva grosor. Raíces fijas en coordenadas del cuerpo: 57/57 píxeles del anclaje
cercano y 50/50 del lejano conservados en todas las fases, comprobados en sus capas antes de las oclusiones.
Orden: ala lejana, volumen oculto del tórax, cuerpo/patas/cola, ala cercana y brazo delantero.
La lejana queda tapada al pasar detrás del cuerpo; la cercana ocluye torso/patas/cola durante la bajada.
Respuesta corporal de un píxel hacia arriba durante el impulso y hacia abajo al recuperar, sin deformar
anatomía ni unión cabeza-cuello. Se conservan los pequeños movimientos de cola, patas y llama.

[Verificado] Primer fotograma idéntico a la base: 0 píxeles distintos. Rostro/cuello 568/568 en cada fase.
16 fases a 12 fps (1,333 s), inclinaciones [-2,0,2], 48 celdas de 104×104; hoja 1664×312.
Lienzo nativo 168×96; display 101×58 al 60 % sin suavizado, Height 60. Una componente conectada e iso 0,
holes unitarios 0 en todas las fases nativas/display y celdas. Ojos presentes en 48/48; cielo previsto y6–62.
f16=f0; cierre 1308 píxeles distintos, dentro del rango interior 1258–2859. 137 archivos protegidos
intactos respecto a 8121f8a. Solo cambia la hoja/entrada de Charizard; base y otros Pokémon intactos.
La integración en código no equivale a aprobación artística: el batido v2 sigue pendiente.

Build de laboratorio con --artifacts-path .lab-art: 0 errores, dos CS0649 previas.
Tests App con --artifacts-path .lab-art: 555 correctas, 0 fallos, 0 omitidas, 24 s (CS8321/xUnit2030 previas).
--especies 6 --medir: Party 178×321 media 1,41–2,09 ms, p95 1,90–4,27 ms; Deep media 0,80–1,12 ms.
Primera preparación 104,87 ms. Medidas locales sin emulador, no comparación controlada ni FPS de juego.
Captura de control del layout revisada, GIF decodificado sin errores. Sin abrir app/Azahar ni desplegar.

Entrega actual: C:/Users/javie/Desktop/imagenes/revision/charizard_batido_v2/
- charizard_bucle_fijo.gif: ciclo nativo ampliado ×3.
- charizard_fotogramas_clave.png: seis poses, hombro ×4 y ala lejana aislada sobre guía gris del cuerpo.
  Esa vista diagnóstica revela la superficie que el cuerpo oculta y no modifica el sprite final.
- charizard_fotogramas.png, charizard_uniones.png y charizard_rostros_48_celdas.png: comprobaciones.
- charizard_layout_bucle_fijo.gif y charizard_layout_fotogramas.png: tamaño de sprite para layout.
- charizard_base_aprobada.png/.json, charizard_hoja_definitiva.png, auditoria.json, rendimiento.txt,
  layout_control.png y LEEME.md. No se exporta otro recorrido completo en esta revisión.
Desde tools/critters: node build_batch.js charizard; node integrate_entries.js;
node review_charizard_flight.js; node review_charizard_wing_keys.js.
render_charizard_flight.ps1 apunta a esta entrega para una futura exportación completa opcional.

Limitaciones: los chequeos geométricos no equivalen a aprobación visual. La perspectiva y las oclusiones
cambian el área visible de las alas; no se exige área opaca constante del animal completo. GIF con tiempos
cuantizados a centésimas. Pendiente revisar este batido; Skarmory, Dragonite y Salamence siguen esperando
poses individuales desde referencias de vuelo, sin modificar en esta tarea. Commit local sin push.
