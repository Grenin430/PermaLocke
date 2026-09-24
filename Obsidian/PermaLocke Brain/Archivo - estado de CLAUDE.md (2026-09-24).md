---
tipo: archivo
movido: 2026-09-24
---
# Archivo: el «Estado actual» de CLAUDE.md hasta el 2026-09-24

Movido tal cual desde `CLAUDE.md` con /doctor el 2026-09-24 para que no se cargue en cada sesión (unos 33k tokens est.). Es el diario fechado del proyecto: cada entrada remite a su § de `docs/ARCHITECTURE.md`. Lo nuevo va a [[Historial de conversaciones]]. Vuelve a [[00 - Inicio]].

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

**Corregido el 2026-09-18 (§132): lo del `maxAbility` estaba mal medido, y escondía un fallo vivo.**
El campo de habilidad **no es un byte**: el mod guarda un **noveno bit por hueco** en el último byte
de la entrada (0x53), y usa **79 habilidades nuevas en 104 entradas**, no 22. El randomizador escribía
solo el byte y dejaba el bit, así que con `randomizeAbilities` en `true` (lo está) repartía la 50 + 256
= 306, o habilidades **que no existen**: en el mundo instalado, **240 huecos rotos en 104 Pokémon, 184
apuntando más allá de la 319**. Arreglado en `PersonalEntry7` y en el módulo de datos, con la misma
semilla dando lo mismo salvo esos bits. **Aplicado el mismo día**, y el mundo regenerado sobre la **1.4 del mod** e instalado (§133).

**Y el mismo día entran todos en el randomizado (§136).** `maxAbility` y `maxMove` pasan a 0 a petición del jugador,
tras ver funcionar en combate cuatro habilidades y doce ataques nuevos. Ponerlos a 0 no habría bastado: el techo se
recortaba además con `GameInfo` de pk3DS, **233 y 728 clavados**, la misma trampa que el 807 de las especies. Ahora sale
de las tablas del juego. Fuera quedan los huecos sin nombre y `bannedAbilities`: las habilidades del mod atadas a las
formas de un Pokémon concreto. Medido en seco: solo cambian habilidades y aprendizajes, nada más.
Instalado ese día desde la app. Y el **gacha y el wonder trade** también las reparten (§137): descartaban todo lo que
pasara de la 233 por la misma premisa falsa, con `Data/species.json` regenerado sobre la 1.4 y un solo sorteo
(`AbilityDraw`) con la misma lista de excluidas que el randomizador.

**Y las formas regionales (§138).** Nunca había salido ninguna: todos los módulos escribían forma 0. Ahora salvajes,
entrenadores, estáticos, regalos e iniciales pueden salir en las **57 formas de Alola, Galar, Hisui y Paldea** del mod
(`regionalForms` en el JSON), decididas **después** de la especie y con su propio sorteo, así que la especie de cada
sitio no cambia: medido, 45.848 huecos salvajes con cero especies distintas y 1.214 en forma regional. Instalado.
Y **el gacha y el wonder trade también (§139)**, con la forma sorteada de una fuente derivada —el resto de la tirada
sale igual— y los tipos de la forma. **Un dibujo por forma**: los de Alola van justo antes del normal (§30) y los de
Galar, Hisui y Paldea salen de la tabla de 136 claves que el propio `code.bin` del mod recorre, desensamblada y
comprobada a ojo. Y ya con forma el cementerio, POKE PASTE («Vulpix-Alola») y la lista de quién puede salir del gacha (§140); la run guarda la forma en una columna nueva, con la primera migración de su tabla.

**Medido qué copia de la tabla de especies lee el juego (§141): la ENTERA**, el último subfichero de `a/0/1/7`. Las MT
solo se escribían en ella y en las copias sueltas no, y con el equipo del jugador discrepaban en 94 de 100; la MT43 en
el juego salió como dice la entera. O sea que las MT siempre funcionaron. Ahora se escriben en las dos por coherencia.

**El recuerda-movimientos, en la app (§142).** La señora del juego está en el Monte Lanakila y **no se puede mover de
forma fiable** (personas y scripts del mundo sin identificar; un script mal escrito cuelga el juego), así que hay sección
**MOVIMIENTOS**, como el de Añil en randomlocke: lo que sabía al llegar (apuntado desde hoy en el evento de captura, más
los huecos «para volver a aprender» del propio Pokémon), lo que su especie **actual** aprende al evolucionar (nivel 0) y
por nivel hasta el suyo, todo del **mundo instalado**. Juego cerrado, por PID, copia y relectura, evento
`MoveRemembered`; un caído no aprende nada y **no cuesta puntos**. De paso: lo que entrega la app aprendía los
movimientos **del cartucho** (PKHeX) y uno de gen 8-9 ninguno; ahora los del mundo, con sus PP.

**ENTRENAR EV y MOVIMIENTOS, con el aspecto de la bolsa del juego (§143).** Solo presentación: fondo naranja a cuadros,
el equipo en tarjetas verdes, la caja como bolsillo de la bolsa con **los iconos al doble**, cápsulas con flecha roja,
ficha con cremallera y los cuatro movimientos del color de su tipo, como en combate. Todo pintado en celdas
(`Views/BagPixels.cs`, `Themes/Bag.xaml`), sin dibujos del juego, con franjas planas y trama en vez de degradados; el resto
de la app no cambia. `--tamano normal` abre a otro tamaño **sin guardarlo**, para ver una pantalla en pequeño.

**MOVIMIENTOS, con los iconos oficiales de categoría (§144).** Están en el cartucho: `a/0/6/6` lleva una lámina de 64×64
con los tres (estado, físico y especial) y `MoveCategoryIconReader` los talla y **los nombra por su color, no por su
orden**. Se sacan de la ROM del jugador a `Data/sprites/categorias`, como el resto de sprites. En pantalla: la lista
**seguida**, sin apartados; cada movimiento con su placa de tipo, el icono (o «-----» si es de estado) y POTENCIA,
PRECISIÓN y PP en cajitas; «---» donde el juego tampoco pone número; y las **seis estadísticas del Pokémon** en la
ficha, coloreadas por la naturaleza, las mismas que en ENTRENAR EV. Y la **descripción** del ataque elegido, y la del que
se va a olvidar, con el texto del propio juego (fichero 117 de `a/0/3/6`, leído del mundo instalado; sin mundo no hay).
`AlytCarver` es ahora el tallador común de los cristales Z y de estos iconos.

**Los objetos de evolución clásicos, cada lista en su tienda (§145).** A 30.000: las once piedras en el Centro Pokémon de
la Ruta 8 (mostrador 14), los doce de intercambio en el de la Ruta 2 (11) y seis sueltos en la tienda de incienso de
Konikoni (8), por `specialMartShelves`. Los de gen 8-9 de la Ruta 2 pasan a Pueblo Paniola (15) y al Supermercado Ultraganga
(21 y 22), y la Moneda de Gimmighoul a **30**, porque Gholdengo pide 999. Los índices salen de las etiquetas de pk3DS, y
**una falló**: el 24 dice Ruta 3 y allí no hay Centro Pokémon, así que no se sabe dónde está y no se usa. Instalado desde la app y
comparado byte a byte. **Venderlos da la mitad**, porque el precio es del objeto.

**La escena de los iniciales nombra a los de verdad (§146).** 21 líneas del texto de historia (`a/0/4/6`, ficheros 38,
39 y 51) pasan a decir el nombre y los tipos del inicial randomizado, y las tres descripciones se sustituyen enteras.
Se escribe con `GameTextPatch`, que solo toca esas líneas, porque pk3DS reescribe el fichero entero y no igual. Ojo: pk3DS
**lee** el salto de línea como las dos letras `\n`. `starterText` en `randomizer.json`. Sin ver dentro del juego.

**Las Poké Balls retiradas pertenecen a su run (§147).** «Empezar de cero» no borraba `Saves/backup/objetos-retirados.txt`,
y la partida nueva recibió en la Ruta 1 los doce tipos de ball que se le debían a la anterior, Master Ball incluida, y con
ellas saltó antes de tiempo el premio de las Super Balls. Ahora el fichero dice de qué run es (`WithheldLedger`) y el de
otra run no se devuelve: se aparta con fecha. Una partida nueva empezada dentro del juego, sin «empezar de cero», sigue
sin distinguirse.

