---
tipo: plan
revisado: 2026-09-26
---
# Plan del próximo torneo (decidido el 2026-09-26)

Trabajo pedido por el usuario para el torneo siguiente, «más grande», con unas **20 personas**. Se hace **solo en el repo**
mientras dure el torneo actual ([[Usuario y forma de trabajar]]: nada de desplegar a `PermaLocke prueba` ni a amigos).
Lo va a continuar desde la nube. Contexto: [[Historial de conversaciones]] · [[Ideas para el futuro]].

## Orden acordado
1. **Subir solo lo nuevo + limpieza del servidor** (ver abajo). Primero, porque asegura el espacio.
2. **Traspaso de carpeta, una sola vez**: botón «TRAER MI PARTIDA DE OTRA CARPETA» en la primera vez guiada.
3. **Actualización automática** con GitHub Releases.
4. **Primera vez guiada** (onboarding).
5. **Copias de seguridad en el servidor** (Supabase Storage).
6. **Informes de fallo directos a Admin** (Supabase Storage).

Descartada de esa tanda: «apuntarse desde la app» (la 2 original). El usuario no la eligió.

## Espacio: lo medido y lo que se explicó al usuario
- Plan gratis de Supabase: **500 MB** de base de datos, **1 GB** de Storage, **10 GB/mes** de tráfico. El proyecto se
  **pausa tras 1 semana sin uso** (entre torneos hay que entrar de vez en cuando).
- Medido: la run del usuario ocupaba **600 KB** tras el primer día de torneo (`Saves/permalocke.db`).
- Hoy la app **sube el historial ENTERO cada 2 min** (`TournamentUpload`/`SyncService.BuildAsync` → fila de `runs`, un
  `upsert`). La fila se reemplaza, no se acumula, pero cada reemplazo de un JSON grande deja tuplas muertas hasta el
  vacuum, y `subidas` gana una fila por subida.
- Estimación 20 jugadores, ~100 h cada uno: 60-100 MB comprimido, 150-200 MB en el peor caso con restos, +20-30 MB del
  resto. **Cabe, pero sin el margen que quiere el usuario.** No se prometió el 100 %.
- **Las versiones de la app NO van a la base de datos**: irán a GitHub Releases (0 bytes en Supabase).
- Copias: partida ~0,5 MB + run 1-2 MB; 5 por jugador × 20 ≈ 250 MB → en **Storage**, no en la base de datos.
- Informes de fallo: 100-300 KB cada uno, en Storage, con borrado de los viejos.
- Alternativas gratis si Storage no llegara: Cloudflare R2 (10 GB, sin coste de descarga) o Backblaze B2 (10 GB). No se
  ven necesarias. **No hace falta cambiar de base de datos.**

## Estado
- **Paso 1 — HECHO en el repo (§194), 2026-09-26.** Falta que el usuario ejecute `tools/supabase/15-eventos-y-limpieza.sql`.
  Hasta entonces las apps nuevas suben entero como antes (vuelta automática) y la LIMPIEZA dice que falta el SQL.
  Probado en Postgres local con `tools/supabase/pruebas/probar.sh`.
- **Paso 2 — HECHO en el repo (§195), 2026-09-26.** Botón en HOME sin run; en el paso 4 irá también en la primera vez guiada. Quitarlo en la versión siguiente a la actualización automática.
- **Paso 3 — HECHO en el repo (§196), 2026-09-26.** Para publicar: `tools/publicar-actualizacion.ps1 -Version x.y.z` y release `vx.y.z` con el zip. El repo tiene que ser público. La primera carpeta repartida del próximo torneo tiene que llevar ya este código (versión 1.0.0).
- **Paso 4 — HECHO en el repo (§197), 2026-09-26.** PRIMEROS PASOS en JUGAR; el traspaso (§195) va dentro, en el paso de la run.
- **Paso 5 — HECHO en el repo (§198), 2026-09-26.** Falta que el usuario ejecute `tools/supabase/16-copias.sql` (después del 15). Devolver una copia es a mano con el LEEME del zip.
- **Paso 6 — HECHO en el repo (§199), 2026-09-26.** Falta que el usuario ejecute `tools/supabase/17-informes.sql` (después del 16). **Plan completo en el repo**: SQL 15, 16 y 17 por orden; nada probado aún en Windows ni contra el Supabase de verdad.

