# Para pasar al torneo

Lo que se hizo en **PermaLocke singleplayer** (`Desktop\Permalocke singleplayer`, rama `singleplayer`) y que el usuario quiere llevar en el futuro a la app del torneo (`Desktop\Permalocke definitivo`). Pedido el 2026-10-17. Cada punto dice dónde está documentado y si es solo del singleplayer.

> **Pasado al torneo el 2026-10-18 (§260, 1.1.0)**: todo lo de esta nota salvo lo que es solo del singleplayer. Lo que se haga desde ahora en el singleplayer, apuntarlo debajo de nuevo.

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

## Solo del singleplayer (decidir antes de pasarlo)

- **Fuera todos los avisos encima del juego** (`Notifier`, `ToastWindow`): el torneo los usa para amigos, fantasmas y actualizaciones; no pasar tal cual.
- **Fuera la carta que volaba al álbum**: decisión del jugador para su partida; preguntar en el torneo.
- Registros más limpios (dos líneas a nivel de depuración) y `PlaqueLab --medir`: se pueden pasar sin más.

Relacionado: [[Historial de conversaciones]], [[Trampas y lecciones]], ARCHITECTURE §248-§250.
