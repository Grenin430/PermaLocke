# Continuidad: sprites de los Pokémon que pasan por el mar

Documento de traspaso para continuar en una sesión nueva sin acceso a la conversación anterior. Escrito el 2026-10-14.
Todo lo marcado **[verificado]** se comprobó al escribir este documento; lo marcado **[pendiente]** no.

Proyecto: `C:\Users\javie\Desktop\Permalocke singleplayer` (rama `singleplayer`, git local, **sin push**). Es la versión de un jugador de
PermaLocke (C# / .NET 10 / WPF). Lee también su `CLAUDE.md` (reglas del repositorio: código y comentarios en inglés, UI y documentos en
español, nunca desplegar a `PermaLocke prueba` ni a `PermaLocke para amigos`, no tocar la app ni Azahar mientras estén abiertos).

---

## 1. Objetivo y estado actual

### Qué estamos creando

El **panel del cap** es un "mar" pixel art que la app dibuja en las dos bandas laterales de la ventana del emulador mientras se juega
(ventana izquierda = "fondo del mar", `CapPanelKind.Deep`; ventana derecha = equipo del jugador, `CapPanelKind.Party`). Por ese mar y su
cielo **pasan Pokémon**: nadadores bajo la superficie y voladores en el cielo, que cruzan de una ventana a la otra por detrás del juego.

El usuario pidió rehacer esos Pokémon con calidad de sprite de Nintendo DS: fieles a su referencia, pixel art nítido, y con animación
corporal propia de cada especie (no un sprite estático que se traslada). Cada Pokémon terminado es una **hoja PNG** (filas = inclinaciones,
columnas = fotogramas del ciclo) incrustada en el ejecutable; la app elige la fila según la dirección de su trayectoria y la columna según
el reloj de su animación.

### Aprobados por el usuario e integrados [verificado en el código y en git]

| Pokémon | Nº | Hoja definitiva en la app | Fotogramas | Inclinaciones (°) | Celda | Commit |
|---|---|---|---|---|---|---|
| Magikarp | 129 | `src/PermaLocke.App/Views/Cap/Critters/magikarp.png` | 8 a 8 fps | 0,4,8,12,16,20,24 | 64 | `0811c5f` |
| Horsea | 116 | `.../Critters/horsea.png` | 8 a 10 fps | -6,-3,0,3,6 | 48 | `adab31d` |
| Goldeen | 118 | `.../Critters/goldeen.png` | 8 a 7 fps | 0,4,8,12,16,20 | 88 | `adab31d` |
| Lapras | 131 | `.../Critters/lapras.png` | 8 a 6 fps | -4,0,4,8 | 88 | `adab31d` |
| Wingull | 278 | `.../Critters/wingull.png` | 8 a 9 fps | -8,-4,0,4,8 | 82 | `adab31d` |
| Zubat | 41 | `.../Critters/zubat.png` | 6 a 12 fps | -8,-4,0,4,8 | 76 | `adab31d` |

El usuario aprobó Magikarp como **referencia de calidad** (aspecto, animación e integración) y dijo del lote 1 que es "perfecto".

### Pendientes (48) [verificado: lista exacta de `CritterArt.All` menos los 6 de arriba]

Siguen con el dibujo antiguo hecho a mano en texto (simple, no aprobado). Todos tienen referencia en `C:\Users\javie\Desktop\imagenes`.

- Nadadores comunes: Finneon (456), Luvdisc (370), Remoraid (223), Qwilfish (211), Wishiwashi (746), Chinchou (170), Carvanha (318), Bruxish (779)
- Nadadores menos comunes/raros: Seaking (119), Mantine (226), Wailmer (320), Sharpedo (319), Lumineon (457), Lanturn (171), Relicanth (369),
  Seadra (117), Gyarados (130), Milotic (350), Kingdra (230), Manaphy (490)
- Grandes del mar: Kyogre (382), Wailord (321), Lugia (249; también vuela)
- Voladores comunes: Pidgey (16), Spearow (21), Pikipek (731), Butterfree (12), Swablu (333)
- Voladores menos comunes/raros: Pelipper (279), Pidgeot (18), Fearow (22), Toucannon (733), Altaria (334), Skarmory (227), Staraptor (398),
  Crobat (169), Togekiss (468), Dragonite (149), Salamence (373), Charizard (6), Aerodactyl (142), Latias (380), Latios (381)
