# PermaLocke

Gestor/dinamizador de Nuzlockes para **Pokémon Ultra Luna** (3DS, jugado en el emulador
Azahar). Aplicación de escritorio Windows en C# / .NET 10 / WPF / MVVM.

El nombre del proyecto es **PermaLocke**, siempre y en todas partes: namespaces, assemblies,
carpetas, ejecutables, UI, documentación y comentarios. Nunca `BxnnyLocke` ni ninguna variante.

---

## Reglas absolutas de este repositorio

### 1. `Locke/` es SOLO LECTURA

La carpeta `Locke/` contiene una instalación ajena (BxnnyLocke) con **una partida en curso del
usuario**. Es material de referencia para analizar formatos de datos.

**Nunca escribir, mover, renombrar ni borrar nada dentro de `Locke/`.** Solo lectura.

No es software del usuario: participa en esa competición, no la desarrolla. Por tanto:
- Se pueden analizar sus **formatos de fichero** y su **concepto de arquitectura** (interoperar
  y aprender es legítimo).
- **No** se copia su código, ni se redistribuye su emulador instrumentado, ni sus assets.
  PermaLocke construye su propia instrumentación.

### 2. La ROM vanilla nunca se modifica

Se trabaja siempre sobre copias o, preferentemente, sobre capas de mods (LayeredFS).
Si la randomización falla: limpiar temporales, no tocar el original, registrar en `Logs/`.

### 3. Nada falso

Prohibido: randomización simulada, anti-cheat de mentira, botones que aparentan funcionar,
datos inventados. Si algo no se puede implementar de forma fiable todavía, se deja una
abstracción limpia y se documenta su estado en `docs/ARCHITECTURE.md`.

### 4. Nada de cambios silenciosos de estado

Ningún punto, Pokémon o regla cambia sin generar un `GameEvent` auditable. La UI nunca
escribe `points = X` directamente; pasa siempre por servicios de dominio.

---

## Licencia: PermaLocke es GPLv3

Decisión tomada y no negociable por dependencias: el proyecto usa **PKHeX.Core** (GPLv3) y
**pk3DS.Core** (GPLv3). La GPL es vírica, así que PermaLocke se distribuye bajo **GPLv3** con
el código fuente disponible. Para un proyecto entre amigos no supone ningún problema.

Consecuencia práctica: no se puede integrar código con licencia incompatible.

---

## Estructura

```
PermaLocke/
├── PermaLocke.slnx                 formato de solución XML de .NET 10
├── src/
│   ├── PermaLocke.Core/            dominio + servicios (sin WPF, sin IO concreto)
│   ├── PermaLocke.Rules/           motor de reglas (sin WPF)
│   ├── PermaLocke.Randomizer/      randomización de ROM (sin WPF)
│   ├── PermaLocke.GameLink/        lectura/escritura del juego (save, memoria)
│   ├── PermaLocke.Data/            persistencia: JSON + SQLite + sync
│   ├── PermaLocke.Infrastructure/  logging, hashes, rutas, DI
│   ├── PermaLocke.App/             WPF jugador
│   └── PermaLocke.Admin/           WPF administrador
├── third_party/pk3DS.Core/       pk3DS recortado a net10.0, GPLv3, ver su README
├── tools/PermaLocke.RomTool/     herramienta de desarrollo contra la ROM real
├── tests/
├── docs/ARCHITECTURE.md
├── Data/    Config/    Logs/    ROM/    Randomized/    Saves/
└── Locke/   ← SOLO LECTURA, material ajeno
```

Dependencias permitidas (dirección única):

```
App ─┐
     ├─→ Core ─→ (nada)
Admin┘     ↑
        Rules, Randomizer, GameLink, Data, Infrastructure ─→ Core
```

`Core`, `Rules`, `Randomizer` y `GameLink` **no referencian WPF** bajo ninguna circunstancia.
`Randomizer` depende además de `third_party/pk3DS.Core`, que se recortó a `net10.0` justamente
para que esa norma siga cumpliéndose: el original arrastra WinForms.
`Admin` no duplica lógica de negocio: consume los mismos servicios que `App`.

---

## Convenciones

- **Idioma**: código, tipos y comentarios en inglés. UI y documentación de usuario en español.
- **Configuración fuera del código**: puntos, precios, probabilidades, reglas y logros viven en
  JSON bajo `Data/`. Nada de valores de juego hardcodeados en la UI.
- **MVVM estricto**: cero lógica de negocio en XAML o code-behind. Los ViewModels llaman a
  servicios inyectados.
- **DI**: `Microsoft.Extensions.DependencyInjection` desde el arranque de cada app.
- **Logging**: `Microsoft.Extensions.Logging` a fichero en `Logs/`. Al usuario final no se le
  muestran stack traces salvo en modo avanzado.
- **Async**: toda IO (ROM, save, red, SQLite) es asíncrona. Nada de bloquear el hilo de UI.
- **Tests**: xUnit. Obligatorios en puntos, reglas, gacha (seed reproducible) y eventos.

---

## Estado actual

**Fase 1 completada.** Solución con 8 proyectos + 3 de tests, DI, logging a fichero, shell WPF
con sidebar navegable y HOME. Compila sin warnings y arranca. `Locke/` sigue en el disco como
material de referencia de solo lectura; la ROM vanilla está en `ROM/`.

ROM verificada: `Pokemon Ultra Moon (Europe)`, TitleID `00040000001B5100`, **desencriptada**
(NoCrypto = true), RomFS ~3,7 GB. Ese TitleID es el que necesita la carpeta LayeredFS de Azahar.

**Fase 2 completada.** Creación de runs con detección y validación de la ROM, seed reproducible,
log de eventos encadenado por hash en SQLite, `run.json` por run, servicio de puntos y HOME
conectado a datos reales. 22 tests en verde.

Azahar instalado en `Azahar/`. Trae `scripting/citra.py`, que documenta su **servidor RPC UDP
en 127.0.0.1:45987** con ReadMemory / WriteMemory / ProcessList / SetGetProcess. Es la vía de
integración con el juego: no hace falta forkear el emulador. Ver `docs/ARCHITECTURE.md` §6.

**Fase 3 completada (capa de dominio).** Rule Engine con 7 reglas, modos por regla y
excepciones que se registran en vez de borrar el veredicto que levantan. 59 tests en verde.

RPC de Azahar **verificado contra el juego real**: lectura de memoria funcionando, escritura
aceptada. Cliente en `PermaLocke.GameLink`, sonda en `tools/PermaLocke.Probe`.

**Fase 3 conectada a la interfaz.** `EncounterService` (en `PermaLocke.Rules/Services`, la única
capa que conoce legítimamente el dominio y las reglas a la vez) convierte un intento de captura
en veredictos, un Pokémon persistido y eventos de auditoría. Diálogo de registro en la App con
evaluación en vivo. Nombres de especie desde **PKHeX.Core** (807 especies, español). 73 tests.

Limitación conocida durante mucho tiempo, **ya resuelta el 2026-08-22**: `LevelCapRule` estaba
implementada y testeada pero `RuleContext.LevelCap` era siempre null porque no había seguimiento de
etapa. Ahora las doce pruebas se cuentan solas por su cristal Z, así que la etapa **se deduce** de
los logros. Ver §49 y el apartado del final.

**Lectura en vivo del juego resuelta y verificada.** El equipo se localiza barriendo heap y
linear y ofreciendo cada offset a PKHeX; se encontró en `0x330128E4`, estable tras reiniciar el
emulador. `AzaharGameStateProvider` cachea la dirección, revalida en cada lectura y vuelve a
barrer si deja de cuadrar. `GameLinkMonitor` consulta cada 3 s y HOME muestra el equipo real.

Trampa documentada: hay que llamar a `SetGetProcess` antes de leer. Sin eso el servidor responde
igual con datos que no son del juego, y solo se ve en el log del emulador.

**Detección automática en marcha.** `GameWatcher` compara el equipo del juego con lo registrado
en la run, emparejando por **PID** (sobrevive a motes, subidas de nivel y evoluciones). Las
muertes se registran solas; los Pokémon sin registrar se anuncian en HOME y abren el diálogo de
captura prerrellenado, **zona incluida**.

Dos fallos que solo aparecieron probando contra el juego real, ambos corregidos y con test:
1. El buscador exigía igualdad exacta del nombre de entrenador y descartaba el equipo del propio
   jugador (la run decía `Grenin`, el juego `Grenin430`). Ahora es preferencia, no requisito, y
   HOME avisa de la discrepancia.
2. Basura del heap pasaba por Pokémon. El filtro definitivo es `PK7.ChecksumValid`; los límites
   de PS siguen haciendo falta aparte, porque las estadísticas de combate quedan **fuera** de la
   zona cubierta por el checksum.

**Fase 7-8: randomizador desbloqueado.** La randomización por LayeredFS está verificada en el
juego. pk3DS.Core se vendoriza recortado y compila como `net10.0` sin WinForms; el extractor de
RomFS es propio. Norma que salió de dos fallos reales: **se parchean bytes en su sitio, no se
regeneran estructuras con los escritores de pk3DS**, y todo fichero se relee y se verifica antes
de publicarse. Detalle completo en `docs/ARCHITECTURE.md` §19.

**Randomizador completo (fases 7-8).** Seis módulos: salvajes, objetos del suelo, iniciales y
fósiles, entrenadores, datos de Pokémon y tiendas. Motor en `PermaLocke.Randomizer`, herramienta
en `tools/PermaLocke.RomTool` y sección en la App. Salvajes y datos de Pokémon verificados en el
juego; el resto genera y se relee bien, pero falta probarlo jugando. Detalle en
`docs/ARCHITECTURE.md` §19-21.

**Mochila localizada por su estructura y verificada (2026-08-19).** El botón de Caramelos Raros
de MISCELÁNEA no era de fiar: buscaba el valor empaquetado del objeto por toda la memoria y daba
falsos positivos —con cero caramelos encontraba uno—, además de ser incapaz de añadir un objeto
que el jugador no lleva, porque entonces no hay hueco que encontrar.

Ahora se localiza **el bloque**: los siete bolsillos van seguidos en 0xE28 bytes y justo detrás
hay una tabla con un puntero a cada uno, así que una candidata se acepta solo si esos siete
punteros apuntan cada uno a su propio bolsillo. Sobre los 96 MB de estado vivo sale **un bloque y
cero falsos positivos**, en `0x33011934`, la misma dirección del §18. Verificado en el juego:
la lectura coincide con la mochila real, la dirección aguanta reiniciar el emulador, y escribir
999 Caramelos Raros en un bolsillo que **no tenía entrada de caramelos** se ve en pantalla.
Toda escritura se relee antes de darla por buena. Detalle en `docs/ARCHITECTURE.md` §22.

**El emulador viaja con la app.** `Emulator/` lleva el fork propio; la app lo detecta junto a su
ejecutable, lo usa en modo portátil y le activa el servidor RPC solo. Verificado sobre el
publicado. Los binarios no se versionan: se reconstruyen desde `github.com/Grenin430/azahar`, y
solo se versiona `Emulator/LEEME.md` con su procedencia y su licencia. Ver §25.

**Randomizador: tipos y evoluciones desactivados (2026-08-19).** A petición del usuario, las
líneas evolutivas y los tipos se quedan como el cartucho; el resto sigue randomizado. Apagar
esos dos interruptores destapó dos fallos que nadie había visto porque **nunca se había apagado
un módulo**: la carpeta del mod no se vaciaba, así que el fichero de la generación anterior
seguía activo y el informe cantaba «0 evoluciones» mientras el juego las tenía randomizadas; y
los cinco aspectos de los datos de Pokémon compartían una sola fuente aleatoria, de modo que
apagar uno desplazaba a los demás. Ambos corregidos. Detalle en `docs/ARCHITECTURE.md` §27.

**Fase 4 empezada: el gacha (2026-08-19).** Tres banners con rarezas por **rango de total de
estadísticas base**, no por listas de Pokémon, así que ninguna especie se queda fuera y sigue
valiendo con la ROM randomizada (el módulo de datos baraja las estadísticas pero conserva el
total). `RomTool species` genera `Data/species.json` desde el cartucho. Cada tirada se calcula
con la seed de la run y su número, así que **se puede recomputar y auditar**.

