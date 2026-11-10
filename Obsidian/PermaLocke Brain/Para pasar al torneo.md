# Para pasar al torneo

Lo que se hizo en **PermaLocke singleplayer** (`Desktop\Permalocke singleplayer`, rama `singleplayer`) y que el usuario quiere llevar en el futuro a la app del torneo (`Desktop\Permalocke definitivo`). Pedido el 2026-10-17. Cada punto dice dónde está documentado y si es solo del singleplayer.

## Imprescindible (fallos que también tiene el torneo)

- **Habilidad 268 Olor Persistente vetada para siempre** (2026-10-17). Con el mod de gen 8-9, al contagiársela a quien golpea por contacto **el juego se cuelga** (visto jugando: Furioseta con ella, un Samurott salvaje la golpeó). Veto fijo en código: `Core/Domain/BrokenAbilities.cs`, usado por `AbilityTable.Assignable` (randomizador) y `AbilityDraw.IsDealable` (gacha, guardería, wonder trade); además en `bannedAbilities` de `Data/randomizer.json`. Test `Lingering_aroma_is_never_dealt_whatever_the_configuration_says`. **Un mundo ya generado la conserva**: hay que regenerarlo o cambiarla en la tabla de especies instalada.
- **Nombres de objetos del mod** (desde el 960 el mod numera a su manera): `MachineItemLookup` lee los del texto del mundo instalado; `SaveBoxReader` los usa (§250).
- **El instalador borra lo que la versión nueva del mod ya no trae**, pasándolo por la copia (`ModInstaller.Install`, §250).
- **El panel del cap no puede ahogar la interfaz**: fotogramas espaciados por su coste (`CapBadge.OnRendering`, §250). Sin esto, un efecto caro dejaba una ventana fantasma encima del juego que se comía los clics.

- **La cura justo después de un combate no se retiene** (`HpPresenter`, `tablesGone`, §251) y **el variocolor se va al segundo** (`ShinyGrace`, §251).

## Objetos y parches del juego

- **SuperCarameloraro**: cursor de la mochila y bucle de «usar otro» (`PatchBagCursor`, §250). **Ojo**: el sitio de la evolución (0xE8BC) debe quedarse con el 50, o la evolución se cuelga.
- **Poción Variocolor** (objeto 121): mochila, parche del generador de PID, la app la gasta al leer el salvaje; 3 en el suelo de mundos nuevos (§250).
- **Iconos de la Poción y del Pokévial** redibujados a ~22 px con margen (la mochila encoge y corta).

## Overlay

- Megas del mar y evento de la prueba 6 (§248).
- Variocolor del panel: solo la animación épica (§250).
- Ojos que parpadeaban al encoger los sprites (`CapVisitors.Pick`).
- Ultraumbral fiel al arte oficial y muertes ajustadas a su núcleo (§252).
- Nubes de Araki y cielo de día más azul (§253).
- Sin el cono de luz del sol o la luna (`CapDeep`); humo del Repelente nuevo; Incubadora y Repelente cambiados de sitio.

## Ruinas de Gimmighoul (el torneo está en la 1.1.0, con todo hasta el Litostal)

- **Electrostal con Raikou** (§260): `CapRaikou.cs`, hojas `raikou`/`raikoupie` en `Views/Cap/Critters` y sus entradas 243 y 20243 en `CapCritterArt`, enganches en `CapTrial` (`Lead`, `Land`, `phoenix`, pintado) y `CapWorldView` (`PaintRest`).

- **Piso enterrado que sube tras el Electrostal** (§261): `CapSeaContext.Rise`/`Rising`, `CapGimmighoulRuins` (`Lift`, `Lower`, `Rising`, `Rumble`, `RiseOf`) y `CapWorldView` (`Won`, `Rumble`). Pasa junto con el Electrostal.

## Solo del singleplayer (decidir antes de pasarlo)

- **Fuera todos los avisos encima del juego** (`Notifier`, `ToastWindow`): el torneo los usa para amigos, fantasmas y actualizaciones; no pasar tal cual.
- **Fuera la carta que volaba al álbum**: decisión del jugador para su partida; preguntar en el torneo.
- Registros más limpios (dos líneas a nivel de depuración) y `PlaqueLab --medir`: se pueden pasar sin más.

Relacionado: [[Historial de conversaciones]], [[Trampas y lecciones]], ARCHITECTURE §248-§250.

## Después de la 1.1.0 del torneo (2026-10-18)

