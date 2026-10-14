# Continuidad: sprites de los Pokémon que pasan por el mar

Documento de traspaso para continuar en una sesión nueva sin acceso a la conversación anterior. Escrito el 2026-10-14.
Todo lo marcado **[verificado]** se comprobó al escribir este documento; lo marcado **[pendiente]** no.

Proyecto: `C:\Users\javie\Desktop\Permalocke singleplayer` (rama `singleplayer`, git local, **sin push**). Es la versión de un jugador de
PermaLocke (C# / .NET 10 / WPF). Lee también su `CLAUDE.md` (reglas del repositorio: código y comentarios en inglés, UI y documentos en
español, nunca desplegar a `PermaLocke prueba` ni a `PermaLocke para amigos`, no tocar la app ni Azahar mientras estén abiertos).

---

## 1. Objetivo y estado actual

**Actualización del lote 11 (2026-10-14): las 54 especies del catálogo ya tienen hoja animada integrada.**
Magikarp y lotes 1–11 aprobados por el usuario. Los lotes 10 y 11 recibieron aprobación conjunta («ok a todo») el 2026-10-14. El usuario pidió
«acaba esos 3» y autorizó terminar Wailord, Rayquaza y Ho-Oh. Las tablas iniciales de esta sección son históricas;
el estado actualizado está en las secciones de cada lote y en el registro externo.

**Revisión posterior:** Pidgey corregido y tanda 1 aprobados e intactos. Tanda 2 RECHAZADA por el usuario. Las cuatro bases rehechas también RECHAZADAS. Charizard con cuello corregido: base definitiva APROBADA y primer batido rechazado; segunda revisión rechazada por el ala lejana; tercera aceptada como avance; cuarta (lectura de las dos alas): movimiento APROBADO; quinta (color de cada cara del ala) APROBADA: Charizard terminado. Skarmory: vuelo APROBADO (skarmory_vuelo, c95d23c). Dragonite: vuelo APROBADO (dragonite_vuelo, 253ccf1). Siguiente: Salamence. Skarmory, Dragonite y Salamence pendientes, uno por uno desde referencias de vuelo.
Ver «Correcciones de vuelo, tanda 2» y tools/critters/REVISION_POSES_VUELO.md. Salamence planea con las alas fijas.

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