**El Pokémon va al PC del juego**, escribiendo en el fichero de partida con PKHeX: copia previa,
y se relee para confirmarlo. Exige el juego cerrado, igual que la referencia. Cómo se supo que
esa era la vía: analizando los backups de BxnnyLocke, que son saves completos de 445.440 bytes.
Eso ahorró localizar las cajas en memoria. Detalle en `docs/ARCHITECTURE.md` §26.

Pantalla con animación abstracta del ultraespacio: cinco portales de color, uno por tier, sin
ningún asset con copyright. **Precios a cero mientras se prueba**; los reales (100/225/300) se
ponen cuando existan los logros, que son la fuente de puntos y hoy no existe ninguna.

**Sprites del cartucho y animación nueva (2026-08-20).** Los sprites **sí se pueden sacar de la
ROM del jugador**: los iconos de caja están en `a/0/6/2`, 1154 imágenes LZ11+BFLIM en **RGBA5551**
—no ETC1—, así que **no hace falta reincorporar nada de lo que se recortó de pk3DS**.
`RomTool sprites` los vuelca a PNG en 1,4 s; `Data/sprites/` está en `.gitignore` porque son de
Nintendo. Detalle en `docs/ARCHITECTURE.md` §28.

**La tabla especie → icono se construyó a mano** mirando el contenedor tramo a tramo, porque el
cartucho no la publica —no está en `personal`, ni en el RomFS, ni en el `code.bin`— y el número
de iconos por especie no coincide con su número de formas (Pikachu tiene 10 y declara 8; Arceus 1
y declara 18). **Resuelta para las 807**, en dos mitades con dos comprobaciones distintas: las
1-649 van en orden nacional y tienen que ocupar exactamente 866 iconos; las 650-807 van en otro
orden y se identificaron una a una, con la exigencia de que el reparto **cierre** —158 especies,
287 iconos, ninguno libre ni repetido—. Si alguna de las dos cuentas falla, el código lanza.
Esa segunda cuenta es la que cazó el único error de bulto: Togedemaru se había leído encima de
Jangmo-o y Hakamo-o. Ojo con las formas de Alola, cuyo icono va **antes** que el de la forma
normal; y con la misma trampa en el segundo bloque, donde el Furfrou sin corte es el sexto de sus
once iconos y Necrozma normal va detrás de sus tres fusiones. Ver §30 y §30 bis.

El gacha ya enseña el sprite del Pokémon que sale. Los iconos los extrae `PokemonSpriteService`
de la ROM del propio jugador la primera vez que se abre la pantalla.

La animación del gacha es una **ruleta**: una tira de 118 iconos del cartucho, a todo color, cruza
la pantalla y frena durante casi media tirada. El final es **a clics**: la rueda se planta **tres
casillas antes** y avanza **de una en una**, con su pausa, su golpe de marcador y su tirón, y el
último clic se pasa de largo y vuelve. Al parar caen fogonazo, onda de choque, ráfaga de rayos y
sacudida del panel. Lleva **escalada de rareza con engaño** —arranca en el tier más barato y sube,
en dos pasos si el tier es alto— y dura de **6 s a 11 s según el tier**. Todo eso es presentación:
el Pokémon, los puntos y el evento ya están decididos, guardados y entregados antes de que la
rueda gire. El número de clics es igual para todos los tiers **a propósito**: variarlo cantaría el
resultado antes de tiempo. Ver §31.

Dos fallos de WPF que costó encontrar y que conviene no repetir: **`OpacityMask` con `ImageBrush`
no pinta nada** (la silueta se cocina ahora en un bitmap) y **un `ItemsControl` dentro de un `Grid`
se recorta al ancho disponible**, así que una tira larga tiene que ir dentro de un `Canvas`.

**Visor Pokémon: la base, el PC de la partida (2026-08-21).** Las 32 cajas con sus 30 huecos, los
vacíos incluidos, y al pinchar un Pokémon su ficha: naturaleza, habilidad, objeto, ball,
entrenador, encuentro, movimientos y la tabla de estadística con IV y EV. Sale del **fichero de
partida** con PKHeX, igual que la entrega del gacha, pero **leer no exige cerrar el juego**: lo que
sí pasa es que se ve **lo último guardado**, y con el juego abierto la pantalla lo avisa.
`PlayerSave` se sacó aparte para que la entrega y el visor coincidan en dónde está la partida.
Trampa: un Pokémon en caja **no lleva sus estadísticas de combate**, hay que calcularlas.
Ver §32.

**Wonder trade (2026-08-21).** Entregas un Pokémon del visor y vuelve otro con un total base entre
**-8% y +10%** del que diste, **al mismo nivel** (si no, sería una lavandería: entregas un nivel 1
del gacha y sacas un nivel 50). La banda está en `Data/wondertrade.json`; los totales salen de la
ROM y los tipos de PKHeX, que es correcto porque los tipos no se randomizan. Es **la única
escritura de PermaLocke que destruye algo**, así que comprueba que el hueco sigue teniendo lo que
la pantalla cree, copia la partida entera y la relee después. Animación: el sprite entra en la
Poké Ball, sale disparada y otra llega del otro lado cruzándose con ella; antes del Pokémon salen
**tipo, generación y total base**, en ese orden. Ver §33.

**Iconos de objeto encontrados; los de tipo no existen (2026-08-21).** Barrido del RomFS entero:
`a/0/6/1` son los **769 iconos de objeto**, 32×32 RGBA5551, con **índice = id del objeto menos
uno** —lo prueban las dieciséis Poké Balls en fila—, y se acaba en el 768 con el «?». Sirve
también para la tienda. Los **iconos de tipo no están en el cartucho**: se buscaron como
contenedor, tallando los ALYT y por número, y no aparecen, porque el juego **compone la placa**
con un color y el nombre. Lo que sí hay, 18 y una por tipo, son los **cristales Z**, pero no en
orden de tipo, así que emparejarlos sería adivinar por color y no se hace. Ver §34.

**Logros y penalizaciones (2026-08-21).** Ya se pueden ganar y perder puntos. Cada muerte **−25**;
que caiga el equipo entero **−100** más, hasta **4 veces (−400)**; y el **saldo puede quedarse en
negativo**, porque una penalización no es una compra y no pregunta si hay saldo. Números en
`Data/penalties.json`, y si el fichero falta se cae a los reales, no a cero. El equipo caído se
cobra **en el flanco** —cuando pasa de tener a alguien en pie a nadie—, así que un equipo que cae
con la app cerrada no se cobra como tal; las muertes sí. Los logros son una proyección sobre el
historial y se cobran a mano. Un logro cuyo disparador no existe **no se borra**: sale marcado
como «sin detectar». Hoy faltan por instrumentar entrenadores y pruebas. Ver §36.

**La lista de la competición ya está puesta (2026-08-21).** 21 logros: las doce pruebas a 100, el
alto mando (300 y 300), las pegatinas 25/50/100 a 75/125/200, y 100 movimientos Z, 200 huidas, 1
variocolor y 100 entrenadores. Pantalla en rejilla de tarjetas, como la de referencia. **Ninguno
de los 21 es detectable hoy**, así que los logros sin disparador se marcan a mano con `+1` y
`COMPLETAR`: cada marca es un evento propio firmado por el jugador, no por la detección, y el
progreso se reconstruye sumando deltas. Ponerle un `trigger` a un logro lo vuelve automático y le
quita los botones solo. Ver §37.

**Que lo cuente el juego (2026-08-21).** Ultra Luna lleva sus propios contadores -los de la ficha
de entrenador- y PKHeX los expone en `SAV7USUM.Records`. Contarlos otra vez desde fuera daría un
segundo número peor, así que se leen los suyos: **41** movimientos Z, **46** huidas, **127**
variocolor, **5** combates contra entrenadores. Salen del fichero de partida, así que **no se
mueven hasta que el jugador guarde**. Los índices se anclaron con un cruce que no engaña: 168 balls
usadas contra 167 capturas. Cada tarjeta dice si su número lo cuenta el juego, PermaLocke o la
mano, y un logro con récord no se puede marcar a mano. **17 de los 21 siguen sin detectarse**
-pruebas, alto mando y pegatinas-: no son contadores sino banderas de evento, y eso es otra
investigación. Ver §38.

**Banderas de evento: medir, no adivinar (2026-08-21).** El **campeonato** ya se detecta solo con
el récord **2**, así que se cuentan solos **5 de los 21**. Los 16 que faltan —doce pruebas, segundo
alto mando y tres de pegatinas— son **banderas de evento**: 4960 sin etiquetar, 696 encendidas en
la partida del jugador, y 138 contadores con valores de 1 a 12 que podrían ser cualquier cosa.
Elegir uno a ojo repartiría puntos por la cosa equivocada sin fallar nunca, así que **se miden**:
`Probe --flags antes`, jugar, **guardar dentro del juego**, `--flags despues`, `--flags-diff`. Lo
que salga se escribe en `Data/achievements.json` y el logro pasa a automático sin tocar código.
Las copias de BxnnyLocke no sirven de referencia: PKHeX no las lee. Ver §39.

**La primera prueba se cuenta sola, anclada en el premio (2026-08-21).** Medida con el §39, y la
primera medición era **falsa**: el diff traía 24 banderas encendidas y ninguna era la prueba. Lo
delató un hecho independiente —el bolsillo de cristales Z, **vacío**, y un solo combate salvaje
cuando el Dominante es un combate salvaje—: aquella tarde fue la Escuela de Entrenadores. Un diff no
dice qué pasó, dice qué cambió. Con la medición buena quedaban **seis** banderas igual de
plausibles, así que no se elige ninguna: lo que la prueba deja con nombre es el premio, el
**Normastal Z (objeto 807)**, visto entrar en un bolsillo vacío. De ahí una tercera fuente de
progreso junto al récord y al disparador: `"item": 807`, presencia y no cantidad, **solo para lo que
el juego da y no quita**. Ningún récord se mueve al superar una prueba, y `Misc7.Stamps` vale 1
antes y después, así que no es la Dominsignia. Ver §40.

**Segunda prueba anclada, y la vía de la bandera enterrada (2026-08-21).** La segunda dio el
**Lizastal Z (813)**; los cristales de tipo son los objetos 807-824 en orden de tipos, así que cada
prueba se ancla mirando cuál aparece. La predicción de que el bloque de banderas bueno encendería
una segunda quedó en **21 parejas plausibles**: son tramos densos de banderas de historia, no una
tabla de hitos, y no va a converger. Tampoco hay contador de pruebas —la 1 encendió work[51] y
[63], la 2 encendió work[75] y [765], ninguno subió de 1 a 2—. Lo único prometedor es `stamps`,
de 1 a 3, pero el bit 0 ya estaba antes de la primera prueba: queda como predicción, si con la
siguiente gran prueba pasa de 3 a 7 es el campo de las grandes pruebas. Ver §40.

**Nombres que faltaban y movimientos Z repartidos (2026-08-21).** Dos fallos con la misma forma:
algo que parecía que el juego resolvería solo. El mote **no es un respaldo, es un campo**: todo lo
entregado por gacha y wonder trade llegaba en blanco porque nadie lo escribía. Ya se escribe, en el
idioma de la partida y con `IsNicknamed` en false. Lo ya entregado no lo alcanza ningún arreglo
—**150 de 155** en la partida real—, así que hay `Probe --nombres [--arreglar]`, con listado
separado de la escritura, copia previa y relectura. Y el randomizador repartía **movimientos Z** en
los aprendizajes: 1313 de 16052 en el mod instalado. Cuáles son se lee de la ROM, no se escribe en
el código: **todo movimiento Z tiene PP = 1** y ningún otro, salvo Forcejeo y Esquema, que tampoco
pintan nada ahí. Verificado generando: 0 de 16052. Ver §41.

**Renombrar no es capturar, y las Dominsignias las cuenta el juego (2026-08-21).** La reparación de
nombres funcionó y de paso subió **capturas 167→317, balls 168→318 y combates salvajes 212→362**:
PKHeX trata meter un Pokémon en una caja como *adquirirlo*. `EntityImportSettings` separa Pokédex
de récords, así que la reparación va con todo en `Disable` y la entrega y el wonder trade con los
récords en `Disable` -la Pokédex se queda, pero nadie tiró una ball a una tirada de gacha-. Se
deshizo comparando contra la copia previa: solo esos tres récords habían cambiado, y la copia era
del mismo minuto, así que se restauró y se repitió. Además, reinstalar el mod ya no borra el
anterior: se mueve a `load/permalocke-mod-anterior`, porque esa carpeta es el mundo en el que
alguien juega. Y las «pegatinas» de la competición son las **Dominsignias**, contadas en
`work[169]`, anclado con cuatro medidas contra lo que el jugador fue diciendo. `Misc7.Stamps`
parecía encajar pero son **14 bits** y las Dominsignias son 100. Ver §42.

