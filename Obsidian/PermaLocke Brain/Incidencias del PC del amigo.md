---
tipo: incidencias
revisado: 2026-09-24
fuentes: docs/DISTRIBUCION-LOCAL.md, ARCHITECTURE §152, §159, §165, §167, §168, §173, código actual
---
# Incidencias del PC del amigo (septiembre de 2026)

Contexto: los amigos juegan cada uno en una **copia local independiente**, publicada con `tools/publicar.ps1` más el marcador `PermaLocke.local`. Drive se descartó el 2026-09-20. El amigo jugaba con la carpeta `PermaLocke para amigos` (la del §167 corrige que había dicho otra cosa). Flujos implicados: [[Flujos del vigilante]]. Memoria: [[GameLink y memoria]].

## Cronología de las revisiones (de `docs/DISTRIBUCION-LOCAL.md`)
- **20/09, primera tanda.**
  - Distribución local y portabilidad de sprites.
  - Killcam con anillo de 141 fotogramas sin reservar memoria nueva.
  - Plazo total por intento RPC.
- **20/09, informe de cierres** (exe SHA256 `9665…46B3`).
  - Barridos de equipo cada ~6 s; el log de Azahar llegaba a 100 MiB en 16 s.
  - Arreglo: enfriamiento también tras un fallo, 20 lecturas antes del primer barrido, reseleccionar el proceso y no barrer sin proceso.
- **Aviso de las primeras Poké Balls:** se emitía el evento, pero nadie lo anunciaba. Ahora `BallControlService.FirstBallDetected` → `PlayNotifications`.
- **Ventana de avisos:** `Notifier` mezclaba coordenadas Win32 y WPF, así que con escala distinta de 100 % los avisos quedaban fuera de pantalla. Ahora usa `SetWindowPos` nativo.
- **Arranque sin guardar, encuentros y killcam (revisión 2):**
  - `FieldZoneReader` vuelve a buscar cuando aparece un guardado nuevo.
  - `BattleCounterReader` reintenta la referencia de la mochila cada 5 s.
  - HOME dice por qué no se detectan encuentros.
  - La killcam la guarda quien registre la muerte.
  - Las tablas de combate aguantan 2 lecturas fallidas.
- **Informe de las 22:09:59 (exe `A609…BEEFD0D` = versión anterior a la r2) → revisión 3**, confirmada en el código actual:
  - búsqueda de hermanos del mapa **paginada** (`_siblingCursors`, presupuesto de 6) y **`Nearby`** (2 páginas de 4 KB) en `FieldZoneReader.Search`;
  - caída emparejada por **PID** del PK7 del combate: `BattlePokemon.MatchPlayer`, llamado en `GameLinkMonitor.OnBattleFaintAsync` (se había visto un Wooloo, especie 831, en la posición 0 sin cuadrar con el equipo);
  - HOME: `StaticResource Visible` pasa a `BoolToVisibility`, cubierto por `HomeViewTests`;
  - pruebas `EncounterRecoveryTests`, `BattleIdentityTests` y `HomeViewTests`, que existen en `tests/` **sin commitear** (untracked).
- **21/09:** `publicar.ps1` compatible con PowerShell 5.1; guardar en cuanto se tenga el primer Pokémon; el equipo se localiza por la partida guardada, sin barrer (§152); el mundo se relee al instalar.
- **22/09:** log sin inundaciones (§167); Visual C++ junto al emulador; informe automático de cierre y avisos previos en JUGAR (§168).
- **23/09 — causa de los cierres encontrada (§173):**
  - en los dos cierres, la última petición era un `SearchMemory` de 64 MB sin respuesta;
  - Windows daba `azahar.exe+0x781693`;
  - una aserción `DownloadFillSurface` de la caché de la GPU;
  - era el fork, que leía con volcado de la GPU desde el hilo RPC con OpenGL. **Parche 4** (`0dfe782`): el viejo cayó 6 de 6 veces y el nuevo 0 de 4, en una copia aislada;
  - la carpeta de amigos y su zip llevan el emulador nuevo.

## Estado por síntoma
| Síntoma | Causa confirmada | Mitigación en el código | Falta |
|---|---|---|---|
| Azahar se cierra | búsquedas y lecturas RPC con volcado de la GPU (§173) | parche 4, menos barridos, VC++ | **resuelto: el amigo confirmó el 2026-09-24 que ya no le pasa** |
| No salían avisos | sin equipo no funcionaba nada (instalación nueva con solo el inicial); coordenadas de los avisos | localizar el equipo por el save; `SetWindowPos` | verlo en su PC |
| Encuentros que unas veces sí y otras no | zona sin mayoría, primera página de hermanos, espera de 2 min sin guardado; transformaciones basura (1,0,0) (§152) | r2, r3, §152, §156, §159, §164 | un diagnóstico nuevo de una sesión posterior |
| Muerte tarde o sin killcam | caída no emparejada por posición, así que la vio el ciclo del equipo al acabar (log 21:38); espera de hasta 6 s de la barra; suelo naranja confundido con la barra (§165) | `MatchPlayer` por PID; killcam la guarda quien gane la puerta; §165 | no se ha reproducido la muerte del amigo |
| Sprites que no cargan | sin ROM o sin expansión, o caché con nulos | `PokemonSpriteService` reintenta y no memoriza nulos | si vuelve a pasar, saber qué Pokémon y en qué pantalla |

## Cómo investigar el siguiente informe
1. Pedir el zip de `RECOGER DIAGNOSTICO.cmd` (o `Diagnosticos/cierre-azahar-*.zip`) y el SHA256 del exe, para saber qué revisión usa.
2. En `Logs/permalocke-*.log` buscar:
   - «Localizando el equipo» (barridos);
   - «Registros de posición» y «Buscando más registros» (zona);
   - «Caída en combate» o «Muerte detectada (memoria del juego)» (quién vio la muerte);
   - «no cuadra con el equipo»;
   - «No se ha podido guardar la killcam».
3. En el zip de cierre, las últimas 128 peticiones: si la última es un `SearchMemory` grande sin respuesta, es la firma del §173.
4. No tocar la run ni la partida del amigo para forzar eventos que ya se guardaron.
