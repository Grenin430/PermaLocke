# Parche 4 para el fork de Azahar — leer sin volcar la caché de la GPU

**Estado: EN `master` DEL FORK, COMPILADO Y VERIFICADO.** Commit `0dfe782`, construido en GitHub Actions
(`windows-msvc`, `azahar.exe` `06143878…`). Con el emulador viejo, las búsquedas de 64 MB lo tumbaron en 6 de
6 intentos; con este, 0 de 4, a 160 FPS todo el rato.

Repositorio: `github.com/Grenin430/azahar`. Tres ficheros, 31 líneas: `src/core/memory.h`,
`src/core/memory.cpp` y `src/core/rpc/rpc_server.cpp`.

## Qué resuelve

A un amigo se le cerraba Azahar muy pronto, varias veces. El informe automático del §168 lo dejó
claro en cuanto llegó: los dos cierres ocurrieron **en mitad de la misma petición**, un
`SearchMemory` de 64 MB sobre el heap lineal (`0x30000000-0x34000000`), y el registro de Windows
ponía el acceso inválido **siempre en el mismo punto**, `azahar.exe + 0x781693`, en tres días y
tres carpetas distintas. El otro cierre fue una aserción del propio emulador:

```
video_core/rasterizer_cache/rasterizer_cache.h: RasterizerCache<OpenGL::Traits>::DownloadFillSurface:1171: Assertion Failed!
```

La causa está en `MemorySystem::ReadBlock`: cuando una página está en la caché de la GPU
(`RasterizerCachedMemory`), la vuelca antes de copiarla, y volcar llama al renderizador. El servidor
RPC atiende las peticiones en **su propio hilo**, y con OpenGL el contexto solo vive en el hilo de
emulación: volcar desde el RPC es tocar el renderizador desde donde no se puede. Una lectura pequeña
casi nunca cae en una página de la GPU; un barrido de 64 MB, casi siempre.

No era el PC del amigo: ni el Visual C++ (su informe dice «suficiente»), ni OneDrive, ni la gráfica
(dibujaba con su RTX 4060).

## El cambio

`ReadBlockImpl` ya tenía una variante sin volcado (`ReadBlockImpl<true>`) que nadie usaba. El
parche la publica como `ReadBlockNoFlush` y la usan las tres lecturas que hace el servidor RPC fuera
del hilo de emulación: `ReadMemory`, `SearchMemory` y el vigilante de bloques. Para PermaLocke no
cambia nada: lo que lee son datos del juego, no dibujos, y que una página que tiene la GPU llegue un
poco atrasada da igual.

Las **escrituras** del RPC siguen invalidando la caché desde su hilo (`WriteBlock`). Son pocas, a
direcciones concretas de datos del juego, y en ninguna prueba han dado problemas; se dejan para no
mezclar dos cambios en uno, y quedan anotadas como el siguiente sitio donde mirar si vuelve a
aparecer un cierre de este tipo.

## Cómo se reprodujo, antes del cambio

Una copia aislada en una ruta corta, `C:\Users\javie\pl-estres`, con el mismo `azahar.exe` que el
amigo (`4469F6F5…`), el mod de gen 8-9 enlazado y la partida de prueba. La ruta importa: en el
scratchpad, la tarjeta SD emulada pasaba de 260 caracteres, el juego no podía crear sus datos extra y
se quedaba en «The game is preparing to load», así que nunca llegaba a la partida.

La prueba abre Azahar con la ROM, pulsa A sola para entrar en la partida y le pide búsquedas de 64 MB
sobre el heap lineal sin parar, como hacían los lectores de zona, equipo y combate. Da por
congelado el emulador si 64 trozos de 1 KB repartidos por ese heap no cambian en 20 s.

| Emulador `4469F6F5…` (el del amigo) | Resultado |
|---|---|
| Sin ninguna petición RPC (control) | Llega al mapa y corre: 160 FPS |
| Con búsquedas, intento 1 | **Se cierra** a los 9 s, `0xC0000005` en `azahar.exe + 0x781697` |
| Con búsquedas, intento 2 | **Se congela**: velocidad 0 %, 0 FPS |
| Con búsquedas, intento 3 | **Se congela**: velocidad 0 %, 0 FPS |
| Con búsquedas, intento 4 | **Se cierra** a los 13,6 s, `0x80000003`: `DownloadFillSurface:1171: Assertion Failed!` |

Las **dos firmas** de los cierres del amigo, reproducidas: el `0xC0000005` en `0x781697`, a cuatro
bytes de su `0x781693` (la misma función), y la misma aserción en la misma línea que su log. El
congelamiento es la otra cara del mismo choque entre hilos: la ventana responde y la emulación se
queda parada.

Luego, con el mismo método exacto que se usó para el emulador nuevo (la barra de estado de Azahar leída por
accesibilidad cada 10 s, desde una segunda copia `pl-viejo`), dos intentos más: **congelado** a 0 FPS desde la
primera lectura, y **cerrado** a los 8,5 s con `0xC0000005`. Seis de seis.

## Después del cambio

El mismo `pl-estres`, con los binarios del commit `0dfe782` en lugar de los viejos y todo lo demás igual: mod,
partida, ROM, configuración, 30 ms entre búsquedas.

| Emulador `06143878…` (este parche) | Resultado |
|---|---|
| Intento 1 | Aguanta los 180 s: 3633 búsquedas de 64 MB, 160 FPS en las 17 lecturas |
| Intento 2 | Aguanta: 3624 búsquedas, 160-161 FPS |
| Intento 3 | Aguanta: 3613 búsquedas, 149-160 FPS |
| Intento 4 | Aguanta: 3622 búsquedas, 160 FPS |

Cero cierres y cero congelamientos, frente a seis de seis. Cada búsqueda sigue tardando 13-14 ms, así que el
cambio no cuesta velocidad.

**Dos detectores de congelamiento que no sirvieron**, para no repetirlos: comparar 64 trozos del heap lineal da
falsos positivos con este parche, precisamente porque ya no vuelca y las páginas de la GPU se leen quietas; y la
página compartida del sistema (`0x1FF81000`) no cambia lo bastante a menudo, y saltó sin ninguna búsqueda. Lo que
sí es fiable es la barra de estado del propio Azahar: su velocidad y sus FPS se leen por UI Automation.

**Lo que no se ha probado**: el PC del amigo. Aquí se reprodujeron sus dos firmas exactas y el parche las quita,
pero la confirmación final es que a él deje de pasarle.
