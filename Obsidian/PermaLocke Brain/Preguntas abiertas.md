---
tipo: pendiente
revisado: 2026-09-24
---
# Preguntas abiertas

Lo que sigue sin saberse o sin verse. La tabla «Lo que NO está resuelto» de `CLAUDE.md` tiene el estado por funcionalidad; aquí van los huecos concretos.

## Sin confirmar jugando
- Por qué se retrasó la muerte del amigo (tablas, PS del equipo o límite de 6 s de la barra) y cómo va la r3 en su PC.
- Tablas de combate en **entrenadores, dobles y SOS**: solo se ha medido el salvaje individual (§114, `BattlePokemon`).
- Regla de primer encuentro entera en una partida y con un variocolor real (§117).
- Dominantes dentro de rutas normales: Ultraganga abandonado y Cañón de Poni siguen sin medir (§160). La Jungla Umbría ya está cerrada hasta el cristal (§179), pero sin jugar.
- La escena de los iniciales (§146), las tiendas de evolución (§145) y los combates importantes y megas (§157), sin ver en el juego.
- En qué mostrador exacto se vende la Moneda de Gimmighoul (se cree que en el 22, el Supermercado Ultraganga del centro).

## Resuelto (se deja para no volver a preguntar)
- 2026-09-24, dicho por el usuario: **el amigo ya no tiene cierres** de Azahar con el parche 4 (§173).
- 2026-09-24, dicho por el usuario: **Gimmighoul necesita 999 Monedas de Gimmighoul** para evolucionar, aunque la tabla de evoluciones solo diga método 8 (usar objeto) con arg 994. La tabla no dice la cantidad: la pone el código del mod. Con la moneda a 30, son 29.970 en total.
- Si las bayas que caen al sacudir un árbol salen de la tabla randomizada.

## Visto y sin arreglar
- El «#822» del cementerio ya no se ve: se quitó el bloque de datos (§177). La causa probable, nombres de gen 8-9 por PKHeX, sigue en `CemeteryViewModel` sin pintarse.
- Hay un Huevo Malo en la partida guardada vieja (hueco 3, del §97), sin reparar a petición del usuario. Puede ser histórico si ya se empezó de cero.