- Cristal 7 Electrostal con Raikou (§260), el piso enterrado que sube (§261) cristal 8 Spectrostal con Giratina (§262) cristal 9 Nictostal con Yveltal (§263) cristal 10 Dracostal con Dragapult (§264) y cristal 11 Feeristal con Mega Diancie (§265, `CapMega.Symbol` pasó a internal) y cristal 12 Geostal con Groudon Primigenio (§266, `CapGroudon`, hojas `groudon`/`primalgroudon`, entradas 383 y 10383); además el cristal 1 rehecho con Arceus (§267: `CapTapuKoko` pasa a `CapArceus`, hoja `arceus` y entrada 493, `CapTrial.Lead` y `tw` para el prólogo del Normastal) y sin los rayos de sol bajo el agua (`CapSeaLive.Shafts` fuera): solo panel del cap, se pasan como el resto de cristales.

## Primer encuentro de una ruta (2026-10-20, §269)

- Archivos:
  - `CapEncounter.cs` entero;
  - `CapModel` (`Encounter` en `CapPlaqueData`);
  - `CapWorldView` (pinta la ruta en las dos ventanas);
  - `CapScenarios` (`primer-encuentro`);
  - `EncounterGuard` (`FirstEncounterSeen`, `RouteEncounter`);
  - `CapBadge` (`_encounterTurn`).
- Preguntar en el torneo: allí los avisos sobre el juego siguen vivos. El aviso «Primer encuentro» y la ruta salen a la vez.
- El banderín escribe en el abismo por decisión del jugador de singleplayer. Confirmarlo también allí.

- Desde 2026-10-20 (§270, §271): el banderín dice «POKÉMON CAPTURABLE» en el primer encuentro de ruta y en los estáticos permitidos, y en un duplicado sale en ámbar con «DUPLICADO, TÚ ELIGES». Se pasan con §269 (`EncounterSeen`, `RouteEncounter` con `Catchable` y `Duplicate`).

- §272-§273: la caché del suelo y de las copas, la espera de 2,5 s (`OverlayDelay`) y los tres finales (`WildFainted` en `EncounterGuard`, `CapBadge.Outcome`).

- §274: `CapItemRain` (archivo nuevo), `CapItemDrop` en `CapPlaqueData.Items`, `CapBadge` (`_drops`), `CapWorldView` (lo pinta en el abismo) y `PlayNotifications` (ya no lanza `CelebrateItem`).
  - Preguntar en el torneo si quieren quitar allí la animación de la mochila.

- §275: `CapVisitors.Pick`/`Tiny` (y la prueba), `CapSea` sin `Glitter`, `CapAnim` sin el estado `Danger` (y sus dos pruebas). Preguntar en el torneo si quieren quitar también la marea roja.

> **Pasado al torneo el 2026-10-20 (1.1.2, allí §271):** §269-§275 (primer encuentro, capturables, duplicados, finales, rendimiento, objetos al mar, ojos, sol, marea roja) y la Isla Exeggutor. Lo de aquí en adelante, apuntarlo debajo.
- §276: portal del randomizador como ultraumbral de verdad (`UltraWormholeScene`). También pendiente de preguntar: el ultraumbral de objetos detrás de los voladores (`CapItemRain.PaintWormhole`).
- §277: líquido del Pokévial (`CapVial`).
- §278: muerte «Autodestrucción» (`CapDeathSelfDestruct.cs`, `Push` en `CapWorldView`).

> **Pasado al torneo el 2026-10-23 (1.1.3, allí §272):** §276-§278, el ultraumbral de objetos detrás de los voladores, todos los visitantes nuevos hasta Naganadel y Poipole, y las pasadas más frecuentes. Lo de aquí en adelante, apuntarlo debajo.

> **Pasado al torneo el 2026-11-10 (sin commit ni publicar, allí §275-§276):** el sistema de vidas (Sana Balls, contador, pérdida del
> Locke, fin de run), los avisos en píxeles (`PixelDialog`), el launcher (elegir juego, volver al menú, datos en `Data/ultraluna/`,
> módulo de Ultra Luna) y HeartGold básico (Universal Randomizer + melonDS). En el torneo: `RulesSync` y Admin leen/escriben las reglas
> oficiales con `paths.DataFile`. Antes de pasarlo se borraron aquí los prototipos de vidas (`CapLivesDecor` solo con las Sana Balls, sin
> `CapLivesHud`). Lo de aquí en adelante, apuntarlo debajo.
