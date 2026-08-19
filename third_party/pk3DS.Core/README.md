# pk3DS.Core (vendorizado)

Copia recortada de `pk3DS.Core`, de **pk3DS** — https://github.com/kwsch/pk3DS

- Autor original: Kaphotics / Project Pokémon
- Licencia: **GPLv3** (ver `LICENSE.md`), la misma que PermaLocke
- Commit de origen: ver `UPSTREAM-COMMIT.txt`

## Por qué está vendorizado y no es un paquete NuGet

`pk3DS.Core` **no se publica en NuGet** (`dotnet package search pk3DS` no devuelve nada), así
que la única forma de usarlo es incluir el fuente. La GPL lo permite y PermaLocke ya es GPLv3.

## Qué se ha quitado, y por qué

El proyecto original declara `net10.0-windows` con `UseWindowsForms`. `PermaLocke.Randomizer`
no puede depender de UI (ver `CLAUDE.md`), así que se han eliminado los ficheros que usan
WinForms o System.Drawing. Ninguno hace falta para randomizar GARCs:

| Fichero | Para qué servía |
|---|---|
| `CTR/RomFS.cs` | Extracción de RomFS. Además es inservible sin interfaz: desreferencia un `ProgressBar` nulo, y solo extrae los 3,5 GB enteros. PermaLocke usa su propio `RomFsReader`. |
| `CTR/NCCH.cs`, `CTR/NCSD.cs` | Empaquetado de ROM. PermaLocke no reconstruye ROMs: usa LayeredFS. |
| `CTR/CTR.cs`, `CTR/CRO.cs`, `CTR/BLZ.cs`, `CTR/SMDH.cs` | Firmas, CROs, compresión de `code.bin`, iconos. |
| `CTR/ETC1.cs`, `CTR/Exheader.cs` | Dependían de `Properties.Resources`, también eliminado. |
| `CTR/Images/`, `ImageUtil.cs`, `Structures/TypeChart.cs` | Conversión de texturas y render de la tabla de tipos a `Bitmap`. |

Con eso, el resto compila como **`net10.0` puro**. Comprobado: 0 errores.

## Aviso: sus escritores no son fieles

Verificado contra el juego real (ver `docs/ARCHITECTURE.md` §19), dos funciones de esta
librería corrompen datos en silencio:

- **`Area7.GetDayNightTableBinary`** pone a cero los 4 primeros bytes de cada tabla de
  encuentros (en vanilla no siempre son cero) y encoge la cabecera del mini de 128 a 80 bytes.
  El resultado es un fichero que Ultra Luna carga y del que no saca ningún encuentro.
- **El round-trip de `TextFile`** deja los dos ficheros de texto japoneses con 0 líneas,
  manteniendo el tamaño en bytes.

Por eso PermaLocke **lee con pk3DS pero no escribe con pk3DS**: parchea bytes sobre el payload
descomprimido y vuelve a leer lo escrito para verificarlo.
