# El mod de expansión de gen 8 y 9, medido

Investigación del **Pokemon Expansion Mod** para Ultra Sol / Ultra Luna, hecha el 2026-09-01 para
decidir si PermaLocke puede jugarse encima de él. **Nada de esto está implementado**: es una medida,
no un plan aprobado.

Todo lo que sigue está **medido contra los ficheros reales**, con los mismos lectores que usa el
randomizador. Donde no se ha podido medir, se dice.

---

## 1. Qué es y dónde vive

- Añade **generación 8 y 9** —Pokémon, formas, habilidades y movimientos— a Ultra Sol y Ultra Luna.
  No añade «todas las demás generaciones»: de la 1 a la 7 ya están, y las megas clásicas también.
- Ficha: <https://gamebanana.com/wips/102511>. Marcado **en desarrollo, 83 %**.
- Código y datos: <https://github.com/TheSomewhatUnknownGuy/PokemonUSUMExpansion>, licencia **CC0**,
  activo (último empuje 2026-08-30). Son 67 ficheros, unos 44 MB.
- **El fichero de modelos `a/0/9/4` NO está en el repositorio.** Va aparte por Google Drive. La
  carpeta se llama, literalmente, `ExpansionNoModelAndEncounterFile`.
- Exige **la versión 1.0** del juego. Reemplaza `exefs/code.bin`, `Battle.cro` y unos cuantos `a/...`.
- El autor original (Aqua) lo dejó; sigue gente de la comunidad.
- **El equipo del mod no hace traducciones**: el idioma es el inglés. Cualquier versión en español es
  un parche aparte de la comunidad, y habría que identificar cuál.

### Choca con nosotros en la carpeta

El mod se instala en `load/mods/00040000001B5100/`, que es **exactamente** donde instala el nuestro,
y el nuestro aparta lo que encuentre (§42). No se apilan: para convivir, PermaLocke tendría que
randomizar **tomando el romfs del mod como base** en vez del cartucho, y conservar su `code.bin`,
que nosotros no tocamos nunca. MODO COMBATE (§80) también tendría que aprender a apartar las dos
capas.

---

## 2. Los números

Medido abriendo cada GARC del mod y su equivalente del cartucho con `GarcPatcher`.

| | cartucho | mod | |
|---|---:|---:|---|
| Entradas de especie y forma (`a/0/1/7`) | 976 | 1330 | **+354** |
| Aprendizajes (`a/0/1/3`) | 976 | 1330 | +354 |
| Evoluciones (`a/0/1/4`) | 976 | 1330 | +354 |
| Tabla de megas (`a/0/1/5`) | 976 | 1330 | +354 |
| **Iconos de caja (`a/0/6/2`)** | 1154 | 1508 | **+354** |
| Movimientos (`a/0/1/1`) | 729 | 921 | **+192** |
| Habilidades distintas usadas | 233 | 255 | **+22** |
| Especies con megaevolución | 45 | 93 | **+48** |
| Objetos (`a/0/1/9`) | 960 | 1024 | +64 |
| Iconos de objeto (`a/0/6/1`) | 769 | 849 | +80 |

Y el dato que más vale para nosotros: **la entrada de datos de especie sigue midiendo 84 bytes**,
igual que en el cartucho. `PersonalEntry7` vale tal cual, sin tocar un offset.

---

## 3. Los sprites están completos

Se descomprimieron y decodificaron **los 354 iconos nuevos, uno a uno** (LZ11 + BFLIM, el camino del
§28), contando píxeles opacos:

> **354 con dibujo. 0 en blanco. 0 ilegibles.**

No hay ni un hueco. Los créditos dicen de dónde salen: los proyectos de sprites de Smogon para gen 8
y gen 9, más los de Leyendas Arceus y Leyendas Z-A.

Consecuencia práctica: el trabajo de sprites del §30 **se puede rehacer**, porque el material está.
Lo que no se hereda es la tabla: la del §30 se construyó a mano para 807 especies y 1154 iconos, con
una comprobación que **lanza** si el reparto no cierra. Con 1508 iconos esa cuenta se cae por diseño
y hay que rehacer la investigación entera. Es el grueso del trabajo.

