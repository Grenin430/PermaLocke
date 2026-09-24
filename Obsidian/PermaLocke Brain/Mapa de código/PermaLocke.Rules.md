---
tipo: mapa-codigo
proyecto: PermaLocke.Rules
generado: 2026-09-24
---
# PermaLocke.Rules — mapa de ficheros

Generado de la primera frase del `<summary>` de cada fichero (`src/PermaLocke.Rules/`). Tipos en negrita. Vuelve a [[00 - Inicio]] · [[Arquitectura]].

- `GameAction.cs` — **GameAction;, AttemptCapture, LevelCheck** — Something the player is trying to do. Rules never care whether it was typed into PermaLocke or detected from the running game, which is what lets the same rule set validate manual input today and a...
- `LevelCapTable.cs` — **LevelCapStage, LevelCapTable** — The level ceiling for each stage of the island tour, read from Data/levelcaps.json. 
- `RuleContext.cs` — **ZoneEncounter, IEvolutionLineProvider, NullEvolutionLineProvider, RuleContext** — The encounter that consumed a zone.
- `RuleEngine.cs` — **IRule, IRuleEngine** — Matches a key in Data/rules.json.
- `RuleResult.cs` — **RuleOutcome, RuleMode, RuleResult, RuleEvaluation** — How strictly a rule is applied. Configured globally and per rule.
- `Rules/DupesClauseRule.cs` — **DupesClauseRule** — The encounter repeats a Pokémon already obtained, so the run may allow rerolling it. Matches on exact species by default; set ignoreEvolutionaryLine to false to match on the whole family, which ne...
- `Rules/EncounterTypeRules.cs` — **SpecialEncounterRule, GiftPokemonRule, StaticEncounterRule** — Shared behaviour for encounter types that are not ordinary wild encounters. Whether they are allowed and whether they burn the zone is configuration; the rule only reports. 
- `Rules/FirstEncounterRule.cs` — **FirstEncounterRule** — One encounter per zone. Which encounter types burn the zone is configuration, not code: a gift or a static Pokémon may well be exempt depending on the run. 
- `Rules/LevelCapRule.cs` — **LevelCapRule** — Enforces the level cap of the current stage: the level of the highest Pokémon of the kahuna or totem the player is up against. 
- `Rules/ShinyClauseRule.cs` — **ShinyClauseRule** — A shiny may be caught even in a zone whose encounter is spent. It does not invalidate the original capture, and whether it burns the zone is configurable. 
- `Rules/SpeciesClauseRule.cs` — **SpeciesClauseRule** — Treats a whole evolutionary family as one species: with a Pikachu registered, a Pichu counts as a duplicate. Kept separate from the dupes clause on purpose — they answer different questions and a...
- `RulesConfiguration.cs` — **RuleIds, RuleSettings, BallControlSettings, TrialZone, AllowedStatic, RulesConfiguration, RulesConfigurationLoader** — Well-known rule identifiers, matching the keys in Data/rules.json.
- `ServiceCollectionExtensions.cs` — **ServiceCollectionExtensions** — Registers the engine and every rule. Adding a rule means adding one line here and one entry in Data/rules.json; nothing else in the application changes. 
- `Services/BallControlService.cs` — **BallControlService** — Carries out what `ncounterPolicy` decides: takes the Poké Balls away or gives them back, and writes down every step. 
- `Services/EncounterPolicy.cs` — **BallAction, EncounterSituation, EncounterDecision, EncounterPolicy** — Whether the player may have their Poké Balls right now.
- `Services/EncounterService.cs` — **RegisterCaptureRequest, RegisterCaptureResult, EncounterService** — Turns an attempted capture into rule verdicts, a stored Pokémon and audit events. 
- `Services/GameWatcher.cs` — **WatcherFindings, GameWatcher** — Compares what the game shows against what the run has recorded, and reports the difference. 
- `Services/ProgressService.cs` — **ProgressService** — Tracks how far the player has got, which is what decides the level cap in force. 
- `Services/TrialZoneService.cs` — **TrialZoneService** — Says whether a zone's wild battles still belong to its trial, so they are not the route's first encounter. 
- `Services/ZoneOutcomeService.cs` — **ZoneMark, ZoneOutcomeService** — What a zone is marked as on the map, and who marked it.
