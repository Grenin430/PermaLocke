---
tipo: mapa-codigo
proyecto: PermaLocke.Admin
generado: 2026-10-08
---
# PermaLocke.Admin — mapa de ficheros

Generado de la primera frase del `<summary>` de cada fichero (`src/PermaLocke.Admin/`). Tipos en negrita. Vuelve a [[00 - Inicio]] · [[Arquitectura]].

- `App.xaml.cs` — **App** — The admin's tool: one window over the shared folder (§129). 
- `AssemblyInfo.cs` — 
- `ConfirmDialog.xaml.cs` — **ConfirmDialog** — The confirmation of every action of Admin. Everything it shows comes from its `onfirmRequest`.
- `Converters.cs` — **NullToVisibleConverter, ZeroToVisibleConverter, EmptyToHiddenConverter** — Visible while the value is null: the «pick one» card that sits where a page will go.
- `MainWindow.xaml.cs` — **MainWindow** — The admin's window: the menu and the page. Everything it does lives in its view models.
- `Pages/AnnouncementsPage.xaml.cs` — **AnnouncementsPage** — A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in `nnouncementsViewModel`.
- `Pages/AuditPage.xaml.cs` — **AuditPage** — A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in `uditViewModel`.
- `Pages/CleanupPage.xaml.cs` — **CleanupPage** — A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in `leanupViewModel`.
- `Pages/FallenPage.xaml.cs` — **FallenPage** — 
- `Pages/GiftsPage.xaml.cs` — **GiftsPage** — Marks every player, or none if they already were.
- `Pages/HomePage.xaml.cs` — **HomePage** — 
- `Pages/LogPage.xaml.cs` — **LogPage** — REGISTRO. Everything it does lives in `ogViewModel`; the menu reads it when the page opens.
- `Pages/MotesPage.xaml.cs` — **MotesPage** — 
- `Pages/PlayerSheetPage.xaml.cs` — **PlayerSheetPage** — A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in `layerSheetViewModel`.
- `Pages/PlayersPage.xaml.cs` — **PlayersPage** — JUGADORES: the table and the sheet. The filters and the selection are passed to `dminViewModel`.
- `Pages/ReportsPage.xaml.cs` — **ReportsPage** — A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in `eportsViewModel`.
- `Pages/RulesPage.xaml.cs` — **RulesPage** — A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in `ulesViewModel`.
- `Pages/SuggestionsPage.xaml.cs` — **SuggestionsPage** — A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in `uggestionsViewModel`.
- `Pages/TournamentPage.xaml.cs` — **TournamentPage** — 
- `Pages/UsagePage.xaml.cs` — **UsagePage** — A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in `sageViewModel`.
- `Pages/WhitelistPage.xaml.cs` — **WhitelistPage** — A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in `hitelistViewModel`.
- `Services/AdminLog.cs` — **AdminLog, OrderLine** — The words for the organiser's log (admin_registro, 21-admin.sql): the server writes short codes such as «orden:revivir» or «lista:suspender», and these say them as a sentence. 
- `Services/Confirm.cs` — **ConfirmRequest, ConfirmAnswer** — What a confirmation asks: the action, what it will do, and whether it needs a reason or typing a word.
- `Services/DemoServer.cs` — **DemoServer** — A tournament server that lives in memory, with made-up players (--demo, 2026-10-08): to look at Admin, take its screenshots and try every button without reading or touching the real tournament. 
- `Services/GiftDesk.cs` — **PlayerLine, LogLine, NoteLine, StoredFile, SentGift, GiftDesk** — A player of the tournament, as the organiser's list shows them.
- `Services/ServerHistory.cs` — **ServerHistory** — A run's history as the server holds it, wherever that is (§194): inside runs.history for the apps that upload it whole, or one row per event in eventos for the ones that only upload what is new. 
- `ViewModels/AdminSections.cs` — **AdminSection, HomeSection, PlayersSection, GiftsSection, TournamentSection** — One entry of Admin's side menu (2026-09-28): its group heading, its name, its pixel icon and the page it opens. The page is a view model; the window's templates pick the view. 
- `ViewModels/AdminViewModel.cs` — **BannerRow, PlayerFilter, HomeAlert, AdminMode, AdminViewModel** — A banner of the gacha with how many free rolls this gift gives on it.
- `ViewModels/AnnouncementsViewModel.cs` — **Announcement, AnnouncementsViewModel** — One announcement to every player.
- `ViewModels/AuditViewModel.cs` — **AuditRow, AuditViewModel** — One run of the tournament, checked.
- `ViewModels/CleanupViewModel.cs` — **CleanupLine, CleanupViewModel** — One kind of thing the cleanup would remove, and what it takes.
- `ViewModels/FallenViewModel.cs` — **FallenLine, FallenViewModel** — One grave of the tournament.
- `ViewModels/LogViewModel.cs` — **LogViewModel** — REGISTRO (2026-10-08): everything the organisers changed, from admin_registro (21-admin.sql). The server writes it, with who from the session, so nothing in Admin can leave it out or change it. 
- `ViewModels/MotesViewModel.cs` — **MoteLine, MotesViewModel** — One voted nickname, with what was proposed and voted.
- `ViewModels/PlayerSheetViewModel.cs` — **SheetPokemon, SheetEvent, SheetZone, SheetWipe, Pick, SheetFigure, Attention, PlayerSheetViewModel** — One Pokémon of the player's run.
- `ViewModels/ReportsViewModel.cs` — **ReportLine, ReportsViewModel** — One crash report on the server, with whose it is.
- `ViewModels/RulesViewModel.cs` — **RuleFile, RulesViewModel** — A rules file the organiser can change for everybody.
- `ViewModels/SuggestionsViewModel.cs` — **SuggestionLine, SuggestionsViewModel** — One suggestion from a player's suggestion box.
- `ViewModels/UsageViewModel.cs` — **UsageTable, UsageRun, UsageViewModel** — One table of the tournament and what it takes.
- `ViewModels/WhitelistViewModel.cs` — **Allowed, WhitelistViewModel** — One Discord account allowed into the tournament.
