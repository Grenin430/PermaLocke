---
tipo: flujo
revisado: 2026-09-26
---
# Actualizaciones y publicación (desde la 1.0.2)

Cómo llega una versión nueva a los jugadores desde el 2026-09-26. Detalle técnico: §196 (actualización), §200 (versión),
§201 (ventana y Action) en `docs/ARCHITECTURE.md`. Contexto: [[Plan del próximo torneo]] · [[Historial de conversaciones]].

## El flujo (lo que hace cada uno)
1. **Claude**, en la rama `claude/...`: el cambio + **subir `<Version>`** en `src/PermaLocke.App/PermaLocke.App.csproj`
   (única fuente de la versión) + **`docs/novedades/<versión>.md`** (lo que lee el jugador al ofrecérsela; la app
   enseña 600 caracteres). Compila, pruebas, § y cerebro, y abre un PR a `main`.
2. **El usuario acepta el PR.** Ese es su visto bueno: nada llega a los amigos sin él.
3. **La Action** `.github/workflows/publicar-actualizacion.yml` (Windows de GitHub, gratis por repo público): si no existe
   la release `v<versión>`, pasa las pruebas (Core, Rules, GameLink, Randomizer), hace el paquete con
   `tools/publicar-actualizacion.ps1` y crea la release con el zip y las notas. Se lanza al cambiar el csproj o el propio
   workflow en main, o a mano (Actions > Publicar actualización > Run workflow). Nunca republica una versión existente.
4. **Cada app**, al abrirse (solo carpeta repartida con `PermaLocke.local`, nunca con `--sin-juego`), pregunta a
   `api.github.com/repos/Grenin430/PermaLocke/releases/latest` (`actualizaciones` en `Data/torneo.json`), la ofrece, y
   con un sí abre la **ventana de descarga** (barra, MB, %, velocidad de los últimos 3 s, tiempo restante, CANCELAR solo
   mientras descarga), comprueba SHA-256, instala (exe + `Data/*.json` salvo reglas oficiales) y se reinicia.
   CONFIGURACIÓN enseña **PERMALOCKE x.y.z** al final.
5. **Desde la 1.0.3 (§202)** no pregunta: mira al abrir y cada 30 min; la versión esperando sale como **franja ámbar
   con ACTUALIZAR** encima de cada sección y, con el juego abierto, como **aviso fijo** sobre el emulador hasta que se
   cierra. Hasta la 1.0.3, solo tres cifras. **Desde la 1.0.4 (§203), cuatro**: 1.0.4.1 > 1.0.4. Una app 1.0.3 o anterior no ve
   el cuarto número: por eso la primera con cuatro cifras tiene que ser 1.0.4.x, no 1.0.3.x.

## Estado verificado
- **1.0.1**: publicada a mano por el usuario (script + release en la web). **Observado**: se ofreció, entró y se vio
  PERMALOCKE 1.0.1 (captura del usuario, 2026-09-26). Prueba de extremo a extremo de §196.
- **1.0.2** (ventana de descarga + Action): la Action falló la 1.ª vez por sintaxis de PowerShell (`"$t:"`), sin publicar
  nada; arreglo en PR #4. **Observado**: la Action la publicó y el usuario la instaló (2026-09-26).
- **1.0.3** (franja y aviso fijo): publicada por la Action. La franja y el aviso fijo se verán por primera vez con la 1.0.4.
- Repo **público** desde el 2026-09-26 (decisión del usuario tras explicarle que el código y el historial quedan
  copiables y que la GPL ya obliga a dar el código; revisado: sin ROM, `Locke/`, partidas ni secretos; la clave de
  Supabase del repo es la pública). Alternativa descartada: repo de código privado + repo público solo de releases.

## Versiones publicadas
| Versión | Cómo salió | Qué traía |
|---|---|---|
| 1.0.0 | carpeta de amigos (`desplegar.ps1 -Amigos`) | el plan del torneo (§194-§199), sin versión a la vista |
| 1.0.1 | release a mano | PERMALOCKE x.y.z en CONFIGURACIÓN (§200) |
| 1.0.2 | Action (2.º intento) | ventana de descarga (§201) |
| 1.0.3 | Action | franja ACTUALIZAR y aviso fijo sobre el juego (§202) |
| 1.0.4 | PR #6, aceptado | versiones de cuatro cifras (§203) y **arreglo de la descarga** (§204) |
| 1.0.4.1 | commit directo a main pedido por el usuario (2026-09-27), Action | aviso de PRIMER ENCUENTRO 6 s después y variocolor del gacha al 2 % (§205) |
| 1.0.4.2 | commit directo a main pedido por el usuario (2026-09-27), Action | ventana de actualización sin cortes y comprobación cada 3 min (§206). La 1.0.4.1 se instaló sola bien |
| 1.0.4.3 | commit directo a main pedido por el usuario (2026-09-27), Action | comprobación cada minuto con espera al límite de GitHub (§207) |
| 1.0.4.4 | commit directo a main pedido por el usuario (2026-09-27), Action | caído curado por la historia justo antes de un combate: vigilancia cada 500 ms y aviso en Admin (§208) |
| 1.0.4.5 | commit directo a main pedido por el usuario (2026-09-27), Action | aviso pequeño de amigo que empieza a jugar, estilo Steam (§209) |
| 1.0.4.6 | commit directo a main pedido por el usuario (2026-09-27), Action | gacha a x2/x3 al entrar varias veces: fotograma apuntado de más (§210) |
| 1.0.4.7 | commit directo a main pedido por el usuario (2026-09-27), Action | intercambio 2 por 1 en el ÁLBUM, panel del cap sobre el juego, tutores y tienda de PB de surf (al regenerar), gacha sin exprés (§211) |
| 1.0.4.8 | commit directo a main pedido por el usuario (2026-09-27), Action | equipo con icono, nivel y PS en el panel del cap; casilla para quitarlo (§212) |
| 1.0.4.9 | commit directo a main pedido por el usuario (2026-09-27), Action | PS del equipo del panel en tiempo real también en combate (§213) |

**1.0.2 y 1.0.3 no pueden actualizarse solas** (§204: la descarga se desbordaba en el primer trozo). Quien las tenga,
una vez a mano: cerrar PermaLocke y cambiar `PermaLocke.exe` por el del zip de la 1.0.4.

Siguiente arreglo pequeño: **1.0.4.1**. El usuario pasa por cada versión (la de antes todavía le enseña su propia forma de
avisar: la 1.0.2 pregunta con sí o no; desde la 1.0.3, franja).

## Para Claude, al tocar esto
- Leer el registro de una Action desde la nube: MCP `actions_list` (runs) y `get_job_logs` con `failed_only`.
- **Antes de subir un workflow o un `.ps1`**, pasarlos por el analizador: `dotnet tool install --global PowerShell` y
  `[System.Management.Automation.Language.Parser]::ParseFile` (los `${{ }}` sustituidos por un valor). Ver
  [[Trampas y lecciones]].
- La app en `LocalOnly` busca releases: una release publicada **llega a todos** los amigos que abran la app. Avisar al
  usuario antes de cada PR que sube la versión.
- El botón de traspaso (§195) se quitará cuando todos los amigos estén en la versión nueva; todavía no.
