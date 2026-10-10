# Mediciones del panel del cap con el juego de verdad

Todo lo que el panel hace con la vida (leerla pronto, enseñarla a la vez que el juego) descansa en cuatro números que **no se pueden sacar sin Azahar
abierto y un combate en marcha**. Estas son las tres mediciones, una a una. Cada sonda solo **lee** (memoria y pantalla): no escribe nada en el juego ni en la
partida. Se corren desde la carpeta del repositorio (`C:\Users\javie\Desktop\Permalocke definitivo`).

Antes de cada una:

1. Cierra PermaLocke (que no compita por el emulador) y abre Azahar con tu partida de siempre.
2. Entra en un combate y **déjalo esperando en el menú de ataques**. Un combate salvaje contra algo que te pueda hacer daño sin matarte, o uno de entrenador.
3. La ventana de Azahar delante y sin nada encima: la sonda mira la barra de PS en la pantalla.
4. Con la configuración de pantallas del emulador a mano: apilado (0, el de siempre), una pantalla (1) o lado a lado (3). `--layout` se lo dices a la sonda.

## 1. Calibrar la barra del juego y el retraso de «Sincronizada» sin barra

Qué da: cuánto tarda la barra del juego en **empezar** a moverse después de que cambie la memoria, y cuánto **dura**, según lo que quita el golpe. Con eso
se rellena `HpSyncTable` (la única tabla de números sin medir).

```bash
dotnet run --project tools/PermaLocke.Probe -c Release -- --combate sincronia 300 --layout 0
```

Mientras corre (300 s, o los que quieras; máximo 600), juega **sin matar a tu Pokémon**:

- **5 golpes pequeños** (menos del 10 % de sus PS: un Placaje flojo, una trampa de púas).
- **5 golpes medios** (del 20 al 40 %).
- **5 golpes fuertes** (más del 45 %, sin llegar a 0: sube antes la dificultad con un rival de más nivel o cambia a uno con menos PS).
- Si puedes, **2 golpes dobles** (un movimiento de dos o más golpes) y un **combate doble** (la sonda solo mira la barra de la primera caja).
- 3 **curas en combate** (Poción, Superpoción): la barra sube y también cuenta.

Tarda un golpe entero entre uno y otro (deja que acabe la barra y que vuelva el menú). Al terminar, la sonda escribe una tabla:

```
golpe  daño  cuota   tabla B→barra  tabla A→barra  la barra dura  relleno final
```

y **dos líneas listas para pegar** en `HpSyncTable` (`src/PermaLocke.App/Services/HpPresenter.cs`): `CalculatedBaseSeconds`, `CalculatedPerShareSeconds` y la
recta de `BarSeconds`. Los golpes están en `%TEMP%\permalocke-sincronia\golpes.csv`. Si alguna fila sale con «tabla A→barra» vacía es que la otra tabla no
llegó a verse cambiar antes; no pasa nada. Con menos de tres golpes de tamaños distintos la recta sale plana: es normal, trae más.

Dime el resultado (pega la tabla) y se cambian las constantes. **Nada más hay que tocar.**

## 2. Latencia real de lectura

Qué da: cuánto tarda cada forma de leer en enterarse de un cambio de PS **con el emulador de verdad** (el informe de `PlaqueLab --latencia` es contra un
emulador falso). Tres lectores del mismo bloque a la vez: uno de referencia que va todo lo deprisa que da el enlace, el rápido de la aplicación (120 por segundo)
y el lento de antes (cada 250 ms).

```bash
dotnet run --project tools/PermaLocke.Probe -c Release -- --combate latencia 180
```

Juega 180 s con golpes y curas (unos 10 cambios bastan). Saca el tiempo de una petición con el emulador de verdad (media, p95, máximo) y una tabla con el
retraso de cada lector respecto a la referencia. Lo normal es: rápido, unos 4-9 ms; lento, de 0 a 260 ms. Si la **media de una petición** sale por encima de
2-3 ms, el emulador está contestando despacio mientras emula: avísame, hay que bajar los 120 por segundo (la bomba ya se frena sola a cuatro veces lo que
tarda una lectura, pero conviene verlo).

Sigue sin poder medirse con una sonda: cuándo escribe el juego el dato (eso es lo que la referencia no puede ver), ni el coste de la ventana transparente
de WPF (`PlaqueLab --medir-ventana`, que abre dos ventanas sobre el escritorio: se corre cuando lo pidas).

## 3. El estado dentro de un combate

Qué da: si el bloque que el combate guarda de cada Pokémon lleva un byte de estado fiable. El equipo guarda el estado del combate anterior hasta que este
acaba, por eso el panel solo enseña el estado fuera de combate. **No se sabe dónde está el byte, si es que está**: la sonda coge los bloques enteros (0x320 bytes,
las dos tablas) en varias situaciones y dice qué desplazamientos cambiaron al Pokémon con estado y a **ningún otro**, y cuáles valen exactamente su estado.

Con el combate empezado y nadie con estado:

```bash
dotnet run --project tools/PermaLocke.Probe -c Release -- --combate estado capturar sano
```

Que uno de los tuyos se envenene (lo más fácil: equípale un **Orbe Tóxico**, o un Orbe Llama para la quemadura; al final del primer turno se pone el estado). En
cuanto la pantalla lo diga:

```bash
dotnet run --project tools/PermaLocke.Probe -c Release -- --combate estado capturar con-veneno
```

Si puedes, otro estado en otro Pokémon (parálisis con un rival que use Onda Trueno, sueño, quemadura) y otra captura, `con-quemadura`. **Acaba el combate**, ya en
el mundo (el equipo se actualiza al terminar), y:

```bash
dotnet run --project tools/PermaLocke.Probe -c Release -- --combate estado capturar despues
dotnet run --project tools/PermaLocke.Probe -c Release -- --combate estado analizar sano con-veneno despues
```

La sonda lista los desplazamientos que **cambiaron a exactamente el estado que el equipo tiene al acabar** sin que cambiara ninguno de los demás Pokémon. Para darlo
por bueno hace falta **el mismo desplazamiento con dos estados distintos y en otro combate**; entonces me das tabla y desplazamiento y se añade a
`BattleHotReader` con su prueba, y el panel enseña el estado en combate. Mientras tanto no cambia nada: el estado se sigue viendo solo fuera de combate, y el
panel **no** inventa curas ni estados al entrar y salir del combate (`CapEventFeed`, probado).

## Qué hay que mirar en el juego con la aplicación puesta

(Cuando la despliegues en la carpeta de prueba, con Azahar y PermaLocke cerrados al copiar.)

```bash
PermaLocke.exe --ensayar-cap captura 40
```

Los escenarios del laboratorio, sobre la banda real: `captura` (seis de nivel 42, doce caídos: mira el foco recorriendo los fósiles), `ev-golpe`, `ev-cura`,
`ev-antidoto`, `ev-curas`, `ev-revivir`, `ev-estado`, `ev-objeto`, `ev-movimiento`, y los de antes (`completo`, `caidos`, `huevos`, `combate`, `tope`, `inicio`,
`liga`, `subida`, `golpe`, `caida`, `curacion`, `cambio-cap`, `eclosion`, `estados`, `casi-tope`, `batalla`, `luto`, `noche`, `amanecer`, `atardecer`, `ballena`,
`manta`, `banco`, `ola`, `estrella`). En CONFIGURACIÓN, «Vida del panel»: Sincronizada, Instantánea y Retrasada.
