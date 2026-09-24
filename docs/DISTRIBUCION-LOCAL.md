# Distribucion local (20 de septiembre de 2026)

La distribucion para amigos es independiente y se ejecuta en un disco local. Sustituye el reparto
mediante carpetas de Drive descrito en las notas historicas. Cada jugador tiene su perfil, run,
semilla, emulador portatil y partida. La expansion viaja dentro; la ROM la aporta cada jugador.

`tools/publicar.ps1` crea una carpeta NUEVA; nunca cierra aplicaciones ni borra un destino existente.
Al terminar deja al lado un zip de esa carpeta, listo para subir (`-SinZip` para no hacerlo): un solo fichero, porque
Drive parte las carpetas grandes en varios zips. `tools/empaquetar.ps1` solo empaqueta una carpeta tal como se publicó
(contrasta `Soporte/contenido.json`), así que nunca sale un zip con la partida de nadie. Se extrae en `C:Juegos`,
nunca en el Escritorio ni en Documentos, que en muchos PC sincroniza OneDrive (§172).
Publica Release win-x64 autocontenido, sin trimming, incluye expansion y mapa, y excluye partidas,
configuraciones personales y caches. El marcador `PermaLocke.local` fija la raiz junto al ejecutable,
impide tomar una expansion de la carpeta superior, desactiva la sincronizacion y oculta comunidad y
competicion. El proyecto de desarrollo conserva sus datos y la compatibilidad con instalaciones anteriores.
El manifiesto `Soporte/contenido.json` registra los hashes del paquete limpio. Los fuentes exactos van en ZIP.

## Portabilidad y sprites

- .NET va incluido en el ejecutable publicado. Copiar `bin` o el repositorio no equivale a publicar.
- Azahar recibe un `user/config/qt-config.ini` nuevo con RPC y resolucion nativa 1x; no se copian
  GPU, mandos, rutas, shaders ni datos de la instalacion del desarrollador.
- El instalador opcional de Visual C++ se valida con la firma de Microsoft antes de incluirlo.
- PokemonIconIndex extrae solo la tabla personal, en vez del workspace completo del randomizador.
- PokemonSpriteService reintenta si antes faltaba la ROM o fallo la extraccion; no memoriza imagenes
  nulas y su cache concurrente separa instalaciones/versiones de ROM y expansion.

## Verificacion y limites

1.260 pruebas automatizadas pasaron tras estos cambios, incluidas cinco regresiones nuevas.
El cierre comunicado por el amigo era de Azahar. No hay log de ese PC: su causa NO esta diagnosticada
ni se afirma que estos cambios lo solucionen. `RECOGER DIAGNOSTICO.cmd` recopila registros, version,
CPU, RAM, GPU y ajustes, sin ROM ni saves y sin enviar nada. Debe ejecutarse antes de reabrir el juego.
La expansion declara que sigue en desarrollo; resolver un fallo del emulador o del mod exige reproducirlo.

Verificacion con ROM real, en carpetas de prueba aisladas: 807/807 especies de vanilla y
1025/1025 de expansion, Poké Ball, Master Ball, tres categorias de movimiento y Vulpix de Alola.
Preparacion en este PC: 1,55 s vanilla / 1,10 s expansion; cache: 0,046 s / 0,059 s.
No es una medida del rendimiento de otro PC ni de los FPS del emulador.
Arranque del ejecutable publicado desde otro directorio de trabajo: raiz local correcta,
sin run, sin errores; host de .NET confirma `Executing as a self-contained app`.
La tienda se vio con sus iconos tras agregar la ROM en la misma sesion.
El recopilador de diagnostico se ejecuto con Windows PowerShell 5.1 y genero su ZIP.

## Optimizacion de estabilidad (segunda revision, 20 de septiembre)

- La killcam reutiliza un anillo de 141 fotogramas y un buffer de captura. Evita los
  384.000 bytes nuevos por fotograma (7,68 MB/s a 20 fps) cuando el anillo esta lleno.
  Conserva resolucion, frecuencia y marcas temporales. Las copias para guardar una muerte
  son independientes de la grabacion posterior; al quedar inactiva expira y libera el anillo.