## Del código
- `Data/zones.json` (40 KB): ningún `.cs` de `src` lo nombra. ¿Histórico (ancla del §23) o lo lee algo por otra ruta? Comprobar antes de tocarlo y **no borrar sin permiso**.
- `Palette.xaml`, `Controls.xaml` e `Icons.xaml` (tema viejo del §74) siguen cargados junto a `Pixel.xaml`. No se sabe qué queda usándolos después de la conversión pixel.
- `SyncView.xaml` (COMPETICIÓN) no está en pixel. Está oculta en la distribución local, pero visible en la de desarrollo.
- Sprites «que no se ven» reportados por el usuario el 2026-09-23: en su carpeta no se reprodujo. Pendiente de que diga qué Pokémon y en qué pantalla.
- **Modelos de la escena de los iniciales** (2026-10-06, para más adelante): el texto ya dice los randomizados (§146),
  pero salen Rowlet, Litten y Popplio. En los guiones del campo (`a/0/9/2`, 15 ficheros: 68, 81, 115, 117, 125, 238,
  282, 387, 423, 440, 441, 535, 536, 537, 722) las especies 722/725/728 solo aparecen en `switch` (Tilo, etc.), no al
  cargar un modelo: la escena parece usar modelos de escena propios. Siguiente: encontrar el guion de elegir, ver qué
  número de modelo usa cada Poké Ball y si es modelo de escena (solo algunos Pokémon) o de Pokémon (todos). Escáner en el
  scratchpad de la sesión (`scan/`); `pk3DS.Core.Script` descomprime los guiones.
  - **Avance 2026-10-06 (con el organizador en la escena):** los tres Pokémon son estructuras de 0x12C bytes seguidas en
    el montón (0x32E5AAC0, 0x32E5ABEC, ... y dos copias más en 0x32E61A..., 0x32E63A...), cada una con `07 01 02 80` y
    justo detrás la ESPECIE (u16 722/725/728) en +0x24: o sea Pokémon por especie, no modelos de personaje (el paquete de
    modelos de mapa `a/0/9/8` solo tiene 18 Pokémon y ninguno de los tres). Ya están al cargar la zona; NO están en ningún
    fichero de la ROM (crudo, GARC ni LZ11), `07010280` no es literal en code.bin ni en los .cro, y la vigilancia de
    escrituras no ve quién la escribe (copias en bloque). Siguiente: Ghidra sobre `FieldRo.cro` (creación de personajes de
    zona) para ver de qué tabla sale la especie. Partida del organizador guardada justo antes de la escena. Sondas:
    `--reglas-juego especies 722 725 728`, `--reglas-juego texto ...`.
  - **Descartado (2026-10-06):** esas estructuras con la especie NO pintan los modelos: cambiadas a 25/4/7 justo antes de la
    escena, salieron Rowlet/Litten/Popplio igual y el juego no las releyó. Casi seguro son los búferes de palabras del texto
    («Ese es [VAR PKNAME]»). Tampoco hay tablas en code.bin/.cro con las tres especies (las que salen son listas ordenadas).
    Lo probable: la escena carga los modelos por número de modelo, no por especie. **Aparcado** por recomendación (horas de
    desensamblar FieldRo.cro sin punto de entrada). Retomar solo con documentación del formato de escenas de USUM.
  - **Cerrado como no viable (2026-10-06):** cambiar la tabla especie→modelo de `a/0/9/4` (fichero 0) no hace nada, y
    rehacer `a/0/9/4` con los ficheros de Rowlet/Litten/Popplio (modelos 1035/1038/1041) sustituidos tampoco (Azahar sí
    cargaba el fichero cambiado). Los tres de la escena son **modelos de campo** (nombres internos `pm0842_00`, `pm0845_00`,
    `pm0848_00`) metidos dentro de 7 escenas de `a/0/8/3` (#36, 124, 322, 740, 894, 1488, 1818; LZ11 → paquete `AC` →
    `CP` con id de personaje 323/325/327 → `CM` con modelo, texturas y animaciones). Es OTRO formato que los de combate
    (no es `gfmodel`), con su propio esqueleto y animaciones de escena, y los Pokémon randomizados no tienen modelo de
    campo en el juego. Hacerlo pediría convertir modelos 3D entre formatos y rehacer animaciones: mucho riesgo de cuelgue.
    Herramientas en el scratchpad de esa sesión: `buscar2/` (busca un nombre en toda la ROM, también dentro de LZ),
    `escena/` (saca una entrada de `a/0/8/3`), `pack/` (árbol de paquetes de dos letras).
  - **Segundo intento, abandonado (2026-10-06, a petición del organizador):**
    - Formatos entendidos y convertidor hecho (scratchpad `iniciales/`): el `CM[0]` de campo es un `GFModelPack`
      (magia 0x00010000, 5 contadores: modelo, texturas, vertex shader, -, fragment shader; tabla de punteros a
      registros «byte largo + nombre + u32 dirección», datos alineados a 0x80) que envuelve el MISMO `gfmodel` que los de
      combate. `CM[1]` es un `GFMotionPack` (u32 273 ranuras, desplazamientos desde +4). Las texturas de combate son RGB8
      (formato 3) en mosaicos de 8×8 en orden Morton; las de campo, ETC1 (0x2A).
    - `a/0/8/3` NO es solo encuentros: entrada `mapa*11 + 3` es el paquete `AC` de personajes de cada mapa (`CP` con id
      de personaje + `CM`). Rowlet/Litten/Popplio son los personajes 323/325/327, en 7 mapas (#36, 124, 322, 740, 894,
      1488, 1818) y en el archivo global `a/2/0/0` (#323/#325/#327). Ojo: la entrada descomprimida más grande del juego
      mide 7.344.640 bytes; pasar de ahí cuelga la carga del mapa (pantalla negra).
    - Probado y sin efecto en la escena: cambiar `a/0/9/4` (modelos de combate 1035/1038/1041), `a/2/0/0` y las 7
      escenas, y todo a la vez. Con la textura del Rowlet de campo a cero, la memoria la tenía negra y en pantalla salía
      normal: lo que se ve no sale de esos paquetes. La textura de combate no está en ninguna memoria legible (la VRAM
      se lee a ceros por RPC) ni hay otra copia en la ROM por nombre ni por contenido; no es vídeo (solo `m/title_*`).
    - Lo único que queda es seguir en Ghidra cómo crea la escena sus Pokémon (`FieldRo.cro`). Muchas horas, sin
      garantía. Mod de prueba restaurado. El registro de lecturas de Azahar (`Service.FS:Trace`) no sale: está quitado
      al compilar.

## Huevos de la guardería (2026-10-07)
- **¿Dónde se ve el huevo como Meltan?** Sin respuesta del organizador (icono de caja, equipo, resumen o modelo 3D). El código del juego da el icono 1153
  (huevo) a cualquier huevo; probablemente es el modelo o la ficha de resumen (hueco 808 compartido con Meltan). Arreglarlo puede exigir parchear el
  juego y afectar a Meltan. Ver §225.
- Sin probar en el juego: que los huevos eclosionen con la Incubadora Turbo en dos pasos y los mensajes de la mochila (§227), que el Repelente
  siga bien, el aviso y el flujo de duplicados (§226), la animación en la app real (§229), eclosión de especies de gen 8-9, y que `HatchedAsync`
  pone la especie al salir del huevo (§230). Las runs con huevos ya apuntados antes de la 1.0.10.1 conservan la especie.
- El organizador debe publicar `roles.json` y `guarderia.json` desde Admin después de que los jugadores tengan la app nueva.

## Avisos en pergamino y placa del cap (2026-10-08, 1.0.14-1.0.15)
- **El aviso de primer encuentro «sale super bugueado»** (dicho por el organizador jugando la 1.0.14). Sin reproducir: el log
  pide y enseña los avisos con normalidad, y la tira de fotogramas en ventana real (`PERMALOCKE_SNAP_LIVE`) se ve bien.
  Pendiente de su captura o descripción. Si llega: mirar escala del monitor, tema GAME BOY (`PixelTheme.Map`) y si el
  aviso se apila con otro (primer encuentro + duplicado + variocolor salen casi a la vez).
- **Hipótesis sin probar (revisor del §239):** una captura estática permitida vista primero sin contador de combate y luego
  con él podría avisarse dos veces (una por la rama vieja con `_battle is null`, otra por `NewsSaid`).
- Sin probar en el juego: el pergamino abriéndose y enrollándose, la placa del cap (tamaño y sitio en pantalla completa,
  ventana pequeña y Vulkan), el aviso de duplicado arreglado.
