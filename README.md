# PermaLocke

Gestor y dinamizador de Nuzlockes para **Pokémon Ultra Luna**, jugado en el emulador Azahar.
Aplicación de escritorio para Windows en C# / .NET 10 / WPF.

Acompaña la partida de principio a fin: genera una randomización propia por jugador, lee el
juego en vivo, aplica las reglas de la Nuzlocke y lleva la cuenta de puntos con un historial
auditable.

## Licencia

**GPLv3.** No es una elección estética: PermaLocke usa [PKHeX.Core](https://github.com/kwsch/PKHeX)
y [pk3DS.Core](https://github.com/kwsch/pk3DS), ambas GPLv3, que son las únicas librerías serias
para estos formatos. La GPL es vírica, así que PermaLocke se distribuye bajo GPLv3 con el código
fuente disponible. El texto completo está en [LICENSE](LICENSE).

`third_party/pk3DS.Core/` es una copia recortada de pk3DS, GPLv3, con su propio aviso.

## Lo que este repositorio NO contiene

- **La ROM.** Es propiedad de Nintendo. Cada jugador aporta la suya, y PermaLocke nunca la
  modifica: trabaja con capas de mods (LayeredFS).
- **El emulador.** Azahar es GPLv3 y se distribuye desde su propio repositorio.
- **Partidas ni saves**, que son datos del jugador.

## Documentación

- [`CLAUDE.md`](CLAUDE.md) — reglas del repositorio, estructura y estado actual.
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — documento vivo: decisiones, formatos de
  memoria, trampas encontradas y qué está verificado contra el juego real y qué no.

Ese segundo documento distingue de forma explícita lo **verificado** de lo **investigado sin
validar**. Nada marcado como sin validar debe darse por bueno.

## Compilar

```bash
dotnet build
dotnet test
dotnet publish src/PermaLocke.App -c Release -r win-x64 --self-contained true
```

Requiere el SDK de .NET 10.
