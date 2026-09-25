---
tipo: mapa-codigo
proyecto: PermaLocke.Infrastructure
generado: 2026-09-25
---
# PermaLocke.Infrastructure — mapa de ficheros

Generado de la primera frase del `<summary>` de cada fichero (`src/PermaLocke.Infrastructure/`). Tipos en negrita. Vuelve a [[00 - Inicio]] · [[Arquitectura]].

- `AppPaths.cs` — **AppPaths** — Resolves the folder layout PermaLocke expects next to the executable and creates the writable ones on first use. Nothing in the app builds paths by hand. 
- `Logging/FileLoggerProvider.cs` — **FileLoggerProvider** — Minimal rolling file logger: one file per day under Logs/. Deliberately dependency-free so the app does not drag in a logging framework for what amounts to appending lines. 
- `ServiceCollectionExtensions.cs` — **SystemClock, ServiceCollectionExtensions** — Folder layout, clock and file logging. Shared by both applications.