- **En marcha (2026-09-26):** SQL 15-17 ejecutados y verificados; el usuario trajo su partida a una carpeta nueva con el traspaso (§195), probado en Windows y bien; borró la vieja y la nueva se llama otra vez `PermaLocke prueba`. Falta repartirla a los amigos.
- **1.0.1 (§200), 2026-09-26:** repo público; primera release para probar la actualización automática. Falta que el usuario la publique y la vea entrar.

## 1. Subir solo lo nuevo y limpiar
- La app sube solo los eventos que el servidor no tiene todavía (p. ej., tabla `eventos` con una fila por evento, o
  añadir al JSON existente desde el último hash), en vez del historial entero. Mantener la huella (`chainHead`,
  `eventCount`) para que la AUDITORÍA siga detectando retrocesos. Compatibilidad: las apps viejas del torneo actual
  siguen subiendo como ahora; no romper `runs`.
- **Botón LIMPIEZA en Admin** (pedido explícito: «borrar TODO lo innecesario que sea imposible que rompa algo»). Solo
  cosas que nada vuelve a leer o que se pueden reconstruir:
  - `fantasmas` y `lluvias` de más de 1 día (la app solo pinta los recientes).
  - `regalos` que **todos** sus destinatarios ya recogieron (su recogida vive en el historial de cada run) y con más de 7 días.
  - `anuncios` viejos salvo el último.
  - `subidas`: compactar dejando por run solo la primera, la última y las que la auditoría marca como retroceso
    (`SnapshotAudit.Rewinds`). **No borrar las de un retroceso**: son la prueba.
  - Historial (`history`) de las runs **archivadas**: vaciarlo y dejar el `snapshot`. Pedir confirmación, porque la
    auditoría de una run archivada deja de poder comprobar su cadena.
  - Storage: copias más allá de las 5 últimas por jugador e informes de fallo de más de 30 días.
  - Antes de borrar: enseñar cuánto se liberaría y pedir confirmación. Postgres recupera el espacio con el vacuum
    automático; con el plan gratis no se puede lanzar `VACUUM FULL` a mano desde la app.
  - Nunca: `runs` activas, `whitelist`, `organizadores`, `reglas`, `reinicios`.

## 2. Traspaso de carpeta (una vez)
- En la primera vez guiada: elegir la carpeta vieja (`PermaLocke prueba`, la de amigos…) y **copiar** (nunca mover ni
  borrar): `Saves/` entera (run, sesiones, copias, killcams); del emulador viejo solo la partida de Ultra Luna
  (`Emulator/user/sdmc/Nintendo 3DS/.../001b5100`) y el mundo instalado (`Emulator/user/load/mods`); de `Config/` el jugador y
  la sesión de Discord; la ROM si falta.
- Guardas: juego y PermaLocke viejo cerrados; si la carpeta nueva ya tiene run, no pisar y avisar; log de todo.
- Se quita en la versión siguiente, cuando ya exista la actualización automática.
- Ojo con `docs/DISTRIBUCION-LOCAL.md` («no copiar Config, Saves ni Emulator/user de una partida»): esa regla es para
  **repartir** a otros, no para que uno traiga lo suyo.

## 3-6
- **Actualización**: comprobar la última release de GitHub (`Grenin430/PermaLocke`), descargar el zip de `publicar.ps1`,
  sustituir el exe y `Data/` sin tocar `Saves/`, `Config/`, `ROM/`, `Emulator/user`. Respetar las reglas oficiales
  descargadas (`RulesSync`, §193), que viven en `Data/`.
- **Primera vez**: ROM, emulador, Discord, generar mundo; y el traspaso.
- **Copias**: bucket privado; cada jugador escribe solo en su carpeta (RLS por `auth.uid()`); Admin recupera.
- **Informes**: `EmulatorCrashReport` ya genera el zip; subirlo al bucket y listarlo en Admin.
- Todo lo del servidor, solo **añadir** (bucket y tablas nuevos), con un SQL numerado (`15-…`) que ejecuta el usuario.