---

## 4. Las megas nuevas: 48

Leídas de `a/0/1/5` con `MegaTrainerRandomizer.ReadForms`, que ya sabe descartar las megas sin
piedra.

Cuarenta son de especie: **Raichu, Clefable, Victreebel, Starmie, Dragonite, Meganium, Feraligatr,
Skarmory, Chimecho, Staraptor, Froslass, Heatran, Darkrai, Emboar, Excadrill, Scolipede, Scrafty,
Eelektross, Chandelure, Golurk, Chesnaught, Delphox, Greninja, Pyroar, Meowstic, Malamar,
Barbaracle, Dragalge, Hawlucha, Crabominable, Golisopod, Drampa, Magearna, Zeraora, Falinks,
Eternatus, Scovillain, Glimmora, Tatsugiri, Baxcalibur.**

Las otras ocho son entradas de forma (índices 1195, 1203, 1214, 1234, 1276, 1315, 1316, 1328).

Además, **Absol, Garchomp y Lucario ganan formas mega adicionales** sobre las que ya tenían.

Son las megas de **Leyendas Z-A** —los créditos incluyen «PLZA sprites»—, y de ahí sale el Mega
Dragonite que se ve en los vídeos.

---

## 5. Las habilidades: el mod se quedó sin sitio, y está medido

Esto explica de forma **estructural** el fallo que la comunidad reporta.

**El campo de habilidad de la tabla de especies es UN BYTE** (offsets 0x18, 0x19 y 0x1A de cada
entrada). Tope 255, y no hay más sitio sin cambiar el formato.

- El cartucho usa **233** habilidades distintas.
- El mod usa **255**. Es decir, metió **exactamente 22 nuevas y chocó con el techo**.
- Las generaciones 8 y 9 trajeron **muchas más de 22**.
- Solo **43 de las 1330 entradas** llevan una de esas 22.

O sea que la inmensa mayoría de los Pokémon nuevos van con **habilidades del cartucho viejo** —una
aproximación—, y las 22 que sí son nuevas son justamente las que el issue #1 del repositorio dice
que no funcionan:

> Los Pokémon con habilidades añadidas por el mod (Hunger Switch, Orichalcum Pulse,
> Protosynthesis/Quark Drive…) **no tienen habilidad funcional después de randomizar**. Se muestra
> el nombre y en combate no pasa nada. Las que ya existían en vanilla funcionan bien.
> — issue #1, abierto el 2026-08-21, sin respuesta

Es el peor tipo de fallo: **no falla, miente**. El nombre sale en pantalla y el efecto no existe. Y
toca de lleno nuestro `randomizeAbilities`.

**Regla que sale de aquí:** si se juega con el mod, las habilidades se cortan en la 233.

---

## 6. Los movimientos: 192 nuevos, partidos en dos

El campo `EffectSequence` (0x10 de cada entrada de movimiento) elige la rutina del motor de combate.

Primer intento de medirlo **salió mal y se detectó solo**: dividir el fichero entre 0x28 daba «801
de 802 movimientos cambiados», que es imposible. El motivo es que `a/0/1/1` no es un array plano
sino un mini-archivo **«WD»**, y había que desempaquetarlo con `Mini.UnpackMini`. Con la lectura
buena, **0 de los 729 movimientos de siempre salen cambiados**, que es la comprobación de que el
alineamiento es correcto. Conviene recordarlo: *la verificación es la que dice si la medida vale*.

De los **192 movimientos nuevos**:

- **160 reutilizan una rutina que el motor ya tiene.** Hacen algo. Otra cosa es que hagan exactamente
  lo que su nombre promete, porque son analogías elegidas a mano.
- **32 piden una rutina que el cartucho no usa nunca** (ids 421 a 478).

**Matiz honesto:** el mod también reemplaza `code.bin`, así que esas 32 rutinas *podrían* estar
implementadas ahí. Saberlo exige desensamblar el ejecutable, que no es una medida sino otro
proyecto. Lo que sí es seguro es que esos 32 dependen del parche de código y los otros 160 no.