- El hilo de captura se crea solo al grabar. Su token se libera despues de la ultima espera,
  evitando una carrera al cerrar la app. Los recursos GDI se liberan en su propio hilo.
- Ventana del juego, lanzador y reloj liberan los objetos Process despues de consultarlos,
  incluidos los procesos descartados. Esto libera handles sin cerrar Azahar.
- Cada intento RPC tiene un plazo total aunque lleguen respuestas antiguas continuamente.
  Se mantiene la recuperacion de datagramas perdidos y respuestas tardias. Los identificadores
  son atomicos y los cambios de timeout se sincronizan con las peticiones en curso.

Validacion: 1.267 pruebas correctas (App 9, GameLink 299, Core 403, Rules 129, Randomizer 427).
Las siete pruebas nuevas cubren limite y orden del anillo, instantaneas independientes,
ocultacion/caducidad, cambio de combate, asignaciones tras calentamiento, cierre antes de
iniciar captura y plazo RPC ante un flujo de respuestas atrasadas.
La prueba de 1.200 fotogramas tras llenar el anillo admite como maximo 1 KB de asignaciones,
frente a los 460,8 MB de nuevos arrays que exigia esa cantidad de capturas.
Estas cifras describen asignaciones de la app, no una reduccion equivalente de RAM ni una
medicion de FPS. No se ha reproducido el cierre de Azahar en el PC del amigo.

## Correccion tras el informe de cierres del 20 de septiembre

El informe del amigo corresponde al ejecutable optimizado (SHA256 96656529D1E7859EFFB0D77EADE729D5D97755350EE39A276D261267412946B3).
El registro de la app repite busquedas completas del equipo aproximadamente cada 6 segundos.
Azahar alcanza 100 MiB de log a los 16,24 segundos: 202.119 peticiones RPC, otros tantos
avisos de proceso no seleccionado y 61.526 lecturas no mapeadas. Ese corte no es la hora
del cierre y no contiene su excepcion final; tampoco se ha reproducido la escena de Kukui.

Correcciones:
- El enfriamiento del buscador se aplica tambien al fallo y a la interrupcion, desde el final
  de cada intento. Una instalacion nueva tambien espera 20 lecturas antes del primer barrido.
- Se vuelve a seleccionar el juego tras un intento fallido y antes de un barrido.
- Equipo y mochila rechazan los barridos sin un proceso seleccionado. Durante el barrido se
  comprueba la seleccion cada 64 KiB y se cede tiempo cada 256 KiB. El reinicio detiene el
  barrido en lugar de continuar leyendo 96 MiB de la nueva sesion sin seleccionar.
- El monitor no consulta la mochila ni aplica reglas cuando el snapshot esta desconectado.
- El diagnostico incluye los errores de Windows de Azahar/PermaLocke y los ajustes por juego,
  porque el limite de tamano del log del emulador puede ocultar el cierre.

El localizador sigue exigiendo dos Pokemon contiguos para confirmar el salto de la estructura;
no se relaja esa validacion de memoria para aceptar un inicial aislado como prueba de equipo.
El enlace puede seguir esperando al principio de la aventura, pero ya no debe barrer en bucle.
No se regeneran semillas ni se alteran mods, partidas, configuraciones personales o datos de la run.
Validacion de esta correccion: 1.274 pruebas correctas (GameLink 306, App 9, Core 403, Rules 129, Randomizer 427), incluidas siete regresiones nuevas con un servidor RPC simulado.

## Aviso de las primeras Poke Balls

El evento FirstPokeBallSeen ya se guardaba en el historial, pero PlayNotifications no tenia
ninguna suscripcion a esa primera deteccion. Ahora BallControlService emite FirstBallDetected
solo despues de guardar el evento, y se muestra el aviso "Primeras Poke Balls" con su icono.
La comprobacion se serializa para que los dos bucles de vigilancia no creen registros o avisos
duplicados. Una run que ya tiene el evento no repite el aviso al cargarla o reiniciar la app.
Una mochila ilegible no genera un aviso y un fallo al guardar permite reintentar sin anunciar
algo que no se ha registrado.

Validacion: 1.279 pruebas correctas (Rules 134, GameLink 306, Core 403, Randomizer 427, App 9).
Cinco pruebas nuevas: aviso tras persistir y una sola vez, carga de una run existente,
mochila vacia/ilegible, dos monitores simultaneos y fallo/reintento al guardar.