**Cinco copias para los amigos (§148).** Carpetas `2` a `6` dentro de `G:\Mi unidad\PermaLocke Competición`, con el
reparto de `publicar.ps1` sin los LEEME y `Config\sync.json` a `".."`: una ruta relativa se resuelve ahora contra la
carpeta de la aplicación, así que cada copia encuentra la competición en cualquier PC. Cada amigo necesita acceso a la
carpeta de la competición entera, y pone su ROM en `ROM\`. Cada carpeta lleva además el mod de gen 8-9 (2,4 GB) con su
`README.txt`: es su licencia, CC BY-NC-ND, que obliga a conservar los créditos al compartirlo.

**Las rutas cuentan desde la primera Poké Ball (§149).** Antes de tener ninguna, un combate salvaje no gasta la ruta
ni marca el MAPA. La primera ball vista queda como `FirstPokeBallSeen`, y desde ahí cuenta siempre.

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


**La lista de vigilancia, y tres fallos mios en una noche (2026-09-05).** Poner el parche a trabajar
en una partida de verdad destapo tres cosas, **ninguna del parche**. Una: `EnsureWatchList` estaba al
PRINCIPIO del ciclo, lanzo cuando el jugador salio al menu del emulador -ahi el RPC deja de contestar
un instante- y **se llevo por delante la deteccion de muertes**; una comodidad no puede tumbar el
trabajo, asi que va detras, envuelta, y la firma se guarda solo si el refresco salio bien. Dos: la
lista se perdia al reiniciar cualquiera de los dos programas, asi que ahora **se adoptan al conectar**
los caidos que ya esten en el equipo, por PID del historial y nunca por «ese parece un Shedinja
MUERTO» -esta run tuvo uno real que nunca fue una muerte (§59)-.

Y tres, el que importa: `RefreshWatchList` daba por hecho que el muerto ocupa **el mismo hueco en
todas las estructuras**, y no lo ocupa -medido unas horas antes en el §93-. Se mandaron cinco
direcciones y **cuatro llevaban a otro Pokemon**, cosa que canta en las etiquetas del log del
emulador. Es la regla que yo mismo puse EN el emulador, olvidada al elegir que direcciones mandarle:
el guardia comprobaba bien y yo le daba mal la lista. Ahora se busca el PID hueco por hueco y `Watch`
no manda nada si no coincide.

**No hubo daño, y el motivo importa**: el emulador no repuso nada -cero `marker restored`- porque
aquellas direcciones ya eran escombros y la etiqueta no cuadraba. **La comprobacion de identidad del
lado del emulador tapo un fallo del lado del cliente.** Tres veces la misma noche di por buena una
POSICION en vez de comprobar la IDENTIDAD -tambien escribiendo 102 PS en un Tinkaton y 999 en un
Gyarados-, y las tres con el dato correcto ya medido. La regla que sale de ahi: ninguna funcion que
escriba en la partida deberia aceptar una direccion sin un PID al lado. Ver §96.

**El emulador mantiene la marca de muerte (2026-09-05).** Parche 2 del fork, **en `master` y
verificado**. Un tipo de paquete nuevo, `WatchBlock`: PermaLocke le da al emulador una direccion,
una etiqueta y el bloque que debe haber ahi, y el emulador lo repone **cinco veces por segundo**
desde un hilo propio. El emulador se queda **tonto a proposito** -no cifra, no calcula checksums, no
sabe que es un Shedinja-; los bytes se le dan hechos y se mandan **en crudo, cifrados**, porque
pasarlos por PKHeX registraria un bloque que no existe en el juego y el emulador reescribiria el
hueco con basura. La etiqueta es la constante de encriptacion, y si deja de coincidir **no se toca
nada**: la identidad por delante del §53.

Verificado con numeros: `Azahar Version: 87ed55b`, `Block watcher started.`, «El emulador vigila 5
huecos», cinco `WatchBlock` aceptados, y un byte cambiado a mano (`FA` → `66` → **`FA`**) con su
`The game rewrote 0x330128E4; marker restored`. Lo que NO demuestra: quien deshizo el bloque fui yo,
no el juego.

**Las dos cosas que hicieron fracasar la primera prueba, ninguna del parche.** El binario que se
instala no es el que se abre —se puso en `Emulator/` y el jugador abre desde `Nuevo_azahar/`, y lo
delato la version del log-. Y **un log filtrado no es un log vacio**: Azahar trae
`RPC_Server:Error`, asi que la linea informativa en la que se apoyaba toda la comprobacion era
invisible. Un plan de verificacion que depende de un mensaje hay que comprobarlo tambien a el.

Y el fallo propio: `RefreshWatchList` volvia **en silencio** cuando el emulador no aceptaba la
lista. Ya lo dice. Alcance: cubre las muertes de **esta sesion** de la aplicacion, y sigue siendo
memoria -cerrar sin guardar la pierde, como la de la referencia-. Ver §95 y
`docs/fork/02-muerte-permanente.md`.

**Los PS no se pueden clavar a cero (2026-09-04).** El jugador pregunto si se puede dejar al Pokemon
TAL COMO ESTA y que siga muerto en rojo aunque lo curen. **Medido contra el juego, y la respuesta es
no por la via obvia**: con el equipo en `0x330128E4` se escribieron 7 PS en el Gyarados del hueco 0
-un solo byte, offset `0xF0`-, se releyo 7 de 131, aguanto quince segundos sin que el juego lo
pisara, y el menu del equipo seguia marcando **131 de 131 con la barra llena**. La escritura entra,
se queda, y el juego no la mira.

Encaja con el §53: la copia autoritativa es la de salto `0x1E4` y guarda las estadisticas de combate
en otro sitio. Lo que si obedece la copia de `0x104` es el **bloque cifrado** -especie, mote, nivel-,
que es justo lo que escribe el marcador de muerte y por eso ese si se ve. Para clavar los PS haria
falta localizar donde los guarda la estructura de `0x1E4`, que es una investigacion del tamaño del
§22. El comentario de `--write-hp` decia «ahora abre el menu y comprueba si lo refleja»; se programo,
nadie fue a mirar, y ahora la respuesta esta escrita en el codigo con sus numeros.

Se busco tambien DONDE estan los PS, ya con la estructura autoritativa localizada, y **no se han
encontrado**: no aparecen en sus 484 bytes -ni en la cola, que no va cifrada-, y barriendo heap y
linear enteros no hay **ningun cuarteto** de los cuatro valores del equipo a paso constante. O estan
en otra forma, o el juego los **deriva** al dibujar, como ya observo el §53 del nivel. La via que
queda es `--scan` y `--refine` cambiando los PS dentro del juego, que necesita al jugador. Ver §92.

**Dos diagnosticos falsos, y los dos eran la sonda (2026-09-04).** Por el camino apunte como
problemas de la aplicacion que «el barrido no encuentra el equipo» y que «Tinkaton va dos niveles por
detras del juego». **Las dos eran de la sonda**, y por la misma causa: `WorldLimits` es estado GLOBAL
que arranca en 807, la aplicacion lo sube al arrancar leyendo la tabla del mod, y **la sonda no lo
hacia**. Con 807, el localizador rechazaba al Ursaluna (901) del hueco 1 y se quedaba sin confirmar
ninguna estructura; y sin las curvas del mod, un nivel derivado de la experiencia caia a la tabla de
PKHeX -que acaba en la 807- y daba 40 para Tinkaton. Medido con las tres cifras juntas: `exp=68225,
Nv(exp)=42, Nv(pkhex)=40, Nv(0xEC)=42`. **La aplicacion leia bien las dos cosas.**

La sonda llama ahora a `InstalledWorld.ApplyQuietly` antes de leer un byte, con el fichero
**enlazado** desde la aplicacion en vez de copiado, y `--peek` enseña los tres niveles para que una
discrepancia se vea en vez de elegir uno. Es el §65 con otro traje: **una herramienta de diagnostico
puede estar peor calibrada que el codigo que diagnostica**, y cuando lo esta no falla, contesta con
seguridad. Ver §91.

**El Shedinja que se deshizo (2026-09-04).** Al jugador se le murio un Latias, PermaLocke lo convirtio
en Shedinja, y al volver a entrar tenia otra vez un Latias debilitado. **Ni el registro ni la
escritura fallaron**: la muerte esta en el historial con su penalizacion, y el log ensena cinco copias
escritas y RELEIDAS una a una. Lo que pasa es que quince segundos despues el jugador cerro el juego,
y **esa marca vive en la memoria del emulador**: sobrevive solo si se guarda dentro del juego. Es la
frontera de siempre del §66 -memoria viva contra fichero de partida-, y lo que faltaba era decirla EN
EL MOMENTO en que se escribe.

Ahora `GameLinkMonitor` lanza `DeathMarked` y HOME lo enseña por el mismo canal que los premios
automaticos: «esta solo en la memoria: guarda dentro del juego, o escribelo en la partida desde
MANTENIMIENTO». La herramienta ya existia -CONVERTIR A LOS CAIDOS EN SHEDINJA, que escribe la partida
y es permanente- y su propio texto ya lo explicaba; lo que no habia era ningun camino desde la muerte
hasta ese boton. Medido en la partida real: **sin marcar 2, ya son Shedinja 5**. Y NO se hace solo al
cerrar el juego a proposito: el precedente del §68 anade Super Balls, esto **destruye un Pokemon** en
el fichero y de forma permanente. Ver §89.

**Volcanion con el dibujo de Hoopa (2026-09-04).** Un wonder trade devolvio Volcanion y la tarjeta lo
enseno como **Hoopa Desatado**. La tabla de especie a icono del §30 bis se construyo a mano porque el
cartucho no la publica, y en un sitio se identificaron cruzados: el tramo real es 1001 Diancie, 1002
Mega Diancie, **1003 Volcanion**, 1004 Hoopa, 1005 Hoopa Desatado -o sea que Volcanion va DELANTE de
Hoopa, al reves que en la Pokedex- y la tabla tenia 720 en 1003 y 721 en 1005.

Lo importante es **por que no lo cazo el guardia**: la comprobacion del bloque exige 158 especies,
iconos distintos y todos en rango, y **un cruce pasa las tres**. Detecta que falte o sobre alguien, no
que dos esten intercambiados, y por construccion no puede. Lo unico que lo encuentra es mirar, asi
que se miro el bloque ENTERO: diez hojas de contacto con el icono de cada especie y su nombre debajo,
leidas las **158 de 158**. Esta pareja es el unico error. La tabla queda auditada de punta a punta.

Y no es un fallo que del wonder trade salga un legendario: `permitirLegendarios` esta en true y la
banda ya limita sola -hay que entregar algo de 600 para sacar uno-. Ver §88.

**El gacha reventaba en CADA tirada (2026-09-04).** Dos tiradas, dos «error inesperado», y lo
primero que dice el log es que no se perdio nada: Roserade y Salamence entregados y registrados.
`ReelEnding` obliga a que el ultimo tiempo del cierre sea 1,00 -el carrete acaba en el ganador-, asi
que el ultimo clic cae en el 1,00 de la tirada y pedia su rebote en el **1,006**; `KeyTime.FromPercent`
lanza por encima de 1. Fallaba desde que se anadieron los cinco cierres, no dos veces. Ahora la linea
de tiempo de los golpes dura un 6% mas que la tirada y las fracciones se reescalan a ella -ese ultimo
rebote ES el golpe del aterrizaje-, y ademas se recorta a [0,1], asi que la excepcion es imposible
aunque alguien escriba un cierre raro.

Lo que de verdad costo fue que **la excepcion se escapaba**. La tirada esta escrita en la partida
antes de animar un fotograma, asi que una animacion rota deberia costar la animacion y nada mas -la
leccion del §84 en la ruleta-. El gacha no la tenia porque arranca el giro desde un
`Dispatcher.BeginInvoke`, o sea FUERA del await, donde ningun try del ViewModel la ve: subia al
manejador de la aplicacion, enseñaba un error por algo que habia funcionado, y dejaba al ViewModel
esperando OCHO SEGUNDOS a su red de seguridad. Ver §87.

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

**El Huevo Malo, y un comentario que avisaba de su propio fallo (2026-09-05).** El jugador guardó y
su Shedinja salió como un huevo. No era un huevo: era un **Huevo Malo**, que es lo que el juego
dibuja cuando una entrada no cuadra con su firma de control, y **ningún campo lo anuncia**. Un huevo
de verdad es la bandera `IsEgg`, que vive en el **bit 30** del entero que guarda los seis IV — de
ahí que una escritura torcida en los IV pueda incubar un Pokémon sin tocar nada más. `Probe --huevo`
pone los dos lados juntos para no volver a confundirlos.

En memoria estaba perfecto; en la partida, de los cuatro bloques de un PK7 **el del mote y los
movimientos era exactamente lo escrito** y los de especie y encuentro eran basura, y **las dos
basuras compartían tramos largos**. Eso no es escribir en el sitio equivocado: es texto en claro
donde debería ir cifrado.

Dos fallos, los dos en `Watch` y los dos míos del §96. **PKHeX descifra el array que se le da, en el
sitio**, así que la comprobación de identidad que añadí convertía los bytes que después se le
entregaban al emulador en su versión descifrada: el emulador estuvo estampando texto en claro sobre
un hueco cifrado **cinco veces por segundo**, y al guardar el juego leyó ese hueco a medio
reescribir. Lo humillante es que **el comentario de esa misma función avisaba del peligro exacto**;
un comentario que describe un riesgo no lo impide, lo impide el código. Y segundo, se registraban
**260 bytes** cuando el Pokémon acaba en el 232: lo que sigue lo actualiza el juego sin parar, así
que el guardia veía diferencia siempre. El §53 ya lo prohibía para las escrituras y el guardia
también tiene que obedecerlo.

`WatchBlockTests` levanta un emulador falso que apunta lo que se le registra, **verificado que falla
con el código viejo** en sus dos mitades. Y una partida ya rota se arregla con `Probe --huevo
--arreglar <captura>`, que reconstruye desde la captura previa a la escritura — un Huevo Malo no se
repara en su sitio porque dentro no hay nada legible. Ahí la identidad va por la **constante de
encriptación** y no por el PID, única excepción a la regla del §96 y por un motivo medido: el PID
está dentro del bloque roto, y esos cuatro bytes del principio van en claro. Ver §97.

**Los PS del equipo no se pueden clavar desde fuera, y ya está medido (2026-09-06).** El jugador
quería que un muerto se quedara **siendo él** a cero PS en vez de volverse Shedinja. La respuesta es
que no, y lo decide **una sola medida** que el §93 no tomó: se escribieron **120** PS por el camino
correcto —descifrar, cambiar, cifrar, releer— y con el menú del equipo abierto la pantalla decía
**128** mientras la memoria **seguía en 120**. O sea que `0x330128E4` es un espejo de **una sola
dirección**: el juego escribe ahí y **nunca lee**. El §93 acertó por el motivo equivocado —escribió
un byte en claro sobre un campo cifrado— y yo corregí el motivo y me quedé con su conclusión al
revés sin comprobarla.

Lo demás quedó excluido con su medida, no por descarte: los PS no aparecen en claro como valor que
los siga, ni como daño recibido, ni desalineados, ni como tabla del equipo a paso constante hasta
8192, ni como bloque de seis valores en 64 bytes —tampoco **con el menú abierto y los números en
pantalla**—, ni a ningún offset de las entradas, ni en crudo ni **descifradas**. Y buscando por la
constante de encriptación solo hay **tres** copias de cada Pokémon: el espejo, la de salto `0x1E4`
—cuya cola son cabeceras del asignador— y un **objeto de gráficos** que empieza por la constante y
sigue con once punteros a *shaders*. Ninguna es un almacén vivo.

Por qué no podía encontrarse: las estadísticas de un PK7 de equipo van **cifradas** con el resto, así
que 118 en pantalla es `EF A6` en memoria; y lo que el juego usa **no vive lo suficiente** —si
descifra al Pokémon en una pila para dibujar la barra y lo tira, una pasada de 30 s sobre 400 MB no
lo pilla nunca—. Lo único en claro es la barra de vida, que es pintura: guarda actual, máximo, el
valor **anterior** y los porcentajes, o sea la interpolación de la animación.

**Y el Shedinja se ha ido (§98 ter).** La marca de muerte es ahora quedarse sin PS, y como `DeathMark` era
ya la única definición de «qué le pasa a un cadáver», cambiarla ahí la cambió en la ruleta y en el
escritor a la vez. Se marca **sola al cerrar el emulador**, que es el único momento en que se puede
escribir la partida. Solo vale en el **equipo**: en una caja un Pokémon no lleva estadísticas de
combate, así que los de caja se cuentan y se dicen en vez de saltarse en silencio. Y se fue con él
toda su maquinaria -el vigilante `WatchBlock`, la lista, `DeathTransform`, `ApplyDeath`-, que existía
para sostenerlo en memoria y encima es el código que corrompió una partida en el §97.

**La otra puerta está abierta (§98 bis).** El juego no lee el espejo de
memoria, pero **sí lee los PS del fichero de partida al cargar**: se escribieron **55** PS al
Tinkaton con el juego cerrado y al cargar el menú decía 55. `SaveFainter` deja a los caídos a **0 PS
siendo ellos** —misma especie, mote, movimientos y nivel—, por PID y por nada más, con copia previa
y relectura del fichero, y hay botón en MANTENIMIENTO en dos pasos. No es irreversible, que es la
diferencia con el Shedinja y está dicho en la tarjeta: un Centro Pokémon revive a un caído sin PS y
no hay forma de impedirlo desde fuera. Es «muerto entre sesiones». **La muerte automática sigue
usando el Shedinja** a propósito: es lo que se ve en el momento, y ya se cambió una vez esa noche
sin verificar y hubo que revertirlo.

Ojo con lo que **no** era prueba: se propuso esta vía apoyándose en que el Mudsdale muerto está a
`0/13` en la partida, y eso no demuestra nada porque ese Mudsdale se murió de verdad y el cero lo
puso el juego. Plausible no es medido.

Lo que sí queda: **`PartyStats.AreHere`**, que decide **midiendo** si la cola de una entrada son las
estadísticas. Antes se preguntaba el salto, y eso es un proxy: las **dos** estructuras de salto
`0x104` leyeron en el mismo segundo `118/131` y `42649/10902`, así que el cap de nivel y la marca de
muerte llevaban tiempo escribiendo en bytes que nadie ha identificado. El ancla es que un Pokémon de
equipo **lleva el nivel dos veces** y donde la cola es buena coinciden. Ver §98.

**Y hay un Huevo Malo en la partida guardada, sin reparar a petición del jugador (2026-09-06).**
Hueco 3 del equipo, especie 13740, checksum que no cuadra: es del §97 y lleva ahí desde el día
anterior. Se reparó **un** hueco aquel día y este no, porque `--huevo --arreglar` tenía el hueco
**fijo en el primero**; ya se elige con `--hueco`. Está **solo en el fichero**, no en la partida en
curso. Hay copia buena y el ensayo sobre una copia reconstruye bien
(`#292 «MUERTO» Nv 1 PID 8EC2769F`). Ojo: una especie tan fuera de rango es buena candidata a
colgar el juego, y esa noche se colgó al huir de un combate con `pc = 00000000`.