**No se puede sacar de los datos qué movimiento está roto.** Un movimiento roto tiene una entrada de
tabla perfectamente normal; lo que falla es el efecto, que vive en el código. El mod tampoco tiene
esa lista: en su FAQ dicen que arreglan movimientos y habilidades **según se los reportan**.

---

## 6 bis. El mod solo trae el texto en INGLÉS, y eso se nota jugando

Medido, y no es lo mismo que «la página está en inglés».

El texto del juego vive en `a/0/3/{idioma}`, un fichero por idioma. **El mod trae únicamente
`a/0/3/2`, que es el inglés.** Cargando cada idioma y pidiendo la lista de nombres de especie:

| idioma | nombres | 807 | 810 | 1025 |
|---|---:|---|---|---|
| inglés (`a/0/3/2`, del mod) | **1026** | Zeraora | Grookey | Pecharunt |
| español (`a/0/3/6`, del cartucho) | **808** | Zeraora | — | — |

O sea que **en español los 354 Pokémon nuevos no tienen nombre en el juego**. La lista se acaba en
Zeraora. Cualquier «versión en español» del mod es un parche aparte de la comunidad, y habría que
identificar cuál.

Dentro de PermaLocke esto **no** es un problema: los nombres los da PKHeX, cuya lista tiene 1026
entradas y conoce a Grookey en español. El problema es del juego.

Y dejó una trampa: `RomTool` indexaba la lista del juego directamente y **reventaba** al pedir el
810. Ahora un nombre que el idioma cargado no tiene sale como `#810`. Decir el número es la
respuesta honesta; dejarlo en blanco o inventar un nombre sería peor.

---

## 7. Los modelos 3D: lo que se puede y lo que no

`a/0/9/4` es el fichero de modelos. **En el cartucho pesa 1.273 MB** —él solo es más de un tercio del
RomFS, que son 3.528 MB en 747 ficheros—. El del mod no está en el repositorio y hay que bajarlo de
Google Drive.

**No se puede contestar «qué modelos están perfectos».** «Perfecto» no es una propiedad de los datos:
exigiría renderizar cada modelo con su esqueleto y sus animaciones y mirarlo, que es el visor 3D que
ya se descartó por tamaño.

**Sí se puede contestar «a qué Pokémon les falta el modelo o las texturas».** `GarcPatcher` solo lee
la cabecera del GARC y luego busca por posición, así que **el índice de un fichero de 1,27 GB se lee
sin cargarlo en memoria**. Con eso se obtiene el tamaño de cada subfichero, y de ahí:

- un subfichero de modelo vacío o mínimo = **no hay modelo**;
- un subfichero de textura vacío = **el Pokémon sale negro**, que es justo uno de los fallos que la
  comunidad reportó y que dicen haber arreglado en enero.

Eso cubre los dos modos de fallo que se han visto de verdad. Lo que no cubre es «se ve raro».

**Coste:** bajar ~1,3 GB o más de Google Drive.

---

## 8. Fallos conocidos, del propio repositorio

Del issue #2 (2026-08-27):

- Se cuelga al quitarle las máscaras a Ogerpon: no se reproduce la animación de transformación.
- Se cuelga (pantalla negra) al hacer fotos en el club del Poké Finder.
- Se cuelga al abrir «Mi Álbum».
- Los Pokémon del mod ganan amistad más despacio con las Habichuelas Arcoíris.
- El icono de Ultra Luna se cambia por el de Ultra Sol.
- La animación del Centro Pokémon enseña **Bulbasaur** en vez del Pokémon que se está curando.
- En Pokémon Refresh los nuevos no reaccionan ni suenan.

De los partes de la propia gente: en enero arreglaron Alcremie, las formas de Ogerpon, los ojos y
Falinks; en septiembre de 2024, las habilidades As One, Commander y No Retreat.

---

## 9. Lo que se puede meter SIN PROBLEMAS

Esto es lo medido y verificado. Nada de aquí depende de una suposición.