**Los 21 logros se cuentan solos (2026-08-21).** Los nombres de los 170 récords de gen 7 estaban en
PKHeX y nunca se habian leido: confirman los cinco ya anclados y traen **72 «Stickers Collected»**
-las pegatinas, que sustituye al work[169] medido y da el mismo numero- y **100 «Champion Title
Defense»** -el segundo alto mando, objetivo 1 y no 2-. Y que prueba da que cristal **lo publica el
cartucho**, en el storytext `a/0/4/<idioma>` que el randomizador no extraia: los mensajes de premio
usan una variable, pero los dialogos de alrededor nombran el cristal en claro, y con eso se cierran
las doce. La linea f714 nombra los dos cristales y las dos pruebas en la misma frase, y son
exactamente los dos que ya se habian medido. Salen 12 y la competicion pide 12. Seis cristales de
tipo NO son de prueba -los regalan PNJ-, asi que contarlos a secas se habria adelantado seis veces
sin fallar una. **La pantalla ya no tiene un solo boton de marcar a mano.** Ver §43.

**Wonder trade: la banda pasa a −8% / +20% (2026-08-21).** El jugador dijo que salía siempre igual
o peor y tenía razón: los 23 intercambios de la run dan 14 peores, 6 iguales y 3 mejores, media
−2,1%. No era el sorteo, que reparte uniforme, sino la forma del cartucho: hay 171 especies entre
450 y 499 pero solo 36 entre 550 y 599, 18 entre 650 y 699 y **una** entre 700 y 749. Entregando un
600, la banda vieja 552-660 tenía 35 especies por debajo, 31 clavadas en 600 y **una** por encima.
Con +20% la banda es 552-720 y quedan 19 por encima, media esperada +1,2%. De 680 para arriba no
cambia nada porque el juego no tiene nada por encima de 720. Ver §44.

**Tienda (2026-08-21).** Los dieciocho objetos de la competicion con sus precios, en `Data/shop.json`,
pagados con puntos de la run y **escritos en la mochila del juego** por el bloque del §22. Orden
deliberado: **primero se entrega, despues se cobra**, porque la entrega es lo que puede fallar por
cosas de fuera y a un jugador cobrado por un objeto que no llego no hay como devolverle los puntos;
hay un test que falla si alguien invierte el orden. La entrega relee la mochila antes de darse por
buena.

De paso, **el §34 estaba mal a partir del objeto 100**: el icono NO es `id-1`. Hay 960 objetos y 769
iconos, las cien MT gastan veinte discos, y el desfase es escalonado. Se midio por zonas -1, -18,
-19, -127, -135- reconociendo cosas inconfundibles, y `ItemIconIndex` guarda **una tabla de lo
comprobado, no una formula**: lanza para un objeto que nadie ha mirado, porque un icono equivocado
no se nota. Ver §45.

**Los roles (2026-08-22).** Se eligen **lo primero**, antes de la ROM y de randomizar, porque parte
del rol se cuece en el cartucho. Tres: NORMAL (x1/x1), CAGONETA (x0,5 y **no pierde puntos**) y
EXPERTO (x1,5 / x2, entrenadores **+27%** manteniendo su cap en +20%, o sea que los rivales le sacan
ventaja). El +20% es el suelo de todos. Los multiplicadores tocan lo que se gana y lo que se pierde,
**nunca lo que se gasta**: la tienda cuesta igual en los tres. Cada evento guarda base,
multiplicador y resultado, y un rol que no se resuelve cobra la tarifa base diciendo
`rol=desconocido` en vez de adivinar. Todo en `Data/roles.json`.

**Ojo con lo que NO esta hecho:** el **Pokemon extra en los combates importantes**. Anadir uno alarga
el subfichero del GARC y `GarcPatcher.Write` rechaza un tamano distinto por diseno (§19); exige
reempaquetar `a/1/0/7`, tocar `a/1/0/6` y ademas decidir cuales son los combates importantes. Es un
trabajo aparte. La pantalla lo anuncia porque es lo que dice la competicion, pero todavia no lo
aplica nadie. Ver §46.

**El Pokémon extra, y el cap corregido (2026-08-22).** Corrección del §46: el **cap del jugador NO se
toca**, es el de `Data/levelcaps.json` tal cual; lo que sube un 20% son los niveles de los
entrenadores. Y el Pokémon extra ya está: **35 clases importantes** sacadas del cartucho —kahunas,
capitanes, alto mando, los dos rivales, Guzmán, la Fundación Æther, Kukui y los seis jefes del
Rainbow Rocket—, por **id** y no por nombre, porque Giovanni y sus reclutas comparten nombre de
clase. Es el único módulo que **reempaqueta un GARC**, porque un séptimo Pokémon alarga el
subfichero; la seguridad viene de releer y comprobar que cada equipo mide lo que su tabla declara.
El añadido es una copia del último del equipo con la especie cambiada, para no inventar campos que
nadie ha identificado. Seis es el techo: quien ya va con seis se deja y se cuenta.

De paso se cazó un fallo latente: `Stage()` copiaba la vanilla encima de lo ya parcheado, así que el
módulo nuevo tiraba a la basura todos los niveles subidos por el anterior. Ahora es **idempotente**.
Se vio releyendo los ficheros generados por fuera, no fiándose del informe. Ver §47.

**Los Dominantes también suben, y la tabla de caps se explica sola (2026-08-22).** Los Dominantes no
son entrenadores: viven en la tabla de estáticos, con el **nivel en el byte 0x03** —identificado
hoy, anclado con Solgaleo/Lunala a 60 y los Dominantes conocidos—. Con eso se pudo comprobar de
dónde sale `Data/levelcaps.json`: **el cap de cada etapa es el nivel del jefe subido un 20%**, nueve
de catorce clavadas. O sea que «tu cap como está» y «los enemigos +20%» son la misma regla vista
desde los dos lados. Y por eso los Dominantes tenían que subir: si no, las ocho pruebas quedaban por
debajo de lo que la propia tabla presupone. Ahora el porcentaje del rol llega también a los
estáticos —Dominantes, Necrozma, Solgaleo, Ultraentes, Tapus, legendarios—, pero **no a los
regalos**: esa tabla ni siquiera lleva nivel, y subir lo que te dan sería un premio. El número del
rol pasa a llamarse `nivelEnemigos`, leyendo aún el nombre viejo. Ver §48.

**Cap de nivel en marcha (2026-08-22).** La etapa ya no se pulsa: **se deduce de los logros**.
`Data/levelcaps.json` lleva un `logro` por etapa y se toma la más alta desbloqueada; el botón se
queda y se combina con `Math.Max`, así que **la detección solo puede subir el cap, nunca bajarlo**.
Visto en la run real: pasó sola de la 1ª prueba a la 3ª, cap 24. Alcance, que conviene tenerlo
claro: corrige **solo el equipo** —lo que duerme en una caja no se toca hasta que entra a jugar— y
**solo con la aplicación abierta y Azahar respondiendo**. Ver §49.

**La interfaz, rehecha entera (2026-08-22).** Repaso **solo de presentación**: ni una regla, ni un
servicio, ni un punto. La paleta pasa a tener profundidad -suelo, panel, panel elevado, línea-, todo
redondea por las mismas tres medidas, y los controles de Windows -barras, desplegables, casillas,
barras de progreso- se retemplan como **estilos implícitos**, así que ninguna vista tiene que pedirlo.
La barra de título ya es oscura, también en los diálogos. Y **no queda un enum en pantalla**:
`DisplayNames` traduce eventos, islas y tipos de encuentro en el borde, y lo que no tiene traducción
sale con su propio nombre, nunca en blanco.

Tres trampas nuevas que conviene no repetir: un **`ComboBox` editable sin `PART_EditableTextBox`
deja de aceptar texto en silencio** -sin excepción y sin log-, y así se usa el selector de especie;
una **pila horizontal mide el contenido con ancho infinito**, así que un texto dentro de un radio
nunca ajusta línea; y un estilo con `x:Key` **no hereda del implícito** salvo que lleve
`BasedOn="{StaticResource {x:Type X}}"`. Comprobado contra la aplicación real sin robar el foco, con
las 100 claves `StaticResource` verificadas y la pantalla de crear run vista en una copia aislada
fuera del repositorio, sin tocar la partida. Ver `docs/ARCHITECTURE.md` §50.

**El equipo en el visor y los EV editables (2026-08-22).** El visor enseña ya los seis del equipo,
que en el save es **otro almacén** y no una caja más: `BoxedPokemon.Box` toma el centinela
`PartyBox = -1`, negativo para que quien se olvide de mirarlo no caiga en la caja 0 sin enterarse, y
el wonder trade corta cualquier índice negativo en la puerta. Los EV se editan con los topes del
propio juego -**252 por estadística y 510 entre las seis**-, que **se aplican de formas distintas a
propósito**: el 252 se recorta, y el 510 solo se comprueba. Recortar el total obligaba a repartir en
un orden concreto -para pasar PS a Velocidad había que saber que primero se vacía PS-, así que ahora
se puede pasar de 510 mientras repartes, el panel se pone en rojo diciendo por cuánto y **GUARDAR se
apaga** hasta que vuelve a ser legal. Cada fila lleva su `MÁX` y su `0`. Se escribe con el juego
cerrado, comprobando por **PID** que el hueco sigue teniendo al mismo, con copia previa y relectura,
y queda como evento `EvsTrained`. **No cuesta puntos**: entrenar es una edición, no una compra.

Hallazgo medido, no supuesto: **recalcular las estadísticas del equipo escribe números falsos**. PKHeX
calcula con SU tabla de estadísticas base y la ROM lleva `shuffleBaseStats`, así que sobre una copia
de la partida real un Kommo-o pasó de **168 PS a 151**. No se tocan; el juego las pone al día solo. De
rebote se supo que las estadísticas que el visor enseña **de los Pokémon en caja** son una estimación
por la misma razón, así que ahora salen marcadas. `Probe --ev [--probar]` lo comprueba **sobre una
copia**, nunca sobre la partida. Ver `docs/ARCHITECTURE.md` §51.

**Darse objetos, dos botones (2026-08-22).** La herramienta de Caramelos Raros pedía una cifra y la
escribía como cantidad absoluta; ahora son **+10 CARAMELOS RAROS** y **AMULETO IRIS** (objeto 632),
que **suman** a lo que llevas. Van por `IItemDelivery`, el camino de la tienda: ping antes, suma en
vez de reemplazo y **relectura** antes de dar nada por bueno. Por poco se repite el cuelgue de la
tienda: preguntar la capacidad de un objeto **localiza la mochila**, o sea 96 MB barridos, así que
solo se pregunta cuando el ping ha contestado; con Azahar cerrado los dos botones responden en 400 ms
diciendo la verdad. El Amuleto Iris es objeto clave y el bolsillo admite **uno**, de modo que dárselo
a quien ya lo tiene se leería igual que una escritura fallida: por eso existe `CapacityFor` y por eso
el botón dice «ya lo llevas». Los ids se buscan, no se recuerdan -`Probe --objeto-find`-, se
comprueba el nombre antes de escribir, y hay tests que fijan id, nombre y bolsillo. Ver
`docs/ARCHITECTURE.md` §52.

**El escritor no releía lo que escribía (2026-08-22).** El cap decía «corregido» y el Yveltal seguía
a nivel 100. Medido con las copias de seguridad que guarda cada escritura: el **contenido era
correcto** -un Pokémon de equipo lleva el nivel dos veces, como experiencia y como `Stat_Level`, y
PKHeX escribe los dos-, pero `AzaharGameWriter.Modify` **devolvía éxito sin releer**, mientras
`SetBagSlot` sí relee desde el §22. Una ruta de escritura aprendió la lección y la otra no, así que
`EnforceLevelCap` daba por buena una escritura que nadie había comprobado y el monitor **metía un
`LevelCapEnforced` en el historial**. La regla 3 en una línea. Ahora `Modify` relee y compara **solo
los bytes que tocó** -el resto de la entrada se mueve solo mientras se juega-, `EnforceLevelCap`
comprueba además el nivel en los dos sitios, no se registra nada sin verificar, y **HOME avisa en
rojo** cuando el cap no se está aplicando.