**Y encontrado: los PS viven en `0x1E4 + 0x158` (§99).** El parche 3 del fork lo cazó. Al guardar, el
juego hace dos memcpy -232 bytes del Pokemon desde `[r4+8]` y **28 de estadisticas desde `[r4+4]`**-
y el origen de la segunda es ese offset. Verificado contra la pantalla: 77 escritos ahi, 77 en el
menu. Eso cierra el §53, que llevaba meses diciendo «las guarda en otro sitio» sin decir donde, y
destapa un fallo de fondo: **el monitor leia los PS del espejo, que va con retraso**, asi que un
Pokemon podia caer y seguir leyendose sano hasta que guardaras. Ya se lee la estructura buena, y
`KeepFallenDownAsync` devuelve al suelo a los caidos **una vez por segundo**. Ver §99 y §99 bis.

**Y el parche 3 del fork, que es lo que lo hizo posible (§98 quater).** El jugador quiere que un muerto lo esté todo el
rato, y desde fuera no se puede: el espejo del equipo no se lee ni abriendo el menú ni entrando y
saliendo de un combate -medido con 40 PS escritos a un Ursaluna de 161: 161 antes y 161 después-. La
vía es que el emulador diga QUIÉN escribe los PS en ese espejo, porque esa instrucción sabe de dónde
vienen. **El parche está en `master` del fork y compila** (`ace262e`), y el cliente hecho y probado
contra un servidor falso (`Probe --escrituras`). No hizo falta pelearse con el JIT como preveía la
especificación: Azahar **ya trae puntos de observación de memoria** para su depurador, y
`RegisterWatchpoint` anula el puntero de la página, con lo que la escritura vuelve sola al camino
lento -comprobado además que el JIT usa el array de punteros y no hay fastmem-. **Nadie lo ha
ejecutado contra el juego todavía**, y puede acabar diciendo que no se puede si el juego descifra en
una pila y la tira. Ver `docs/fork/03-donde-vive-el-ps.md`.

