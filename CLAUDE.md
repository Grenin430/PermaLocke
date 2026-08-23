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

**Siguiente.** Sincronización, y probar en partida real los combates importantes, los iniciales y
las tiendas.

Lo que NO está resuelto todavía y no debe darse por hecho (detalle en `docs/ARCHITECTURE.md`):

| Tema | Estado |
|---|---|
| Lectura del equipo en vivo | **RESUELTA Y VERIFICADA** contra el juego real |
| Detección automática de capturas y muertes | **NO FUNCIONABA Y YA PUEDE** — el 97 % de los Pokémon no tenía PID, que es lo único por lo que empareja. Corregido para lo nuevo y reparable para lo viejo con `Probe --pids`; sigue sin haberse visto una muerte real. Ver `ARCHITECTURE.md` §56 |
| Poke Paste | **HECHO Y VISTO EN LA APP** — exporta equipo o caja en formato `pokepast.es`, en inglés. Solo exporta. Ver `ARCHITECTURE.md` §57 |
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
| Roles | **HECHOS Y VERIFICADOS CONTRA LA ROM** — se eligen lo primero, multiplican los puntos, suben el nivel de todo lo que combate contra ti (entrenadores, Dominantes, Necrozma y legendarios) y añaden el Pokémon extra en 35 clases de combate importante. Falta probarlo jugando. Ver `ARCHITECTURE.md` §46, §47 y §48 |
| Sincronización | **SIN EMPEZAR** |
| API concreta de pk3DS.Core | **VERIFICADA** contra la ROM real — ver `ARCHITECTURE.md` §19 |

---

## Comandos

```bash
dotnet build
dotnet test
dotnet publish src/PermaLocke.App -c Release -r win-x64 --self-contained true

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
dotnet run --project tools/PermaLocke.RomTool -- randomize 20260818 --install

# EV de la partida real; --probar demuestra la escritura SOBRE UNA COPIA
dotnet run --project tools/PermaLocke.Probe -- --ev
dotnet run --project tools/PermaLocke.Probe -- --ev --probar

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
```

SDK requerido: .NET 10 (instalado: 10.0.400).