**Y la causa de fondo, medida contra el juego (`Probe --equipo`):** la copia que el juego lee de
verdad -salto `0x1E4`, la que el locator ya documentaba como autoritativa- guarda las estadísticas de
combate en otro sitio, así que `Pk7Reader` la rechaza entera, y un filtro de «solo las copias que se
dejan leer» **la borraba de la lista de escritura**. Las correcciones iban a las copias del bloque de
partida, que el juego pisa. Leer y escribir son cosas distintas: al escribir la garantía la pone el
checksum de PK7 más la relectura, no que el lector entienda la estructura. De paso, el barrido daba
**ocho copias que eran dos estructuras** y seis vistas solapadas de ellas, y escribir en una vista
manda la corrección al Pokémon de al lado; `PartyLayoutLocator.Distinct` lo arregla, con prueba sobre
las ocho direcciones reales.

**Y una regresión de ese mismo arreglo, que costó un Pokémon:** se añadió «corregir si CUALQUIERA de
los dos niveles se pasa», incluyendo `Stat_Level`. Pero **`Stat_Level` solo significa algo en la
estructura de equipo de verdad**; en las de salto `0x1E4` ese offset es de otra cosa y devuelve 145,
187, 202. Leyó 145 en un **Ledyba de nivel 4**, le escribió el cap de 24 y el juego lo **evolucionó a
Ledian**. Un cap que sube un Pokémon es lo contrario de un cap. La partida guardada no se enteró, así
que cerrar sin guardar lo deshizo. Ahora: **solo decide la experiencia** -avalada por el checksum en
cualquier estructura-, **el PID por delante** para que un hueco desalineado no se toque, y **no se
escribe más allá del bloque cifrado** en las estructuras cuya cola no está identificada. La lección
general: un campo solo vale **donde la estructura está identificada**; el mismo offset en otro sitio
no es una lectura mala, es la lectura de otra cosa, y una condición «por si acaso» encima de eso es
un disparador aleatorio. Ojo también con el enlace: ese día PermaLocke solo estuvo conectado al juego
**11 y 108 segundos**. Ver `docs/ARCHITECTURE.md` §53.

**El enlace se moría a media sesión (2026-08-22).** El cap solo vigila mientras hay enlace, y
PermaLocke aguantaba **11 y 108 segundos**: no parpadeaba, se caía y **ya no volvía**. Estaba en
`AzaharRpcClient.Send`, que mandaba un datagrama, esperaba uno y lo daba por bueno. Dos defectos: el
cliente es un **singleton sobre un socket** y el sondeo pregunta cada segundo mientras la tienda, el
visor y las sondas preguntan desde sus hilos -dos peticiones solapadas y cada una lee la respuesta de
la otra-; y una **respuesta que llega tarde** se leía como la de ahora, con lo que el socket se
quedaba *permanentemente una respuesta por detrás* y ya no se recuperaba. Ahora hay **cerrojo**, se
**vacía la cola** hasta encontrar el id bueno, y hay **tres intentos con el mismo id**. Comprobado
con un servidor UDP falso que se porta mal a la carta -y verificando que la prueba del descarte falla
con el código viejo-, y contra el emulador real con la app a 1 Hz y seis sondas barriendo a la vez:
ni una caída. Ver `docs/ARCHITECTURE.md` §54.

**La regla de las Poké Balls no actuaba porque nunca supo dónde estabas (2026-08-22).** Estaba
encendida y no hacía nada, y el log lo decía **cada segundo** desde hacía días: «las copias de la zona
no concuerdan». `Probe --zona` enseña las cuatro: dos con asa normal coinciden en la **zona 32**, y
las otras dos tienen el asa a **2** y números que no son zonas -48074 y 65418, cuando `encdata` tiene
336-. Ese par no guarda un registro de zona, pero la regla exigía que **las cuatro** coincidieran, así
que contaban como desacuerdo y tiraban la lectura entera: **no acertó ni una vez**. Ahora una copia
solo vota si **puede** ser una zona -asa no nula y número dentro de las 336-; las que no, se abstienen
en vez de envenenar la votación, y hacen falta al menos **dos** de acuerdo. `LooksLive` no bastaba,
porque un asa de 2 no es cero. Corrobora que el ancla sigue buena que la zona 32 es **Pueblo Lilii** y
el §23 midió Pueblo Lilii como área **1**: el cartucho reparte un mismo sitio entre varias áreas
-Ruta 2 es 5, 37, 57, 58 y 64-.

**Y al andar se vio que el ancla está muerta, y que la relajación era un error.** Aquella lectura se
tomó con el jugador **quieto**. Con `Probe --zona --vigilar` andando de la Ruta 2 al Centro Pokémon de
Hauoli, el campo pasó por 32, 1, 28, 0 y acabó fijo en 22, y las asas se volvieron `3D957735`,
`BD565D86`, `3DBA2EEA` -que en coma flotante son **0,073, −0,052 y 0,091**- y `7FFF0835`, que es un
**NaN**. Esa memoria ya no guarda una zona: guarda posiciones. **El ancla del §23 está muerta** y el
32 era un resto. Con la regla **estricta** todo eso se rechaza, que es lo correcto; con la relajada,
PermaLocke habría dicho «Cementerio de Hauoli» estando el jugador en el Centro Pokémon. Así que la
relajación **se revierte** y vuelven las cuatro copias de acuerdo, con las lecturas reales fijadas en
las pruebas. Segunda vez en el día que aflojo un guardia con una medida parcial -la otra evolucionó
un Ledyba-, y la forma es la misma: **una medida tomada en una sola situación no sostiene una regla
que gobierna todas**. La regla de las balls queda **apagada**; reencenderla exige volver a localizar
el campo de zona desde cero, que es una investigación del tamaño del §22, no un ajuste. Ver
`docs/ARCHITECTURE.md` §55.

**Ninguna muerte se había contado nunca, y el motivo era un cero (2026-08-23).** El jugador pidió
comprobar el cap por prueba y el recuento de muertos. El cap está bien -pasó solo de la 1ª a la 3ª
prueba, cap 24-. Lo otro no: **186 Pokémon registrados y cero muertes**. `GameWatcher` empareja el
equipo vivo con la run **por PID y por nada más**, y la auditoría nueva lo cuenta: **6 con PID y 180
sin él**, o sea que el 97 % de lo que hay era invisible. Dos mitades: la run guardaba su entrada
*antes* de que el Pokémon existiera, así que no había PID que guardar; y peor, `PokemonBuilder` no
ponía `PID` y un `PK7` nace a cero, de modo que **149 de los 154 Pokémon de la partida real
compartían el mismo**. Ya se reparte PID y constante de encriptación -tirando primero y corrigiendo
el brillo después, porque un PID al azar sale shiny una vez de cada cuatro mil-, `DeliveryResult` lo
devuelve y `PokemonIdentityService` lo guarda con su evento `PokemonDelivered`. Para lo ya entregado
está `SavePidRepair` (`Probe --pids`), que empareja por **los seis IVs más el shiny** -lo único que
no cambia nunca- y solo acepta firmas únicas por los dos lados. Ojo: **hay dos órdenes de IVs vivos
en el repositorio**, el de la tirada y el del lector, y confundirlos dio 0 de 180 emparejados sin
error ninguno. Alcance que conviene tener claro: detectable no es detectado, el vigilante solo mira
**el equipo** y solo **con la aplicación abierta**. Ver §56.

**POKE PASTE (2026-08-23).** Exporta el equipo o cualquier caja en el formato de `pokepast.es`.
**Solo exporta**: leer un pegado sería crear Pokémon a partir de texto. Los nombres van en **inglés**
porque el sitio se guía por ellos, así que usa un `SaveBoxReader` propio con idioma `en`. Ver §57.

**Fuera los dos botones de HOME (2026-08-23).** ETAPA SUPERADA y REGISTRAR CAPTURA hacían lo que ya
hace otra cosa. La etapa la deducen los logros desde el §49, y el botón «por si la detección se
retrasa» lo que hace es **adelantarla**: en la run real se había pulsado **seis** veces con **dos**
pruebas detectadas, y el cap estaba en **40** en vez de 24, porque sale de `Math.Max(a mano,
detectado)`. Lo que hay en la partida se ve entero en el visor, y lo que aparece sin registrar lo
anuncia el aviso de HOME con su propio botón. Deshacerlo **no se hace editando `run.json`**: hay
`Probe --etapas [n]`, que pasa por `ProgressService.AdvanceAsync` y por tanto deja su evento. De
paso, `DetectedAsync` se hace pública -sin ella las dos cifras se leen iguales y no se ve cuál
sostiene el cap- y el evento dice «cap **por marcas a mano**», que es lo que ese número significa.
Ver §58.

**El que se va también cuenta, y el «muerto» que no lo es (2026-08-23).** Repartidos ya los PID
-157 de 157 en la partida-, quedaban **28 registros sin PID**: tienen IVs guardados y **no hay
ningún Pokémon en la partida con esa firma**. Son los que el jugador entregó en sus 29 wonder
trades. `WonderTradeService` daba de alta al que llega y **no decía nada del que se va**, así que su
registro seguía `Alive` para siempre y HOME contaba **187 vivos** con 28 que no están en el juego:
el mismo error que una muerte sin registrar. `MarkGivenAsTradedAsync` lo cierra, **después** de
escribir la partida -antes no se ha ido nadie- y **por PID**, que es lo que solo se pudo hacer desde
el §56. Lo ya intercambiado se mide con `Probe --intercambiados`, cruzando la especie que el propio
evento apuntó como entregada: **26 de 28 encajan**, y las 2 en disputa se dejan, porque elegir cuál
de dos Giratina se fue sería inventar historia en un registro encadenado por hash.

Y el «muerto» del jugador era un **Shedinja apodado MUERTO, sin movimientos, en el equipo**: la
marca exacta de `DeathTransform`. **No se murió, se escribió**, probando la escritura en memoria. La
run no lo cuenta y hace bien. Quién era **no se puede saber**: el respaldo de partida más antiguo
que hay ya lo lleva puesto. Lección: una prueba destructiva sobre la partida de alguien tiene que
dejar por escrito qué destruyó. Ver §59.

**Premios de una sola vez (2026-08-23).** Botón en MISCELÁNEA que, con las **doce pruebas**
superadas, da **12 Hiperpociones y 12 Curas Totales**, y solo una vez. Tres condiciones y ninguna
apoyada en una marca de la pantalla: **ganado** -los doce logros, que salen del cartucho por su
cristal Z-, **no recogido** -que no haya un evento `RewardClaimed` con ese id, que es lo que hace
que «una vez» sea una vez; un booleano del ViewModel se cae al cerrar la app- y **entregable**, por
el camino de la tienda con relectura. Orden como en la tienda: **primero entregar, después
registrar**. La **entrega a medias cuenta como recogida** a propósito: entre deberle Curas Totales a
alguien y un botón que se puede repulsar para duplicar lo ya dado, en una competición lo segundo es
peor. Todo en `Data/rewards.json`, con los ids buscados (`Probe --objeto-find`) y el **nombre
comprobado contra la tabla del juego antes de escribir**. Ver §60.

**Los cristales Z, sacados del cartucho por fin (2026-08-23).** El §34 decía que no se podían
emparejar con su tipo sin adivinar por color, y a medias sigue siendo verdad. Lo nuevo: están
incrustados en dos pantallas -**ALYT** `a/1/5/5` y `a/1/4/2`- y **la tabla de ficheros del ALYT los
nombra**: `item_807.bflim` … `item_824.bflim`. O sea que el cartucho dice **qué objetos son**, y lo
confirman por partida doble el orden de nombres de objeto y las **doce pruebas**, que caen cada una
en el tipo que su prueba es. Lo que sigue sin medirse es **cuál es cuál**: un ALYT guarda su tabla
de nombres y sus datos en órdenes distintos y no publica el mapa -probado de tres maneras-. Así que
el emparejamiento va **por color**, con las familias -un amarillo, un rojo, dos verdes, tres
marrones, dos rosas, dos morados, cinco azules, uno gris y uno negro- y las decisiones escritas en
`ZCrystalIndex`. Es un ancla **más floja** que las del resto y va dicha así: aquí una imagen a un
tono de distancia no cuesta nada, mientras que en el §45 un icono equivocado enseñaba un objeto que
no era. Trampa al tallar: **buscar las letras `FLIM` no vale**, salen dentro de los píxeles de otras
imágenes; hace falta la marca `FEFF`, el bloque `imag` y que el tamaño declarado cuadre. Y las
**Dominsignias no tienen sprite** en el cartucho -barridos los 3431 dibujos-, así que van con perla,
perla grande y pepita de oro, declarado en el JSON. `achievements.json` gana `icono`, que es el id
del objeto que se **ve** (distinto de `item`, que es el que **desbloquea**). Ver §61.