- Legendarios del cielo: Rayquaza (384), Ho-Oh (250), Articuno (144), Zapdos (145), Moltres (146)

Sharpedo (319) además es "el tiburón del peligro" (`CapVisitors.Prowl`), que hoy usa el dibujo antiguo.

Registro de estado por especie: `C:\Users\javie\Desktop\imagenes\revision\registro_sprites.md` (actualízalo al terminar cada lote).

---

## 2. Qué se ha hecho y dónde

### Código de la app (todo en `C:\Users\javie\Desktop\Permalocke singleplayer`)

| Archivo | Qué hace |
|---|---|
| `src/PermaLocke.App/Views/Cap/CapCritterArt.cs` | Catálogo `CritterArt.All` de las 54 especies. Una entrada **con hoja** es una línea `new(id, "Nombre", CritterMotion.Fish, "", "", Fps:, Sheet:, Cell:, Angles:, Pose:, Height:, Drift:, Bob:, Surge:)`. Las demás son el dibujo antiguo en texto (varias líneas con `"""`). El record está en la línea ~33. |
| `src/PermaLocke.App/Views/Cap/CapVisitors.cs` | Planifica y pinta los Pokémon que pasan. `Lanes` (6 rutas: 3 de mar, 3 de cielo; cada una con periodo, duración, probabilidad y lista de especies), `SheetOf` (decodifica la hoja PNG incrustada, una vez), `Swimmer` (recorrido, inclinación, fotograma y pintado de una especie con hoja, nadando o volando), `Paint` (recorre las rutas), `Prowl` (Sharpedo del peligro), `Gallery` (vista del laboratorio). Banderas solo para el laboratorio: `ForcedSpecies`, `ForcedAll`. |
| `src/PermaLocke.App/Views/Cap/CapFauna.cs` | Fauna del mar. `CapVisitors.Paint` se llama **al final** (después del banco de peces, la ola de luz y las almas) para que nada pinte encima de los Pokémon. |
| `src/PermaLocke.App/Views/Cap/CapWorldView.cs` | Orden de capas de una ventana. `CapFauna.Paint` va **después** de `CapDeep.Focus` (el haz de luz del fósil). |
| `src/PermaLocke.App/PermaLocke.App.csproj` | `<EmbeddedResource Include="Views\Cap\Critters\*.png" LogicalName="PermaLocke.App.Critters.%(Filename).png" />`: cualquier PNG en esa carpeta entra en el exe con ese nombre lógico. |
| `src/PermaLocke.App/Views/Cap/Critters/*.png` | **Las hojas definitivas** (6 archivos, ver tabla de arriba). |
| `tools/PermaLocke.PlaqueLab/Program.cs` | Laboratorio del panel: `--ventana`, `--especie`, `--especies`, `--bichos` (ver §3). |
| `tests/PermaLocke.App.Tests/CapDeepTests.cs`, `CapFossilTests.cs` | Pruebas del panel (actualizadas cuando se quitaron farolillos y ataques). |

### Herramientas de sprites (Node.js) — `tools/critters/` [verificado: copiadas aquí y probadas]

Se ejecutan **desde `tools/critters`** con `node`. Escriben sus salidas intermedias en esa carpeta (ignoradas por `tools/critters/.gitignore`).