## Ventana de notificaciones en otros equipos

Notifier mezclaba las coordenadas Win32 de Azahar con Left/Top de WPF. Con escalado
de Windows distinto de 100%, una ventana desplazada o un monitor secundario,
los avisos podian quedar fuera del area visible. Ahora se colocan con SetWindowPos,
en coordenadas nativas, dentro del area util del monitor del juego; si no hay juego
se usa el monitor de PermaLocke. Una ventana minimizada no aporta su posicion oculta.
La escala se vuelve a consultar tras cambiar de monitor y la pila de tarjetas se
reduce si no cabe en una pantalla pequena, conservando todos los avisos visibles.

Los fallos sincronos y asincronos del despacho de UI quedan observados en Notifier,
sin propagarse al monitor del juego. El log distingue aviso solicitado, omitido por
desactivacion, ventana mostrada (posicion, escala y monitor) y error de visualizacion.
Los avisos siguen respetando la casilla "Avisarme mientras juego".

Validacion: 1.293 pruebas correctas (App 23, Rules 134, GameLink 306, Core 403,
Randomizer 427). Catorce regresiones cubren 100/125/150/200%, monitores con origen
negativo o positivo, pantalla pequena, ventana fuera de pantalla, juego en ventana,
fallos sincronos/asincronos y avisos desactivados. Prueba visual en copia aislada:
las cuatro tarjetas de "VER UN AVISO" aparecen, caducan y pueden volver a mostrarse.
La prueba local se hizo a escala 100%; no reproduce el PC ni la pantalla completa
del amigo. Su causa exacta sigue pendiente de la prueba manual o un informe nuevo.

Comprobacion para el jugador: MISCELANEA > marcar "Avisarme mientras juego" >
"VER UN AVISO". Si estos salen pero los del juego no, comprobar que HOME indique
conexion y el equipo real, y recoger un diagnostico nuevo despues de una situacion
que deberia producir un aviso. No repetir ni reiniciar la run para forzar eventos
que ya se guardaron (como las primeras Poke Balls).

## Arranque sin guardar, encuentros y killcam

Caso comunicado: primeras Poke Balls notificadas, equipo conectado, encuentros sin
registrar; el jugador no habia guardado inicialmente y guardar despues no lo arreglo
de inmediato. Ademas comunica una muerte tardia y sin repeticion.

Fallos reproducidos y corregidos:
- FieldZoneReader podia consumir su busqueda antes del primer guardado y mantener
  dos minutos de espera aunque despues apareciera una partida. Cuando la zona es
  desconocida ahora detecta un guardado nuevo y usa su posicion para recuperar la
  lectura, conservando un minimo de 20 segundos entre busquedas y sin buscar durante
  un combate. Guardar repetidamente el mismo archivo no provoca barridos en bucle.
- BattleCounterReader esperaba hasta un minuto incluso si entretanto se habia
  localizado una mochila valida. Reintenta la referencia corta cada cinco segundos,
  valida antes su tabla de punteros y conserva el minuto minimo para la busqueda
  completa. No dispara un barrido de mochila desde el lector de contadores.
- Una direccion de contadores ilegible se descarta tras tres fallos consecutivos.
  Una lectura aislada fallida conserva la direccion; la recuperacion no inventa un
  incremento ni un encuentro que ocurrio antes de disponer de una referencia.
- HOME muestra por separado los problemas de deteccion de encuentros aunque el
  equipo este conectado: falta de guardado, contadores pendientes o ruta desconocida.
  Los cambios de estado quedan en el log.
- Toda muerte automatica registrada solicita guardar la grabacion disponible,
  incluso cuando el monitor de equipo gana la carrera al del combate.
  El cementerio se refresca al terminar de escribir una killcam, no solo al registrar
  la muerte, que ocurre antes de que exista el archivo.

Validacion: 1.308 pruebas correctas (GameLink 318, App 26, Rules 134, Core 403,
Randomizer 427). Siete regresiones con RPC simulado y un guardado USUM sintetico:
primer guardado, enfriamiento sin cambios, no buscar durante combate, mensaje sin
guardado y contadores futuros, mochila disponible despues, direccion perdida y
fallo transitorio. Tres pruebas del guardado asincrono de video: refresco tras
completarlo, ausencia de fotogramas y fallo de escritura.

