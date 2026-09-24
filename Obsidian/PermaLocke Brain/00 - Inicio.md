---
tipo: indice
revisado: 2026-09-24
---
# PermaLocke Brain — memoria de trabajo de Claude

**Para quién:** para Claude, no para el usuario. Sirve para no releer el proyecto en cada conversación y para saber lo que se habló y se decidió antes. `CLAUDE.md` ya se carga solo y trae el relato largo del «Estado actual»: aquí no se repite, aquí va **dónde está cada cosa en el código**, cómo fluye y qué se decidió en las conversaciones.

**Protocolo**
1. Antes de explorar código, mira aquí qué fichero o símbolo toca. Luego lee **solo** ese tramo.
2. Al acabar un trabajo que cambie comportamiento, ficheros o decisiones, actualiza:
   - [[Historial de conversaciones]], siempre;
   - la nota del área afectada;
   - [[Mapa de código/PermaLocke.App]] (u otro proyecto) si hay ficheros nuevos o borrados. Se puede regenerar: ver [[Comandos]].
3. Distingue siempre: **código** (visto en fichero), **pruebas** (hay test), **observado** (jugando o en un log, con fecha), **hipótesis** y **pendiente**.

## Según la tarea, lee
| Tarea | Nota |
|---|---|
| Cómo trabajar con el usuario (reglas, permisos, carpetas intocables) | [[Usuario y forma de trabajar]] |
| Qué se hizo en conversaciones anteriores | [[Historial de conversaciones]] · el diario viejo de CLAUDE.md: [[Archivo - estado de CLAUDE.md (2026-09-24)]] |
| Proyectos, DI, rutas, ficheros de datos, persistencia | [[Arquitectura]] |
| El vigilante: bucles, muertes, killcam, encuentros, avisos, cap | [[Flujos del vigilante]] |
| RPC de Azahar, memoria, equipo, zona, combate, partida guardada | [[GameLink y memoria]] |
| Pantallas, kit pixel, estilos, capturas de verificación | [[UI y kit pixel]] |
| Errores ya cometidos que no hay que repetir | [[Trampas y lecciones]] |
| Cierres de Azahar, encuentros intermitentes, avisos y killcams en el PC del amigo | [[Incidencias del PC del amigo]] |
| Pruebas y cómo verificar | [[Pruebas]] |
| Compilar, publicar, desplegar en la carpeta de prueba, sondas | [[Comandos]] |
| Buscar un § de ARCHITECTURE sin leer 11.7k líneas | [[Índice de ARCHITECTURE]] |
| Qué sigue sin saberse | [[Preguntas abiertas]] |
| Ficheros por proyecto | [[Mapa de código/PermaLocke.App]] · [[Mapa de código/PermaLocke.GameLink]] · [[Mapa de código/PermaLocke.Core]] · [[Mapa de código/PermaLocke.Rules]] · [[Mapa de código/PermaLocke.Data]] · [[Mapa de código/PermaLocke.Randomizer]] · [[Mapa de código/PermaLocke.Infrastructure]] · [[Mapa de código/PermaLocke.Admin]] · [[Mapa de código/Tests]] |

## Qué es, en tres líneas
- Gestor de Nuzlocke para **Pokémon Ultra Luna** (TitleID `00040000001B5100`) en el emulador **Azahar**, con un **fork propio** del emulador que va en `Emulator/`.
- C# / .NET 10 / WPF / MVVM, GPLv3 porque usa PKHeX.Core y pk3DS.Core.
- Lee y escribe el juego por el RPC UDP del emulador (memoria viva) y por el fichero de partida (PKHeX, solo con el juego cerrado). Randomiza la ROM por LayeredFS. Lleva la run como un registro de eventos encadenado por hash en SQLite.

## Estado del repositorio (2026-09-24)
- El último commit es del **2026-09-13** (`43e439e`). Hay unos **410 cambios sin commitear**, entre ellos ficheros nuevos sin seguimiento (p. ej. `BattlePokemon.cs`, `FieldZoneReader.cs`, `HomeViewTests.cs`), así que **git log no cuenta la historia reciente**: la cuentan `CLAUDE.md`, `docs/` y [[Historial de conversaciones]].
- Solo se hace commit cuando el usuario lo pide.