**Rol LUDÓPATA y RULETA (2026-08-23).** Cuarto rol sin cambios en los multiplicadores de puntos:
debe una tirada por prueba, tres al ganar la liga y dos por el rematch. La deuda sale de los logros
automáticos menos los eventos `RouletteSpun`; no hay nada manual. `Data/roulette.json` tiene las
16 caras y cada rueda toma seis sin repetir de todas juntas, con seed reproducible y objetivos
decididos antes de animar. La pantalla revela los seis `?` y gira con una Poké Ball del cartucho en
el centro; aparece justo encima de MISCELÁNEA y solo para este rol. Escribe el save con Azahar
cerrado, backup y relectura completa: habilidades, los seis IV, muerte y los objetos. Tiene pruebas
en memoria para cada clase de escritura; **queda probar una copia de la partida real antes de usar
una cara destructiva**. Ver `docs/ARCHITECTURE.md` §62.

**Rol LUDÓPATA y su RULETA (2026-08-23).** Cuarto rol; no cambia ni un punto de lo que se gana o se
pierde, lo que cambia es que **después de cada hito hay que girar y vivir con lo que salga**: una
tirada por prueba, tres por la liga, dos por el rematch. Las tiradas **se deben**, con la misma forma
que los premios del §60 -logros menos eventos `RouletteSpun`- y nada que marcar a mano. Cada tirada
sale de la seed y del número, así que **se recomputa** y una tirada fallida se repite sin riesgo:
misma cara. La rueda enseña **seis de las dieciséis sorteadas entre todas juntas**, sin equilibrar:
pueden salir seis malas, y hay test que lo exige. **Todo va por el fichero de partida** -equipo,
mochila y cajas están detrás de una sola puerta; por memoria harían falta tres mecanismos y una
ruleta que necesitase el juego abierto para unas caras y cerrado para otras-. Medidas que costaron:
las cien MT **no son un rango** -328-419, 618-620 y 690-694- y la lista buena la da
`pouch.GetAllItems()`; escribir en la mochila del save es `SetPouch(game.Data)` más `Write()`,
probado sobre una copia; y **diez nombres de habilidad de la lista original no existen** -«Absorbe
Elec», «Fuerza Mental», «Energía Pura», «Rezagado»...-, así que se resuelven por nombre y un nombre
que no cuadra **tira un test** en vez de repartir otra habilidad en silencio. Los puntos van en el
delta del propio evento y **no los multiplica el rol**. Una muerte de ruleta no resta puntos pero
sigue siendo un `PokemonDied`. **Verificado en la partida real**: dos tiradas, un Archeops entregado
y el MT56 escrito. Ver §62.

**Ojo con las copias aisladas.** Levantar una copia de la app en otra carpeta aísla la **run**, pero
`PlayerSave` localiza el save de Azahar, que es **el de verdad**. Aislar la run no aísla la partida.

**Tiradas gratis y wonder trades: crédito, no regalos sueltos (2026-08-27).** Dos peticiones que
son la misma cosa. La ruleta ya **no tira por ti** cuando sale una cara de gacha: da el crédito y
aparece como «2 tiradas gratis» en el banner que toca, y se tira allí, con su rueda y su sprite. Y
cada prueba da tiradas y wonder trades según la tabla de la competición, en `Data/grants.json`.
Misma forma que el §60 y el §62: **ganado** de los logros -más las caras de gacha, leídas de los
propios eventos de tirada-, **gastado** de los eventos marcados `gratis`, disponible la resta. Nada
guardado, nada que desincronizar. Esa marca es lo que hace que la regla **no mire hacia atrás**: los
30 wonder trades que ya había no la llevan, así que no se cobran. **Los wonder trades pasan a estar
limitados**, que es lo único que hace que «te dan 1 wonder trade» signifique algo -antes eran gratis
e ilimitados-; va como `limitarWonderTrades` en el JSON, y **si el fichero falta no se limita nada**.
Ver §63.

**Cambiar de rol, y cuadrar la run con la partida (2026-08-27).** `ChangeRoleAsync` existía desde el
§46 y **no tenía ningún sitio desde el que llamarse**, así que el rol LUDÓPATA era inalcanzable sin
empezar una run nueva. Ya hay botón en HOME: marca el rol actual, exige el motivo y **avisa de que
cambiar de rol NO vuelve a randomizar** -los niveles y el Pokémon extra están escritos en el mod
instalado-. De paso apareció un crédito que se habría pagado dos veces: las dos tiradas de ruleta
son de cuando la cara de gacha tiraba en el acto, así que **cada tirada guarda ahora en su evento a
qué banners da crédito** (`credito`) en vez de deducirlo de la cara; las viejas no lo llevan y no
pagan. Mismo principio que la marca `gratis`: lo que se cobra y lo que se paga salen de lo que el
evento dijo que pasó, no de la configuración de hoy. Y la run vuelve a cuadrar con la partida: los
diez eventos de la copia aislada incorporados -comprobado dos veces que era superconjunto estricto-
y 26 registros cerrados como entregados. **Alive 189 → 163, Traded 1 → 29.** Ver §64.

**Las dieciséis caras, contra una copia de la partida real (2026-08-27).** `Probe --ruleta --probar`
pasa **todas** las caras, cada una sobre su **propia copia**, y comprueba releyendo el fichero. Las
que hacía falta ver eran las que **destruyen** -Shedinja, IV a cero-, que nunca habían tocado una
partida. Todas hacen lo que dicen, y la fecha del save no se movió. La sonda registra su **propio**
`IRouletteWorldPort` con la carpeta de respaldo en el temporal, así que ni por error hay una ruta de
escritura que apunte a la partida: es la lección del §62 aplicada antes, aislar la run no aísla la
partida. De paso, dos caras salieron MAL en la primera pasada y **la equivocada era la
comprobación**, que no leía el «antes»; ahora exige `después == recorte(antes + delta)` y de rebote
queda probado que quitar no baja de cero. Vale la pena recordarlo: **una verificación puede estar
peor pensada que el código que verifica**. Ver §65.

**Diez Super Balls, los iniciales, y empezar de cero (2026-08-27).** «La primera vez que te dan Poké
Balls» **no mueve ningún récord**, pero deja Poké Balls en la mochila, así que los premios del §60
ganan una segunda clase de condición, `objetosEnMochila`, que es el truco del §40 aplicado a otra
cosa. Con lo que hay que distinguir: un cristal Z el juego no lo quita nunca y una Poké Ball **se
gasta**, así que esa condición sí puede apagarse -lo que hace que el premio sea de una vez no es la
condición sino el evento `RewardClaimed`-. Si la partida no se lee, la condición queda **no
cumplida**, nunca cumplida por accidente.

Los tres iniciales se llegaron a enseñar con nombre y sprite y **el jugador pidió quitarlo el mismo
día**: saber qué huevo es cuál le quita a la elección lo que la hace una elección. Lo que queda es
`StarterReader` y su comando `RomTool iniciales <carpeta>`, y sobre todo el anclaje que salió de
hacerlo, que llevaba desde el §19 dicho sin medir: **las entradas 0-2 de la tabla de regalos son los
iniciales**, porque en el cartucho sin parchear valen **722, 725 y 728**.

Y ya se puede **empezar de cero** con una run cargada, cosa que no se podía: CREAR RUN solo existía
en el HOME vacío.

Y cada pantalla dice arriba a la derecha si necesita el juego **abierto** (verde), **cerrado**
(ámbar) o le **da igual** (apagado), con una línea diciendo por qué. Son las dos puertas de siempre
-memoria viva contra fichero de partida- pero dichas donde hacen falta, que es donde estás a punto de
pulsar. Cada sección lo declara en `SectionViewModel.Needs`, no hay tabla en el shell: la respuesta
sale de por qué puerta escribe cada una. El VISOR va marcado **cerrado** aunque leer funcione
siempre, porque la insignia falla hacia el lado que no rompe nada. Ver §66.

**Empezar de cero borra de verdad (2026-08-27).** A petición del jugador, el botón del §66 pasa de
crear una run nueva a **borrar la run entera y la partida**. La run son tres almacenes -carpeta,
tabla `events`, tabla `pokemon`- con un test por cada uno y otro que exige que **una segunda run no
sea daño colateral**. La partida es fichero de otro programa: `SaveEraser` la **copia antes y sin
copia no borra**, va a `Saves/backup/borrada-<fecha>/`, y es deliberadamente estrecho -vacía la
carpeta del título y **se niega** si esa carpeta no es la de Ultra Luna, en cuyo caso borra solo el
`main` que localizó-. El orden lo decide qué puede fallar: **primero la partida**, que es la que
puede negarse porque Azahar la tenga abierta, y así fallar ahí lo deja todo como estaba.

Y una aclaración sobre el registro encadenado: `DeleteRunAsync` es el único DELETE y **no es una
grieta**, porque no existe forma de borrar **un** evento -la cláusula es `WHERE run_id` y no hay
sobrecarga que acepte un id de evento-. Borrar uno suelto dejaría una cadena cuyos hashes siguen
cuadrando alrededor del hueco; borrar la run entera no deja nada que falsificar. Una run descartada
no es una run corregida. Por lo mismo no se escribe evento de despedida: iría a la cadena que se está
borrando. Ver §67.

**Nada contaba porque nadie había pulsado un botón (2026-08-28).** El jugador reportó que no le daban
las Super Balls, que los muertos no se volvían Shedinja y que no se restaban puntos. Dos de las tres
eran lo mismo: `Probe --run` daba **0 Pokémon registrados**, y el vigilante empareja por PID contra
lo registrado, así que la lista de caídos era vacía siempre. La cadena estaba entera; faltaba el
primer eslabón, y el primer eslabón era **un botón**. El §56 ya avisaba de que «detectable no es
detectado»: aquí se vio que lo que deja es un sistema de penalizaciones **inerte y en silencio**.

Ahora **se registra solo**, y sin inventar. De memoria salen especie, nivel, mote, variocolor, PID y
el lugar de encuentro que el Pokémon lleva escrito; lo que no sale es la **ball**, y sin ella no se
puede decir el tipo de encuentro, que **decide si la captura gasta el encuentro de la zona**. Poner
«salvaje» habría gastado zonas sin fallar nunca, así que hay un `EncounterType.Unknown` que no gasta
zona ni dispara reglas especiales. Registrar no es arbitrar. Y una captura que una regla bloquea **no
se fuerza**: vuelve al aviso de HOME, porque saltarse una regla es del jugador. El evento guarda
**quién lo decidió**, `AutoDetect` contra `Player`.

Y las Super Balls pasan a `"automatico": true` en `Data/rewards.json`: un premio que tiene que llegar
junto a las Poké Balls de Tilo y que hay que ir a buscar a otra pantalla no es un premio, son
deberes. Mismas condiciones, misma relectura, mismo `RewardClaimed`; solo cambia quién pulsa. Va a
una consulta cada 30 s porque saber si está ganado lee el save entero. Y **se dice**: HOME enseña en
verde lo último que la app ha hecho sola.

Y todo eso **en vivo, como el cap**: la condición del premio se preguntaba al fichero de partida, o
sea que exigía guardar a mano; ahora se pregunta **primero a la mochila viva** (§22) y solo se cae al
save si Azahar está cerrado. Trampa de contrato que conviene no olvidar: `CarriedAllAsync` devuelve
una entrada por id **aunque valga cero** cuando el juego responde, y el diccionario **vacío** cuando
no, así que vacío es «no se sabe» y no «no lleva ninguno». Además el monitor lanza `RunDataChanged`
cuando de verdad cambia algo y HOME se refresca solo: `TeamWiped` llevaba desde el §36 lanzándose
**sin que nadie lo escuchara**, así que una muerte se cobraba bien y el saldo de la pantalla no se
movía hasta salir y volver a entrar. Ver §68.

