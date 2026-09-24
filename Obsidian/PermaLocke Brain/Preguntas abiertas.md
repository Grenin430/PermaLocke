---
tipo: pendiente
revisado: 2026-09-24
---
# Preguntas abiertas

Lo que sigue sin saberse o sin verse. La tabla «Lo que NO está resuelto» de `CLAUDE.md` tiene el estado por funcionalidad; aquí van los huecos concretos.

## Sin confirmar jugando
- Por qué se retrasó la muerte del amigo (tablas, PS del equipo o límite de 6 s de la barra) y cómo va la r3 en su PC.
- Tablas de combate en **entrenadores, dobles y SOS**: solo se ha medido el salvaje individual (§114, `BattlePokemon`).
- Regla de primer encuentro entera en una partida y con un variocolor real (§117).
- Dominantes dentro de rutas normales: Ultraganga abandonado y Cañón de Poni siguen sin medir (§160). La Jungla Umbría ya está cerrada hasta el cristal (§179), pero sin jugar.
- La escena de los iniciales (§146), las tiendas de evolución (§145) y los combates importantes y megas (§157), sin ver en el juego.
- En qué mostrador exacto se vende la Moneda de Gimmighoul (se cree que en el 22, el Supermercado Ultraganga del centro).

## Resuelto (se deja para no volver a preguntar)
- 2026-09-24, dicho por el usuario: **el amigo ya no tiene cierres** de Azahar con el parche 4 (§173).
- 2026-09-24, dicho por el usuario: **Gimmighoul necesita 999 Monedas de Gimmighoul** para evolucionar, aunque la tabla de evoluciones solo diga método 8 (usar objeto) con arg 994. La tabla no dice la cantidad: la pone el código del mod. Con la moneda a 30, son 29.970 en total.
- Si las bayas que caen al sacudir un árbol salen de la tabla randomizada.

## Visto y sin arreglar
- El «#822» del cementerio ya no se ve: se quitó el bloque de datos (§177). La causa probable, nombres de gen 8-9 por PKHeX, sigue en `CemeteryViewModel` sin pintarse.
- Hay un Huevo Malo en la partida guardada vieja (hueco 3, del §97), sin reparar a petición del usuario. Puede ser histórico si ya se empezó de cero.

## Del código
- `Data/zones.json` (40 KB): ningún `.cs` de `src` lo nombra. ¿Histórico (ancla del §23) o lo lee algo por otra ruta? Comprobar antes de tocarlo y **no borrar sin permiso**.
- `Palette.xaml`, `Controls.xaml` e `Icons.xaml` (tema viejo del §74) siguen cargados junto a `Pixel.xaml`. No se sabe qué queda usándolos después de la conversión pixel.
- `SyncView.xaml` (COMPETICIÓN) no está en pixel. Está oculta en la distribución local, pero visible en la de desarrollo.
- Sprites «que no se ven» reportados por el usuario el 2026-09-23: en su carpeta no se reprodujo. Pendiente de que diga qué Pokémon y en qué pantalla.