Limites pendientes: no se ha reproducido la muerte del PC del amigo ni confirmado
si llego por las tablas del combate, por los PS del equipo al terminar, o por el
limite de seis segundos del lector visual de la barra. No se cambia a ciegas ese
limite ni se amplian los barridos del combate que anteriormente congelaban Azahar.
Hace falta un informe actual posterior a ese caso para diagnosticar el retraso.
Una killcam que no se llego a grabar no puede reconstruirse con este parche.

Ante la intermitencia posterior se cubren cinco regresiones adicionales:
- Un contador localizado por busqueda puede ser una copia estatica aun legible.
  Si aparece la mochila, se prefiere su referencia validada, comprobada cada cinco
  segundos sin un nuevo barrido. Un puntero de mochila roto no sustituye la lectura.
- Una lectura RPC fallida de una tabla ya localizada no termina inmediatamente el
  combate ni borra la transicion de PS: se conserva el seguimiento durante dos
  lecturas fallidas. Tres fallos seguidos permiten recolocar las tablas; una lectura
  correcta de un bloque liberado sigue dando por perdida la tabla inmediatamente.
  La busqueda mantiene su ventana original de 1 MB y su intervalo de tres segundos.
- Tampoco se buscan registros de posicion durante un combate reconocido por tablas
  que no tenga contador salvaje (por ejemplo, un entrenador).
- El diagnostico incluye hashes de mapas y marcadores, referencias de lectura
  recordadas y fecha/tamano/ruta de guardados portatiles, nunca su contenido.

Los mapas, marcadores y Azahar del paquete coinciden por SHA-256 con los de la
carpeta de trabajo. La instalacion de desarrollo conserva referencias de sesiones
anteriores; eso puede influir en el arranque, pero no prueba la causa en el amigo.
El fallo de la siguiente ruta necesita un diagnostico nuevo de esa sesion.
La notificacion de primer encuentro se emite una vez por ruta, no cada vez que
se vuelve a una ruta ya gastada.

La compilacion Release habitual estaba bloqueada por un proceso de PermaLocke
sin ruta accesible. Las pruebas de App se ejecutaron en Debug (26 correctas).
La version local nueva se prepara en src/PermaLocke.App/bin/Actualizada-20260920,
sin emulador propio, conservando la resolucion de datos y Azahar de la instalacion.
El acceso directo local apunta a ese ejecutable, sin modificar partidas.

## Informe del amigo de las 22:09:59: zona desconocida y muerte por otro monitor

El SHA-256 A60916218B8F95619E49F54A2E68E06FD5F550A275581C8171CA0D653BEEFD0D
corresponde a .dist/notifications/PERMALOCKEEEE/PermaLocke.exe: el informe aun
usa la version previa a la revision 2 de encuentros.

Evidencias del registro:
- De 21:34 a 21:45 se leen especies y combates salvajes pero la zona es desconocida.
  El localizador conserva un solo registro (0x33F6E510) y busca el mapa 1/mundo 4.
- A 21:45:38, 22:04:00 y 22:08:50 la busqueda de hermanos del mapa 0 devuelve
  exactamente 255 candidatos. Antes se aceptaba esa primera pagina como el total.
- A 22:07:18 un encuentro se situa en Afueras de Hauoli; tras terminar, a 22:08:50,
  se identifica Escuela Entrenadores. Esto evidencia problemas de resolucion de zona,
  aunque el informe no contiene una captura simultanea de pantalla que permita
  reconstruir donde estaba el jugador en cada lectura.
- A 21:38:32 la especie 831 de la posicion de combate 0 no coincide con el equipo.
  La muerte se registra a 21:38:46 por memoria del equipo. Esa rama explica que no
  hubiera repeticion con la version antigua. A 21:53:36 otra muerte si guarda 92
  fotogramas. No se atribuye todo el retraso a rendimiento o a la barra de PS.

Revision 3, acumulativa:
- Las busquedas de hermanos paginan. Hay un presupuesto total de seis consultas
  por busqueda, compartido con los patrones de guardado/aterrizaje. Si no basta,
  se recuerda el cursor y se retoma al vencer el intervalo existente.
