# Revisión de poses de vuelo — piloto de Pidgey

Estado: propuesta separada, pendiente de revisión del usuario. No integrada en la app ni en el laboratorio WPF.
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