1. **Los 354 Pokémon y formas nuevos como encuentros salvajes.** En la tabla de encuentros la especie
   ocupa **11 bits** (`& 0x7FF`, tope 2047) y la forma otros 5. La última especie de gen 9 es la
   **1025**: cabe con holgura y **no hay que tocar el formato**. Además es justo lo que el mod
   deliberadamente no hace —se define como un andamio y no trae encuentros—, así que **el
   randomizador de PermaLocke es la pieza que le falta para poder jugarse como Nuzlocke**.
2. **Los sprites de los 354 nuevos en toda la aplicación** —visor, gacha, wonder trade, tienda—. Los
   iconos existen los 354, comprobados uno a uno.
3. **Las 48 megas nuevas**, en la tienda de megapiedras y en los combates importantes por el módulo
   que ya existe. Se leen de la tabla del mod igual que las 45 de siempre.
4. **Los 64 objetos y 80 iconos de objeto nuevos** en la tienda.
5. **Los aprendizajes y las evoluciones de los nuevos**, que vienen hechos y con el mismo formato.
6. **Toda la capa de partida.** Medido con PKHeX.Core 26.7.7: un PK7 con especie 810 se escribe, se
   relee y **el checksum sale válido**; y `specieslist` tiene **1026 entradas** y conoce los nombres
   en español. O sea que visor, entrega de gacha, wonder trade, EV, PID y récords siguen funcionando.
7. **Los totales base**, porque ya salen de la ROM y no de PKHeX: basta regenerar `species.json`
   contra el `a/0/1/7` del mod y el gacha y el wonder trade trabajan con 1330 entradas solos.
8. **El combate por link entre jugadores sigue siendo posible**, con la misma condición del §80 más
   una nueva: todos con **el mismo mod y el mismo `code.bin`**.

### Lo que hay que restringir, porque está medido que falla

9. **Habilidades cortadas en la 233.** Un número, y esquiva el único fallo confirmado.
10. **Movimientos, en dos niveles**: o todos, o solo los 160 que no dependen del parche de código.
    `MoveTable.Teachable(pp, maxMove)` ya recibe el tope como parámetro, así que es configuración.

### Lo que cuesta trabajo nuestro, por tamaño

11. **Rehacer la tabla icono ↔ especie del §30** para 1508 iconos. El grueso, y es una investigación,
    no un ajuste.
12. **Tres filtros que rechazan lo que pasa de 807** —`Pk7Reader`, `PartyLayout`, `PartyLocator`—.
    Son tres líneas, pero están en el camino que detecta capturas y muertes: mal hecho, es otra vez
    el §68, que no falla y simplemente no cuenta nada.
13. `maxSpecies` en `Data/randomizer.json` y la tabla de generaciones de `WonderTrade`.
14. **Randomizar sobre el romfs del mod** en vez del cartucho, conservando `code.bin`, y enseñárselo
    a MODO COMBATE.

### Lo que no es técnico

Cambiar la base del juego a mitad de competición obliga a **randomizar de nuevo los cinco y empezar
de cero**, todos con la misma versión del mod. Y el mod va por el 83 %, avisa de cuelgues y tiene
fallos abiertos. Para una competición en curso eso se paga; para la siguiente, el encaje es bueno.

---

## 10. Cómo se midió, para poder repetirlo

- Repositorio clonado a un temporal fuera del proyecto. Los ficheros del mod son GARC sueltos, así
  que se abren con `GarcPatcher` directamente, sin ROM.
- Los equivalentes del cartucho se sacaron con `RomFsReader.ExtractTo` a un temporal.
- Iconos: `BflimTexture.Decode` sobre el subfichero descomprimido, contando píxeles con alfa distinto
  de cero.
- Movimientos: `Mini.UnpackMini(garc.Read(0), "WD")` y `Move7`. **Nunca dividiendo el fichero entre
  0x28**, que es lo que salió mal la primera vez.
- Megas: `MegaTrainerRandomizer.ReadForms`.
- Habilidades: los tres bytes de `PersonalEntry7.AbilityOffsets` sobre la copia empaquetada, que es
  el último subfichero.
- Nombres de especie: `GameInfo.GetStrings("es").specieslist` de PKHeX.Core.
