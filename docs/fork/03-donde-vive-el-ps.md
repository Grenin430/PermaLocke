# Parche 3 para el fork de Azahar — quién escribe los PS

**Estado: EN `master` DEL FORK Y COMPILADO.** Commits `ace262e` y `8deb3d8`, construidos en GitHub
Actions. **Nadie lo ha ejecutado todavía contra el juego**, así que de aquí abajo lo único
verificado es que compila.

Repositorio: `github.com/Grenin430/azahar`. A diferencia de los parches 1 y 2, este **no se queda en
`src/core/rpc/`**: engancha en el subsistema de memoria. Cinco ficheros, 222 líneas.

## Qué resuelve

El jugador quiere que un Pokémon muerto **siga muerto pase lo que pase**, en vivo, sin dejar de ser
él. Para eso hay que ponerle los PS a cero y que se queden. Y desde fuera no se puede:

- Los PS del fichero de partida **sí** los lee el juego, pero solo al cargar, y el fichero solo se
  deja escribir con el emulador cerrado (§98 bis). Eso da «muerto entre sesiones», no «muerto».
- Los PS de la copia del equipo en memoria, `0x330128E4`, **el juego no los lee jamás**. Medido tres
  veces: 120 escritos y la pantalla diciendo 128 con la memoria aún en 120; 40 escritos a otro
  Pokémon y ni el menú ni **entrar y salir de un combate** lo cambiaron. Esa copia es un espejo de
  una sola dirección — el juego la rellena y no la consulta (§98, §98 quater).
- Y no están en claro en ninguna otra parte. Ocho hipótesis, 400 MB barridos, todas negativas.

Buscando por la **constante de encriptación**, que va en claro, solo hay **tres** copias de cada
Pokémon en toda la memoria: el espejo, una estructura de salto `0x1E4` cuya cola son cabeceras del
asignador, y un objeto de gráficos con punteros a *shaders*. Ninguna es un almacén vivo.

Pero el juego **escribe** los PS buenos en el espejo. Por eso lo sigue clavado. Así que hay una
instrucción suya que los copia desde donde de verdad viven, y este parche hace que el emulador la
nombre.

## Lo que NO hizo falta inventar

El documento original daba por hecho que habría que pelearse con el JIT: escribe directo a la
memoria del anfitrión saltándose `MemorySystem`, así que un enganche ahí no vería nada, y el plan B
era desactivar el JIT o marcar las páginas con `RasterizerMarkRegionCached`.

**Nada de eso.** Azahar ya trae puntos de observación de memoria para su depurador:
`MemorySystem::RegisterWatchpoint` anula el puntero de la página, con lo que el JIT deja de escribir
por su cuenta y la escritura vuelve al camino lento — que es exactamente lo que se necesitaba, ya
escrito y ya probado. Y se comprobó lo que lo sostiene: `arm_dynarmic.cpp` monta el JIT con
`config.page_table = &current_page_table->GetPointerArray()` y **no configura fastmem**, así que
anular el puntero basta.

Leer antes de construir se ahorró el parche entero.

## Diseño

Dos paquetes nuevos, y el emulador se queda **tonto** igual que en el parche 2: no sabe qué es un
Pokémon, solo apunta quién escribió dónde.

### `WatchWrites(address, size)` — tipo 7

Hace dos cosas distintas y las dos hacen falta: `RegisterWatchpoint` saca esas páginas del camino
rápido, y `SetWriteWatch` dice cuáles de esas escrituras hay que anotar. `size == 0` para de vigilar
y devuelve las páginas a su sitio.

El rango anterior **se desmarca antes** de poner el nuevo: sin eso, pedir dos rangos seguidos
dejaría el primero frenado para siempre.

### `ReadWriteLog()` — tipo 8

Devuelve las anotaciones y vacía. Una anotación son pc, dirección, tamaño, valor y **los dieciséis
registros**: 80 bytes, así que en un paquete de 1024 caben doce. Contesta **cuántas van y cuántas
quedan**, y lo que no cabe se guarda para la petición siguiente en vez de tirarse.

Eso último no es celo: es la lección del §98. `SearchMemory` trunca a 255 aciertos por llamada, lo
dice en su propio resumen, y aun así sus totales —510 y 765, dos y tres veces el tope clavados— se
leyeron como medidas y costaron una noche. Un protocolo que puede perder cosas tiene que decirlo.

## Dónde engancha, exactamente

En `MemorySystem::Write<T>`, en los dos casos de página vigilada, después de la escritura, y **solo
cuando la tabla de páginas es la que la CPU está ejecutando**: ese mismo `Write` lo usan otros
hilos, y preguntarle el PC al núcleo desde uno de ellos sería leer un estado que no es de nadie.

Dos topes, por lo mismo que `MAX_WATCH_ENTRIES`: el rango no pasa de **una página** —vigilar la saca
del camino rápido, así que uno grande frena el emulador entero y entierra la respuesta en escrituras
que nadie ha pedido— y el registro guarda **256** anotaciones, contando en el log las que se dejan de
apuntar.

## Cómo se usa

```
Probe --escrituras 330129D4 2     # el offset 0xF0 del hueco 0: los PS, dos bytes
# recibir un golpe en combate
Probe --escrituras --leer
```

Para cada anotación, PermaLocke prueba los dieciséis registros como dirección —y también
`registro - 0xF0`, porque un bucle de copia lleva tantas veces un puntero al campo como al
registro—, lee 260 bytes, los pasa por PKHeX y mira si sale un Pokémon válido. El que salga es el
almacén de verdad. Mecánico, sin desensamblar y sin elegir a ojo.

## Cómo se comprueba que funciona

Con la disciplina del parche 2: **una línea del log del emulador no es opcional**, es la prueba.

1. El log tiene que llevar `Write watch: 0x... + 2 bytes` al registrarlo.
2. Después de un golpe, `--escrituras --leer` tiene que devolver **al menos una** anotación.
3. Y al menos uno de sus registros tiene que llevar a un Pokémon legible con los PS correctos.

Si el paso 2 vuelve vacío, el enganche no ve las escrituras y el problema es el emulador, no el
juego. Distinguir esos dos casos es la mitad del valor del parche, así que el cliente dice cuál de
los dos ha pasado en vez de contestar «nada».

## Lo que puede salir mal, dicho antes

**Que no haya dirección.** Si el juego descifra al Pokémon en una pila, lo usa y la tira, el paso 3
volverá vacío y no habrá nada que clavar — habría que parchear el código del juego, que es otra
liga. Es la hipótesis que mejor encaja con todos los negativos del §98, así que **hay que contar con
ella**. Sería una respuesta igual, y hoy no la tenemos.

**Y el rendimiento.** Este parche toca el camino de memoria, que es lo más caliente del emulador.
Con la lista vacía tiene que comportarse exactamente como antes, y eso es lo primero que hay que
comprobar: arrancar el juego con el parche puesto y sin nada vigilado, y ver que va igual.