| Archivo | Para qué |
|---|---|
| `recon2.js` | Reconstruye la cuadrícula de pixel art de una referencia JPG/PNG: detecta el tamaño de celda, quita el fondo y reduce a una paleta. Salida: `<nombre>.txt` (paleta + cuadrícula de letras), `<nombre>.ppm`, `<nombre>.grid.json`. |
| `critter.js` | Biblioteca: `load`, `frames` (compone fotogramas por partes y rellena costuras), `check` (píxeles sueltos, huecos, volumen), `swing` (gira una pieza sobre su pivote, estilo RotSprite), `rotate`/`sheet` (hoja de inclinaciones × fotogramas con rasgos protegidos), `previews` (GIF y hoja de fotogramas), `pupilOf`. |
| `<especie>_anim.js` | Animación de cada especie del lote 1 (`horsea`, `goldeen`, `lapras`, `wingull`, `zubat`). Exporta `{ name, pal, fr, fps, angles, pose, protect }`. **Son las versiones aprobadas.** |
| `<especie>.txt`, `<especie>.grid.json` | Cuadrículas reconstruidas aprobadas (incluye `magikarp.txt`, idéntica a `magikarp/recon.txt`). |
| `build_batch.js` | Genera las hojas de las especies que le pases, las copia a `src/PermaLocke.App/Views/Cap/Critters/` y escribe `batch_entries.json`. Los parámetros de recorrido por especie están en su tabla `META`. |
| `integrate_entries.js` | Sustituye en `CapCritterArt.cs` la entrada de cada especie de `batch_entries.json` por su entrada con hoja. Se puede repetir sin efectos. |
| `zoom.js` | Recorte ampliado de unos fotogramas para revisar de cerca. |
| `magikarp/` | Las herramientas exactas que produjeron el Magikarp aprobado (anteriores a `critter.js`): `recon.js` (reconstrucción con cuadrícula fija 4,3 px), `recon.txt` (base aprobada 44×57), `anim.js` → `mk_anim2.js` → `anim2.js` (ciclo de 8 fotogramas), `rot.js` (hoja con pupila y bigotes protegidos), `check.js`, `refmap.js` (mapa de medición). `recon.js` y `refmap.js` leen `ref.rgb` (la referencia en RGB crudo, no versionada); se regenera desde `tools/critters/magikarp` con `"C:/Program Files (x86)/Soundux/ffmpeg.exe" -i C:/Users/javie/Desktop/imagenes/magikarp.jpg -f rawvideo -pix_fmt rgb24 ref.rgb`. `anim2.js` y `rot.js` solo necesitan `recon.txt`. |

Comprobaciones hechas al copiar **[verificado]**:
- `node build_batch.js horsea goldeen lapras wingull zubat` regenera las 5 hojas **idénticas byte a byte** a las de la app (git no ve cambios).
- En `magikarp/`: `node anim2.js` y `node rot.js` regeneran `sheet.rgba` idéntico a los píxeles de `Critters/magikarp.png`.
- `node recon2.js C:/Users/javie/Desktop/imagenes/horsea.jpg rev/horsea_check 16` reproduce `horsea.txt` exacto.
- `node integrate_entries.js` con el lote 1 no cambia `CapCritterArt.cs`.

### Referencias y revisiones