**Siguiente.** Probar en partida real los combates importantes, los iniciales y las tiendas.

Y decidir entre los cinco el trueque del combate por link, que ya está medido: **o estadísticas
barajadas y habilidades al azar, o poder pelear entre vosotros.** Las dos cosas a la vez no.

Lo que NO está resuelto todavía y no debe darse por hecho (detalle en `docs/ARCHITECTURE.md`):

| Tema | Estado |
|---|---|
| Lectura del equipo en vivo | **RESUELTA Y VERIFICADA** contra el juego real |
| Detección automática de capturas y muertes | **NO FUNCIONABA Y YA PUEDE** — el 97 % de los Pokémon no tenía PID, que es lo único por lo que empareja. Corregido para lo nuevo y reparable para lo viejo con `Probe --pids`; sigue sin haberse visto una muerte real. Ver `ARCHITECTURE.md` §56 |
| Muerte en el momento, durante el combate | **HECHA Y VERIFICADA CON UNA MUERTE REAL** — el combate lleva los PS en bloques de 800 bytes, dos tablas (cálculo al instante, barra con retraso), en `0x30002748` y `0x30009730` en los tres combates medidos. Una caída es cuando las dos dan cero tras verlo en pie. `Probe --combate detector` vio caer a dos salvajes en el momento y soltar las tablas al terminar. **Sin ver todavía**: entrenadores, dobles (solo se mira la primera caja) y SOS. La investigación **congeló el emulador dos veces** —memoria inexistente, y luego una ráfaga de 137 búsquedas—: el lector solo busca un megabyte cada 3 s y nunca en bucle. **La escena espera a ver la barra de PS llegar a cero en la pantalla** (las tablas lo saben antes: cambian con el mensaje del ataque); verificado con la barra de un rival y **con una muerte del jugador** (Empoleon, «barra a cero vista a los 478 ms», justo a tiempo según el jugador). Ver `ARCHITECTURE.md` §114, bis, ter y quater |
| Cementerio y killcam | **HECHO, VISTO EN LA APP Y CON UNA KILLCAM REAL** — una tumba por caído, del tamaño de lo que aguantó, y su historia sacada de los eventos. La killcam graba la pantalla de arriba solo en combate y guarda −4,5 s / +1,3 s alrededor de la barra a cero; se amplía, va a cámara lenta y fotograma a fotograma. **Copiar la pantalla copia lo que haya delante**: descarta fotogramas con otra ventana encima de Azahar, y deja de grabar mientras sale la escena de muerte, que sí se copia (lo contrario de lo que decía el §113). **Sin ver**: una muerte con esos dos arreglos puestos. Ver `ARCHITECTURE.md` §115 |
| El cielo de Alola | **HECHO Y VISTO EN LA APP** — una franja en pixel art detrás de la cabecera con el mar, las cuatro islas y el cielo a la hora del juego, y una ventanita igual en la barra lateral (solo si cabe). Hubo una versión con degradados y resplandores por toda la ventana y **se tiró porque parecía hecha por una IA**: aquí funciona lo dibujado celda a celda, no lo difuminado. La hora sale de la configuración del Azahar abierto (hora local, leído en su código) más las 12 h de Ultra Luna, sin tocar la memoria; con la hora fija de Azahar lo dice en vez de inventar. Los tramos del día no están medidos contra el juego. Ver `ARCHITECTURE.md` §116 |
| Avisos encima del juego | **REHECHOS EN PIXEL ART, VISTOS EN RENDERS** — caja con contorno, relieve y sombra dura a celdas de 3 píxeles, pestaña de color por tipo de aviso, placa con el sprite del cartucho saltando como en el menú de equipo, marcas dibujadas a mano (estrella de variocolor, prohibido, flecha, cruz) y cuenta atrás en doce segmentos. Nada de difuminados. Sin ver todavía encima de Azahar. Ver `ARCHITECTURE.md` §120 |
| Poke Paste | **HECHO Y VISTO EN LA APP** — exporta **el equipo** en formato `pokepast.es`, en inglés. Solo exporta. Ver `ARCHITECTURE.md` §57 |
| Sprites de los cristales Z | **RESUELTO** — tallados del ALYT `a/1/5/5`, que los nombra `item_807..824`. Cuál es cuál va **por color** y así está dicho. Los de Dominsignia **no existen** en el cartucho. Ver `ARCHITECTURE.md` §61 |
| Randomización vía LayeredFS | **RESUELTA Y VERIFICADA** en el juego — salvajes y textos |
| Escritura en el juego (cap de nivel, objetos) | **RESUELTA Y VERIFICADA** — exige el fork propio de Azahar |
| Marca de muerte: 0 PS siendo él | **RESUELTA EN VIVO Y VERIFICADA** — el juego lee los PS de la estructura de salto `0x1E4` en el offset **`0x158`**, hallado con el parche 3 del fork y comprobado contra la pantalla (77 escritos, 77 en el menú). PermaLocke los escribe ahí y devuelve al suelo a los caídos una vez por segundo. Antes solo sabía escribir en el espejo, que el juego rellena y nunca lee. **Dejó de funcionar sin avisar** (§135): la lista de direcciones del equipo tenía ocho días y la estructura del juego cambia de sitio cada sesión; ahora, si al conectar solo se lee el espejo, se vuelve a barrer. Arreglado y **verificado en el juego** el 2026-09-19: barrió al conectar y devolvió dos veces a Bouffalant a 0 PS. Ver `ARCHITECTURE.md` §98, §99, §99 bis y §135 |
| Mochila del juego (leer, poner cantidad, añadir lo que no llevas) | **RESUELTA Y VERIFICADA** en el juego — ver `ARCHITECTURE.md` §22 |
| Zona actual del jugador | **RESUELTA Y MEDIDA EN DOCE SITIOS** — el ancla del §23 murió (§55) y se retiró. Ahora son los registros de posición del juego: mundo y mapa delante de las coordenadas, validados con `Data/mapas.json` (el mapa tiene que ser de ese mundo), con dos copias de acuerdo, localizados con la posición guardada o la firma del aterrizaje, y sobreviven a reiniciar. **Conectar después de dar unos pasos encontraba una sola copia** y no había zona en 2 minutos: ahora se prueban las direcciones de la última sesión y, con menos de dos, una búsqueda por mundo y mapa encuentra las demás (6 en 16 ms, medido). Ver `ARCHITECTURE.md` §117 y §118 |
| MAPA | **SE MARCA SOLO, SIN JUGAR TODAVÍA** — ya no se pincha: PermaLocke marca cómo acaba el primer combate salvaje de cada ruta (capturas y huidas por los récords del juego, K.O. por las tablas), con la regla de Poké Balls encendida o apagada. Si la zona no se leía al empezar, se lee al acabar. Las 18 zonas que la run ya tenía marcadas a mano se quedan y la carta lo dice; una marca a mano nunca pisa una detectada. Ver `ARCHITECTURE.md` §118 |
| Fuera del MAPA no se captura | **HECHO, SIN JUGAR TODAVÍA** — donde no hay marcador no hay Poké Balls nunca, ni andando ni en combate. Excepciones: un variocolor, y las capturas estáticas de `allowedStatics` en `rules.json` (Necrozma del Monte Lanakila, que en este mundo es un Wo-Chien y no una mega, y los cuatro Tapus), reconocidas por zona y por la especie que el mod instalado puso en su fila del cartucho. Como no se sabe si un combate estático sube el récord de combates salvajes, la excepción también salta leyendo al rival en las tablas del combate. Ver `ARCHITECTURE.md` §119 |
| Regla de primer encuentro (Poké Balls) | **HECHA Y ENCENDIDA, SIN JUGAR TODAVÍA** — en las rutas del MAPA: el primer combate salvaje gasta la ruta, en una gastada no hay Poké Balls, un duplicado de línea no se atrapa ni gasta, un variocolor siempre. Combate salvaje por el récord 4 del juego (sube al empezar, cuenta al huir); quitar Poké Balls en mitad del combate verificado. Cada pieza medida, 946 tests; **falta verla entera en una partida** y con un variocolor real. Ver `ARCHITECTURE.md` §117 |
| Gacha (motor, probabilidades, reproducible) | **HECHO** — 220 tests; **visto funcionando en la app, con sprites** |
| Entrega del Pokémon al PC del juego | **VERIFICADA EN LA PARTIDA REAL** — dos entregas a la caja 1 con copia previa. Exige el juego cerrado |
| Extraer los sprites de Pokémon de la ROM | **RESUELTA Y VERIFICADA** — `a/0/6/2`, RGBA5551, 1154 iconos a PNG en 1,4 s; ver `ARCHITECTURE.md` §28 |
| Saber qué icono es de qué especie | **RESUELTO PARA LAS 807** — construido a mano en dos bloques; el reparto cierra sin iconos libres ni repetidos y hay tests. Ver `ARCHITECTURE.md` §30 y §30 bis |
| Visor Pokémon (leer el PC de la partida) | **HECHO Y VERIFICADO** en la partida real — 149 Pokémon, 32 cajas, ficha al pinchar. Solo lectura; sin equipo, sin formas, sin cruce con la run. Ver `ARCHITECTURE.md` §32 |
| Entrenar EV | **HECHO Y VISTO CON LA PARTIDA REAL, SIN GUARDAR DESDE ELLA TODAVÍA** — sección propia debajo del VISOR, que ya no edita EV (solo lleva a ella con ENTRENAR EV). Equipo y cajas a la izquierda; retrato, naturaleza con lo que sube y baja, hexágono de EV rasterizado celda a celda y seis filas con `−`/`+` de 4, MÁX y 0. Cada fila dice **la estadística que quedaría**, con la misma fórmula y tabla del mundo que usa el escritor, y al nivel guardado junto a las estadísticas (un Ferrocuello real va a 64 por experiencia y a 59 por nivel guardado), y **por especie y forma**: una forma regional usa sus propias bases, también al guardar (§131, medido con 304 formas del mundo instalado). **Nunca escribe una entrada dañada** (Huevo Malo): lo impide el servicio. Con el aspecto de la bolsa del juego desde el §143. Ver `ARCHITECTURE.md` §130 y §143 |
| Recuerda-movimientos | **HECHO Y VISTO CON LA PARTIDA REAL, SIN RECORDAR DESDE ELLA TODAVÍA** — sección MOVIMIENTOS, con la regla del de Añil: lo que sabía al llegar, lo de evolución y lo de su nivel, de su especie actual y del mundo instalado. La escritura probada sobre una copia de la partida real (`Probe --recordar --probar`). Con el aspecto de la bolsa del juego (§143) y los iconos oficiales de categoría sacados del cartucho, con la lista toda seguida (§144). Ver `ARCHITECTURE.md` §142, §143 y §144 |
| Wonder trade (banda de BST, escritura, animación) | **MOTOR Y ESCRITURA HECHOS** — 16 tests y verificado contra una copia real del save. **La animación es una cabina en pixel art** en la sala del gacha (§174): la ball sube y baja por tubos de cristal y la pantalla de la cabina da generación, tipos y total. Vista con un intercambio real en una copia aislada. Generación y tipos de gen 8-9 leídos del mundo instalado (antes salían «7» y «?»). **Solo acepta Pokémon vivos**: el servicio rechaza a un caído de la run, por PID, antes de sortear nada. Ver `ARCHITECTURE.md` §33, §121 y §174 |
| Logros y penalizaciones | **HECHO Y VISTO EN LA APP** — motor, pantalla y 6 tests. Falta la lista real de logros, que la tiene que dar el jugador. Ver `ARCHITECTURE.md` §36 |
| Contadores del juego (movimientos Z, huidas, shiny, entrenadores) | **HECHO Y VISTO EN LA APP** — se leen de `SAV7USUM.Records`; 28/200 huidas y 6/100 entrenadores reales. Ver `ARCHITECTURE.md` §38 |
| Detectar pruebas, pegatinas y alto mando | **HECHO Y VISTO EN LA APP** — los 21 logros se cuentan solos y no queda un botón de marcar a mano. Las doce pruebas por su cristal Z, con la correspondencia sacada del storytext del cartucho. Ver `ARCHITECTURE.md` §43 |
| Tiradas gratis y wonder trades por prueba | **HECHO Y VISTO EN LA APP** — crédito ganado de los logros y gastado del historial, con la marca `gratis`; los wonder trades pasan a estar limitados. Ver `ARCHITECTURE.md` §63 |
| Ruleta del rol LUDÓPATA | **HECHA Y VISTA EN LA APP** — 16 caras, seis en la rueda, tiradas que se deben y se recomputan; escribe equipo, mochila y cajas por el fichero de partida. **Dos tiradas verificadas en la partida real.** Ver `ARCHITECTURE.md` §62 |
| Pantalla de la ruleta, rehecha | **REHECHA EN PIXEL ART (§175) Y VISTA CON DOS TIRADAS REALES EN UNA COPIA AISLADA** — rueda de feria en la sala recreativa: aro con bombillas, clavijas que doblan la flecha, caras que se destapan una a una, tablero de premios y castigos, marcador DEBES, fichas y palanca, confeti o alarma al parar. Verde paga y rojo cuesta, un solo barrido hasta la ganadora (§84, §86), ahora con pruebas que lo exigen para todos los perfiles de frenada. Ver `ARCHITECTURE.md` §84, §86 y §175 |
| Roles | **HECHOS Y VERIFICADOS CONTRA LA ROM** — se eligen lo primero, multiplican los puntos, suben el nivel de todo lo que combate contra ti (entrenadores, Dominantes, Necrozma y legendarios) y añaden el Pokémon extra en 35 clases de combate importante. Falta probarlo jugando. Ver `ARCHITECTURE.md` §46, §47 y §48 |
| Dificultad de los entrenadores | **HECHA, VERIFICADA CONTRA LA ROM E INSTALADA, SIN JUGAR** — módulo propio configurado en `trainerDifficulty` de `Data/randomizer.json`: IA Básica+Fuerte+Experta (0x07) sumada a todos los entrenadores (antes 166 de 653), IV +10-20 % de lo que tenían (un 0 sigue a 0), EV con la misma cantidad pero repartidos para la especie randomizada, y objetos equipados hasta el 25 % (antes 12 %). Movimientos, habilidades, naturalezas y rol sin tocar. Con la seed real: sin el módulo sale idéntico al mod instalado; con él solo cambian esos campos. Explicación para cualquiera en `subida_de_dificultad_explicada.txt`. Ver `ARCHITECTURE.md` §122 |
| Sincronización | **HECHA Y VISTA EN LA APP** — clasificación por carpeta compartida, sin servidor ni cuentas. Cada uno publica un resumen y lee los de los demás. **No es una verificación y la pantalla lo dice.** Falta probarla con la carpeta sincronizando de verdad entre dos máquinas. Ver `ARCHITECTURE.md` §79 |
| Jugadores y carpeta por jugador | **HECHO Y VISTO EN UNA COPIA AISLADA, SIN DOS PCS** — perfil en `Config/jugador.json` (id fijo, nombre cambiable), la run guarda su dueño (las viejas se vinculan al arrancar con `PlayerLinked`; una run ajena no se toma nunca), y en la carpeta compartida `jugadores/<nombre>-<id>/` con `perfil.json`, `run-activa.json` e `historial.json`. El resumen se **comprueba contra el historial** (cadena, eventos, último hash, puntos = suma de deltas) y cada app recuerda lo último visto para cazar copias restauradas: CUADRA / NO CUADRA / HA RETROCEDIDO / SIN HISTORIAL, y lo que no cuadra va detrás. **No es antitrampas**: un historial rehecho con herramientas cuadra. Reglas oficiales en `reglas/`, validadas con los cargadores de la app, adoptadas con copia previa y `RulesAdopted`; se aplican al reiniciar. Ver `ARCHITECTURE.md` §123 |
| Lanzador (JUGAR) | **HECHO Y PROBADO CONTRA EL EMULADOR REAL** — primera sección y de arranque: portada en píxeles con tu equipo del cartucho en una playa de Alola a la hora del juego, botón JUGAR que abre Azahar con tu ROM (el del reparto, o un Azahar **no portátil** para no abrir otra partida), vigilancia del proceso aunque se abra a mano, CERRAR JUEGO con pregunta dentro de la barra y cierre limpio de su ventana (1,3 s medido), FORZAR CIERRE aparte, tiempo jugado por run en `Saves/<run>/sesiones.json`, y lo que falta para jugar en una línea (los paneles de abajo pasaron a amigos y actividad, §126). Botón pequeño en la cabecera de todas las pantallas. Apaga la pregunta de salir de Azahar (`confirmClose`). Ver `ARCHITECTURE.md` §125 |
| Amigos y actividad en JUGAR | **HECHO Y VISTO EN UNA COPIA AISLADA, SIN DOS PCS** — como la página de un juego en Steam: a la derecha los jugadores de la competición en verde jugando, azul en la app y gris desconectado, y a la izquierda los logros que reclama cada uno, por días. Presencia en `jugadores/<nombre>-<id>/presencia.json` cada 45 s y al abrir o cerrar el juego; más de 3 min sin escribir es desconectado. La run se republica sola cuando crece (cada 90 s como mucho) para que la actividad llegue. `--sin-juego` lee pero no escribe. Ver `ARCHITECTURE.md` §126 |
| Regalos del admin | **HECHO Y VISTO EN UNA COPIA AISLADA** — el admin deja un regalo en `admin/regalos/` de la carpeta compartida (puntos, objetos, tiradas de gacha o wonder trades, siempre con motivo) y la app del jugador lo enseña en un regalo de la cabecera con su contador; se recoge a mano y queda en su historial como `AdminGiftClaimed`, una sola vez. Con objetos exige Azahar abierto y no empieza si no lo está. Ver `ARCHITECTURE.md` §129 |
| Mod de expansión de gen 8 y 9 | **IMPLEMENTADO Y VERIFICADO CONTRA LOS FICHEROS, SIN JUGAR** — randomiza encima del mod, 1025 especies, sprites, nombres en español oficial e instalación de la capa entera. La Ruta 1 da Dreepy, Meltan, Snom y Tarountula. **Nadie ha arrancado el juego con esto**: sin comprobar que instale, que el `code.bin` del mod arranque, que el texto generado no cuelgue, ni el combate por link. **Habilidades de nueve bits (§132)**: el randomizador las rompía en 104 Pokémon; arreglado y aplicado. **La 1.4 del mod está instalada** (§133): español oficial, animaciones por código, mismo orden de iconos; el mundo de la run sale idéntico salvo lo que trae el mod. Sin jugar todavía con ella. **Cuatro Pokémon de prueba en la caja 2** (§134) con habilidades y movimientos nuevos, metidos con `Probe --dar-mod` y registrados como dados por el admin; de paso, **el Pokémon guardado también lleva el noveno bit de habilidad** (bit 4 de 0x15) y **el nivel de las especies del mod se escribía y leía con la curva de PKHeX**, arreglado en el constructor, el visor y la ruleta. Ver `docs/MOD-EXPANSION.md` |
| Combate por link entre jugadores | **VERIFICADO Y CERRADO** — se puede combatir con cada uno su propio mundo, siempre que `shuffleBaseStats` y `randomizeAbilities` esten en `false` en todos. **Medido con una prueba controlada**: con esas dos en `true` el combate se desincroniza, con ellas en `false` no. Desde la sección COMBATES da igual el mundo de cada uno: solo tiene que coincidir el juego base (con o sin gen 8-9), y COMPETICIÓN lo avisa así. Ver `ARCHITECTURE.md` §80 y §124 |
| Copia de seguridad de la run | **HECHA** — al arrancar, diez copias rotativas. Antes había 319 copias de la partida y cero de la run. Ver `ARCHITECTURE.md` §76 |
| Mantenimiento desde la aplicación | **HECHO Y VISTO EN LA APP** — auditoría, reparar PID, cerrar entregados y corregir etapas, sin terminal. Ver `ARCHITECTURE.md` §77 |
| Estadísticas | **HECHA Y VISTA EN LA APP** — libro de puntos, curva de saldo, colección y récords, todo proyectado sobre la cadena de eventos. Ver `ARCHITECTURE.md` §78 |
| `PermaLocke.Admin` | **EMPEZADA (§129)** — una ventana sobre la carpeta compartida: quién hay, si sus números cuadran, y mandar regalos. No abre la base de datos de nadie. Falta el resto del control: arreglar muertes, corregir pruebas y editar las reglas oficiales |
| API concreta de pk3DS.Core | **VERIFICADA** contra la ROM real — ver `ARCHITECTURE.md` §19 |