**Los iniciales, primera etapa de una línea de tres (2026-08-28).** La competición pide que un
inicial sea algo que **crece**: dos evoluciones por delante. No es una lista de especies sino una
propiedad leída del cartucho — `EvolutionTable` interpreta `a/0/1/4` (subfichero por especie,
entradas de 8 bytes, método en 0 y destino en 4) y responde a una sola pregunta. Medido con
`RomTool evoluciones`: de 589 primeras etapas, **298 no evolucionan, 197 son de dos etapas y 94 son
de tres**. Trece anclas comprobadas, y las trece cuadran -Pikachu fuera porque evoluciona de Pichu,
Eevee fuera porque sus ramas son de un paso-. De 94 a las **92** que usa el randomizador: una está
por encima de `maxSpecies` y **Cosmog** está en `bannedSpecies`.

`SpeciesPool.Where` estrecha el saco en vez de repetir la tirada, a propósito: con un filtro no hay
un límite de intentos del que caerse, que es como una restricción deja de serlo justo cuando más
cuesta cumplirla. Dos fallos los cazaron los tests, no la ROM: `FromTargets` no limpiaba las
autoevoluciones como sí hacía `Read`, y la profundidad **memorizaba resultados obtenidos cortando un
ciclo**, que dependen del camino de llegada -o sea que la tabla contestaba distinto según el orden de
las preguntas-. Verificado generando: seed 20260828 da Duskull, Fletchling y Nidoran♂. Ojo con que
los iniciales se eligen **antes** de que se toquen las líneas evolutivas, así que con
`randomizeEvolutions` en true la garantía es sobre las familias del cartucho. Ver §69.

**Las evoluciones que a solas no existen (2026-08-28).** Un Nuzlocke se juega solo, así que una
evolución por intercambio es una evolución que **no existe**, y con `randomizeLearnsets` en true las
nueve que esperan un movimiento tampoco llegan. Qué cambiar sale de la lista de Universal Pokémon
Randomizer ZX que pasó el jugador; los **números de método se midieron** con `RomTool evo-dump`, cada
uno anclado a un caso reconocible: 5 es intercambio a secas (Kadabra), 6 con objeto (Poliwhirl con la
Roca del Rey, arg 221), 7 las dos que se cambian entre sí, 19 subir de nivel con objeto **de día**
(Happiny con la Piedra Oval), 22 con otra en el equipo (Mantyke con Remoraid). Conversión: 5→4 a
nivel 37, 6→19 conservando el objeto, 7→22 con la otra de argumento, y las de movimiento a nivel.
**Solo hay variante de día y de noche**, así que esas evoluciones piden ahora que sea de día.

Se buscan **por método y no por lista de especies**, y no es estilo: la lista enumera 27 casos y el
cartucho tiene **30**, porque las tres tallas de Calabruja y el Geodude de Alola son entradas propias
que nadie nombra. El emparejamiento Karrablast/Shelmet tampoco está escrito: **se deduce** de que hay
exactamente dos entradas de método 7. La única excepción a mano es Slowking, que pasa a Piedra Agua
porque su entrada lleva nivel 37, justo el nivel al que Slowpoke ya se hace Slowbro. Verificado
releyendo el mod generado: metodo 4 de 266 a 287, el 8 de 43 a 44, el 19 de 1 a 16, el 22 de 1 a 3, y
los métodos 5, 6, 7 y 21 desaparecen. Ver §70.

**La interfaz, con identidad propia (2026-09-01).** Segundo repaso visual, y esta vez no de orden
sino de carácter: el jugador pidió que pareciera un producto de videojuego y no un tema aplicado por
encima. Lo primero fue medir qué la hacía parecer una plantilla: **25 tamaños de letra** en 132 usos
—con los cuatro tokens `FontSize*` sin usar por nadie—, **50 valores de padding**, **10 radios** a
mano, **37 colores fuera del tema** con una subpaleta entera dentro de `ShopView.xaml`, y **29 usos
del mismo contenedor con borde**: todo era una tarjeta. No faltaba gusto, faltaba sistema.

La regla nueva: **el gris hace el trabajo y un único acento significa valor y acción**. Si algo es
del color de acento, o es dinero o se pulsa. Los semánticos solo como distintivos pequeños con fondo
teñido, y la rareza confinada al gacha. Empezó en latón y **el jugador lo pasó a violeta**, el del
ultraespacio; con el cambio se tiñeron también los neutros, porque un gris exacto debajo de un
violeta se lee sucio. Y salió una colisión: **el tier 4 ERA violeta**, así que pasó a magenta para no
decir dos cosas con el mismo color.

**Cuatro materiales en vez de una card para todo** —sección sin caja por defecto, `Inset` plano sin
borde, `Well` hundido para listas, `Card` con borde solo para objetos que se cogen—, y la palanca
fue que `Inset` se usaba en 29 sitios: quitarle el marco **en el tema** desencajonó la aplicación
entera sin tocar una vista. La elevación la da el escalón de gris; no hay ni una sombra.

Tipografía de dos papeles: **Bahnschrift** para cifras y rótulos, Segoe para frases, Consolas para lo
que se alinea en columna. La trae Windows 10 en adelante, así que viaja en el reparto sin licencia.
Primer intento con la **condensada**, que a 10 px se cierra y no se lee: se vio en una etiqueta, no
en el contador de puntos, donde quedaba bien. Y **catorce iconos propios**, silueta rellena porque a
18 px un trazo de 1,5 px se emborrona. Ver §74.

Tres trampas de WPF de esta tanda: **`Style` puesto como atributo Y como `<TextBlock.Style>` es error
de compilación** —cayó cuatro veces—; **los comentarios XML no admiten `--`**; y `PrintWindow`
**devuelve el dibujo anterior** si la ventana no está delante y WPF no ha repintado, así que dos
capturas de verificación iban desfasadas una pantalla.