- **Referencias** (del usuario, 54, todas cuadrículas de pixel art): `C:\Users\javie\Desktop\imagenes\` (`magikarp.jpg`, `horsea.jpg`, …; Charizard y Latios en `.png`).
- **Revisiones entregadas**: `C:\Users\javie\Desktop\imagenes\revision\`
  - Definitivas del lote 1: `revision\lote1\` → `lote1_referencias_y_sprites.png`, `lote1_fotogramas.png`, `<especie>_bucle_fijo.gif`,
    `<especie>_fotogramas.png`, `<especie>_referencia_y_sprite.png`, `lote1_juntos_en_el_layout_tamano_real.gif`.
  - Definitivas de Magikarp: `magikarp_v4_*` (pose base aprobada, comparada y superpuesta con la referencia), `magikarp_ojo_*` (ciclo y
    recorrido finales con el ojo corregido: rostro en las 56 celdas, hoja completa, bucle fijo a 12°, recorrido en el layout).
  - **Pruebas antiguas, no usar como modelo**: `magikarp_antes_despues.png`, `magikarp_bucle.gif`, `magikarp_fotogramas.png`,
    `magikarp_tamanos_*` (primer dibujo inventado, rechazado), `magikarp_base_*` y `magikarp_v3_*` (redibujos interpretados, rechazados),
    `magikarp_anim_*`, `magikarp_fotograma_0..7.png`, `magikarp_en_el_layout_*` (primer ciclo, solo movía la cola), `magikarp_v2_*`
    (ciclo con cuerpo, aún con el fallo del ojo).

### Pendiente de guardar o integrar

- `tools/critters/` y este documento están en el commit que sigue a `adab31d` ("Sprite tools and continuity notes"). [verificado]
- `Config/ajustes.json` y `Config/jugador.json` son datos de ejecución del usuario: **no** se versionan.
- **La app instalada no está actualizada**: el usuario usa un acceso directo a `src/PermaLocke.App/bin/Debug/net10.0-windows/PermaLocke.App.exe`
  y lo tenía abierto, así que todo se compiló aparte (`--artifacts-path .lab-art`). Para que vea los cambios: con la app **cerrada**,
  `dotnet build src/PermaLocke.App` desde la raíz del proyecto. [pendiente: pedirle que la cierre]

---

## 3. Cómo se ha hecho (el método aprobado)

### 3.1 Preparar la referencia y construir el sprite base

Las referencias son "patrones" de pixel art con una cuadrícula gris fina sobre fondo blanco. **No se redibuja ni se interpreta**: se
reconstruye celda a celda sobre la cuadrícula de la propia referencia (el usuario rechazó explícitamente las versiones dibujadas con formas
genéricas o "interpretadas").

```
cd "C:\Users\javie\Desktop\Permalocke singleplayer\tools\critters"
node recon2.js "C:/Users/javie/Desktop/imagenes/<especie>.jpg" <especie> 16
```

Qué hace `recon2.js`:
1. Lee la imagen con ffmpeg (`C:\Program Files (x86)\Soundux\ffmpeg.exe`) a RGB crudo.
2. **Cuadrícula**: celdas cuadradas, un solo periodo para los dos ejes. Para cada periodo de 3 a 16 px (paso 0,02) busca la fase en que las
   columnas/filas son más oscuras (las líneas); elige el **menor** periodo que puntúa ≥ 80 % del mejor (un múltiplo del periodo puntúa
   igual: sin esto Magikarp salía con 8,58 en vez de 4,30).
3. Un color por celda: la **mediana** de su interior, lejos de las líneas, que quita la cuadrícula y el ruido JPEG.
4. **Fondo**: relleno desde el borde a través de celdas casi blancas. Los blancos encerrados por contorno (ojos, aletas blancas) se conservan.
5. **Paleta**: k-means de 16 colores con inicio por el punto más lejano, para que oscuros, claros y cada tono tengan su centro.
6. Recorta al personaje. Salida `<especie>.txt`: primera línea `A=RRGGBB B=...`, luego la cuadrícula (`.` = transparente).

Después, **comparar con la referencia a la misma altura** y revisar. Fondo atrapado: el relleno desde el borde no llega a huecos blancos
encerrados entre partes del cuerpo. En Horsea había uno entre la tripa y el abanico; se quita en su script
(`horsea_anim.js`: celdas `B` de filas 17–23, columnas 16–23 → `.`). Hay que mirar cada especie por si tiene uno.

Resoluciones resultantes (= las de la referencia): Magikarp 44×57, Horsea 30×35, Goldeen 64×39, Lapras 68×66, Wingull 58×41, Zubat 51×37.

### 3.2 Animación por partes

Siempre sobre la cuadrícula base; los píxeles que no se mueven se conservan. Para ver las coordenadas de las partes:

```
node -e "const fs=require('fs');const [p,...r]=fs.readFileSync('<especie>.txt','utf8').trim().split('\n');console.log(p);r.forEach((l,y)=>console.log(String(y).padStart(2),l))"
```

Un `<especie>_anim.js` define `spec` para `C.frames(rows, spec)`:
- `count`: fotogramas del ciclo (6–8). `pad`: margen del lienzo (4–9) para que lo que se mueve no se salga. **Todos los fotogramas tienen el
  mismo lienzo y la misma posición del cuerpo** (anclaje estable).
- `parts`: lista **en orden de dibujo** (lo primero queda detrás). Cada parte: `test(x, y, letra)` elige sus celdas en la base (una celda es
  de la primera parte que la acepta) y `move(f, x, y) → [dx, dy]` la desplaza en el fotograma `f`; o `sample(f, put)` para piezas que giran
  (`C.swing`).
- `seam(x, y)`: zonas de costura donde un hueco con 3 vecinos también se rellena (fuera de ellas solo los de 4). El relleno toma el color de un
  vecino que no sea contorno (`ink`).
- Fase: `t = f / N * 2π`. El reloj de cada parte se desfasa para que la onda corra **de la cabeza a la cola** y los apéndices lleguen tarde.

Técnicas usadas y en qué especie:
- **Ondulación por columnas** (`dy` según la distancia a la raíz, con desfase por columna): colas y aletas largas (Goldeen, colas de Zubat,
  abanico de Horsea). Las filas de cada columna se mueven juntas, así se conserva el grosor.
- **Desplazamiento de 1 px de grupos rígidos** (cabeza y cuerpo con un fotograma de diferencia): Magikarp, Goldeen, cabeza de Lapras.
  El corte entre grupos va por **una línea que ya existe en el dibujo** (en Magikarp, la línea del opérculo, con la corona tapando el escalón
  de arriba y las aletas el de abajo). Los huecos de la costura se rellenan.
- **Cizalla por distancia** (la punta se mueve, la raíz no): aletas laterales, aletas de Lapras.
- **Cambio de anchura** para la profundidad en vista de tres cuartos: la cola de Magikarp se estrecha al alejarse y se ensancha al acercarse
  (escala en x desde la raíz), con su extremo subiendo y bajando.
- **Giro de una pieza sobre su hombro** (`C.swing`: Scale2x ×3, giro con muestreo más cercano, reducción): alas de Wingull y Zubat. Ángulos de
  Wingull ±17°, de Zubat ±24 · 0,9 (izquierda) y ±24 (derecha). El cuerpo sube 1 px en el golpe hacia abajo.

Ritmo por especie (fps del ciclo): Lapras 6 (lento), Goldeen 7, Magikarp 8, Wingull 9, Horsea 10 (aleta rápida), Zubat 12 (aleteo rápido).

Revisar el ciclo:
```
node -e "const C=require('./critter.js');const h=require('./<especie>_anim.js');C.previews(h.name,h.pal,h.fr,h.fps,6,'rev')"
node zoom.js <especie>_anim.js 0,4 <x>,<y>,<ancho>,<alto> 8 rev/zoom.png
```
`previews` deja `rev/<especie>_bucle_fijo.gif` y `rev/<especie>_fotogramas.png`; `check` (impreso al cargar el módulo) da
`{iso, holes, pixels}`: **iso y holes deben ser 0** y `pixels` solo debe variar por lo que la animación mueve a propósito.

### 3.3 Hoja de inclinaciones (rotación tipo RotSprite)

`C.sheet` gira cada fotograma: Scale2x tres veces (×8), giro con muestreo más cercano alrededor del centro del lienzo, se toma el píxel
central de cada bloque 8×8 y se quitan los píxeles aislados. Sin suavizado ni colores nuevos. **Rasgos protegidos**: los píxeles que
`protect(g, x, y)` marca (ojo entero, pupila, dientes; coordenadas del fotograma = base + `pad`) se recolocan después en su posición girada
exacta, porque el muestreo puede perder un detalle de 1 px. La celda (`Cell`) se calcula para que quepa el fotograma girado al ángulo máximo.

`Pose`: grados que el dibujo ya apunta el morro hacia abajo (Magikarp 15, Goldeen 10, el resto 0). La app gira `Pose − rumbo` para que vaya
recto cuando avanza en horizontal. Ángulos positivos = horario = levanta el morro de un dibujo que mira a la izquierda (todos los dibujos
miran a la izquierda; la app los voltea al ir a la derecha).

### 3.4 Integración en la app

```
cd "C:\Users\javie\Desktop\Permalocke singleplayer\tools\critters"
# 1. añade la especie a META en build_batch.js: { id, drift, bob, surge }
node build_batch.js <especie1> <especie2> ...      # hojas a src/PermaLocke.App/Views/Cap/Critters/ + batch_entries.json
node integrate_entries.js                          # entradas en CapCritterArt.cs
cd ..\..
dotnet build tools/PermaLocke.PlaqueLab --artifacts-path .lab-art -v q   # compila sin tocar el bin de la app (puede estar abierta)
```

Cómo se mueve en el layout (`CapVisitors.Swimmer`):
- Cruza el mundo (las dos ventanas y el hueco del juego) en la duración de su ruta (`Lanes`: 32, 42, 70 s en el mar; 14, 20, 26 s en el cielo).
- Altura: `mid + Drift·sin(2π·τ/13 + p1) + Bob·sin(2π·τ/4,3 + p2)`, con fases de la propia pasada → curvas suaves que no se repiten igual.
  Los voladores entre el cielo alto y la superficie; los nadadores entre la superficie y el fondo (usa `Height`).
- Impulso: `x += dir·Surge·sin(latido)`; el latido es el mismo reloj que el fotograma → empuje y deslizamiento.
- Inclinación: solo de la **onda lenta** a velocidad media (`atan2(Drift·ω1·cos, velocidad)`), redondeada a la fila más cercana. El impulso y
  la onda corta no entran, porque hacían oscilar entre dos filas.
- Fotograma: `floor((τ + (Variant % 13)·0,173)·Fps) % fotogramas` → cada pasada con su desfase.
- Color: detrás del equipo, nadadores al 47 % de opacidad y azulados (para no tapar el texto); en el fondo del mar, opacos con un poco de agua
  según la profundidad; voladores opacos de día y siluetas de noche. Burbujas solo para nadadores en el fondo del mar.
- Las especies con hoja salen **solas** (sin grupos de tres) y a escala 1.

Parámetros de recorrido aprobados (`META` en `build_batch.js`; Magikarp usa los valores por defecto del record):

| Especie | Drift | Bob | Surge |
|---|---|---|---|
| Magikarp | 8 | 1,5 | 1,6 |
| Horsea | 5 | 3 | 0,5 |
| Goldeen | 8 | 1,5 | 1,4 |
| Lapras | 4 | 1 | 1,0 |
| Wingull | 10 | 1,5 | 0,8 |
| Zubat | 7 | 3,5 | 1,0 |

### 3.5 Revisar en el layout (laboratorio)

Desde la raíz del proyecto, con el laboratorio compilado en `.lab-art` (exe: `.lab-art/bin/PermaLocke.PlaqueLab/debug/PermaLocke.PlaqueLab.exe`):

```
# una ventana entera del emulador con el panel, escenario "completo" en el segundo 36, preset de 1920x976
.lab-art/bin/PermaLocke.PlaqueLab/debug/PermaLocke.PlaqueLab.exe --especies 116,118,131,278,41 --ventana completo 36 "1920" salida.png
# una sola especie en todas las rutas de su tipo, en un momento relativo del escenario "nadador" (m = su momento; m+8 = 8 s después)
.lab-art/bin/PermaLocke.PlaqueLab/debug/PermaLocke.PlaqueLab.exe --especie 129 --ventana nadador "m+8" "1920" salida.png
# rendimiento del panel (con --especies fuerza las 6 rutas a la vez)
.lab-art/bin/PermaLocke.PlaqueLab/debug/PermaLocke.PlaqueLab.exe --especies 116,118,131,278,41 --medir
```

Para un GIF del recorrido: renderiza fotogramas cada 0,125 s con `--ventana ... <t>` en un bucle y únelos con ffmpeg
(`-framerate 8 -i h%02d.png`, recorte `crop=640:976:0:0` para la ventana izquierda, `palettegen`/`paletteuse=dither=none`).
**Cuidado con ffmpeg**: superponer sobre `-f lavfi -i color=...` sin `format=rgba` convierte a YUV y emborrona la vista previa (no el
sprite); usa `format=rgb24` o parte de PPM/RGB crudo. Un `color=` sin `-frames:v 1` no termina nunca.

Rendimiento medido [verificado]: panel del equipo 1,1–1,7 ms por fotograma; con las 6 rutas a la vez y todas con hoja, 1,7–2,1 ms; la primera
lectura de las hojas ~90 ms una vez.

---

## 4. Decisiones y problemas resueltos

### Lo que falló con Magikarp y qué lo arregló
1. **Dibujarlo a mano en texto a 14 px** (versión original de los 54): demasiado simple. Rechazado.
2. **Dibujarlo a mano "de memoria" a 43×31** con formas: parecía un pez genérico (bigotes como patas, cola de flecha). Rechazado.
3. **Redibujarlo sobre medidas de la referencia** (formas propias ajustadas a puntos medidos, v2 y v3): "modifica demasiado el diseño".
   Rechazado: el usuario pidió **usar la referencia como plantilla espacial** y no reinterpretar.
4. **Reconstrucción celda a celda** de la cuadrícula de la referencia (v4): **aprobada**. Es el método obligatorio.
5. Primera animación: solo movía la cola. Se añadió la onda cabeza → cuerpo → cola con 1 px por grupo y la costura por el opérculo.
6. Recorrido recto horizontal con el sprite mirando abajo: se añadieron las curvas, la inclinación por filas giradas y el impulso por latido.
7. La inclinación saltaba 60–76 veces por pasada entre dos filas: se quitó el impulso y la onda corta del cálculo del rumbo (simulado con
   varias pasadas: 0 idas y vueltas en menos de 1 s).

### El ojo que desaparecía (Magikarp) y cómo evitarlo
Dos causas, ninguna era la reconstrucción ni las máscaras (sin girar, el ojo estaba en los 8 fotogramas):
1. **La rotación perdía la pupila**: es 1 píxel oscuro dentro del blanco del ojo; tras Scale2x es un bloque 8×8 y, girado, ningún punto de
   muestreo cayó dentro a 4° (los 8 fotogramas), 16° (fotogramas 5–6) y 20° (fotogramas 1–2). Arreglo: los rasgos pequeños se recolocan
   tras el giro en su posición girada exacta (`protect`). También las líneas de 1 px (bigotes y mejilla de Magikarp: letra `J` en filas ≥ 30).
2. **Capas por encima**: el banco de pececillos, las almas de los fósiles y el haz de luz se pintaban después de los Pokémon y aclaraban la
   pupila. Arreglo: `CapVisitors.Paint` al final de `CapFauna.Paint` y `CapFauna.Paint` después de `CapDeep.Focus`.

Para cada especie nueva: define `protect` con la caja del ojo (y dientes u otros detalles de 1–2 px) **en coordenadas del fotograma** (base +
`pad`), y después de generar la hoja **cuenta** que la pupila esté en todas las celdas (ver §5, comprobaciones).

### Criterios ya aprobados por el usuario
- Fidelidad máxima a la referencia: silueta, proporciones, rostro, paleta y contornos de la propia referencia; nada genérico ni simplificado.
- Pixel art nítido, escalado entero, sin suavizado; fondo transparente; paleta controlada (16 colores por especie).
- Animación del cuerpo según la anatomía, no traslación de un sprite fijo; apéndices con retraso; sin efecto goma (desplazamientos de 1 px).
- Los rasgos (ojos, boca, aletas, bigotes) presentes en todos los fotogramas y en todas las inclinaciones; sin huecos ni parpadeos; bucle sin salto.
- Orientación coherente con la trayectoria; recorridos curvos y tranquilos; ritmo y amplitud propios de cada especie; sin sincronía.
- El layout debe seguir legible (nadadores tenues detrás del texto del equipo).
- Trabajo **por lotes de 5**, cada uno con: sprites junto a sus referencias, bucles fijos, vista conjunta a tamaño real en el layout y
  limitaciones. **Parar después de cada lote** para que el usuario lo revise.

---

## 5. Continuación

### Qué debe hacer la próxima sesión
1. Leer este documento, `CLAUDE.md` del proyecto, `tools/critters/critter.js`, un script aprobado de cada tipo (`goldeen_anim.js` para
   peces, `lapras_anim.js` para aletas, `wingull_anim.js` y `zubat_anim.js` para alas, `horsea_anim.js` para un erguido) y
   `src/PermaLocke.App/Views/Cap/CapVisitors.cs` (`Swimmer`).
2. Comprobar con `git status` en la raíz que no hay cambios a medias (solo `Config/*.json`, que no se versionan).
3. Empezar el **lote 2**: 5 especies de las pendientes, con anatomías distintas, **una a una**: sprite base (§3.1) → revisar con la referencia →
   animación (§3.2) → revisar ampliada → hoja (§3.3) → integración (§3.4) → vista en el layout (§3.5).
4. Entregar el lote (§1 de los criterios) y **parar**.

### Lote 2 (2026-10-14): entregado, pendiente de la revisión del usuario
Gyarados (130), Mantine (226), Chinchou (170), Pidgey (16) y Butterfree (12), confirmados por el usuario. Integrados en la app (hojas en
`Critters/`, entradas en `CapCritterArt.cs`, `META` en `build_batch.js`); revisión en `C:\Users\javie\Desktop\imagenes\revision\lote2\`.
**No dar por aprobados** hasta que el usuario lo diga. Lo nuevo respecto al método:
- `recon2.js` acepta un 4.º argumento opcional, el periodo de la cuadrícula: la referencia de Gyarados mide 230 px y sus celdas 2,77 px, por
  debajo del mínimo de la búsqueda (3), que cogía la cuadrícula gruesa (13,8). `node recon2.js .../gyarados.jpg gyarados 16 2.77`. Sin el
  argumento todo sale igual que antes (comprobado con Horsea, idéntico byte a byte).
- `gyarados_base.js`: las aletas blancas de Gyarados solo tienen un contorno gris fino y roto, y el relleno del fondo se comía su interior; en
  las cajas de esas aletas, cada hueco cerrado por los cuatro lados a ≤ 7 celdas vuelve a ser blanco (`B`). 46 celdas.
- Chinchou tenía fondo atrapado dentro del bucle de la antena derecha: su script lo quita (`B` con x ≥ 36 en filas 0–27).
- **Doblar en vez de girar** (Mantine): un ala girada con `C.swing` abría muescas en la unión con el cuerpo. El ala de arriba se dobla (la raíz
  quieta y la punta desplazada en perpendicular al ala) y se dibuja **hacia atrás** (cada celda pregunta de dónde viene), sin grietas; el ala
  de abajo, casi horizontal, se dobla columna a columna.
- **Lo que hay debajo de un ala que se levanta** (Pidgey): la referencia está posada con el ala plegada; al subirla quedaba un agujero. Se
  rellena fila a fila con el color del cuerpo más cercano a su izquierda (pasado el contorno).
- **Alas vistas de frente** (Butterfree): cada ala se estrecha hacia su raíz (`swing` con escala) y las alas de delante y de detrás se separan
  por su borde real (fila 36 a la izquierda, 35 a la derecha), si no el borde de una asomaba por debajo de la otra.
- Herramientas nuevas: `ppm.js` (una base o el primer fotograma de una animación a PPM, para comparar) y `rostro.js` (el mismo recorte, la
  cara, de todas las celdas de una hoja, en mosaico: así se comprueba el ojo en todas las inclinaciones y fotogramas).
- Comprobado: `iso 0, holes 0` en las cinco; ojos en las 40 celdas (30 en Pidgey) con `rostro.js`; 551 pruebas en verde; `--medir` con las
  cinco a la vez: panel del equipo en columna 1,7–2,0 ms de mediana.

### Siguiente lote
No hay un orden acordado. El criterio del usuario para el lote 1 fue "anatomías y movimientos diferentes". **Propuesta** (a confirmar o
ajustar con el usuario): Gyarados (serpiente, ondulación de todo el cuerpo), Mantine (aletas-ala bajo el agua), Chinchou (redondo con
antenas de luz), Pidgey (ave pequeña de aleteo rápido) y Butterfree (mariposa). Kyogre, Rayquaza y Lugia son los más grandes y complejos:
mejor cuando el método de cada anatomía esté probado.

### Comprobaciones antes de entregar cada lote
- `recon2.js`: la reconstrucción comparada con la referencia a la misma altura; sin fondo atrapado (blanco que no es del personaje).
- `check`: `iso: 0, holes: 0` en todos los fotogramas; `pixels` estable salvo lo que se mueve a propósito.
- Ver los fotogramas ampliados (`zoom.js`), sobre todo costuras y extremos del movimiento.
- Tras `build_batch.js`, contar en la hoja que el ojo/pupila (y los rasgos protegidos) esté en **todas** las celdas. Ejemplo de conteo de
  pupila (oscuro con blanco en 3 de 4 lados) sobre `<especie>_sheet.rgba` (ancho = celda × fotogramas):
  ```
  node -e "const fs=require('fs');const W=<ancho>,cell=<celda>,b=fs.readFileSync('<especie>_sheet.rgba');const px=(x,y)=>{const i=(y*W+x)*4;return [b[i],b[i+1],b[i+2],b[i+3]]};const wh=(x,y)=>{const[r,g,bb,a]=px(x,y);return a&&r>230&&g>230&&bb>230};const dk=(x,y)=>{const[r,g,bb,a]=px(x,y);return a&&r<60&&g<60&&bb<60};const rows=b.length/4/W/cell;for(let r=0;r<rows;r++){let l='';for(let f=0;f<W/cell;f++){let n=0;for(let y=r*cell;y<(r+1)*cell;y++)for(let x=f*cell;x<(f+1)*cell;x++)if(dk(x,y)&&[wh(x+1,y),wh(x-1,y),wh(x,y+1),wh(x,y-1)].filter(Boolean).length>=3)n++;l+=n+' '}console.log('fila',r,l)}"
  ```
  (los umbrales de color dependen de la paleta de la especie: ajústalos al blanco y al oscuro de su ojo).
- En el layout: renderizar el recorrido completo y comprobar fotograma a fotograma que el rostro se ve (no tapado por otras capas).
- `dotnet test tests/PermaLocke.App.Tests --artifacts-path .lab-art` (551 pruebas en verde al escribir esto [verificado]).
- `--medir` con `--especies` para el peor caso.
- Commit local (sin push) y actualizar `registro_sprites.md`.

### Avisos
- No vuelvas a ejecutar `recon2.js` sobre una especie aprobada salvo para comprobar (da el mismo resultado, pero no hace falta).
- `build_batch.js` sobrescribe las hojas de la app de las especies que le pases.
- `recon2.js` imprime en la consola la información de entrada de ffmpeg antes de su resultado; es normal.
- Requisitos [verificado]: Node v24.21.0, ffmpeg en `C:\Program Files (x86)\Soundux\ffmpeg.exe`, SDK .NET 10.