### Lote 2 (2026-10-14): aprobado por el usuario
Gyarados (130), Mantine (226), Chinchou (170), Pidgey (16) y Butterfree (12), confirmados por el usuario. Integrados en la app (hojas en
`Critters/`, entradas en `CapCritterArt.cs`, `META` en `build_batch.js`); revisión en `C:\Users\javie\Desktop\imagenes\revision\lote2\`.
Lo nuevo respecto al método:
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

### Lote 3 (2026-10-14): aprobado por el usuario
Sharpedo (319), Wailmer (320), Qwilfish (211), Spearow (21) y Swablu (333). Revisión en `imagenes\revision\lote3\`. Lo nuevo:
- Sharpedo: referencia de celdas de 3,3 px (`recon2.js ... sharpedo 16 3.3`). La cuadrícula de la referencia se colaba como un damero entre los
  dos azules del cuerpo (`K`, `L`): su script pone a cada celda K/L el color mayoritario de su vecindad 3×3. Una celda blanca suelta (ruido
  JPEG, fila 29) se quita.
- **El tiburón del peligro** (`CapVisitors.Prowl`) usa ahora la hoja de Sharpedo (`ProwlSheet`: fila sin inclinación, su ciclo, oscuro y con la
  opacidad del peligro); sin hoja sigue el dibujo antiguo. Prueba: `CapProwlTests` (con `PERMALOCKE_SNAP_DIR` deja el fotograma en BGRA).
  El laboratorio no tiene escenario de peligro (pide `ActiveShare` ≤ 0,30 en combate), por eso se comprobó con la prueba.
- Spearow: la referencia tiene el ala levantada; se dobla hacia abajo columna a columna desde el hombro (cada columna entera, sin agujero
  debajo), en vez de girarla.
- Qwilfish: la cola-paleta se dobla desde su raíz, dibujada hacia atrás como el ala de Mantine.
- Wailmer: aleta grande con onda columna a columna y aleta pequeña con cizalla; solo 3 inclinaciones (-4, 0, 4), es lento y casi plano.
- Swablu: las dos nubes giran sobre sus hombros (`C.swing`) como las alas de Wingull.
- Comprobado: `iso 0, holes 0`; ojos en todas las celdas con `rostro.js`; 553 pruebas en verde; `--medir` con los cinco: 1,7–1,9 ms de mediana.

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

### Lote 4 (2026-10-14): aprobado por el usuario
Finneon (456), Milotic (350), Manaphy (490), Pelipper (279) y Crobat (169), elegidos y confirmados por el usuario antes de empezar.
Fuentes e integración en la rama `singleplayer`; entrega en `C:\Users\javie\Desktop\imagenes\revision\lote4\`. Aprobados por el usuario el 2026-10-14 («aprobado, continua»).

| Especie | Base reconstruida | Celda de hoja | Fotogramas / inclinaciones | FPS | Movimiento |
|---|---|---|---|---|---|
| Finneon | 38×33 | 62×62 | 8 / 7 | 9 | Cola bilobulada y aletas con retraso |
| Milotic | 84×76 | 106×106 | 8 / 5 | 7 | Ondulación de la serpiente enroscada, abanico y antenas |
| Manaphy | 54×38 | 72×72 | 8 / 5 | 8 | Brazos alternos y antena que sigue al cuerpo |
| Pelipper | 65×61 | 88×88 | 8 / 5 | 7 | Aleteo lento de las alas colgantes, pico estable |
| Crobat | 88×48 | 112×112 | 8 / 5 | 11 | Cuatro alas; el par inferior sigue al superior |

Lo nuevo del método:
- Crobat: referencia de 246×137; el detector confundía el periodo real con su múltiplo 13,60. Se reconstruye con
  `node recon2.js C:/Users/javie/Desktop/imagenes/Crobat.jpg crobat 16 2.72`. Su JPEG limita el detalle: no se inventa lo que no se ve.
  Se quitan las celdas claras de fondo/compresión fuera de la boca; se conservan sus dientes.
- Milotic: se elimina el fondo blanco atrapado entre la espira y el apéndice, además de una celda junto al abanico. La máscara de la antena
  derecha necesita un límite en X (53): sin él capturaba el extremo del abanico y abría una grieta grande que `check` no detectaba.
  **`iso 0, holes 0` no sustituye la revisión de las costuras ampliadas**.
- Pelipper: el ala cercana se separa siguiendo su borde blanco fila a fila y se dobla por muestreo inverso con desplazamiento cero en la raíz.
  Crobat: las cuatro alas se doblan por columnas completas, con las raíces fijas y fases distintas.
- `audit_batch4.js` lee las hojas RGBA reales, comprueba dimensiones, píxeles aislados, huecos de una celda, bordes y colores del ojo en sus
  regiones giradas. Hay **216 celdas y 256 regiones de ojos** (dos por celda en Manaphy). Todas pasan. No es una prueba de similitud perceptual:
  se complementa con `rostro.js` y los mosaicos ampliados.
- La rotación introducía 7 huecos de una celda aun con fotogramas base correctos: 2 en Finneon y 5 en Milotic. `seal_sheet.js`, activado solo
  mediante `sealRotatedHoles` en esas dos especies, cierra las celdas transparentes rodeadas por cuatro vecinos usando el color vecino mayoritario.
  No se modifica `critter.js`, ni las animaciones ni las hojas aprobadas. Resultado: **iso 0, holes 0 en los 40 fotogramas y las 216 celdas**.
- Crobat reserva `layoutHeight: 61` en su entrada: su altura de referencia (48) no bastaba para que las puntas elevadas quedasen dentro del cielo.
  `build_batch.js` permite esta reserva opcional; las demás especies mantienen su cálculo anterior.
- El laboratorio admite `--secuencia-ventana escenario inicio segundos fps preset carpeta`, para exportar una secuencia sin reiniciar por
  fotograma. `render_batch4.ps1` documenta la exportación de 70 segundos (840 PNG a 12 fps), el GIF a 1920×976 sin reescalar y la medición.
  La paleta del GIF se genera en una pasada aparte para evitar acumular todos los fotogramas en RAM.
- `review_batch4.js deliver` genera comparaciones a la misma altura, bucles fijos y mosaicos. La entrega añade rostros e informe JSON.
- Bucles individuales periódicos de 8 fotogramas; el cambio del último al primero está dentro del rango de cambios del ciclo (tolerancia del 5 %).
  El GIF del recorrido completo vuelve al inicio tras 70 segundos: ese reinicio de la escena no representa el bucle individual del Pokémon.
- Integración mediante `integrate_entries.js`; solo se sustituyen las cinco entradas nuevas. Las 16 hojas anteriores, sus 15 scripts anatómicos
  y `critter.js` mantienen sus 32 SHA-256 originales. No se han tocado las especies grandes reservadas.

Limitaciones: revisión en el laboratorio, sin abrir el juego ni desplegar. El agua aplica el tinte y la transparencia existentes detrás del
texto del equipo; en ventanas que no tienen espacio la fauna sigue las reglas actuales del layout. Los FPS de un GIF se aproximan a centésimas
por el propio formato. El siguiente lote requiere aprobación explícita; Kyogre, Lugia, Rayquaza, Wailord y Ho-Oh quedan pendientes.

Comprobación final del lote 4 [verificado]: `dotnet build tools/PermaLocke.PlaqueLab --artifacts-path .lab-art`, 0 errores (dos avisos CS0649
previos en `EmulatorLauncher`). `dotnet test tests/PermaLocke.App.Tests --artifacts-path .lab-art`: **553/553**, 0 fallos y 0 omitidas.
`--especies 456,350,490,279,169 --medir`: columna del equipo 178×321 **1,90–2,30 ms de media por fotograma**, p95 **2,55–3,53 ms**;
columna del fondo **1,18–1,42 ms**. Primera preparación 90,05 ms. Son medidas de este PC y del laboratorio, sin el coste del emulador.
Las cinco hojas PNG suman 101.804 bytes. GIF final decodificado completo: **840 fotogramas, 70,00 s, 1920×976**, sin errores.
Se han revisado las hojas de rostros completas y ambos paneles del recorrido mediante mosaicos; el GIF mantiene las oclusiones normales del
juego y del equipo. `--especies` fuerza seis rutas simultáneas y, con dos voladores, repite Crobat: no es la frecuencia normal de la app.

### Lote 5 (2026-10-14): aprobado por el usuario
Luvdisc (370), Seadra (117), Lumineon (457), Pikipek (731) y Skarmory (227), propuestos y confirmados por el usuario antes de empezar.
Integración en `singleplayer`; entrega en `C:\Users\javie\Desktop\imagenes\revision\lote5\`. Aprobados por el usuario el 2026-10-14.

| Especie | Base | Celda de hoja | Fotogramas / inclinaciones | FPS | Movimiento |
|---|---|---|---|---|---|
| Luvdisc | 22×34 | 50×50 | 8 / 5 | 7 | Flexión leve de los dos lóbulos del cuerpo plano |
| Seadra | 50×47 | 68×68 | 8 / 5 | 10 | Aleta dorsal rápida y cola enroscada con retraso |
| Lumineon | 57×57 | 80×80 | 8 / 5 | 8 | Membranas dorsales, cola y aletas ventrales |
| Pikipek | 30×42 | 60×60 | 8 / 5 | 9 | Cabeza, ala plegada y patas en la pose posada |
| Skarmory | 66×68 | 88×88 | 8 / 3 | 8 | Alas metálicas con recorridos distintos, cola y patas |

Método y comprobaciones:
- Reconstrucción celda a celda con `recon2.js`, comparación a la misma altura y animación separando las piezas por sus bordes existentes.
  Luvdisc no tiene aletas visibles en la referencia: se flexionan sus lóbulos sin inventarlas.
- Seadra: las celdas blancas detrás de la cabeza (x >= 24) son fondo atrapado; se conservan las del ojo. La cola se dobla por filas enteras,
  con desplazamiento cero en la unión. La aleta dorsal mueve columnas enteras desde su raíz.
- Lumineon: la cola pequeña tiene prioridad sobre la máscara de la membrana grande para no absorberla. Las dos membranas tienen distinta
  fase y dirección de movimiento; las aletas ventrales siguen al cuerpo.
- Pikipek: movimiento pequeño de cabeza y ala plegada, conservando la pose posada. La amplitud del ala aumenta desde la raíz, evitando
  descubrir un hueco bajo ella. Cuello y patas se revisaron ampliados.
- Skarmory: referencia pequeña de 221×228, cuadrícula automática incorrecta de 12,94. Periodo explícito **3,24** con el cuarto argumento.
  Se retira fondo blanco entre los dedos delanteros y una celda clara aislada de ruido JPEG en (60,31). Las dos alas se separan antes de
  moverlas, con las raíces fijas. `layoutHeight: 61`, inclinaciones -2/0/2 y sin desplazamiento vertical adicional del cuerpo: la referencia
  es alta y un aleteo vertical grande no cabe en el cielo existente. La pose, escala y anatomía de referencia se conservan.
- `audit_batch5.js`: **40 fotogramas base y 184 celdas inclinadas**, `iso 0, holes 0`, ninguna toca los bordes de su celda.
  **184/184 regiones de ojos** conservan pupila y colores del ojo; mosaicos `rostro.js` completos revisados.
- Rotación: 3 huecos aislados en Luvdisc, 3 en Lumineon y 1 en Skarmory; corregidos con `sealRotatedHoles` y el `seal_sheet.js` aprobado,
  sin modificar la herramienta compartida. Las máscaras y costuras se revisaron con `zoom.js`; el conteo no detecta grietas grandes.
- Los ciclos son periódicos de ocho fotogramas. El cambio del último al primero no supera el mayor cambio entre fotogramas consecutivos.
- `dotnet build tools/PermaLocke.PlaqueLab --artifacts-path .lab-art`: 0 errores, dos avisos previos CS0649 de `EmulatorLauncher`.
  `dotnet test tests/PermaLocke.App.Tests --artifacts-path .lab-art`: **553/553**, 0 fallos, 0 omitidas.
- Las 21 hojas anteriores, sus 20 scripts anatómicos y `critter.js` mantienen sus **42 hashes SHA-256**. No se toca ningún sprite aprobado.
- `review_batch5.js deliver`, `audit_batch5.js` y `render_batch5.ps1` reproducen entrega, auditoría y recorrido usando el laboratorio aislado.

Limitaciones: Pikipek mantiene su pose posada mientras recorre el cielo, como la referencia y el método de aves posadas; no se inventa una
pose de vuelo distinta. Skarmory tiene amplitud e inclinación contenidas por su altura y por la resolución de la referencia. Revisión en el
laboratorio, sin abrir juego ni desplegar. Los nadadores mantienen el tinte del agua y se atenúan detrás del texto del equipo. El GIF usa
256 colores y tiempos en centésimas; la hoja PNG de la app conserva su paleta original. El reinicio del recorrido largo no es el bucle
individual de cada Pokémon. Kyogre, Lugia, Rayquaza, Wailord y Ho-Oh siguen reservados. Lote aprobado. Confirmar las cinco especies del lote 6 antes de comenzar.

Rendimiento final [verificado] con `--especies 370,117,457,731,227 --medir`: columna del equipo 178×321 **1,73–2,03 ms de media** por
fotograma, p95 **2,23–3,31 ms**; fondo **0,95–1,48 ms**. Primera preparación 90,77 ms. Medición del laboratorio en este PC, sin emulador.
Las cinco hojas suman **60.951 bytes**. Se exportaron **840 fotogramas, 70,00 s a 12 fps, 1920×976**; GIF decodificado completo sin errores.
Mosaicos de ambos paneles revisados; `--especies` fuerza seis rutas y repite Skarmory en dos, sin cambiar la frecuencia normal de la app.

### Lote 6 (2026-10-14): aprobado por el usuario
Remoraid (223), Lanturn (171), Kingdra (230), Toucannon (733) y Altaria (334), propuestos y confirmados por el usuario antes de empezar.
Integración en `singleplayer`; entrega en `C:\Users\javie\Desktop\imagenes\revision\lote6\`. Aprobados por el usuario el 2026-10-14.

| Especie | Base | Celda de hoja | Fotogramas / inclinaciones | FPS | Movimiento |
|---|---|---|---|---|---|
| Remoraid | 36×36 | 54×54 | 8 / 5 | 9 | Cola, aleta superior y ventral con fases distintas |
| Lanturn | 65×46 | 86×86 | 8 / 5 | 7 | Dos antenas, cola y aletas pectorales |
| Kingdra | 54×66 | 88×88 | 8 / 5 | 9 | Membrana dorsal, cola enroscada y puntas de la corona |
| Toucannon | 61×59 | 80×80 | 8 / 3 | 8 | Cabeza, ala plegada, cola y patas en pose posada |
| Altaria | 58×63 | 80×80 | 8 / 3 | 7 | Nube doblada por columnas, plumas largas, cola y patas |

Método y comprobaciones:
- Reconstrucción celda a celda, sin redibujar. Remoraid: periodo 9,98; Kingdra: 8,94; Altaria: 3,60.
  Lanturn requiere **3,66** explícito: el detector elegía 7,32 y perdía detalles. Toucannon requiere **3,61**: con 7,22 se perdían contornos
  y el relleno de fondo atravesaba el pecho blanco. Se comprobó cada reconstrucción contra su referencia a la misma altura.
- Kingdra: se quita el blanco encerrado en la corona y el fondo blanco/gris entre tronco y aleta, conservando la membrana clara de la aleta.
  La membrana se dobla por columnas completas desde la raíz y la cola enroscada por filas. No se sustituye la membrana por transparencia.
- Toucannon: blanco entre las piernas retirado. La máscara de la cola empieza en x=47: incluir x=46 absorbía un dedo y lo dejaba aislado
  cuando las patas se desplazaban. Separar por el borde real corrigió el problema sin borrar el dedo.
- Altaria: retirada únicamente la celda blanca de fondo entre las plumas de la cola (45,57). El blanco de la nube y las mejillas permanece.
  La nube se dobla por columnas, sin mover su unión al cuello; plumas largas y cola llevan desfases propios.
- Toucannon y Altaria usan `layoutHeight: 61` y no añaden balanceo vertical a los fotogramas. Inclinaciones -3/0/3 y -2/0/2, respectivamente.
  En el cielo del laboratorio (superficie y=69), todas sus celdas quedan verticalmente entre y=4..64 y y=1..67, sin recorte vertical.
- `audit_batch6.js`: **40 fotogramas base, 168 celdas inclinadas, iso 0, holes 0, cero celdas tocando el borde**.
  **192/192 regiones de ojos** pasan (Altaria tiene dos); revisión visual adicional de los cinco mosaicos `rostro.js` y las costuras con `zoom.js`.
- La rotación creó 7 huecos aislados en Lanturn y 2 en Kingdra. Se cierran con el `seal_sheet.js` aprobado, mediante `sealRotatedHoles`
  solo en estas dos especies. No se modifica la biblioteca compartida. El cambio del último al primer fotograma no supera el mayor cambio
  del resto del ciclo; la auditoría lo comprueba explícitamente.
- Compilación aislada del laboratorio: **0 errores**, dos avisos CS0649 previos de `EmulatorLauncher`.
  `dotnet test tests/PermaLocke.App.Tests --artifacts-path .lab-art`: **553/553**, cero fallos y cero omitidas, 25 s.
- Las 26 hojas aprobadas, sus 25 scripts anatómicos y `critter.js` conservan sus **52 SHA-256** originales.
  Las cinco hojas nuevas suman **60.536 bytes**. Solo se reemplazan las cinco entradas correspondientes en `CapCritterArt.cs`.
- Herramientas de reproducción: `review_batch6.js deliver`, `audit_batch6.js`, `render_batch6.ps1` desde `tools/critters`.

Limitaciones: Toucannon mantiene la pose posada de la referencia. Los dos voladores tienen movimientos pequeños para respetar el cielo;
no se cambia su escala ni se inventa una pose. Las referencias pequeñas limitan el detalle de origen. Verificación en el laboratorio,
sin abrir el juego ni desplegar. Los nadadores mantienen el tinte del agua y la atenuación detrás del equipo. El GIF usa 256 colores y tiempos
en centésimas; la app conserva los PNG originales. El reinicio del recorrido largo no representa el bucle individual de cada especie.
`--especies` fuerza seis rutas simultáneas y repite Altaria; no es la frecuencia normal de la app. Los cinco grandes siguen reservados.
**Lote 6 aprobado. Confirmar las cinco especies del lote 7 antes de comenzar.**

Rendimiento final [verificado] con `--especies 223,171,230,733,334 --medir`: columna del equipo 178×321 **1,80–1,99 ms de media** por
fotograma, p95 **2,41–3,74 ms**; fondo **1,00–1,35 ms**. Primera preparación 85,42 ms. Son medidas de este PC en el laboratorio, sin emulador.
GIF final decodificado sin errores: **840 fotogramas, 70,00 s, 1920×976**, exportado a 12 fps. Revisados los mosaicos de ambos paneles;
se conservan las oclusiones habituales al pasar detrás del juego, del equipo o de otro Pokémon en esta vista forzada.

### Lote 7 (2026-10-14): aprobado por el usuario
Wishiwashi (746), Relicanth (369), Pidgeot (18), Togekiss (468) y Aerodactyl (142), propuestos y confirmados por el usuario antes de empezar.
Integrados en `singleplayer`; entrega en `C:\Users\javie\Desktop\imagenes\revision\lote7\`. Aprobados por el usuario el 2026-10-14.

| Especie | Base | Celda de hoja | Fotogramas / inclinaciones | FPS | Movimiento |
|---|---|---|---|---|---|
| Wishiwashi | 43×25 | 60×60 | 8 / 5 | 10 | Cola rápida, dorsal y dos aletas ventrales desfasadas |
| Relicanth | 51×49 | 92×92 | 8 / 5 | 6 | Cuerpo firme, dorsal, cola y tres aletas lobuladas |
| Pidgeot | 55×68 | 84×84 | 8 / 3 | 8 | Cabeza, ala plegada, cresta larga, cola y dedos |
| Togekiss | 77×43 | 100×100 | 8 / 5 | 8 | Alas anchas dobladas por columnas, cola y pies |
| Aerodactyl | 79×59 | 100×100 | 8 / 3 | 9 | Dos membranas, cola curvada y patas con retraso |

Método y comprobaciones:
- Reconstrucción celda a celda, sin redibujar: Wishiwashi periodo 15,90; Relicanth 9,98; Aerodactyl 7,10.
  Pidgeot requiere **3,55** explícito (automático 7,10); Togekiss, **3,08** (automático 15,44). Con la cuadrícula gruesa se perdían rasgos
  y el relleno de fondo atravesaba el contorno claro de Togekiss. Comparaciones a la misma altura revisadas antes de animar.
- Pidgeot: retirar blanco de fondo detrás de la cabeza y entre puntas de la cresta (x >= 20, y < 60); conservar ojo y uñas claras.
  La máscara de la cresta sigue el primer rojo de cada fila para separar el apéndice largo del ala plegada y del cuerpo.
- Togekiss: siete celdas claras de ruido de cuadrícula/JPEG retiradas fuera de la cresta roja y sobre el ala derecha, con coordenadas
  explícitas en el script. Se conservan las zonas blancas del cuerpo y su patrón de sombreado. Las alas se doblan por columnas desde
  raíces fijas, con distinta amplitud; la máscara de la cola no invade el ala.
- Aerodactyl: se separa primero la cola que pasa por encima de la cabeza para que su punta no pertenezca al ala izquierda. Ambas
  membranas bajan por columnas completas, con movimiento cero en el hombro; las patas siguen el ciclo con fases distintas.
- Relicanth: la referencia apunta hacia abajo; `Pose: 28`, inclinaciones 16/22/28/34/40, para avanzar aproximadamente horizontal en el
  layout. Se conserva el ojo cerrado de la referencia, sin inventar pupila. La auditoría verifica sus colores en esa región y el mosaico
  permite comprobar su forma. Boca y marca roja lateral protegidas al girar.
- Pidgeot: `layoutHeight: 61`, sin traslación vertical del cuerpo e inclinaciones -1/0/1 por su altura. Aerodactyl usa altura 61 y -3/0/3;
  Togekiss reserva altura 56 y -6/-3/0/3/6. La nueva auditoría de cielo reproduce `Math.Round` de C# (empates al par), no `floor`:
  los límites verticales de todas las celdas son **Pidgeot y=0..67, Togekiss y=8..59, Aerodactyl y=3..63**, con superficie en y=69.
  Pidgeot cabe con poco margen; no se reduce la escala ni se modifica el layout para esconder el problema.
- `audit_batch7.js`: **40 fotogramas base, 168 celdas inclinadas, iso 0, holes 0, cero celdas tocando sus bordes**.
  **168/168 regiones de ojos** comprobadas; mosaicos `rostro.js` y costuras ampliadas con `zoom.js` revisados visualmente.
  La auditoría también comprueba la transición del último al primer fotograma, dentro del mayor cambio del resto del ciclo.
- Rotación: 17 huecos aislados de una celda en Relicanth y 4 en Pidgeot, cerrados con `sealRotatedHoles` y el `seal_sheet.js` aprobado.
  Ninguna modificación de `critter.js`, de la herramienta de sellado ni de las especies aprobadas.
- Laboratorio compilado con `--artifacts-path .lab-art`: **0 errores**, dos avisos CS0649 previos de `EmulatorLauncher`.
  `dotnet test tests/PermaLocke.App.Tests --artifacts-path .lab-art`: **553/553**, cero fallos y cero omitidas, 25 s.
- Las 31 hojas anteriores, sus 30 scripts anatómicos y `critter.js` conservan sus **62 hashes SHA-256** originales.
  Las cinco hojas nuevas suman **61.801 bytes**. Solo se reemplazan sus cinco entradas en `CapCritterArt.cs`.
- Reproducción desde `tools/critters`: `review_batch7.js deliver`, `audit_batch7.js`, `render_batch7.ps1`.

Limitaciones: Pidgeot conserva la pose posada y movimientos contenidos por su altura. Aerodactyl también tiene una amplitud moderada
para caber en el cielo. La calidad de las referencias pequeñas limita los detalles de Pidgeot y Togekiss. Verificado en el laboratorio,
sin abrir juego ni desplegar. Los nadadores mantienen el tinte del agua y la atenuación detrás del equipo. GIF con 256 colores y tiempos
en centésimas; la app conserva los PNG originales. El reinicio del recorrido largo es distinto del bucle individual de cada especie.
`--especies` fuerza seis rutas simultáneas y repite Wishiwashi: no es la frecuencia normal de la app. Los cinco grandes siguen reservados.
**Lote 7 aprobado. Confirmar las cinco especies del lote 8 antes de comenzar.**

Rendimiento final [verificado] con `--especies 746,369,18,468,142 --medir`: columna del equipo 178×321 **1,68–2,02 ms de media** por
fotograma, p95 **2,13–3,66 ms**; fondo **1,02–1,21 ms**. Primera preparación 88,45 ms. Medidas de este PC en el laboratorio, sin emulador.
GIF decodificado completo sin errores: **840 fotogramas, 70,00 s, 1920×976**, exportado a 12 fps. Mosaicos de ambos paneles revisados;
se mantienen las oclusiones habituales detrás del juego y del equipo, y los solapamientos entre Pokémon de la vista forzada.

### Lote 8 (2026-10-14): aprobado por el usuario
Carvanha (318), Seaking (119), Fearow (22), Dragonite (149) y Latias (380), propuestos y confirmados por el usuario antes de empezar.
Integrados en `singleplayer`; entrega en `C:\Users\javie\Desktop\imagenes\revision\lote8\`. Aprobados por el usuario el 2026-10-14.

| Especie | Base reconstruida | Celda de hoja | Fotogramas / inclinaciones | FPS | Movimiento |
|---|---|---|---|---|---|
| Carvanha | 48×51 | 70×70 | 8 / 5 | 10 | Cola rápida, dorsal y aletas laterales y ventral |
| Seaking | 59×50 | 80×80 | 8 / 5 | 7 | Cola amplia, dorsal y aletas inferiores con desfase |
| Fearow | 82×72 | 100×100 | 8 / 3 | 8 | Alas parcialmente plegadas, cola y patas |
| Dragonite | 64×80; visualización al 75 % (48×60) | 76×76 | 8 / 3 | 8 | Alas pequeñas, cola enroscada, antenas y brazo |
| Latias | 68×53 | 88×88 | 8 / 5 | 7 | Planeo, alas, patas y cola inferior |

Lo nuevo del método:
- Periodos explícitos: Carvanha **4,34**, Fearow **2,84** y Latias **3,49** píxeles; el detector tomaba la cuadrícula doble.
  Seaking se reconstruye automáticamente a 8 y Dragonite a 6. Comparaciones a la misma altura, sin interpretar ni redibujar.
- Fearow: la reconstrucción mide 72 de alto y no cabe completamente extendida en el cielo de 69. Las alas permanecen parcialmente
  plegadas: la trasera se dobla por muestreo inverso y la delantera por columnas completas, raíces fijas. Anclaje del lienzo desplazado
  tres píxeles hacia arriba; `layoutHeight: 61`, inclinaciones -1/0/1. No se reduce su escala ni se cambia el layout.
  Se quitan las celdas B de fondo en las puntas superiores (y < 12); las plumas claras y el pecho se conservan.
- **Excepción de Dragonite, autorizada por el usuario:** su base 64×80 no cabe en el cielo; el usuario eligió «Reducir solo Dragonite».
  El TXT y `rows` conservan la reconstrucción original; `sourceFrames` conserva la animación a escala nativa. Solo `fr`, los fotogramas
  para visualizar, se muestrea al 75 % por vecino más cercano. Paleta intacta, sin suavizado. Los huecos totalmente encerrados de una
  celda del muestreo se rellenan con un color vecino. El ojo y la boca se protegen en las coordenadas reducidas. Reserva 61, giros -2/0/2.
  La comparación junto a la referencia enseña la base original; los bucles y fotogramas enseñan la visualización reducida.
- Latias: dos celdas claras aisladas de fondo, (0,28) y la D de fila 22 entre x=35..49, se retiran. Alas de distinta longitud dobladas
  por columnas, patas y cola con retraso. Reserva 59, giros -4/-2/0/2/4.
- `audit_batch8.js`: **40 fotogramas de visualización, 168 celdas, 168/168 regiones de ojos**, iso 0, holes 0 y cero celdas tocando bordes.
  También audita los ocho `sourceFrames` de Dragonite. Sus regiones de ojos se expresan en las coordenadas reducidas, antes del pad.
  Todas las transiciones de cierre del bucle quedan dentro del máximo del resto del ciclo, con la tolerancia previa del 5 %.
- Sellado aprobado de rotaciones: **21 huecos** (Seaking 3, Fearow 17, Latias 1). Sin modificar `critter.js` ni `seal_sheet.js`.
- Límites verticales de todas las celdas, con el redondeo al par de C#: **Fearow y=1..66, Dragonite y=4..63, Latias y=3..64**, superficie 69.
- Laboratorio compilado solo con `--artifacts-path .lab-art`: **0 errores**, dos avisos CS0649 previos. Pruebas: **553/553**, 0 fallos,
  0 omitidas, 24 s; avisos CS8321 y xUnit2030 previos. No se abre el juego ni se despliega.
- **72 SHA-256 anteriores idénticos**: 36 hojas aprobadas, sus 35 scripts y `critter.js`. Las cinco hojas nuevas suman **80.866 bytes**.
  `integrate_entries.js` sustituye únicamente las cinco entradas de este lote en `CapCritterArt.cs`.
- Herramientas reproducibles desde `tools/critters`: `review_batch8.js deliver`, `audit_batch8.js`, `render_batch8.ps1`.

Limitaciones: Fearow vuela con las alas parcialmente plegadas y giros mínimos. Dragonite pierde detalle fino por su reducción autorizada,
aunque la base original se conserva. Los JPEG pequeños limitan la reconstrucción. Verificado en el laboratorio de este PC, sin emulador
ni despliegue. Los nadadores mantienen el tinte del agua y la atenuación tras el equipo. La vista forzada repite Carvanha en seis rutas;
no representa la frecuencia normal y puede producir solapamientos. GIF de 256 colores y tiempos en centésimas; los PNG son los originales.
El reinicio del recorrido de 70 segundos es distinto del bucle individual. Los cinco grandes siguen reservados.
**Lote 8 aprobado. Confirmar las cinco especies del lote 9 antes de comenzar.**

Rendimiento final [verificado] con `--especies 318,119,22,149,380 --medir`: columna del equipo 178×321 **1,75–2,10 ms de media** por
fotograma, p95 **2,21–3,50 ms**; fondo **1,07–1,30 ms**. Primera preparación 86,33 ms. Medidas de este PC en el laboratorio, sin emulador.
GIF completo decodificado sin errores: **840 fotogramas, 70,00 s, 1920×976**, exportado a 12 fps. Revisados ambos paneles mediante mosaicos
cada dos segundos del recorrido y una vista completa a tamaño real. Los solapamientos y la oclusión tras el juego y el equipo son los normales.
### Lote 9 (2026-10-14): aprobado por el usuario
Bruxish (779), Staraptor (398), Salamence (373), Charizard (6) y Latios (381), confirmados por el usuario antes de empezar.
Integrados en `singleplayer`; entrega en `C:\Users\javie\Desktop\imagenes\revision\lote9\`. Aprobado por el usuario el 2026-10-14.

| Especie | Base reconstruida | Escala de visualización | Celda | Fotogramas / inclinaciones | FPS | Movimiento |
|---|---|---|---|---|---|---|
| Bruxish | 60×39 | 100 % | 78×78 | 8 / 5 | 8 | Cola, señuelo dorsal y aletas con desfase |
| Staraptor | 55×64 | 100 % | 80×80 | 8 / 3 | 8 | Cabeza y cresta, ala plegada, cola y patas |
| Salamence | 79×72 | 83,3 % | 78×78 | 8 / 3 | 7 | Alas de distinta profundidad, cola y patas |
| Charizard | 76×79 | 75 % | 74×74 | 8 / 3 | 8 | Membranas de las alas, llama y cola, brazo |
| Latios | 74×74 | 80 % | 78×78 | 8 / 3 | 7 | Planeo, alas, antenas y apéndices inferiores |

Lo nuevo del método:
- Periodos: Bruxish automático 9,98; Staraptor explícito 3,66; Salamence explícito 2,004; Charizard explícito 5,16;
  Latios automático 7,32. Comparaciones de referencia y reconstrucción a igual altura, sin redibujar.
- `recon2.js`: con una cuadrícula de dos píxeles, el margen interior puede no contener ninguna coordenada entera.
  Antes esto generaba filas y columnas vacías en Salamence. Solo cuando no hay muestras interiores se toma el centro de la celda.
  Regresión comprobada: reconstruir Horsea produce el mismo TXT que antes, byte por byte.
- Fondos atrapados: blanco entre las patas de Staraptor; grises y blancos entre cuello y alas de Salamence; tablero blanco/gris entre
  alas y cuello de Charizard. Máscaras acotadas para conservar los blancos anatómicos de ojos, dientes, vientre y uñas.
- **Reducciones autorizadas por el usuario para los que no caben en el cielo:** Salamence 5/6, Charizard 3/4 y Latios 4/5.
  TXT, `rows` y `sourceFrames` conservan la reconstrucción y animación nativas; solo `fr` se reduce por vecino más cercano.
  Las comparaciones enseñan la base nativa; los bucles y hojas de fotogramas enseñan la visualización. Sin colores interpolados.
  Los huecos totalmente encerrados de una celda tras muestrear se sellan con colores vecinos. Charizard pierde seis muestras aisladas
  de una celda de la llama, repartidas entre sus ocho fotogramas reducidos; no se recorta la animación nativa.
- Latios: la reducción saltaba la fila del iris y el brillo. Se proyectan explícitamente las celdas originales D/M/C de la región
  x=38..42, y=16..19 sobre su posición reducida antes de girar; no se inventan rasgos ni colores. Ojo protegido en todas las inclinaciones.
- Alas desde raíces fijas, dobladas por columnas o muestreo inverso; separación siguiendo los bordes de la referencia.
  Staraptor conserva su pose posada. Giros contenidos: Staraptor -1/0/1; los tres reducidos -2/0/2. Altura de layout 61 para los cuatro.
- Sellado de rotaciones: cuatro huecos de una celda en Salamence con el mecanismo aprobado `sealRotatedHoles`.
  No se cambia `critter.js` ni `seal_sheet.js`.
- `audit_batch9.js`: **40 fotogramas de visualización, 24 fotogramas nativos adicionales y 136 celdas inclinadas**.
  **136/136 regiones de ojos**, iso 0, holes 0, cero celdas tocando bordes. Cierre de los cinco bucles dentro del máximo del resto
  del ciclo con tolerancia del 5 %. Mosaicos de ojos de todas las celdas y costuras ampliadas revisados.
- Cielo con redondeo al par de C#: Staraptor **y=2..65**, Salamence **3..63**, Charizard **4..64**, Latios **3..64**; superficie en 69.
- Laboratorio: había tres rutas de cielo y el modo `ForcedAll` omitía una especie al pedir cuatro voladores.
  `ForcedLaneSpecies` distribuye los excedentes entre las rutas con desfase, conservando las asignaciones antiguas cuando caben.
  Solo cambia la vista forzada; el flujo normal no cambia. `CapVisitorLabTests.cs` añade dos pruebas para cobertura y compatibilidad.
- Build aislado con `--artifacts-path .lab-art`: **0 errores**, dos advertencias CS0649 previas.
  Pruebas **555/555**, cero fallos y cero omitidas, 25 s; advertencias CS8321 y xUnit2030 previas.
- **82 hashes SHA-256 intactos**: 41 hojas aprobadas, 40 scripts anatómicos y `critter.js`.
  Las cinco hojas nuevas suman **52.629 bytes**. Integración limitada a sus cinco entradas en `CapCritterArt.cs`.
- Reproducción desde `tools/critters`: `review_batch9.js deliver`, `audit_batch9.js`, `render_batch9.ps1`.

Limitaciones: la referencia diminuta de Salamence y el JPEG de Staraptor limitan el detalle. Las tres reducciones pierden detalles finos,
aunque las bases nativas se conservan. Staraptor mantiene la pose posada y movimientos pequeños. Verificado en laboratorio, sin emulador
ni despliegue. El modo forzado muestra siete recorridos (Bruxish repetido tres veces y cuatro voladores), con posibles solapamientos;
no representa la frecuencia normal. Se conserva la oclusión detrás del juego/equipo y el tinte del agua. El GIF usa 256 colores y tiempos
en centésimas; el reinicio del recorrido largo es distinto del bucle individual. Los cinco grandes siguen reservados.
**Lote 9 aprobado. Confirmar la selección del lote 10 antes de comenzar.**

Rendimiento final [verificado] con `--especies 779,398,373,6,381 --medir`: columna del equipo 178×321 **1,83–2,14 ms de media** por
fotograma, p95 **2,39–3,86 ms**; fondo **1,06–1,43 ms**. Primera preparación 92,21 ms. Medidas de este PC en laboratorio, sin emulador.
Recorrido de revisión: **840 fotogramas, 70 s, 1920×976**, exportado a 12 fps. Mosaicos de ambos paneles cada dos segundos y vista completa
revisados: aparecen las cinco especies; se mantienen solapamientos y oclusiones normales. Registro externo y cerebro actualizados.
GIF completo decodificado sin errores: 840 fotogramas y 70,00 s; registro en `lote9/verificacion_gif.txt`.

### Lote 10 (2026-10-14): aprobado por el usuario
Articuno (144), Zapdos (145), Moltres (146), Kyogre (382) y Lugia (249), propuestos y confirmados antes de empezar.
El usuario confirmó incorporar ahora Kyogre y Lugia, antes reservados. Quedan Wailord, Rayquaza y Ho-Oh para el último lote.
Integrados en `singleplayer`; entrega en `C:\Users\javie\Desktop\imagenes\revision\lote10\`. Aprobado por el usuario el 2026-10-14 («ok a todo»).

| Especie | Base reconstruida | Escala de visualización | Celda | Fotogramas / inclinaciones | FPS | Movimiento |
|---|---|---|---|---|---|---|
| Articuno | 72×81 | 75 % | 76×76 | 8 / 3 | 7 | Alas elevadas dobladas desde la raíz, cola larga y patas |
| Zapdos | 91×58 | 100 % | 108×108 | 8 / 3 | 9 | Alas angulosas por columnas, cresta, cola y garras |
| Moltres | 87×70 | 85,7 % (6/7) | 90×90 | 8 / 3 | 8 | Alas, cola de fuego, cresta y patas con desfase |
| Kyogre | 94×57 | 100 % | 132×132 | 8 / 5 | 6 | Aletas amplias, cola bifurcada y dorsal; balanceo del cuerpo |
| Lugia | 80×80 | 75 % | 76×76 | 8 / 3 | 6 | Alas articuladas por doblado inverso, cola y patas |

Lo nuevo del método:
- Cuadrículas: Articuno automática **7,04**, Moltres **6,90**, Kyogre **7,34**, Lugia **6,58**. Zapdos detectaba el doble (**14,74**):
  se usa el cuarto argumento explícito **7,37**. Comparaciones de referencia y reconstrucción a igual altura.
- Fondo atrapado: celdas blancas dentro del bucle de la cola y entre plumas de Articuno; entre patas de Zapdos; dos celdas claras entre
  llamas de Moltres y blanco puro entre uñas de Kyogre. Lugia conserva sus blancos anatómicos sin limpieza adicional.
- Se mantiene la autorización de reducir solo la visualización cuando no cabe en el cielo: Articuno y Lugia al 75 %, Moltres a 6/7.
  `rows`, TXT y `sourceFrames` conservan base y animación nativa. `batch10_display.js` aplica vecino más cercano, proyección de rasgos
  originales y sellado de huecos de una celda totalmente encerrados con color vecino. Ningún color interpolado ni redibujado.
  Sellado tras reducir: Articuno 0, Moltres 12, Lugia 1 celdas sumadas entre sus ocho fotogramas. Cero muestras aisladas eliminadas.
- Articuno: al reducir, iris rojo y blanco contiguo coincidían en el mismo píxel. Se proyecta primero el blanco del ojo y después la celda
  original roja (29,33); el blanco de la fila superior queda visible. La auditoría exige expresamente rojo C y blanco B en las 24 celdas.
  No proyectar todo el contorno después del iris: puede taparlo cuando dos celdas originales convergen en la misma celda reducida.
- Moltres: el ala izquierda elevada se desplaza por filas desde su raíz; la derecha por columnas. La cola de fuego usa muestreo inverso
  horizontal con desfase por altura. Se ajustó su máscara en y=24/31/40 para no incluir puntas de llama del ala derecha.
  Así desaparecen las cinco muestras aisladas del intento inicial sin borrarlas de la animación nativa.
- Lugia: alas por muestreo inverso, raíces quietas; la máscara deja fuera la cola que pasa por detrás. Ojo y boca preservados al reducir.
  Usa la misma hoja en el mar y en el cielo; no se cambia la programación de sus apariciones.
- Kyogre: **Pose 28°**, filas 16/22/28/34/40 para compensar la pose diagonal original. Reserva de altura **90** en el mar para cubrir
  las inclinaciones; no se amplía ni se reduce. Se conservan las marcas rojas de las aletas y la pupila negra dentro del iris amarillo.
- `integrate_entries.js`: la última entrada del catálogo (Moltres) no llevaba coma final. El patrón acepta ahora coma opcional.
  Integración repetida comprobada sin cambios en el archivo; las demás entradas mantienen su sintaxis. Solo se sustituyen las cinco de este lote.
- `audit_batch10.js`: **40 fotogramas de visualización + 24 nativos**, **136 celdas inclinadas**, **160/160 regiones de ojos**
  (Moltres tiene dos regiones), **iso 0, holes 0**, cero celdas tocando bordes y cierre de los cinco ciclos dentro del máximo con tolerancia 5 %.
  Costuras revisadas con `zoom.js` y todos los rostros con `rostro.js`.
- Sellado de rotaciones aprobado: **76 huecos de una celda** (Articuno 4, Moltres 3, Kyogre 69), mediante `sealRotatedHoles`.
  No se modifica `critter.js`, `seal_sheet.js` ni ninguna especie aprobada.
- Cielo (superficie 69, redondeo de C#): Articuno **y=3..64**, Zapdos **4..62**, Moltres **3..64**, Lugia **4..64**.
  Auditoría adicional de mar, conservadora para el redondeo: Kyogre mínimo **y=80**, margen mínimo al suelo **13**; Lugia **y=75**, margen **16**.
- Laboratorio compilado solo con `--artifacts-path .lab-art`: **0 errores**, dos advertencias CS0649 previas.
  Pruebas **555/555**, cero fallos y cero omitidas, **24 s**; advertencias CS8321 y xUnit2030 previas.
- **92 hashes SHA-256 idénticos**: 46 hojas aprobadas, 45 scripts anatómicos y `critter.js`. Las cinco hojas nuevas suman **90.822 bytes**.
- Reproducción desde `tools/critters`: `review_batch10.js deliver`, `audit_batch10.js`, `render_batch10.ps1`.

Limitaciones: las tres reducciones pierden detalle fino, conservando los originales. Las amplitudes de los voladores se contienen para caber
en el cielo; el dibujo de referencia determina la pose. El modo forzado mantiene siete recorridos: Kyogre en dos rutas y Lugia en mar y cielo,
además de las tres aves; no representa su frecuencia normal y puede haber solapamientos. Se conservan el tinte del agua y la atenuación tras
el equipo. El GIF usa 256 colores y tiempos en centésimas, mientras la app conserva sus PNG. El reinicio del recorrido largo es distinto del
bucle individual de cada especie. Solo laboratorio, sin emulador ni despliegue.
**Lote 10 aprobado junto al lote 11. Catálogo completo.**

Rendimiento final [verificado] con `--especies 144,145,146,382,249 --medir`: columna del equipo 178×321 **1,80–2,26 ms de media** por
fotograma, p95 **2,46–3,43 ms**; fondo **1,15–1,60 ms**. Primera preparación 94,23 ms. Medidas de este PC en laboratorio, sin emulador.
Recorrido de revisión: **840 fotogramas, 70 s, 1920×976**, exportado a 12 fps. Mosaicos de ambos paneles cada dos segundos y una vista
completa revisados: aparecen las cinco especies; Lugia pasa por mar y cielo. Se mantienen los solapamientos y las oclusiones habituales.
GIF final completo decodificado sin errores: **840 fotogramas, 70,00 s**; registro en `lote10/verificacion_gif.txt`.

### Lote 11 (2026-10-14): los tres últimos, aprobados por el usuario
Wailord (321), Rayquaza (384) y Ho-Oh (250). El usuario pidió expresamente «acaba esos 3»: autoriza este lote final,
sin aprobar entonces el lote 10. Después confirmó «ok a todo»: ambos lotes aprobados el 2026-10-14. Las 54 entradas de CritterArt.All ya usan hojas PNG.
Entrega: C:\Users\javie\Desktop\imagenes\revision\lote11\. Rama singleplayer, commit local sin push.

| Especie | Base reconstruida | Escala de visualización | Celda | Fotogramas / inclinaciones | FPS | Movimiento |
|---|---|---|---|---|---|---|
| Wailord | 87×74 | 100 % | 110×110 | 8 / 3 | 5 | Elevación lenta del cuerpo, cola bifurcada y aletas pequeñas alternadas |
| Rayquaza | 89×83 | 75 % | 80×80 | 8 / 3 | 7 | Onda por columnas del cuerpo enroscado, cuernos y punta de cola desfasados |
| Ho-Oh | 84×91 | 70 % | 80×80 | 8 / 3 | 6 | Alas anchas dobladas desde la raíz y cola en abanico retrasada |

Lo nuevo del método:
- Wailord: la detección automática daba periodo 13,62, cinco veces el fino. Cuarto argumento explícito 2,724.
  Se retiran restos de fondo A por encima de y=24; conserva vientre, rayas y manchas claras del lomo. Aletas separadas por bordes existentes.
- Rayquaza: periodo automático 6,28. Blanco B entre las curvas y detrás de la mandíbula retirado; garras lavanda M/P conservadas.
  La onda crece desde el cuello hacia la cola. El iris amarillo C original se proyecta después del negro G al reducir, para no perderlo.
- Ho-Oh: periodo explícito 2,52, frente al 12,60 automático. Se retiran restos blancos exteriores conservando pecho, ojo y garras.
  Las alas usan muestreo inverso con cuatro iteraciones; sus raíces quedan quietas. La cola parte del borde ya dibujado del cuerpo.
- Se reutiliza batch10_display.js sin modificarlo: vecino más cercano, colores originales y proyección de rasgos. Solo se reducen los
  fotogramas de visualización de Rayquaza/Ho-Oh, dentro de la autorización anterior. TXT, rows y sourceFrames conservan sus tamaños nativos.
  Sellado al reducir: Rayquaza 0, Ho-Oh 2 huecos de una celda sumados entre los ocho fotogramas. Cero muestras aisladas eliminadas.
- Wailord: proteger toda la zona del ojo hacía que un azul vecino sobrescribiera el punto negro al girar -4° en un fotograma.
  La protección selecciona ahora solo B en esa región. Las 24 celdas conservan el ojo negro original; la referencia no tiene iris blanco.
- Inclinaciones: Wailord -4/0/4, Rayquaza y Ho-Oh -1/0/1. Pose 0 en los tres. Reservas de altura 84, 61 y 61 respectivamente.
- La primera vista conjunta reveló una condición adicional: Swimmer no dibuja si bottom <= top. Con cielo de 69, la reserva
  debe ser menor de 62, aunque los píxeles quepan. Rayquaza y Ho-Oh usan reserva 61 y conservan sus tamaños; no se cambia el motor.
  La auditoría exige ahora trajectoryRange > 0 además de los límites de los píxeles. Revisar siempre el recorrido real después de auditar.
- audit_batch11.js: 24 fotogramas de visualización, 16 nativos adicionales, 72 celdas y 72/72 regiones de ojos correctas.
  Iso 0, holes 0 y cero celdas tocando bordes. Los tres cierres de bucle pasan el criterio previo (máximo de transiciones con tolerancia 5 %).
  Ningún sellado adicional de rotaciones necesario. Revisadas costuras ampliadas y los ojos en todas las celdas.
- Cielo, superficie 69: Rayquaza y=3..64, Ho-Oh y=2..66. Wailord: mínimo y=76 y margen conservador de 20 píxeles al suelo.
- Build del laboratorio solo en .lab-art: cero errores, dos advertencias CS0649 previas. Pruebas 555/555, cero fallos y cero omitidas,
  22 s; advertencias CS8321 y xUnit2030 previas. No se abre el juego ni se despliega.
- 102 SHA-256 anteriores idénticos: 51 hojas, 50 scripts de anatomía y critter.js. Las tres hojas nuevas suman 39.098 bytes.
  Solo se sustituyen sus tres entradas del catálogo; helpers anteriores intactos.
- Reproducción desde tools/critters: review_batch11.js deliver, audit_batch11.js y render_batch11.ps1.

Limitaciones: Wailord y Ho-Oh parten de JPEG diminutos y Rayquaza/Ho-Oh pierden detalle fino al reducir solo su visualización.
Las bases originales quedan guardadas. Los movimientos de las alas y las inclinaciones se contienen para respetar los 69 píxeles de cielo.
El modo forzado repite Wailord en tres rutas de mar, Ho-Oh en dos de cielo y Rayquaza en una: seis recorridos, sin representar frecuencia normal.
Se conservan el tinte del agua, las oclusiones tras el juego/equipo y los solapamientos entre visitantes. GIF con 256 colores y tiempos en
centésimas; los PNG integrados conservan sus paletas. El reinicio del recorrido largo es distinto del bucle individual. Solo laboratorio,
sin emulador ni despliegue. Lote 11 aprobado junto al lote 10: las 54 especies quedan terminadas, integradas y aprobadas.

Rendimiento final [verificado] con --especies 321,384,250 --medir: columna del equipo 178×321, media 1,83–2,45 ms por fotograma,
p95 2,57–3,73 ms; fondo 1,01–1,69 ms. Primera preparación 90,80 ms. Medido en este PC y sin emulador.
GIF definitivo decodificado sin errores: 840 fotogramas, 70,00 s, 1920×976, exportado a 12 fps. Revisados ambos paneles mediante mosaicos
cada dos segundos y una vista completa: aparecen las tres especies y permanecen dentro de sus bandas verticales.

### Revisión posterior: poses de vuelo (2026-10-14), piloto pendiente
El usuario detectó postura de apoyo en varios voladores y autorizó redibujar torso, patas, alas y cola, conservando identidad, proporciones,
rostro, paleta y detalle. Esta indicación sustituye «nunca redibujar» únicamente para los casos afectados. Primero exige revisar un piloto.
Inventario: Pidgey, Spearow, Pikipek, Pidgeot, Toucannon, Staraptor, Skarmory, Dragonite, Charizard y Salamence. Los otros 18 voladores intactos.
Piloto separado de Pidgey: tools/critters/pidgey_flight_anim.js; comparación, ciclo fijo y simulación externa de dirección en
C:/Users/javie/Desktop/imagenes/revision/vuelo_piloto/. Método y diagnóstico: tools/critters/REVISION_POSES_VUELO.md.
8 fotogramas a 12 fps, 24 celdas, 24 ojos correctos; 212 píxeles de cabeza conservados en cada fotograma; iso/holes 0 y una sola componente
conectada en fotogramas y rotaciones. No integrar ni extender a los otros nueve antes de aprobar pose y ciclo. La integración prematura
fue rechazada por revisión automática; se completó la propuesta separada. Las 54 hojas y los scripts aprobados permanecen sin cambios.
La aprobación anterior de los lotes es histórica: estos diez quedan ahora señalados para revisión de postura. No se han repetido las pruebas
WPF porque no se ha cambiado la app; falta validación integrada después de aprobar el piloto. Sin despliegue ni push.

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

### Correcciones de vuelo, tanda 2 (2026-10-14): entrega histórica RECHAZADA posteriormente
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

### Charizard: tercera revisión del batido (2026-10-14), pendiente de aprobación
El usuario conserva la base y los avances del ala cercana, y rechaza el ala lejana: en casi todo el ciclo era una tira naranja
o una pieza triangular detrás de la cabeza, y Charizard parecía volar con una sola ala. También pide limpiar la raíz del ala
cercana en los fotogramas 7–11, donde se acumulaban líneas y pliegues.

**Ala lejana derivada de la cercana del mismo fotograma** (charizard_approved_flight.js, farWing). Se pinta el ala cercana en una
rejilla aparte con su misma pose (o la base en el fotograma 0) y se transforma:
- Raíz movida 8 px hacia delante y 3 hacia arriba, en el hombro lejano.
- Giro RotSprite (C.swing) hacia la cabeza, proporcional a la altura de la muñeca: 30° con el brazo alto y 28° con el bajo. Así se
  abre por delante del ala cercana cuando las dos suben y bajo el pecho cuando bajan.
- Envergadura al 90 % y aplastamiento vertical hacia la raíz solo cuando hace falta para no pasar de la altura que ya ocupa la
  cercana (filas 3–96 de la base). Así el dibujo cabe en el cielo: 99×60 en pantalla, celda 102.
- Muestra el reverso en sombra (C→K, J→M, M→G, I→M, H→J) y el cuerpo tapa su raíz.
- Queda de canto solo en los pasos (fotogramas 4–5 y 10–12).

El fotograma 0 deja de ser idéntico a la base: 945 píxeles distintos, todos dentro del ala lejana antigua o de la nueva. El script
lo comprueba.

**Raíz del ala cercana.** Con la muñeca por debajo del hombro (fotogramas 5–12), el ala cercana:
- No copia el manguito de la pose alta, que apuntaba al revés.
- Borra lo que el remuestreo dejaba por encima del brazo (a más de 3,2 px de su borde).
- Quita astillas de 1–2 píxeles en tres pasadas.

Queda el brazo continuo del hombro a la muñeca, con la membrana colgando debajo. Con el brazo alto se mantiene el manguito de la
base (57/57 píxeles).

[Verificado]
- iso 0 y holes 0 en los dibujos nativos y de pantalla y en las 48 celdas, con una componente conectada en cada uno.
- Ojos en 48/48 celdas y rostro/cuello 568/568 en cada fase.
- Cielo previsto y4–62. Cierre del bucle con 1385 píxeles distintos (máximo interior 3592).
- Las dos alas tocan su raíz en los 16 fotogramas. 137 archivos protegidos intactos.
- Build del laboratorio: 0 errores. Tests App: 555/555.
- --medir: Party media 1,48–1,69 ms, Deep 0,77–1,06 ms.
- GIF del layout revisado: aparece en la banda del cielo con las dos alas.

Entrega: C:/Users/javie/Desktop/imagenes/revision/charizard_batido_v3/
- charizard_fotogramas_clave.png: seis poses y las dos alas aisladas ×2.
- charizard_bucle_fijo.gif, charizard_en_el_layout_tamano_real.gif, auditoria.json y rendimiento.txt.
- Las mismas comprobaciones que la v2.

Desde tools/critters: node build_batch.js charizard; node review_charizard_flight.js; node review_charizard_wing_keys.js;
node integrate_entries.js; powershell -ExecutionPolicy Bypass -File render_charizard_flight.ps1.

Limitaciones:
- El reverso del ala lejana es un recoloreado en sombra de la cercana, no un dibujo propio.
- En los pasos se ve estrecha o tapada por el cuerpo.
- Pendiente de aprobación visual. Commit local sin push.

### Charizard: cuarta revisión, lectura de las dos alas (2026-10-14), pendiente de aprobación
El usuario acepta la v3 como avance y pide una pasada localizada, sin más amplitud ni rehacer la animación:
- En las posiciones bajas los contornos de las dos alas se superponen y parecen fusionadas.
- En la recuperación aparecen puntas sin continuidad entre fotogramas.

Cambios, todos en charizard_approved_flight.js:
- **Hombro lejano más adelantado con el brazo bajo:** hasta 5 px más hacia la cabeza y 1 px más abajo (FAR_LOW_SHIFT). Las dos
  alas cuelgan de lados distintos de la espalda en lugar de ir una pegada a la otra.
- **Borde oscuro del ala cercana:** donde el ala cercana pisa a la lejana, su píxel de borde pasa a E.
- **Un solo muestreo para el ala lejana:** giro, envergadura y aplastamiento se hacen en una sola pasada hacia atrás sobre el ala
  ampliada ×8 con Scale2x (copia local de la de critter.js). Antes se perdían filas al aplastar y el contorno salía dentado.
- **Trozos sueltos del ala lejana fuera:** se quitan los trozos visibles de menos de 70 píxeles que quedan al borde del cuerpo
  (FAR_MIN_PIECE), porque saltaban de un fotograma a otro.
- **Puntas recortadas:** en los fotogramas 1–15 se recortan los bigotes de 1 px de las puntas de las dos alas (2 pasadas).
- **Manguito del hombro:** solo con el brazo claramente alto (muñeca por encima de y=31). En los fotogramas 4 y 12 ya no asoma.

Sin cambios: cuerpo, cabeza, extremidades, cola, poses clave y amplitud. El fotograma 0 difiere de la base en 956 píxeles, todos
de ala lejana.

[Verificado]
- iso 0 y holes 0 en los dibujos nativos y de pantalla y en las celdas. Ojos 48/48.
- Cielo y4–62, 99×60, celda 102. Cierre del bucle 1389 (máximo interior 3598).
- Raíces en los 16 fotogramas. 137 protegidos intactos. Tests App 555/555.
- --medir: Party media 1,39–1,68 ms, Deep 0,71–1,05 ms.
- Ciclo revisado a tamaño real en el laboratorio (charizard_ciclo_tamano_real_recorte.gif, 12 fps).

Entrega: C:/Users/javie/Desktop/imagenes/revision/charizard_batido_v4/ (mismas piezas que la v3 más el recorte a tamaño real).

### Charizard: quinta revisión, color de cada cara del ala (2026-10-14), pendiente de aprobación
El usuario aprueba el movimiento de la v4. Pide corregir solo el color: la membrana azul es la cara inferior y la superior es
naranja. Hay que decidir en cada fotograma qué cara se ve, aplicar el mismo criterio a las dos alas y representar la transición
cuando el ala pasa de canto. Bulbapedia confirma que la cara inferior de las alas es verde azulado; el naranja de la cara superior
es el del cuerpo.

**Criterio, por geometría.** Charizard mira a la izquierda, así que el ala cercana es la izquierda. Al subir un ala sobre el eje
del cuerpo, su cara superior gira hacia fuera:
- Ala cercana, levantada: cara inferior (azul). Bajada: cara superior (naranja).
- Ala lejana (derecha), levantada: cara superior (naranja). Bajada: cara inferior (azul).

La elevación se mide por la muñeca respecto al hombro: e = -dy/|brazo|.

**Transición.** Con |e| < 0,3, la parte delantera, junto al brazo (hasta las cinco primeras anclas del borde de ataque), toma
primero la cara nueva y la membrana de detrás la sigue según su distancia al borde de ataque. Ocurre en los fotogramas 4–5 y 11–12.

**Colores, en charizard_approved_flight.js (faces).**
- Cara superior del ala cercana: C→J y K→M, con los dedos M→G.
- Cara superior del ala lejana, más oscura: C→M, K→G, J→J y M→G.
- Cara inferior del ala lejana: el reverso en sombra de antes.
- Con la cara naranja hacia nosotros, el ala cercana lleva borde oscuro donde pisa el cuerpo naranja, salvo a menos de 5 px del
  hombro, para no cortar la unión.

Sin cambios: movimiento, siluetas, articulaciones, cuerpo, cabeza, extremidades y cola. El fotograma 0 solo cambia de color en el
ala lejana (939 píxeles distintos de la base, todos de ala lejana).

[Verificado]
- iso 0 y holes 0 en los dibujos y en las celdas. Ojos 48/48.
- Cielo y4–62, 99×60, celda 102. Cierre del bucle 1398 (máximo interior 3636).
- Raíces en los 16 fotogramas. 137 protegidos intactos. Tests App 555/555.
- --medir: Party media 1,46–1,74 ms, Deep 0,78–1,09 ms.
- Ciclo revisado a tamaño real y 12 fps en el laboratorio.

Entrega: C:/Users/javie/Desktop/imagenes/revision/charizard_batido_v5/ (mismas piezas que la v4).

### Charizard aprobado; Skarmory: base de vuelo v3 (2026-10-14), pendiente de aprobación
El usuario aprueba la v5 de Charizard (charizard_batido_v5, commit c9b48ee): Charizard queda terminado. Sigue Skarmory con el mismo
método: primero SOLO la base de vuelo junto a su referencia; sin animación ni integración hasta que se apruebe.

Referencia de postura: ilustración de la carta Skarmory 59/119 de XY Phantom Forces (Sanosuke Sakuma), vista en cardcodex.com.
Muestra a Skarmory de perfil, volando hacia la izquierda:
- Cuerpo nivelado y cabeza delante.
- Ala cercana levantada hacia arriba y atrás.
- Ala lejana barrida hacia abajo y adelante bajo el pecho, enseñando su cara roja.
- Cola recta atrás y patas recogidas bajo el vientre.

Se descartaron otras cuatro cartas porque no son vuelo de perfil (cernido, picado de frente, de frente y posado). La procedencia
está en referencias/fuentes.json.

**Método** (tools/critters/skarmory_flight_pose_v3.js): piezas del sprite original a escala 1, giradas al estilo RotSprite (Scale2x
×3 y muestreo hacia atrás), sin reducir ni redibujar desde cero.
- **Núcleo rígido:** cabeza, cuello, cuerpo y cola sin girar. Inclinarlo 12° rompía las rayas del cuello.
- **Ala cercana:** levantada 16° desde el hombro.
- **Ala lejana:** el ala cercana girada 170° hacia abajo y adelante. Va acortada en profundidad (85 % × 70 %) y con los rojos y
  platas un tono más oscuros.
- **Patas plegadas hacia atrás** desde la cadera: la delantera 95° y la trasera 45°.
- Se quitan las cuchillas plateadas verticales del ala lejana del original. Donde nacían, la parte de arriba del cuello se cierra
  con el contorno azul oscuro (G).
- Los poros de hasta 4 píxeles que dejan las piezas al separarse toman el color mayoritario de sus vecinos. Los huecos mayores son
  aire entre partes y se quedan.

[Verificado, primera entrega] 70×71 nativo (original 66×68), iso 0, holes 0 y solo la paleta original. (El recuento de opacos de
esa entrega estaba inflado: C.check recibía filas como arrays.) Estas pruebas no
demuestran que la pose sea artísticamente correcta.

Entrega: C:/Users/javie/Desktop/imagenes/revision/skarmory_pose_v3/
- skarmory_pose_y_referencia.png: la comparación para revisar.
- skarmory_base_ampliada.png (×6) y skarmory_base_nativa.png.
- skarmory_base.txt/.json, auditoria.json y referencias/.

Desde tools/critters: node review_skarmory_pose_v3.js. No se ejecutan build_batch.js ni integrate_entries.js: la app sigue con el
Skarmory de la tanda 2 rechazada (410e8d0).

Siguiente paso: esperar la aprobación visual de esta base. Dragonite y Salamence esperan.

#### Skarmory base v3, segunda entrega: correcciones pedidas (2026-10-14), pendiente de aprobación
El usuario acepta la pose, pero señala cuatro fallos: el cuello duplicado, el ala derecha invertida, plumas sin conectar al cuerpo
y patas que no se entienden.
- **Cuello duplicado.** Por encima de la línea que va de la nuca a la raíz del ala, el bloque rayado era la base blindada del ala
  lejana. Sin sus cuchillas parecía un segundo cuello, así que sale de la pose (isPlating). Queda una sola banda rayada de la
  cabeza al cuerpo, cerrada con contorno G donde se cortó.
- **Ala derecha (lejana) invertida.** Girar 170° ponía el borde plateado debajo y el abanico del revés. Ahora es el ala cercana en
  espejo y girada 80° (piece con flip): borde plateado arriba y cuchillas rojas colgando detrás, como en la carta.
- **Plumas sin conectar.** El borde plateado del ala cercana se cortaba en su base porque isFarBlades llegaba a x 40. Ahora llega
  a x 36 (x 37 por encima de y 19, la última cuchilla lejana), y el borde baja entero hasta el hombro. La punta de la cuchilla
  roja más baja (x 51–52, y 35) se contaba como cola y quedaba suelta: ya es del ala.
- **Patas.** Girarlas emborronaba los dedos. La delantera va en espejo (los dedos apuntan atrás) y girada 15°; la trasera, girada
  15°. Las dos salen de detrás del vientre, de modo que los muslos quedan ocultos y se ven la caña y el pie plegados hacia atrás.

[Verificado] 70×66, 1791 píxeles opacos en una sola pieza (8 vecinos), iso 0, holes 0 y solo la paleta original. Además, cada
capa es una pieza: ala lejana, ala cercana, núcleo y las dos patas.

#### Skarmory base v3, tercera entrega: patas de vuelo (2026-10-14), pendiente de aprobación
El usuario aprueba el resto y señala que las patas estaban colocadas como en tierra. En la carta, las patas van plegadas contra el
cuerpo y no se ven las cañas, solo los pies cerrados: uno bajo el vientre, con las garras hacia abajo, y otro detrás, en la raíz de
la cola.

Solo se ven los dedos de cada pie original, sin la caña (isFrontFoot e isBackFoot):
- **Pie delantero:** girado 55° y con el tobillo pegado bajo el vientre (FRONT_FOOT). Se dibuja delante del cuerpo.
- **Pie trasero:** girado 40° y con el tobillo en la raíz de la cola (BACK_FOOT). Se dibuja detrás del cuerpo.

El resto de la pose no cambia.

[Verificado] 70×62, 1710 píxeles opacos en una sola pieza (8 vecinos), iso 0, holes 0 y solo la paleta original.

### Skarmory: base aprobada, vuelo animado e integrado (2026-10-14), pendiente de aprobación
El usuario aprueba la base v3 con las patas plegadas (commit 9b13c6c). Queda congelada en
tools/critters/skarmory_flight_approved.json (70×62, origen en el lienzo del generador [48, 39]).
SHA256: 0225669839f5d7c64f6d21e8cc205e19972271c8c8a4ef3b3e218f3f814cad3a.
skarmory_flight_pose_v3.js expone ahora compose({near, far}), la misma pose con cada ala girada unos grados más sobre su raíz.
build() sigue dando exactamente la base aprobada.

**Animación** (skarmory_approved_flight.js): 12 fotogramas a 10 fps, ciclo de 1,2 s.
- Las cuchillas de acero son rígidas: cada ala gira entera desde su raíz al estilo RotSprite, a partir de las piezas de la base, y
  ninguna se dobla.
- Las dos baten a la vez: la cercana baja de levantada hacia horizontal (hasta 32°) y la lejana baja más bajo el pecho (hasta 18°).
  Después vuelven.
- El cuerpo sube un píxel en el golpe hacia abajo. Cabeza, cuello, cuerpo, cola y pies no cambian.
- El fotograma 0 es la base aprobada píxel a píxel, y el script lo comprueba.
- Todo el ciclo usa una sola caja de 78×68, a escala 1 y sin reducir.

skarmory_flight_anim.js apunta a este módulo; build_batch.js e integrate_entries.js integran la hoja.
- Entrada en CapCritterArt.cs: Fps 10, Cell 82, Angles [-2, 0, 2], Height 60.
- Drift, Bob y Surge de vuelo: 1, 0,3 y 0,6.

[Verificado]
- iso 0 y holes 0 en los 12 fotogramas y en las 36 celdas, con una sola pieza en cada uno.
- El ojo (3 píxeles amarillos) aparece en las 36 celdas.
- Cielo previsto y0–67, dentro de 0–68. Cierre del bucle con 1253 píxeles distintos, por debajo del máximo interior.
- Build del laboratorio: 0 errores. Tests App: 555/555.
- --medir: Party media 1,46–1,73 ms, Deep 0,70–0,92 ms.
- Recorrido del laboratorio revisado a tamaño real: vuela hacia la derecha en la banda del cielo.

Entrega: C:/Users/javie/Desktop/imagenes/revision/skarmory_vuelo/
- skarmory_bucle_fijo.gif y skarmory_fotogramas.png: ciclo ×4.
- skarmory_ciclo_tamano_real_recorte.gif y skarmory_en_el_layout_tamano_real.gif: laboratorio a 12 fps.
- skarmory_hoja_definitiva.png, skarmory_rostros_36_celdas.png, auditoria.json y rendimiento.txt.

Desde tools/critters: node build_batch.js skarmory; node review_skarmory_flight.js; node integrate_entries.js.

Limitaciones: el cielo va justo (y0–67). Las alas no se flexionan porque son de acero. Pendiente de aprobación visual; Dragonite y
Salamence esperan.

### Skarmory aprobado; Dragonite: base de vuelo v3 (2026-10-14), pendiente de aprobación
El usuario aprueba el vuelo de Skarmory (skarmory_vuelo, commit c95d23c). Sigue Dragonite con el mismo método: primero SOLO la base
junto a su referencia.

Referencia de postura: carta Dragonite 5 de las Wizards Black Star Promos (1999, Toshinao Aoki), vista en cardcodex.com. Muestra a
Dragonite de perfil, volando nivelado hacia la izquierda:
- Cabeza delante.
- Ala cercana verde levantada sobre la espalda y ala lejana marrón detrás.
- Brazo hacia delante y abajo.
- Piernas atrás y cola recta detrás.

La bolsa de correo no se dibuja. Se descartaron tres cartas: Fossil 4/62 (sentado), Tag Bolt 065/095 (de frente) y Miracle Twin
068/094 (boca arriba).

**Método** (tools/critters/dragonite_flight_pose_v3.js): piezas del original de pie y de frente, a escala 1 y sin reducir.
- **Torso, brazo izquierdo y pierna izquierda:** giran un cuarto exacto (90°, sin remuestreo), así que la barriga queda abajo, el
  brazo cuelga delante y la pierna va atrás. Las rayas de la barriga quedan verticales, como en la carta.
- **Mano derecha:** se quita, porque con el giro saldría por la espalda y de perfil queda tras el cuerpo.
- **Pie derecho:** se traslada abajo, junto al otro, detrás del cuerpo.
- **Cabeza:** queda derecha delante, con las antenas.
- **Ala derecha del original:** es el ala cercana, girada 15° sobre la espalda. El ala lejana es la misma girada 55°, más
  adelantada y con la cara exterior en naranja de sombra (D→P, G→F, O→A, E→M, L→I). El ala izquierda pequeña del original no se usa.
- **Cola:** girada 52°, sale recta hacia atrás desde la parte trasera.
- Los poros de hasta 4 píxeles toman el color de sus vecinos, como en Skarmory.

[Verificado] 106×68, 2543 píxeles opacos en una sola pieza, iso 0, holes 0 y solo la paleta original.

Dudas para la revisión:
- Las alas conservan su tamaño original, más pequeño que en la carta.
- El hombro derecho, sin mano, queda como un bulto en la espalda entre las alas.
- El torso es redondo, como el original.

Entrega: C:/Users/javie/Desktop/imagenes/revision/dragonite_pose_v3/ (dragonite_pose_y_referencia.png, dragonite_base_ampliada.png,
dragonite_base_nativa.png, .txt/.json, auditoria.json y referencias/). Desde tools/critters: node review_dragonite_pose_v3.js. Sin
integrar: la app sigue con el Dragonite de la tanda 2 rechazada.

#### Dragonite base v4: construida en lugar de girada (2026-10-14), pendiente de aprobación
El usuario rechaza la v3: no se ven la mano ni la pata izquierdas, la cola y las alas están raras, y la unión de la cabeza con el
cuerpo también. La causa común es que girar un cuarto el torso de un sprite de frente no da una anatomía de perfil.

Cambio de método, como en la base aprobada de Charizard (tools/critters/dragonite_flight_pose_v4.js; la v3 queda como histórico):
- **Construido de nuevo con la paleta original:** torso (elipse horizontal), cuello, cola gruesa que se afila hacia atrás, brazo de
  dos segmentos y dos piernas de muslo y caña.
- **Sombreado por volumen:** contorno H, banda iluminada M arriba, base C y sombra N abajo. La pierna lejana es un tono más oscura.
- **Vientre:** crema a lo largo de la parte de abajo, con las rayas de los segmentos de través (I/L/E) y una franja fina bajo la
  raíz de la cola.
- **Piezas del original a escala 1:**
  - La cabeza hasta la garganta, con las dos antenas.
  - La mano izquierda con sus garras blancas, girada hacia delante y abajo.
  - El pie izquierdo en espejo, con los dedos atrás, para las dos piernas; el lejano, en sombra.
  - El ala derecha del original como ala cercana, levantada 20°; la lejana es esa ala girada 52°, con su cara exterior en
    naranja de sombra.

[Verificado] 102×65, 2530 píxeles opacos en una sola pieza, iso 0, holes 0 y solo la paleta original.

Entrega: C:/Users/javie/Desktop/imagenes/revision/dragonite_pose_v4/. Desde tools/critters: node review_dragonite_pose_v4.js.
Sin integrar.

#### Dragonite base v4, segunda entrega: proporciones del original (2026-10-14), pendiente de aprobación
El usuario acepta la postura de vuelo y pide guiarse por las proporciones del sprite original. Señala cuatro fallos: manos y patas
mal proporcionadas, cuello hinchado, alas que deben ir algo más arriba del cuerpo y una cola distinta de la original.
- **Cuello:** pasa de polígono ancho a un tramo de radio 5, más estrecho que la cabeza, como en el original.
- **Brazo:** corto y grueso (unos 8 px), en dos tramos, con la mano original de garras blancas.
- **Piernas:** muslo grande y redondo (9×8), caña muy corta y el pie original en espejo; la lejana, en sombra.
- **Cola:** unos 17 px de grosor en la raíz y se afila poco a poco hasta la punta. Pintada como la original: arriba iluminada,
  columnas alternas P/N y la parte de abajo marrón F/I.
- **Alas:** las raíces suben 2–3 px y las alas se alzan sobre la espalda sin despegarse de ella.
- **Lienzo:** gana 12 filas por arriba (TOP) para que el ala lejana levantada no se corte. Se quitan las migas de menos de 6
  píxeles que quedan sueltas de los bordes de las piezas.

[Verificado] 103×66, 2583 píxeles opacos en una sola pieza, iso 0, holes 0 y solo la paleta original.

#### Dragonite base v5: la lista de proporciones del usuario (2026-10-14), pendiente de aprobación
El usuario pasa una lista de correcciones sobre la v4 (tools/critters/dragonite_flight_pose_v5.js; la v4 queda como histórico):
- **Torso:** compacto y redondo (elipse 18×16).
- **Vientre:** crema ancho con bandas horizontales: una línea I cada 3 filas, crema L y E en la parte baja.
- **Cabeza:** la original, a su tamaño, ligeramente levantada (girada 6°), con sus antenas.
- **Cuello:** corto y grueso (tramo de radio 7).
- **Cola:** gruesa en la raíz, se afila y se curva un poco hacia arriba, con la punta en crema (L/E).
- **Patas traseras:** muslos grandes y cañas estiradas hacia atrás, con las garras originales blancas.
- **Brazo:** recogido bajo el pecho, con la mano original.
- **Alas construidas:**
  - La verde es grande y nace de la espalda: hueso naranja en el borde de ataque, tres dedos y membrana D/G/O, más oscura hacia
    el borde de salida festoneado.
  - La marrón es pequeña y va detrás, pegada a la espalda (F/P/A).

La lista pedía mirar a la derecha «como en la referencia», pero la referencia vuela hacia la izquierda. Se mantiene la izquierda,
como en todas las bases: la app voltea el sprite cuando cruza hacia la derecha.

[Verificado] 106×67, 2727 píxeles opacos en una sola pieza, iso 0, holes 0 y solo la paleta original.
Entrega: C:/Users/javie/Desktop/imagenes/revision/dragonite_pose_v5/. Desde tools/critters: node review_dragonite_pose_v5.js.

#### Dragonite base v6: el original inclinado (2026-10-14), pendiente de aprobación
El usuario rechaza la v5 («peor que la anterior») y, entre tres opciones (volver a la v4, inclinar el original o dejar Dragonite),
elige inclinar el original. tools/critters/dragonite_flight_pose_v6.js conserva el sprite casi entero y lo inclina hacia delante.
- **Cuerpo:** gira 55° como una sola pieza (RotSprite a escala 1). Brazo, alas, vientre con sus bandas y garras conservan su dibujo y
  sus proporciones; las bandas quedan en diagonal.
- **Cabeza:** inclinada 30° (25° menos que el cuerpo), para que mire hacia delante.
- **Pierna izquierda:** gira 65° más que el cuerpo para quedar atrás, junto a la derecha, que ya queda atrás con la inclinación.
- **Cola:** se separa y gira para salir hacia atrás.
- **Piezas quitadas:** la mano derecha, que quedaría encima de la espalda, y el ala pequeña izquierda, que colgaría bajo la barbilla.
  De perfil, las dos quedan detrás del cuerpo.

[Verificado] 110×73, 2475 píxeles opacos en una sola pieza, iso 0, holes 0 y solo la paleta original.
Entrega: C:/Users/javie/Desktop/imagenes/revision/dragonite_pose_v6/. Desde tools/critters: node review_dragonite_pose_v6.js.

#### Dragonite base v6, segunda entrega (2026-10-14), pendiente de aprobación
El usuario aprueba el camino. Pide inclinar las alas un poco hacia la derecha y corregir una mano cortada y una pata cortada.
- **Ala cercana:** gira por su cuenta 10° más hacia atrás que el cuerpo, desde el hombro.
- **Mano cortada:** era la derecha. Al quitarla quedaba el brazo sin mano; vuelve con el cuerpo.
- **Pata cortada:** era la izquierda, girada aparte desde la caña y partida en la cadera. Vuelve con el cuerpo; las dos piernas y las
  dos manos ya no se separan.
- **Lienzo:** más margen (AT [78, 56], ancho 160), porque la mano izquierda se recortaba en el borde y luego la punta de la cola.

[Verificado] 110×81, 2608 píxeles opacos en una sola pieza, iso 0, holes 0 y solo la paleta original.

#### Dragonite base v6, tercera entrega (2026-10-14), pendiente de aprobación
El usuario pide levantar un poco la cabeza, para que no mire hacia abajo, y arreglar un corte en la cola.
- **Cabeza:** inclinada 15° en lugar de 30° (HEAD_TURN 40 frente a LEAN -55), así que mira al frente, un poco hacia arriba.
- **Cola:** la raíz tocaba el cuerpo en el original y ese borde no tenía contorno; girada aparte, quedaba abierta. Ahora todo píxel
  de la cola que queda junto al fondo toma el contorno negro H.

[Verificado] 107×81, 2558 píxeles opacos en una sola pieza, iso 0, holes 0 y solo la paleta original.

#### Dragonite base v6, cuarta entrega (2026-10-14), pendiente de aprobación
El usuario señala que la cola se une al cuerpo demasiado cerrada: un cuello estrecho. Ahora se dibuja encima del cuerpo, con la
raíz gruesa metida 4 px hacia delante y 2 px hacia abajo en la cadera (TAIL_SINK). Tapa el contorno trasero del cuerpo en la unión y
la cola sale de él a todo su grosor. Los bordes de la cola junto al fondo conservan el contorno negro.

[Verificado] 103×81, 2517 píxeles opacos en una sola pieza, iso 0, holes 0 y solo la paleta original.

#### Dragonite base v6, quinta entrega: cola con la raíz del original (2026-10-14), pendiente de aprobación
El usuario aclara que en el original la cola empieza casi tan ancha como el cuerpo, y separarla y girarla la estrechaba. Ahora la
cola se inclina con el cuerpo, así que la raíz queda pegada tal como en el original y a su anchura. Después se curva progresivamente
hacia atrás, dibujando hacia atrás (bentTail), la misma técnica de Mantine y Qwilfish:
- Ningún giro a menos de 2 px de la raíz (punto 45,58 del original).
- Hasta 112° en el sentido de las agujas del reloj a 20 px, con transición suave, de modo que sale hacia atrás y la punta se alza
  un poco.

Se dibuja detrás del cuerpo, como en el original. Desaparecen TAIL_TURN y TAIL_SINK.

[Verificado] 103×81, 2583 píxeles opacos en una sola pieza, iso 0, holes 0 y solo la paleta original.

#### Dragonite base v6, sexta entrega: solo el nacimiento de la cola (2026-10-14), pendiente de aprobación
El usuario rechaza la cola curvada de la quinta entrega: solo había que cambiar el nacimiento y el resto de la cola estaba bien. Se
recupera la cola de la cuarta entrega (201a148: girada aparte y con la raíz metida en la cadera). TAIL_ROOT_FILL rellena la raíz
desde la línea de la espalda hasta la cadera, detrás del cuerpo y de la cola, con las columnas naranjas P/N de la cola y contorno H
donde da al fondo. Así la cola nace casi tan ancha como el cuerpo, como en el original, y el resto no cambia.

[Verificado] 103×81, 2597 píxeles opacos en una sola pieza, iso 0, holes 0 y solo la paleta original.

### Dragonite: base v6 aprobada, vuelo animado e integrado (2026-10-14), pendiente de aprobación
El usuario aprueba la base v6 en su sexta entrega (commit a950fe3). Queda congelada en tools/critters/dragonite_flight_approved.json
(103×81, origen [34, 23] en el lienzo del generador).
SHA256: b3a73389d383296844ef64d4226ad55c508066ddc09862f103bab73c0fb7389a.
dragonite_flight_pose_v6.js expone compose({wing}), la misma pose con el ala cercana girada unos grados más sobre el hombro.
build() sigue dando exactamente la base aprobada.

**Animación** (dragonite_approved_flight.js): 8 fotogramas a 12 fps, ciclo de 0,67 s.
- El ala es pequeña, así que bate corto y rápido: gira entera desde el hombro (RotSprite) hasta 34° hacia atrás y vuelve.
- El cuerpo sube un píxel en el golpe hacia abajo. Cabeza, cuerpo, brazos, piernas y cola no cambian.
- El fotograma 0 es la base aprobada píxel a píxel, y el script lo comprueba.

**Tamaño en pantalla.** El original inclinado mide 82 px de alto, más de lo que cabe en el cielo, así que el panel lo recibe al 75 %
por vecino más próximo (78×62). Se hace como con Charizard al 60 %, y el dibujo nativo queda en nativeFrames.
- **Ojo:** la reducción se hace sobre los fotogramas sin balanceo y el balanceo se aplica después. Así se toman las mismas filas de
  la cara en todos los fotogramas y el blanco del ojo no desaparece.
- **Píxeles sueltos y huecos:** se limpian los de 1 px.
- **Antena:** junto a la cabeza, los píxeles unidos solo por la esquina reciben un vecino lateral. Si una parte fina queda separada
  (el rizo de la antena), se une con un puente escalonado de su color, para que las inclinaciones de ±2° del panel no la rompan.

skarmory y dragonite siguen el mismo esquema: <especie>_flight_anim.js apunta al módulo, y build_batch.js e integrate_entries.js
integran la hoja.
- Entrada en CapCritterArt.cs: Fps 12, Cell 82, Angles [-2, 0, 2], Height 60.

[Verificado]
- iso 0 y holes 0 en los 8 fotogramas nativos y de pantalla y en las 24 celdas, con una sola pieza en cada uno.
- El ojo aparece en las 24 celdas (2 píxeles blancos).
- Cielo previsto y4–64. Cierre del bucle con 917 píxeles distintos, por debajo del máximo interior.
- Build del laboratorio: 0 errores. Tests App: 555/555.
- --medir: Party media 1,51–1,69 ms, Deep 0,69–0,97 ms.
- Recorrido del laboratorio revisado a tamaño real.

Entrega: C:/Users/javie/Desktop/imagenes/revision/dragonite_vuelo/
- dragonite_bucle_fijo.gif: pantalla ×4.
- dragonite_nativo_bucle_fijo.gif: nativo ×3.
- dragonite_ciclo_tamano_real_recorte.gif y dragonite_en_el_layout_tamano_real.gif.
- Hoja, rostros, auditoria.json y rendimiento.txt.

Desde tools/critters: node build_batch.js dragonite; node review_dragonite_flight.js; node integrate_entries.js.

Pendiente de aprobación visual. Salamence espera.

### Dragonite aprobado (2026-10-14)
El usuario aprueba el vuelo de Dragonite (dragonite_vuelo, commit 253ccf1). Queda Salamence, último de la tanda de vuelo 2.

### Salamence: base de vuelo v3 (2026-10-14), pendiente de aprobación
Último de la tanda de vuelo 2. Planea con las alas fijas, sin batirlas.

Primero se buscó una referencia de planeo de perfil en cardcodex.com y se eligió la carta japonesa Sky-Splitting Charisma 065/096
(Mitsuhiro Arita). Sobre ella se empezó una propuesta con la cola atrás y las patas plegadas, que **no se entregó**: el usuario aportó
otra referencia (una imagen de Salamence en vuelo, de tres cuartos hacia la izquierda, boca abierta, ala lejana alzada detrás, ala
cercana baja delante, patas colgando y cola curvada detrás) con la indicación: «hazlo de nuevo usando esta imagen como referencia, lo
único que tienes que hacer es bajarle un poco las alas». Esa postura es la del sprite original. La carta y las 13 descartadas, con el
motivo de cada una, constan en referencias/fuentes.json.

**Método** (tools/critters/salamence_flight_pose_v3.js): el original entero a escala 1. Solo cambian las alas.
- Las dos alas forman una masa roja continua. Se separan del resto con edge(y), la línea del hueso blanco y gris del borde de ataque
  del ala grande, y las máscaras de cresta y boca (isCrestOrMouth). El ala de delante es el rojo por delante de esa línea, junto a la
  cabeza, con la punta bajo la barbilla. El ala grande es todo lo que queda detrás de la línea, contorno y huesos incluidos.
- Girarlas por separado (la grande hacia abajo y la de delante hacia el otro lado) abría un hueco de cielo entre ellas y dejaba el ala
  de delante con un corte recto. Por eso bajan **juntas, como una pieza rígida**: 9° en sentido horario sobre el hombro (SHOULDER
  [45, 38]) y 5 px más abajo (WINGS_TURN, WINGS_DROP), dibujadas detrás de la cabeza y el cuerpo, al estilo RotSprite.
- El contorno oscuro del ala de delante se mueve con ella donde da al cielo (isNearOutline). Donde toca el cuello se queda, para que el
  cuello conserve su borde. Todo píxel rojo de ala que queda junto al cielo toma el contorno H.
- Rellenado de poros de hasta 4 px, eliminación de migas de menos de 6 px (desaparecen las del original en las filas 69-71) y recorte
  con 2 px de margen.
- compose({ wings }) suma grados a las dos alas, para un leve balanceo de planeo en la fase 2. build() da la base.

[Verificado] 85×71 (original 79×72), 3507 píxeles opacos en una sola pieza (8 vecinos), iso 0, holes 0 y solo la paleta original.
2852 de los 3660 píxeles del original siguen exactamente en su sitio; el resto son las alas movidas y las migas quitadas. Estas pruebas
no demuestran que la pose sea artísticamente correcta.

Entrega: C:/Users/javie/Desktop/imagenes/revision/salamence_pose_v3/
- salamence_pose_y_referencia.png: referencia | original ×3 | nueva base ×3.
- salamence_base_ampliada.png (×6) y salamence_base_nativa.png.
- salamence_base.txt/.json, auditoria.json y referencias/ (referencia_usuario_vuelo.png, la carta y fuentes.json).

Desde tools/critters: node review_salamence_pose_v3.js. Sin build_batch.js ni integrate_entries.js: la app sigue con el Salamence de
la tanda 2 rechazada.

Siguiente paso: esperar la aprobación visual de esta base; después, la fase 2 (planeo con alas fijas).

### Salamence descartado (2026-10-14)
El usuario descarta Salamence: no se aprueba la base v3 ni se anima. La app sigue mostrando la hoja de la tanda 2 rechazada (410e8d0) en la
ruta 14 del cielo hasta que se decida qué hacer con él (quitarlo de la ruta o dejar esa hoja). Los scripts salamence_flight_pose_v3.js y
review_salamence_pose_v3.js quedan como histórico.

### Salamence fuera de la ruta y Pidgey de vuelo integrado (2026-10-14)
- **Salamence (373):** sale de la ruta 14 de los voladores en CapVisitors.cs, así que no aparece. Su entrada y su hoja siguen en el
  catálogo, sin uso. CapVisitorLabTests usaba el 373 como ejemplo de lote con cuatro voladores; ahora usa Dragonite (149).
- **Pidgey (16):** build_batch.js lo incluye en FLIGHT, así que usa pidgey_flight_anim.js, intacto desde 8da0848. La hoja integrada es
  idéntica píxel a píxel a revision/vuelo_piloto/detalle_pidgey/pidgey_hoja_propuesta.png (624×234, celda 78, inclinaciones -4/0/4,
  12 fps). Entrada: Height 58, Drift 1, Bob 0,3, Surge 0,6, como los demás voladores corregidos.
- [Verificado] Build del laboratorio: 0 errores. Tests App: 555/555. Pidgey revisado en el cielo del laboratorio, en las dos direcciones.
- Desplegado **solo** en la carpeta de PermaLocke singleplayer: `dotnet build src/PermaLocke.App`, con la app y Azahar cerrados. Es el
  ejecutable al que apunta el acceso directo PermaLocke singleplayer.lnk. Sin push y sin tocar PermaLocke prueba ni para amigos.

### Todos los visitantes a 2/3 y el doble de frecuentes (2026-10-14)
El usuario prueba los sprites en la app y pide reducir TODOS a 2/3 y que aparezcan más a menudo. Se hace al pintar, sin regenerar hojas: CapVisitors.Swimmer y ProwlSheet (Sharpedo) dibujan cada celda a ceil(celda × 2/3) tomando el píxel de la hoja por vecino más próximo (Shrink, Shrunk, Source). La altura reservada (Height) se escala igual. Los periodos de los seis carriles bajan a la mitad (mar 34/48/150 s, cielo 22/60/165 s); duración y probabilidad no cambian. La caché de hojas (SheetOf) pasa a usar un lock: un test de determinismo falló una vez con varios hilos pintando a la vez. Tests App 555/555 (tres pasadas). Limitación: a 2/3 por vecino más próximo, los detalles de 1 px (ojos, antenas, garras) pueden perderse en algún fotograma; si alguno se nota, habría que regenerar esa hoja a su nuevo tamaño.

### Layout del cap: cartel del cap, arrecife con vida, sin banco ni ola, Spearow y reducción (2026-10-15)
El usuario pide, tras probar los sprites a 2/3 (commit 43f2a03):
1. **Cartel del cap** (idea 4 de las propuestas, solo el cartel): CapSign.cs. Un poste con tabla clavado en la arena a la izquierda del
   suelo del panel del equipo. Dice «CAP ACTUAL» y debajo «LVL» pequeño y el cap en letras grandes (escala 2, o 1 si no cabe). Con el
   equipo en el cap, el número lo rodea un halo dorado que respira (mood Cap). Solo en modo Columna y solo si cabe entre el borde y las
   seis imágenes (44 columnas de ancho con escala 2); si no cabe, no se pinta. Los corales se apartan de él (CapSign.Extent).
2. **Sin el banco de peces en V ni la ola de luz vertical**: se quitan School y LightWave de CapFauna.cs y los escenarios «banco» y «ola»
   de CapScenarios.cs. Los demás sucesos raros mantienen su número de clase, así que no cambia cuándo aparece ninguno.
3. **Corales y vida en la arena** (CapReefLife.cs), todo del mundo Pokémon y pintado a escala 1 con la paleta del mar:
   - Coral de Corsola: tronco rosa que se bifurca y puntas claras, a veces en colonia con otro más joven al lado; se mece con la corriente.
   - Abanico de coral morado con costillas.
   - Un Staryu tumbado con su núcleo rojo que late.
   - Un Shellder que abre y cierra la concha sobre una perla.
   - Una Poké Ball medio enterrada con el botón que brilla de vez en cuando.
   Cada uno ocupa una casilla de 19 columnas del mar único, calculada con la semilla y la columna del mundo, así que las dos ventanas
   coinciden. Se apartan de los fósiles y del cartel, sueltan burbujas y no se pintan en banda ni compacto.
4. **Spearow, cabeza-cuerpo** (spearow_flight_anim.js): el contorno negro del propio mentón se quedaba dentro del dibujo como una barra
   negra entre la garganta roja y el pecho. Esos píxeles negros interiores (filas 26–29) pasan al marrón oscuro del pecho; el contorno
   exterior sigue. Medido: el dibujo nativo ya era una sola pieza, así que lo que se leía como corte era esa barra.
5. **La reducción a 2/3 cortaba uniones finas** (CapVisitors.Pick): con vecino más próximo, 31 especies perdían alguna unión
   (cuello, antena, cola) en 4-vecinos. Ahora un píxel de salida se enciende si el dibujo cubre al menos el 30 % del parche de hoja que
   tiene debajo, y toma el color que más lo llena (un contorno oscuro cuenta una vez y media). Con ese criterio ninguna especie pierde
   una unión salvo 8 cuyas hojas ya venían partidas en trozos de pocos píxeles.

[Verificado] Build del laboratorio sin errores; pruebas App 555/555; capturas del laboratorio de ambos paneles, del cartel con los datos
del escenario (cap 42) y de Fearow, Spearow y Dragonite cruzando a 2/3.
Limitaciones: el halo del cartel es sutil; en modo Delgado o denso no hay cartel por falta de sitio.

#### Cap panel, 2026-10-15 (segunda tanda)
El usuario quita la aurora del cielo y no queda convencido de los corales y objetos de CapReefLife. Se borra Aurora de CapFauna.cs (el cielo ya no lleva cintas verdes y doradas con el equipo en el cap). CapReefLife.cs deja de pintarse (sigue en el repositorio, sin llamada en CapWorldView.Paint) hasta que se elija una de las cuatro opciones del prototipo imagenes/revision/prototipo_arena: A tesoros del fondo, B ruinas, C pecio, D nido de huevos. El cartel del cap se queda. Pruebas App 555/555.