**La carpeta que se reparte (2026-09-01).** `tools\publicar.ps1` construye el reparto: un exe
autocontenido más `Data\` y `Emulator\`, y nada más. Dos cosas estaban rotas y **no se veían
jugando**: `Data\` no se publicaba —solo el emulador estaba en el csproj—, y el empaquetado de
fichero único **se tragaba el emulador**, dejando `Emulator\` con tres ficheros y el exe en 267 MB.
Eso solo habría fallado en el ordenador de quien la recibiera. El script comprueba **lo que acaba de
escribir**: exe, `azahar.exe`, los catorce json, la guía, y que no se hayan colado sprites, `Saves`
ni `ROM`. Ver §75.

**La run no se copiaba nunca (2026-09-01).** 319 copias de la partida y **cero de la run**:
PermaLocke respaldaba el fichero de otro programa antes de cada escritura y no respaldaba su propia
base de datos, con la cadena firmada por hash, los puntos y los 157 Pokémon dentro. `RunBackup` copia
al arrancar y conserva las diez últimas. Va en `App.OnStartup` **antes de que nada abra la base de
datos**, que es el único momento en que el fichero está en reposo —copiar un SQLite que otro escribe
puede capturar media página, y una copia que quizá esté corrupta es peor que ninguna—. No lanza
nunca, y la rotación ordena **por nombre y no por fecha**, que es lo único que sobrevive a copiar o
restaurar la carpeta. Ver §76.

**El mantenimiento sale de la terminal (2026-09-01).** `Probe` tiene 62 comandos y la carpeta que se
reparte lleva `PermaLocke.exe` y nada más. Cuando esta run se desincronizó se arregló desde una línea
de comandos; **la de otro se habría quedado rota para siempre**. Sección MANTENIMIENTO con auditoría
de la run, reparar PID, cerrar entregados y corregir etapas, todas **en dos pasos** —mirar cuántos, y
solo entonces se enciende el botón que escribe—.

La fila que importa de la auditoría es **SIN PID**, porque el vigilante empareja por PID y por nada
más y HOME no distingue eso de «no ha muerto ninguno». De las cuatro herramientas, **las etapas no
tenían nada que extraer** —la lógica ya estaba en `ProgressService`— y lo que aporta la pantalla es
**separar los tres números**: a mano, detectado y en vigor. Los intercambiados sí tenían lógica y solo
en el probe: sale a `TradedAwayReconciler` y **el probe pasa a usarlo**, porque dos implementaciones
de «cuál se fue» acabarían discrepando. Su test principal es el que **se niega a adivinar**: tres
Giratina sin PID y dos entregas no cuadran, así que no toca ninguno. Ver §77.

**ESTADÍSTICAS (2026-09-01).** La aplicación guardaba una cadena firmada con todo lo que ha pasado,
enseñaba las últimas veinte líneas y tiraba el resto. La pantalla no calcula nada nuevo: es esa
cadena, contada. El centro es **el libro de puntos**, que contesta por qué tienes los que tienes, y
sale del `PointsDelta` que cada evento guardó **en su momento** y no de los precios de hoy — así
sigue siendo verdad si se cambia la configuración con una run empezada. La curva se escala entre su
mínimo y su máximo **y no desde cero**, porque el saldo puede quedarse en negativo y anclada a cero
una run hundida se dibujaría plana. Y la racha sin bajas se cuenta **en eventos**, con la etiqueta
diciéndolo: PermaLocke solo ve lo que pasa con la aplicación abierta. Ver §78.

**COMPETICIÓN: la clasificación (2026-09-01).** Lo único que estaba sin empezar. **No hay servidor y
no lo va a haber**: el transporte es una carpeta compartida —Drive, Dropbox, red, un pendrive— donde
cada aplicación escribe **un** fichero con el resumen de su run y lee los de las demás. Se publica un
resumen y no la run: mandar la cadena entera sería mandar el diario para que lean la última página.

**No está verificado y no puede estarlo** sin ese servidor, así que la pantalla lo dice con su
distintivo SIN VERIFICAR en vez de fingir un anti-trampas. Lo que sí guarda es el número de eventos y
el hash del último: no demuestran que un número sea cierto, pero hacen **comparables** dos
instantáneas de la misma run —un contador que baja es una copia restaurada, un hash que cambia sin
que el contador suba es historial reescrito—. Detalles que se pagan si no se piensan: se escribe a un
temporal y se mueve porque la carpeta la sincroniza otro programa; el fichero va **por id de run** y
no por nombre, que dos Ash se pisarían; los empatados comparten puesto; y un fichero ajeno roto **se
dice por nombre** sin impedir que carguen los demás. Ver §79.

**Se puede combatir entre los jugadores, y está verificado (2026-09-01).** Dos Azahar en el mismo PC,
**combate completo de séptima generación** por red local emulada. Era la única incógnita y estaba en
el lado que no controlamos: la red de Nintendo cerró en abril de 2024, así que el modo local es la
única vía.

Lo que puede romperlo es la randomización, no el emulador: un combate por link es una **simulación en
paso fijo** y las dos consolas calculan lo mismo. Medido, de los **ocho ficheros** que el mod
reemplaza **siete no se leen en combate**; solo `a/0/1/7` sí, y de él solo cambian `shuffleBaseStats`
y `randomizeAbilities` —los tipos ya están en false y **los datos de movimiento no se tocan nunca**—.
Y esta **medido, no razonado**: tres combates cambiando una sola cosa cada vez. Vanilla funciona;
dos mundos distintos con esas dos en `true` **se desincronizan**; los mismos dos mundos con esas dos
en `false` funcionan. Entre el segundo y el tercero solo cambian esos interruptores, asi que la causa
queda identificada. **Se puede combatir con cada uno su propio mundo** con esas dos iguales en todos. Salvajes,
entrenadores, iniciales, objetos del suelo, tiendas, el Pokémon extra y las megas de jefe pueden ser
distintos sin problema.

COMPETICIÓN avisa de quién puede y quién no, con el dato sacado **del evento `RomRandomized` y no de
`randomizer.json`**: el fichero dice cómo se generaría la próxima vez, el evento con qué se hizo el
mundo que se está jugando. Tres estados y no dos, porque `null` es **«no se sabe»** y jamás se lee
como «no puede». Y el formato de la instantánea **se queda en 1** a propósito: un lector rechaza
cualquier formato más nuevo, así que subirlo habría dejado a todo amigo con la versión anterior
viendo una clasificación vacía.

Montar la prueba costó dos hallazgos que no documenta nadie: **`CITRA_USER_DIR` no funciona** —el
emulador la ignora y se va a la instalación real; hay que usar modo portátil con una carpeta `user\`
junto al ejecutable— y **los dos campos de apodo de multijugador están bloqueados**, porque Azahar
los espeja del nombre de la consola (`Emulación → Configurar → Sistema`). Ver §80.

**Y las bayas del suelo están randomizadas:** 97 de los 539 objetos del suelo son bayas y entran en
el mismo barajado que el resto. Lo que no se ha comprobado es si las que caen al **sacudir un árbol**
salen de esa misma tabla.

**Gen 8 y 9: PermaLocke puede randomizar encima de otro mod (2026-09-01).** Existe un mod de la
comunidad que mete las generaciones 8 y 9 en Ultra Luna, con las megas de Leyendas Z-A incluida la
de Dragonite. Se define a sí mismo como un **andamio** y a propósito **no trae encuentros salvajes**:
los Pokémon nuevos solo se pueden meter a mano con PKHeX, o sea que en un Nuzlocke **no existen**.
Escribir `a/0/8/3` es justo lo que hacemos, así que el encaje es bueno: **PermaLocke es la pieza que
le falta.** Todo medido en `docs/MOD-EXPANSION.md`; **nada de esto se ha jugado todavía.**

Hay una carpeta `Expansion/` al lado de `ROM/`, con sus mismas reglas —solo lectura, y en el
`.gitignore` porque son gigabytes de Nintendo—. Si está, **todo** se randomiza encima de ella. Se
detecta por existir y no por un interruptor: un interruptor puede discrepar del disco, y entonces la
pantalla estaría diciendo que randomizó un mundo que no leyó.

**El fallo que se habría comido el proyecto entero sin decir nada:** `SpeciesPool` recortaba contra
`config.MaxSpeciesID`, que es una **constante de pk3DS clavada en 807** que no mira los ficheros. El
saco se habría construido, la randomización habría dicho «listo», y ni uno de los 354 Pokémon nuevos
habría aparecido jamás. Ahora la cuenta **se deduce de la tabla** —del índice de forma más bajo que
declara cualquier especie—, verificada en los dos mundos: cartucho 807, mod 1025, las dos respuestas
conocidas. Y `maxSpecies: 0` pasa a significar «las que tenga el juego», para que el caso seguro sea
el que sale de no pensarlo.

**Los sprites costaron una línea en vez de una investigación**, y solo porque se midió antes de
empezar: los **1154 iconos del cartucho son byte a byte idénticos** en el mod, así que añade en vez
de reordenar y la tabla del §30 sigue valiendo entera. Lo añadido va en orden nacional y está
anclado por los dos extremos —icono 1154 = Meltan (808), icono 1371 = Pecharunt (1025)—, y cierra
sin holgura: 217 = 217.

**El mod solo trae el texto en INGLÉS**, y eso no es lo mismo que «la página está en inglés». Medido:
inglés 1026 nombres, español **808**, se acaba en Zeraora. Así que los nombres nuevos **se ponen
desde PermaLocke**, y no es traducir: los nombres españoles son **oficiales** y PKHeX los lleva.
El ancla es que estas listas se direccionan **por índice**, así que una desplazada un puesto le
pondría a cada Pokémon el nombre de su vecino sin que nada fallara: 805 de 808 especies coinciden.
Y esa medida cambió el diseño — movimientos coinciden 659/729 y objetos 738/960, pero **no son
errores**: el cartucho abrevia para que le quepa («Picotazo Ven») y usa las traducciones de su
generación («Golpe» donde hoy es «Saña»). Así que la fusión **solo añade, nunca reescribe**. Las
descripciones se copian del inglés del mod, porque no hay fuente oficial y **una descripción de
movimiento equivocada mata un Pokémon**.

**Instalar copia la capa base entera y lo nuestro encima**, más el `exefs` —que va al lado de romfs,
no dentro—. Antes copiaba los siete ficheros del randomizador y nada más, así que los datos de
especie, los aprendizajes y los 2,5 GB de modelos se quedaban fuera: los Pokémon nuevos salían en
las tablas y el juego no tenía con qué dibujarlos.

Y dos topes puestos, los dos por medidas: **`maxAbility` 233**, porque el campo de habilidad es un
byte y el mod llenó los 22 huecos que quedaban con habilidades que **salen con nombre y no hacen
nada** (es una trampa latente, no un fallo activo: `randomizeAbilities` está en `false`); y
**`maxMove` 729**, que ese sí estaba vivo con `randomizeLearnsets` en `true`, porque 32 de los 192
movimientos nuevos piden una rutina de combate que solo existiría en el `code.bin` del mod.

**La ruleta, rehecha (2026-09-04).** Repaso visual de la pantalla del LUDÓPATA, y lo que la sostenía
era un fallo de fondo: el color de la cuña salía de `WedgeBrush(index)`, o sea de **la posición**,
así que la misma cara salía roja o azul según dónde cayera y **el color no podía significar nada ni
queriendo**. Mirando la rueda no sabías si te iba bien o mal. Ahora verde paga y rojo cuesta, con
dos tonos por bando alternados —que no es una escala de gravedad, que sería un juicio inventado,
sino lo justo para que dos cuñas seguidas no se lean como una mancha—. El comentario que defendía
los seis colores del arcoíris decía que un verde y un rojo «chafarían la tensión»; la premisa no se
sostiene y por eso se revoca por escrito: **las seis caras se desvelan una a una antes de que la
rueda arranque**, o sea que el color no cuenta nada que no estuviera ya en pantalla.

Cada cuña lleva ahora **dibujo, cifra grande y nombre corto**, declarados en `Data/roulette.json`.
Los dibujos salen del cartucho del propio jugador por un camino que ya estaba enchufado y solo
pintaba una Poké Ball. Tres iconos hubo que **medirlos** con la disciplina del §45: MT01 es el icono
309 —anclado en que el 308 es el Colmillo Agudo y el 310 otro disco—, y las Chapas Plateada y Dorada
son 649 y 650, anclados por tres seguidos y en orden con la Pulsera Z. **Solo se reclama la primera
MT**: las cien comparten veinte discos y las demás no tienen correspondencia. Las dos caras de
muerte llevan **Shedinja**, que no es decoración sino en lo que el juego convierte a un caído.

Y lo demás: la lista de dieciséis baja a una **tira** y le devuelve a la rueda un tercio de la
pantalla; el final ocurre **en la rueda** —la ganadora encendida, las otras cinco apagadas, la
tarjeta encima—; el panel **se tiñe** del bando de la cuña que pasa por la marca, y solo a partir del
primer golpe, porque durante el barrido pasa una cada cincuenta milisegundos; hay **cinco perfiles
de frenada** con la misma norma que los del gacha —ninguno correlaciona con lo que salió—, uno de
ellos con la rueda pasándose una cuña y volviendo; y el eje **ya no gira ni se ve borroso**: el icono
mide dieciocho píxeles y se estiraba a setenta y seis con interpolación suave.

Lo que solo se vio abriendo la ventana: antes la limitaba el ancho y ahora **la limita el alto**, y a
700 **no cabía** —aro cortado por arriba y por abajo, marca invisible—. Va en un `Viewbox` que la
dibuja siempre al tamaño de diseño y la encoge hasta caber.

**Y el marco, de ruleta de feria (mismo día).** El jugador pasó una foto de una ruleta de premios y
zanjó la duda en una frase: «el relleno no lo cambies, cambia los contornos y el color de ellos». Así
que las cuñas siguen diciendo verde/rojo y lo que se rehizo es el marco: **aro dorado con veinte
bombillas**, juntas doradas y más gruesas, **flecha roja** y buje dorado. Las bombillas se generan en
código y no se escriben a mano —veinte círculos sobre una circunferencia son veinte ocasiones de
teclear mal una coordenada— y son **veinte porque seis no divide a veinte**, así que ninguna cae
sobre una junta. El escenario pasa de 700 a 740 y **la rueda sigue midiendo 700**: la banda dorada se
gana por fuera, sin tocar la geometría de las cuñas.

**Y fuera el aplastado.** El jugador vio que la rueda «no es simétrica, está levemente aplastada» y
tenía razón: era el `ScaleY` de 0,94 de la idea 5, puesto para que se leyera apoyada. Con el aro
dorado y su sombra **el bulto ya lo da el metal**, así que lo único que seguía aportando era que la
rueda no fuese redonda. Medido comparando radio vertical y horizontal del aro: pasa de **V/H 0,976 a
1,008**, redonda dentro del error.

Lo que cuesta todo esto, medido sobre las capturas y no estimado: el disco de color va de **506 →
486 → 458 px** — el aro dorado se llevó 20 y la redondez otros 28, porque el escenario necesita 740
de alto en vez de 696 y el `Viewbox` reparte lo que hay. Sigue por encima de los 428 de antes del
rediseño, pero es la dirección contraria a la que pidió la idea 4, así que va dicho con números. La
palanca para recuperarlo está sin usar porque nadie la ha pedido: la tira de dieciséis ocupa 110 px
de alto y a los lados sobran casi 400 a cada lado. **El pie de la foto no está**: no es un contorno,
es un objeto nuevo, y se comería otros 45 px del mismo presupuesto.

Ojo con lo **no comprobado**: lo estático está visto en la aplicación real, pero **la rueda desvelada
y la tirada entera no**, porque verlas exige girar de verdad y girar escribe en la partida. Ver §84.


**La rueda paraba y luego cambiaba de opinion (2026-09-04).** Primera tirada de verdad con la
pantalla nueva: «se ha parado en el medio de IV AL MAXIMO y ha pasado por la cara a la siguiente;
quiero que en el que pare, paro». Es la idea 8 del §84 haciendo lo que se le pidio: `WheelEnding`
era una lista de **paradas** y la rueda se plantaba dos o tres cunas antes del ganador para entrar
de una en una. Lo que costaba no se vio hasta verla girar. Y el motivo va mas alla del gusto: **el
resultado ya esta escrito en la partida antes de que la rueda arranque**, asi que una rueda que se
detiene sobre una respuesta y luego la cambia no crea tension, ensena algo que no ha pasado.

La variedad **no se pierde, se muda**: de donde para a como frena. Cinco perfiles con potencia de
frenado, vueltas de mas y rebote distintos, todos de un solo barrido monotono hasta el ganador. El
numero que lo sostiene: media cuna son **30 grados** -ahi llega la vecina bajo la marca- y el rebote
esta acotado a **20**, con el perfil mas movido en 14. El constructor estatico lanza si alguien
escribe uno que se pase. De rebote, el tinte del panel ya no depende de una bandera de tramo sino de
**cuanto se mueve la rueda por fotograma**, que es mejor: entra cuando va despacio de verdad. Ver
§86.

**La sexta prueba comparaba dos unidades distintas (2026-09-04).** El jugador peleó contra un
recluta del Team Skull y le salió **un Larvitar**, con la regla de «de la sexta prueba en adelante,
todos evolucionados del todo» encendida. El corte estaba en **34**, copiado de `levelcaps.json`,
pero **un cap ya va subido un 20%** (§48) y esto compara contra el nivel del **cartucho**: cinco
niveles de más. El número bueno sale de la tabla de estáticos, donde los ocho Dominantes miden 12,
20, 22, 24, **29**, 33, 35 y 49 — el de la sexta es Vikavolt a **29**, y multiplicar esos ocho por
1,2 da los caps, que es la comprobación cruzada que nadie había hecho y ahora es un test. El
recluta era el entrenador **473** con el Larvitar a nivel 33 de cartucho, o sea entre la sexta
prueba y la séptima.

Y **el mismo requisito tenía un segundo agujero**: `ExtraPokemonRandomizer` no aplicaba la regla en
absoluto, así que el Pokémon extra del rol podía ser una primera etapa en cualquiera de los 106
combates importantes. Al arreglarlo, la trampa es que ese módulo corre **después** de que el rol
haya subido los niveles, así que hay que leer el nivel del cartucho del último que el entrenador ya
tenía — comparar el del fichero habría metido el mismo error del otro lado.

**Y la comprobación estaba ciega justo ahí**: `RomTool trainers` empezaba con `if (before.Length !=
after.Length) continue;`, o sea que los 106 equipos que crecen quedaban fuera de todas sus
comprobaciones. Pasa de vigilar 474 Pokémon a **809**. Verificado releyendo lo generado con la seed
real: **809 de nivel 29 en adelante, 0 sin evolucionar**, cero Larvitar en todo el juego, y el
entrenador 473 pasa de Larvitar a **Tyranitar**. Hay dos herramientas nuevas para no volver a
contestar a mano: `RomTool quien-lleva <especie>` y `RomTool entrenador <id>`, las dos sobre un mod
generado **o instalado**. Ver §85.

**Siguiente.** Probar en partida real los combates importantes, los iniciales y las tiendas.

Y decidir entre los cinco el trueque del combate por link, que ya está medido: **o estadísticas
barajadas y habilidades al azar, o poder pelear entre vosotros.** Las dos cosas a la vez no.

Lo que NO está resuelto todavía y no debe darse por hecho (detalle en `docs/ARCHITECTURE.md`):

| Tema | Estado |
|---|---|
| Lectura del equipo en vivo | **RESUELTA Y VERIFICADA** contra el juego real |
| Detección automática de capturas y muertes | **NO FUNCIONABA Y YA PUEDE** — el 97 % de los Pokémon no tenía PID, que es lo único por lo que empareja. Corregido para lo nuevo y reparable para lo viejo con `Probe --pids`; sigue sin haberse visto una muerte real. Ver `ARCHITECTURE.md` §56 |
| Poke Paste | **HECHO Y VISTO EN LA APP** — exporta **el equipo** en formato `pokepast.es`, en inglés. Solo exporta. Ver `ARCHITECTURE.md` §57 |
| Sprites de los cristales Z | **RESUELTO** — tallados del ALYT `a/1/5/5`, que los nombra `item_807..824`. Cuál es cuál va **por color** y así está dicho. Los de Dominsignia **no existen** en el cartucho. Ver `ARCHITECTURE.md` §61 |
| Randomización vía LayeredFS | **RESUELTA Y VERIFICADA** en el juego — salvajes y textos |
| Escritura en el juego (Shedinja, cap, objetos) | **RESUELTA Y VERIFICADA** — exige el fork propio de Azahar |
| Mochila del juego (leer, poner cantidad, añadir lo que no llevas) | **RESUELTA Y VERIFICADA** en el juego — ver `ARCHITECTURE.md` §22 |
| Zona actual del jugador (regla de las Poké Balls) | **RESUELTA Y VERIFICADA** — es el área de `encdata`, leída anclando a la mochila; ver `ARCHITECTURE.md` §23 |
| Regla de las Poké Balls (impedir la captura, no solo registrarla) | **IMPLEMENTADA Y APAGADA** — cableada y con tests, sin probar en el juego; ver `ARCHITECTURE.md` §24 |
| Gacha (motor, probabilidades, reproducible) | **HECHO** — 220 tests; **visto funcionando en la app, con sprites** |
| Entrega del Pokémon al PC del juego | **VERIFICADA EN LA PARTIDA REAL** — dos entregas a la caja 1 con copia previa. Exige el juego cerrado |
| Extraer los sprites de Pokémon de la ROM | **RESUELTA Y VERIFICADA** — `a/0/6/2`, RGBA5551, 1154 iconos a PNG en 1,4 s; ver `ARCHITECTURE.md` §28 |
| Saber qué icono es de qué especie | **RESUELTO PARA LAS 807** — construido a mano en dos bloques; el reparto cierra sin iconos libres ni repetidos y hay tests. Ver `ARCHITECTURE.md` §30 y §30 bis |
| Visor Pokémon (leer el PC de la partida) | **HECHO Y VERIFICADO** en la partida real — 149 Pokémon, 32 cajas, ficha al pinchar. Solo lectura; sin equipo, sin formas, sin cruce con la run. Ver `ARCHITECTURE.md` §32 |
| Wonder trade (banda de BST, escritura, animación) | **MOTOR Y ESCRITURA HECHOS** — 16 tests y verificado contra una copia real del save; la animación está sin ver porque exige un intercambio de verdad. Ver `ARCHITECTURE.md` §33 |
| Logros y penalizaciones | **HECHO Y VISTO EN LA APP** — motor, pantalla y 6 tests. Falta la lista real de logros, que la tiene que dar el jugador. Ver `ARCHITECTURE.md` §36 |
| Contadores del juego (movimientos Z, huidas, shiny, entrenadores) | **HECHO Y VISTO EN LA APP** — se leen de `SAV7USUM.Records`; 28/200 huidas y 6/100 entrenadores reales. Ver `ARCHITECTURE.md` §38 |
| Detectar pruebas, pegatinas y alto mando | **HECHO Y VISTO EN LA APP** — los 21 logros se cuentan solos y no queda un botón de marcar a mano. Las doce pruebas por su cristal Z, con la correspondencia sacada del storytext del cartucho. Ver `ARCHITECTURE.md` §43 |
| Tiradas gratis y wonder trades por prueba | **HECHO Y VISTO EN LA APP** — crédito ganado de los logros y gastado del historial, con la marca `gratis`; los wonder trades pasan a estar limitados. Ver `ARCHITECTURE.md` §63 |
| Ruleta del rol LUDÓPATA | **HECHA Y VISTA EN LA APP** — 16 caras, seis en la rueda, tiradas que se deben y se recomputan; escribe equipo, mochila y cajas por el fichero de partida. **Dos tiradas verificadas en la partida real.** Ver `ARCHITECTURE.md` §62 |
| Pantalla de la ruleta, rehecha | **HECHA; LO ESTÁTICO VISTO EN LA APP** — el color de la cuña dice si te conviene, dibujos del cartucho, rueda grande y redonda con marco de feria, final en la propia rueda y tinte del panel. La frenada por tramos se **revocó** tras verla girar: paraba sobre una cara y luego cambiaba, y ahora es un solo barrido. **La tirada entera sigue SIN VERSE desde el desarrollo**: exige girar de verdad y girar escribe en la partida. Ver `ARCHITECTURE.md` §84 y §86 |
| Roles | **HECHOS Y VERIFICADOS CONTRA LA ROM** — se eligen lo primero, multiplican los puntos, suben el nivel de todo lo que combate contra ti (entrenadores, Dominantes, Necrozma y legendarios) y añaden el Pokémon extra en 35 clases de combate importante. Falta probarlo jugando. Ver `ARCHITECTURE.md` §46, §47 y §48 |
| Sincronización | **HECHA Y VISTA EN LA APP** — clasificación por carpeta compartida, sin servidor ni cuentas. Cada uno publica un resumen y lee los de los demás. **No es una verificación y la pantalla lo dice.** Falta probarla con la carpeta sincronizando de verdad entre dos máquinas. Ver `ARCHITECTURE.md` §79 |
| Mod de expansión de gen 8 y 9 | **IMPLEMENTADO Y VERIFICADO CONTRA LOS FICHEROS, SIN JUGAR** — randomiza encima del mod, 1025 especies, sprites, nombres en español oficial e instalación de la capa entera. La Ruta 1 da Dreepy, Meltan, Snom y Tarountula. **Nadie ha arrancado el juego con esto**: sin comprobar que instale, que el `code.bin` del mod arranque, que el texto generado no cuelgue, ni el combate por link. Ver `docs/MOD-EXPANSION.md` |
| Combate por link entre jugadores | **VERIFICADO Y CERRADO** — se puede combatir con cada uno su propio mundo, siempre que `shuffleBaseStats` y `randomizeAbilities` esten en `false` en todos. **Medido con una prueba controlada**: con esas dos en `true` el combate se desincroniza, con ellas en `false` no. Ver `ARCHITECTURE.md` §80 |
| Copia de seguridad de la run | **HECHA** — al arrancar, diez copias rotativas. Antes había 319 copias de la partida y cero de la run. Ver `ARCHITECTURE.md` §76 |
| Mantenimiento desde la aplicación | **HECHO Y VISTO EN LA APP** — auditoría, reparar PID, cerrar entregados y corregir etapas, sin terminal. Ver `ARCHITECTURE.md` §77 |
| Estadísticas | **HECHA Y VISTA EN LA APP** — libro de puntos, curva de saldo, colección y récords, todo proyectado sobre la cadena de eventos. Ver `ARCHITECTURE.md` §78 |
| `PermaLocke.Admin` | **SIGUE SIENDO EL ANDAMIO DE VISUAL STUDIO** — 66 líneas, `Title="MainWindow"` y un `Grid` vacío. O se construye o se borra |
| API concreta de pk3DS.Core | **VERIFICADA** contra la ROM real — ver `ARCHITECTURE.md` §19 |

---

## Comandos

```bash
dotnet build
dotnet test
# la carpeta que se le pasa a otro jugador: exe autocontenido + Data + Emulator.
# Comprueba lo que acaba de escribir y lanza si falta algo. NO incluye ROM ni tu partida.
pwsh -File tools/publicar.ps1
pwsh -File tools/publicar.ps1 -Destino "otra/ruta"

