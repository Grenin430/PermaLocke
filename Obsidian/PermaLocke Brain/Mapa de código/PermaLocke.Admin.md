---
tipo: mapa-codigo
proyecto: PermaLocke.Admin
generado: 2026-09-26
---
# PermaLocke.Admin — mapa de ficheros

Generado de la primera frase del `<summary>` de cada fichero (`src/PermaLocke.Admin/`). Tipos en negrita. Vuelve a [[00 - Inicio]] · [[Arquitectura]].

- `AnnouncementsWindow.xaml.cs` — **AnnouncementsWindow** — The tournament's announcements. Everything it does lives in `nnouncementsViewModel`.
- `App.xaml.cs` — **App** — The admin's tool: one window over the shared folder (§129). 
- `AssemblyInfo.cs` — 
- `AuditWindow.xaml.cs` — **AuditWindow** — The tournament audit. Everything it does lives in `uditViewModel`.
- `CleanupWindow.xaml.cs` — **CleanupWindow** — The server cleanup. Everything it does lives in `leanupViewModel`.
- `MainWindow.xaml.cs` — **MainWindow** — The admin's window. Everything it does lives in its view model.
- `PlayerSheetWindow.xaml.cs` — **PlayerSheetWindow** — A player's sheet. Everything it does lives in `layerSheetViewModel`.
- `RulesWindow.xaml.cs` — **RulesWindow** — The official rules. Everything it does lives in `ulesViewModel`.
- `Services/GiftDesk.cs` — **PlayerLine, SentGift, GiftDesk** — A player of the tournament, as the organiser's list shows them.
- `Services/ServerHistory.cs` — **ServerHistory** — A run's history as the server holds it, wherever that is (§194): inside runs.history for the apps that upload it whole, or one row per event in eventos for the ones that only upload what is new. 
- `UsageWindow.xaml.cs` — **UsageWindow** — What the tournament takes on the server. Everything it does lives in `sageViewModel`.
- `ViewModels/AdminViewModel.cs` — **BannerRow, AdminViewModel** — A banner of the gacha with how many free rolls this gift gives on it.
- `ViewModels/AnnouncementsViewModel.cs` — **Announcement, AnnouncementsViewModel** — One announcement to every player.
- `ViewModels/AuditViewModel.cs` — **AuditRow, AuditViewModel** — One run of the tournament, checked.
- `ViewModels/CleanupViewModel.cs` — **CleanupLine, CleanupViewModel** — One kind of thing the cleanup would remove, and what it takes.
- `ViewModels/PlayerSheetViewModel.cs` — **SheetPokemon, SheetEvent, SheetZone, SheetWipe, Pick, PlayerSheetViewModel** — One Pokémon of the player's run.
- `ViewModels/RulesViewModel.cs` — **RuleFile, RulesViewModel** — A rules file the organiser can change for everybody.
- `ViewModels/UsageViewModel.cs` — **UsageTable, UsageRun, UsageViewModel** — One table of the tournament and what it takes.
- `ViewModels/WhitelistViewModel.cs` — **Allowed, WhitelistViewModel** — One Discord account allowed into the tournament.
- `WhitelistWindow.xaml.cs` — **WhitelistWindow** — The tournament's whitelist. Everything it does lives in `hitelistViewModel`.