- A partir de registros ya validados se inspeccionan como maximo dos paginas
  mapeadas de 4 KB. Esto permite descubrir registros vecinos de otro mapa aunque
  no coincidan con la posicion del guardado ni con la rotacion del aterrizaje.
  Se mantienen las validaciones de cada registro y la mayoria de al menos dos.
  No se usan direcciones del PC del autor como constantes.
- La muerte se empareja por PID de PK7 validado en las tablas del combate, incluso
  si la posicion de equipo es otra. Identidades contradictorias o ajenas al equipo
  no producen una muerte. Si no se puede leer el PK7, el antiguo cotejo de posicion
  solo se admite cuando la especie es unica en el equipo.
- No cambia la ventana de busqueda de combate de 1 MB, ni se manipulan partidas,
  marcas de ruta anteriores o eventos que el informe no permite reconstruir.

Validacion: las tres regresiones nuevas de localizacion fallaban antes del cambio
y pasan despues. Cuatro pruebas nuevas de zonas (incluida la negativa sin mayoria)
y siete de identidad del caido. GameLink: 329 correctas. App: 26 correctas.
La suma con las suites Core 403, Rules 134 y Randomizer 427 ya verificadas es 1.319.
Pendiente: confirmar el resultado de esta revision jugando en el PC del amigo.
El acceso directo local de revision 3 usa src/PermaLocke.App/bin/Actualizada-20260920-r3, compilado en Release sin avisos ni errores.

Comprobacion adicional de HOME: el registro local del 20/09 a las 22:14 mostro
XamlParseException por StaticResource Visible, introducido en el aviso de revision 2.
Se cambia a BoolToVisibility, que si existe en el tema compartido.
Una prueba STA carga HomeView con los tres diccionarios reales del tema, realiza
Measure/Arrange y comprueba el aviso visible y oculto. Reproduce el error antes del
cambio y pasa despues. App queda en 27 pruebas, total acumulado 1.320.
La distribucion final de revision 3 incluye esta correccion. Su acceso directo
local usa src/PermaLocke.App/bin/Actualizada-20260920-r3-final.

## Tercera revision (21 de septiembre)

- `tools/publicar.ps1` funciona ya con el PowerShell de Windows (5.1), el que trae cualquier Windows: usaba
  `[IO.Path]::GetRelativePath`, que solo existe en PowerShell 7, y el PC del desarrollador no lo tiene. `ZipFile`
  necesita alli su propio ensamblado (`System.IO.Compression.FileSystem`).
- La carpeta lleva `EVOLUCIONES CAMBIADAS.txt` (ARCHITECTURE §151) y la guia dice que hay que guardar la partida en
  cuanto se tiene el primer Pokemon: desde el §152 PermaLocke no barre la memoria buscando el equipo hasta que hay
  una partida guardada y sus Pokemon estan en memoria, que es lo que evita los cierres de Azahar en la intro.
- Se publica en dos carpetas del escritorio: `PermaLocke para amigos`, que no se abre nunca y es la que se reparte, y
  `PermaLocke prueba`, una copia identica en la que el desarrollador juega. Jugar en la que se reparte la llenaria de
  su run, su partida y su perfil.
- El registro del Azahar del desarrollador volvio a `*:Info RPC_Server:Error` (estaba en `RPC_Server:Info` y
  `Service.FS:Trace` de alguna prueba). Copia de la configuracion anterior junto a `qt-config.ini`.
- Dos arreglos que solo se ven en una instalacion NUEVA, y por eso nunca en el PC del desarrollador (ARCHITECTURE §152):
  con solo el inicial el equipo no se encontraba -el barrido exige dos Pokemon y no hay direccion de la ultima vez-, y
  sin equipo no funcionaba nada, que es la causa mas probable de «no salian las notis»; ahora se localiza por la
  partida guardada, sin barrer. Y el mundo instalado se leia solo al arrancar, asi que instalarlo con la app abierta la
  dejaba en 807 especies hasta reiniciar; ahora se relee al instalar.
- Comprobado sobre el publicado, en una copia desechable: arranca, raiz local, emulador propio, sin run, 0 errores.
  Sin ver todavia jugando en una carpeta nueva.