# randomizador contra la ROM real (escribe en Randomized/, no en Azahar)
dotnet run --project tools/PermaLocke.RomTool -- inspect
dotnet run --project tools/PermaLocke.RomTool -- randomize 20260818
dotnet run --project tools/PermaLocke.RomTool -- dump 20260818 "Ruta 1"
dotnet run --project tools/PermaLocke.RomTool -- statics 20260818
dotnet run --project tools/PermaLocke.RomTool -- trainers 20260818
dotnet run --project tools/PermaLocke.RomTool -- pokemon 20260818
dotnet run --project tools/PermaLocke.RomTool -- shops --gen
dotnet run --project tools/PermaLocke.RomTool -- zones
dotnet run --project tools/PermaLocke.RomTool -- species
dotnet run --project tools/PermaLocke.RomTool -- sprites --sheets

# los tres iniciales de un mod ya generado o instalado; sin carpeta mira todo Randomized/
dotnet run --project tools/PermaLocke.RomTool -- iniciales
dotnet run --project tools/PermaLocke.RomTool -- iniciales "ruta/al/mod"

# pone en espanol los nombres que un mod base solo trae en ingles. Sin --escribir solo mide.
dotnet run --project tools/PermaLocke.RomTool -- traducir
dotnet run --project tools/PermaLocke.RomTool -- traducir --escribir

# especies que son primera etapa de una linea de tres, contra la tabla del cartucho
dotnet run --project tools/PermaLocke.RomTool -- evoluciones "ruta/a/a/0/1/4"

# la tabla de evoluciones en crudo, agrupada por metodo; con un metodo, solo ese
dotnet run --project tools/PermaLocke.RomTool -- evo-dump "ruta/a/a/0/1/4"
dotnet run --project tools/PermaLocke.RomTool -- evo-dump "ruta/a/a/0/1/4" 19
dotnet run --project tools/PermaLocke.RomTool -- randomize 20260818 --install

# EV de la partida real; --probar demuestra la escritura SOBRE UNA COPIA
dotnet run --project tools/PermaLocke.Probe -- --ev
dotnet run --project tools/PermaLocke.Probe -- --ev --probar

# quien lleva una especie, o que lleva un entrenador, en un mod GENERADO O INSTALADO
dotnet run --project tools/PermaLocke.RomTool -- quien-lleva 246 "ruta/al/mod/romfs/a/1/0/7"
dotnet run --project tools/PermaLocke.RomTool -- entrenador 473 "ruta/al/mod/romfs/a/1/0/7"

# id de un objeto por su nombre, para no teclearlo de memoria
dotnet run --project tools/PermaLocke.Probe -- --objeto-find "Amuleto Iris"

# auditoría de la run: puntos, muertes, cap por etapa y cobertura de PID
dotnet run --project tools/PermaLocke.Probe -- --run

# PID que faltan; --probar lo demuestra SOBRE UNA COPIA, --arreglar escribe de verdad
dotnet run --project tools/PermaLocke.Probe -- --pids
dotnet run --project tools/PermaLocke.Probe -- --pids --probar
dotnet run --project tools/PermaLocke.Probe -- --pids --arreglar

# etapas marcadas a mano; con un número las corrige, dejando su evento
dotnet run --project tools/PermaLocke.Probe -- --etapas
dotnet run --project tools/PermaLocke.Probe -- --etapas 0

# cada cara de la ruleta contra una COPIA; la partida no se toca nunca
dotnet run --project tools/PermaLocke.Probe -- --ruleta
dotnet run --project tools/PermaLocke.Probe -- --ruleta --probar

# entregados en un wonder trade y contados todavía como vivos
dotnet run --project tools/PermaLocke.Probe -- --intercambiados
dotnet run --project tools/PermaLocke.Probe -- --intercambiados --arreglar
```

SDK requerido: .NET 10 (instalado: 10.0.400).
