---
tipo: mapa-codigo
proyecto: PermaLocke.Admin
generado: 2026-10-10
---
# PermaLocke.Admin — mapa de ficheros

Generado de la primera frase del `<summary>` de cada fichero (`src/PermaLocke.Admin/`). Tipos en negrita. Vuelve a [[00 - Inicio]] · [[Arquitectura]].

- `App.xaml.cs` — **App** — The admin's tool: one window over the shared folder (§129). 
- `AssemblyInfo.cs` — 
- `Converters.cs` — **NullToVisibleConverter** — Visible while the value is null: the «pick one» card that sits where a page will go.
- `MainWindow.xaml.cs` — **MainWindow** — The admin's window: the menu and the page. Everything it does lives in its view models.
- `Pages/AnnouncementsPage.xaml.cs` — **AnnouncementsPage** — A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in `nnouncementsViewModel`.
- `Pages/AuditPage.xaml.cs` — **AuditPage** — A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in `uditViewModel`.
- `Pages/CleanupPage.xaml.cs` — **CleanupPage** — A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in `leanupViewModel`.
- `Pages/FallenPage.xaml.cs` — **FallenPage** — 
- `Pages/GiftsPage.xaml.cs` — **GiftsPage** — Marks every player, or none if they already were.
- `Pages/HomePage.xaml.cs` — **HomePage** — 
- `Pages/MotesPage.xaml.cs` — **MotesPage** — 
- `Pages/PlayerSheetPage.xaml.cs` — **PlayerSheetPage** — A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in `layerSheetViewModel`.
- `Pages/PlayersPage.xaml.cs` — **PlayersPage** — 
- `Pages/ReportsPage.xaml.cs` — **ReportsPage** — A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in `eportsViewModel`.
- `Pages/RulesPage.xaml.cs` — **RulesPage** — A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in `ulesViewModel`.
- `Pages/SuggestionsPage.xaml.cs` — **SuggestionsPage** — A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in `uggestionsViewModel`.
- `Pages/TournamentPage.xaml.cs` — **TournamentPage** — 
- `Pages/UsagePage.xaml.cs` — **UsagePage** — A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in `sageViewModel`.
- `Pages/WhitelistPage.xaml.cs` — **WhitelistPage** — A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in `hitelistViewModel`.
- `Services/GiftDesk.cs` — **PlayerLine, StoredFile, SentGift, GiftDesk** — A player of the tournament, as the organiser's list shows them.
- `Services/ServerHistory.cs` — **ServerHistory** — A run's history as the server holds it, wherever that is (§194): inside runs.history for the apps that upload it whole, or one row per event in eventos for the ones that only upload what is new. 
- `ViewModels/AdminSections.cs` — **AdminSection, HomeSection, PlayersSection, GiftsSection, TournamentSection** — One entry of Admin's side menu (2026-09-28): its group heading, its name, its pixel icon and the page it opens. The page is a view model; the window's templates pick the view. 
- `ViewModels/AdminViewModel.cs` — **BannerRow, AdminViewModel** — A banner of the gacha with how many free rolls this gift gives on it.
- `ViewModels/AnnouncementsViewModel.cs` — **Announcement, AnnouncementsViewModel** — One announcement to every player.
- `ViewModels/AuditViewModel.cs` — **AuditRow, AuditViewModel** — One run of the tournament, checked.
- `ViewModels/CleanupViewModel.cs` — **CleanupLine, CleanupViewModel** — One kind of thing the cleanup would remove, and what it takes.
- `ViewModels/FallenViewModel.cs` — **FallenLine, FallenViewModel** — One grave of the tournament.
- `ViewModels/MotesViewModel.cs` — **MoteLine, MotesViewModel** — One voted nickname, with what was proposed and voted.
- `ViewModels/PlayerSheetViewModel.cs` — **SheetPokemon, SheetEvent, SheetZone, SheetWipe, Pick, PlayerSheetViewModel** — One Pokémon of the player's run.
- `ViewModels/ReportsViewModel.cs` — **ReportLine, ReportsViewModel** — One crash report on the server, with whose it is.
- `ViewModels/RulesViewModel.cs` — **RuleFile, RulesViewModel** — A rules file the organiser can change for everybody.
- `ViewModels/SuggestionsViewModel.cs` — **SuggestionLine, SuggestionsViewModel** — One suggestion from a player's suggestion box.
- `ViewModels/UsageViewModel.cs` — **UsageTable, UsageRun, UsageViewModel** — One table of the tournament and what it takes.
- `ViewModels/WhitelistViewModel.cs` — **Allowed, WhitelistViewModel** — One Discord account allowed into the tournament.
