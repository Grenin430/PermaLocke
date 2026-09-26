---
tipo: mapa-codigo
proyecto: PermaLocke.Infrastructure
generado: 2026-09-26
---
# PermaLocke.Infrastructure — mapa de ficheros

Generado de la primera frase del `<summary>` de cada fichero (`src/PermaLocke.Infrastructure/`). Tipos en negrita. Vuelve a [[00 - Inicio]] · [[Arquitectura]].

- `AppPaths.cs` — **AppPaths** — Resolves the folder layout PermaLocke expects next to the executable and creates the writable ones on first use. Nothing in the app builds paths by hand. 
- `AppUpdate.cs` — **UpdateAsset, UpdateResult, AppUpdate** — The update package of a GitHub release, as PermaLocke looks for it.
- `CrashReportQueue.cs` — **CrashReportQueue** — Which crash reports still have to reach the organiser (§199, plan del próximo torneo, paso 6): the zips EmulatorCrashReport wrote in Diagnosticos\ that are not yet in Config/informes-subidos.json. 
- `Logging/FileLoggerProvider.cs` — **FileLoggerProvider** — Minimal rolling file logger: one file per day under Logs/. Deliberately dependency-free so the app does not drag in a logging framework for what amounts to appending lines. 
- `ServiceCollectionExtensions.cs` — **SystemClock, ServiceCollectionExtensions** — Folder layout, clock and file logging. Shared by both applications.
