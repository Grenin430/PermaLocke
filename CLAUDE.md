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

Limitación conocida: `LevelCapRule` está implementada y testeada, pero `RuleContext.LevelCap`
es siempre null porque **no existe seguimiento de etapa** (pruebas superadas). Empezará a
disparar cuando se registre el progreso de pruebas.

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

**Siguiente.** Tienda, sincronizacion, y probar en partida nueva iniciales, entrenadores y tiendas.

Lo que NO está resuelto todavía y no debe darse por hecho (detalle en `docs/ARCHITECTURE.md`):

| Tema | Estado |
|---|---|
| Lectura del equipo en vivo | **RESUELTA Y VERIFICADA** contra el juego real |
| Detección automática de capturas y muertes | **Sin validar** — falta una captura real |
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
| Tienda y sincronización | **SIN EMPEZAR** |
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
```

SDK requerido: .NET 10 (instalado: 10.0.400).