## Revisión del mundo instalado — 2026-09-21

Comparado todo lo que cambia el randomizador contra la capa base, y está bien salvo dos cosas, ya corregidas para los
mundos que se generen a partir de ahora: los **legendarios de gen 8-9 salían en la hierba** (ahora `wildBannedSpecies`,
solo salvajes; entrenadores y estáticos los siguen llevando, por decisión del jugador) y **seis evoluciones por
movimiento del mod eran imposibles** (ahora por nivel; Hydrapple al 45). Lista para jugadores en
`EVOLUCIONES CAMBIADAS.txt`. El mundo instalado del jugador no cambia hasta que se regenere. Ver `docs/ARCHITECTURE.md` §151.

**Y el mismo día, jugando (§152):** los combates de la Ruta 1 caían en «Afueras de Hauoli» porque unas
transformaciones basura en (1, 0, 0) se leían como mundo 0 / mapa 0 y eran lo único que votaba al empezar un combate;
ya se rechazan, y un empate entre registros reales lo decide el que se mueve. Y Azahar se cerraba al poner el nombre
durante el primer barrido del equipo: ya no se barre sin partida guardada ni cuando sus Pokémon no están en memoria.
Y preparando la carpeta de los amigos: en una instalación nueva, con solo el inicial, el equipo no se encontraba (el
barrido exige dos Pokémon) y sin equipo no funcionaba nada; ahora se localiza por la partida guardada, sin barrer. Y el
mundo instalado se relee al instalar, no solo al arrancar. Carpetas en el escritorio: `PermaLocke para amigos` (la que
se reparte) y `PermaLocke prueba` (copia idéntica para jugar). Ver `docs/DISTRIBUCION-LOCAL.md`.
Primera partida en la carpeta de prueba (§153): la regla de las balls **perdonaba** deudas en vez de devolverlas (mi
arreglo de la deuda doble tomaba «cero y cero» por una recarga), en la Escuela no había ruta porque dos mapas de la
misma zona empataban, y un combate de la run anterior se terminaba en la nueva. Los tres arreglados.
El **cap de nivel** volvía a no verse en pantalla (§154): solo bajaba la experiencia; el nivel del menú vive en la cola
de `0x1E4 + 0x158`. Ahora baja también ese nivel y recalcula las estadísticas con el mismo código de ENTRENAR EV.
Y el **gacha entrega al equipo** si tiene hueco (§155), con las estadísticas calculadas con la tabla del mundo; si no, al PC.
Y **abrir la bolsa ya no te devuelve a la ruta anterior** (§156): la bolsa apaga los registros que siguen al jugador y
votaban solos los que guardan dónde estuvo. Una zona se deja cuando los que la decían cambian de opinión o uno anda, no
cuando se callan.
Y **las megas de los combates importantes empiezan después de la sexta prueba** (§157): `megaTrainerMinimumLevel` pasa
de 1 a 29 de cartucho (la Gran Prueba de Mayla es un 28, cap 34). El Nihilego del Paraíso Æther sigue siendo mega, a
propósito. Reinstalar el mundo con la misma semilla solo cambia el hueco de la mega.
Y **los ataques aprendidos van de flojo a fuerte** (§158): el sorteo tomaba cada ataque del catálogo entero sin mirar el
nivel y un 59 % eran de 80 o más desde el nivel 5 (5 % en el cartucho). Ahora se ordenan por potencia, como UPR, pk3DS y
la referencia (`learnsetReorderByPower`, elegido por el jugador); la curva exacta del cartucho queda como opción.
Y **dos cierres de Azahar en una tarde** (§159): la búsqueda del equipo por la partida guardada miraba el heap de
`0x08000000`, que durante el vídeo de inicio no existe (28.871 lecturas de memoria inexistente, y murió); ya solo mira el
heap lineal, donde está todo. Al cambiar de mapa, la zona se buscaba justo mientras el juego recargaba el campo; ahora
espera a que la duda dure 5 s. Y **ya no se pueden abrir dos PermaLocke**: había dos a la vez escribiendo en el mismo juego.
Y **los combates de una prueba ya no gastan la ruta** (§160): en la Sala de la Prueba de la Cueva Sotobosque y la del Dominante de la
Colina Saltagua, hasta tener el cristal Z, un combate salvaje no gasta ni marca salvo que acabe en captura. Los Dominantes
que se pelean dentro de una ruta normal (Jungla Umbría, Ultraganga abandonado, Cañón de Poni) quedan pendientes de medir. La sala
del Dominante de la Cueva Sotobosque sale del MAPA: ahí no se atrapa nada.
Y **curarte en el Centro Pokémon ya no cuenta como otro equipo caído** (§161): en pie es con PS y vivo en la run, y al abrir
la app el estado sale del historial. Para lo ya cobrado hay `WipeRevoked`, que devuelve lo que quitó y no cuenta para los cuatro.
Y **los ataques fulminantes están prohibidos para todos** (§162): Guillotina, Perforador, Fisura y Frío Polar salen de los
aprendizajes, huevos, entrenadores y estáticos del mundo (`bannedMoves`, cambiando solo esos huecos) y de lo que la app
enseña o entrega (`WorldMoves.Banned`).
Y **los objetos del suelo salen al azar, como en la carpeta Locke** (§163): cada sitio se sortea entre los bolsillos de
objetos, medicinas y bayas del juego (con gen 8-9, 597 objetos), como mucho dos veces cada uno, y cada Poké Ball dorada
entre las cien MT. Nunca objetos clave, cristales Z ni Master Ball. `fieldItemsMode` en `randomizer.json`; `Shuffle` es
lo de antes y da el mismo mundo byte a byte. Solo cambian los huecos de objeto de `a/0/8/3`.
Y **cruzar a otra ruta sin puerta ya no se cuenta en la de detrás** (§164): unas copias de la posición que el juego solo
refresca a ratos se quedaban en el mapa anterior y le ganaban la votación al registro que te sigue, hasta que un combate
las ponía al día. Ahora, en otro mapa, manda el registro que anda. Explicación que encaja con el log pero sin ver los
registros en ese momento; el log los apunta desde ahora cuando pasa.
Y **la muerte por veneno o por trampa de rocas ya no tarda seis segundos** (§165): el vigilante de la barra de PS tomaba
el **suelo naranja** del combate (170,110,65) por una barra llena, así que esperaba su tope entero. Ahora el relleno son
los tres colores de la barra, una caja vacía sin color previo cuenta como la caída, y una caja que no aparece se espera
2,5 s y no 6. Medido sobre las killcams reales.
Y **cada Pokémon vuelve a aprender lo suyo** (§166): las listas por nivel salían del azar puro y solo el 9,6 % era del
tipo del Pokémon (cartucho 48,7 %, referencia 28 %), así que todos jugaban igual. `learnsetSameTypePercent` (el viejo
interruptor, ahora un porcentaje) en 20 da 27,9 %, la referencia clavada. La potencia ya estaba como la referencia y la
tolerancia de potencia NO la reproduce, la aplana. Hay que regenerar el mundo.
Y **el log ya no se ahoga en sí mismo** (§167): el 61 % de las líneas de un día eran «Azahar propio encontrado», escrita
hasta 3,6 veces por segundo, más un «Récords leídos» cada 20 s con los mismos números. Ahora se escriben cuando cambian.
Salió de los cierres de un amigo con `PermaLocke para amigos` (dije que tenía otra carpeta y era falso; corregido en el
§167). La causa probable es la del §168.
Y **el emulador lleva su Visual C++** (§168): `azahar.exe` se compila con las herramientas 14.51 y con un runtime anterior
se cierra a media partida (fallo documentado por Microsoft); la carpeta no llevaba ninguno y cada PC ponía el suyo.
`publicar.ps1` copia las seis DLL al lado del emulador, comprobando firma y versión; medido que se cargan de ahí. Es la
causa probable de los cierres de un amigo, sin comprobar en su PC. Y cuando Azahar se cae sin que nadie lo cierre, la app
escribe sola `Diagnosticos\cierre-azahar-<fecha>.zip` (código de salida, PC, logs y las últimas 128 peticiones al
emulador) y avisa. JUGAR avisa antes de lo que el PC puede estropear: runtime, OneDrive, permisos, espacio.
Y **la portada de JUGAR es ahora el cuarto del entrenador** (§169), en pixel art y con la run de verdad: la vitrina con
el cristal Z de cada prueba superada y la silueta de los que faltan, una figurita de piedra por caído hecha con su
sprite, la última killcam en la tele, el equipo vivo en la alfombra, el tope en un póster, las Dominsignias en el corcho y
la ventana a la hora de Alola. Sustituye a la playa, que se ha borrado. HOME tendrá otro rediseño más adelante.
Y **el gacha vuelve a su sitio** (§170): la ficha del resultado y la tira parada se quitan solas a los 8 s o al cambiar
de pestaña (un aviso de entrega pendiente se queda), y LO QUE HA SALIDO ya no pasa de una run a otra al empezar de cero.
Verificado con tiradas reales en una copia aislada, y con el ejecutable anterior reproduciendo el fallo.
Y **el gacha es ahora una máquina de cápsulas en pixel art** (§171), elegida por el jugador: la cúpula lleva las balls
del banner en sus proporciones reales (una ball por tier: Poké, Super, Ultra, Gloria y Master), la ball rueda hasta la
alfombrilla, se sacude tres veces subiendo de ball y se abre con el Pokémon, y a los 8 s vuela a la estantería de ÚLTIMAS
TIRADAS. Misma duración para todos los tiers; las probabilidades no se tocan. Sustituye a la ruleta y al ultraespacio.
Y **la carpeta de amigos se reparte en un zip** (§172): `publicar.ps1` deja al lado `<destino>.zip` con una carpeta
`PermaLocke` dentro, y `empaquetar.ps1` se niega a comprimir una carpeta que alguien haya abierto (la contrasta con
`Soporte\contenido.json`). El `EMPIEZA AQUI.txt` ya no recomienda el Escritorio: se extrae en `C:\Juegos`, porque
OneDrive sincroniza el Escritorio en muchos PC.
Y **los cierres de Azahar del amigo eran del emulador** (§173): las lecturas y búsquedas del RPC volcaban la caché de la
GPU desde su hilo, y con OpenGL una búsqueda de 64 MB lo cerraba o congelaba. Parche 4 del fork (`0dfe782`,
`ReadBlockNoFlush`), verificado: el emulador viejo cayó en 6 de 6 intentos con las dos firmas exactas del amigo, el
nuevo en 0 de 4. `Emulator/` y la carpeta de amigos llevan ya el nuevo (`azahar.exe` `06143878…`). **Confirmado el 2026-09-24: al amigo ya no se le cierra.**
Y **el wonder trade es una cabina en pixel art en la misma sala que el gacha** (§174): el Pokémon entra en su ball con el
rayo rojo, sube por un tubo de cristal al techo, la pantalla busca con un globo y dice ¡CONECTADO!, otra ball baja por el
otro tubo y rueda a la plataforma, y la pantalla da generación, tipos y el total contado desde el que entregaste (verde
mejor, rojo peor) antes de que se abra. La sala es ya común (`PixelScene.PaintRoom`); el gacha sale idéntico píxel a
píxel. `TradeStage.cs`, la escena del §121, se ha borrado a petición del jugador. La carpeta de amigos ya la lleva.
Y **la ruleta del LUDÓPATA es una rueda de feria en pixel art en la misma sala** (§175): aro de oro con bombillas,
clavijas que doblan la flecha al pasar, las seis caras que se destapan una a una, el tablero de premios y castigos con la
ganadora marcada, el marcador DEBES, las fichas que pagan cada tirada y entran por la ranura, la palanca, confeti o alarma
roja al parar. Misma lógica que antes: verde paga, rojo cuesta, un solo barrido y la ganadora bajo la flecha (§84, §86).
Y **una muestra del estilo en píxeles para toda la aplicación** (§176), pendiente de que el jugador decida: marco,
barra lateral, cabecera y HOME con piezas propias (`Views/Pixel/`: fuente propia que no es un TTF, texto, iconos por
sección, sprites a píxeles enteros) y en HOME el recorrido de las doce pruebas con su cristal Z. Solo presentación. Está
puesta en la carpeta de prueba, no en la de amigos.
Y **más pulido, y el VISOR en píxeles** (§176 bis): ventanas con barra de título (`PixelWindow`), pestañas, botones que
se hunden al pulsar, subtítulo de cada pantalla e icono por evento en el historial. El visor es el PC del juego: equipo
como el menú del equipo, 32 cajas en miniatura, y ficha en tres páginas con tipos y ataques del color de su tipo.
Y **todas las secciones en píxeles, y fuera ESTADÍSTICAS** (§176 ter): catorce pantallas convertidas, con campo de texto,
casilla y desplegable nuevos en el kit. ESTADÍSTICAS sale del menú; sus ficheros esperan confirmación para borrarse.
Quedan sin convertir los diálogos, la tarjeta del wonder trade y el reproductor de la killcam.
Y **ENTRENAR EV y MOVIMIENTOS sin bolsa, y los diálogos** (§176 quater): diseño general con un selector de equipo y cajas
compartido, los tipos del elegido en las dos, y en píxeles los tres diálogos, la tarjeta del wonder trade, la killcam y
los nombres de las tumbas. ESTADÍSTICAS borrada. La bolsa (`Bag.xaml`, `BagPocketPanel`, `BagPixels.cs`), borrada
con confirmación del jugador.
Y **limpieza y dos fallos del kit** (§177): CEMENTERIO sin el bloque de datos, COMBATES sin COMPROBAR, MISCELÁNEA sin
escamas, HOME con IR A en vez de CAÍDOS, ÚLTIMAS ACCIONES y cambiar rol, y **MANTENIMIENTO sustituida por CONFIGURACIÓN**
(`Config/ajustes.json`). Los botones con piezas pixel sin fondo no recibían clics (JUGAR, pestañas del visor, portales,
chinchetas): toda plantilla interactiva lleva `Background="Transparent"`. Y la marca de las casillas nunca se pintaba.
Las reparaciones de MANTENIMIENTO quedan solo en `Probe`.
Y (§178) HOME sin IR A, GACHA con la máquina entera y el bote a la izquierda, stats del visor de la tabla del mundo, y
premios sin «0 → 1» ni «Nv · naturaleza · IV». `Probe --credito` acepta `PERMALOCKE_ROOT`.
Y (§179) **Colina Saltagua y Jungla Umbría sin Poké Balls hasta tener el cristal Z** de las pruebas 3 y 5 (`trialZones`, `EncounterSituation.PendingTrial`); la Sala del Dominante de la Colina sale del MAPA.
