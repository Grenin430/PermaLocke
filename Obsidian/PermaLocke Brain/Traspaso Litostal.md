# Traspaso: el Litostal (cristal 6) y las ruinas de Gimmighoul

Prompt para la sesión nueva (copiar tal cual):

---

Trabajo en PermaLocke singleplayer (`C:\Users\javie\Desktop\Permalocke singleplayer`, rama `singleplayer`). Antes de nada lee `CLAUDE.md`, `Obsidian/PermaLocke Brain/00 - Inicio.md` y sobre todo `Obsidian/PermaLocke Brain/Plan de las ruinas de Gimmighoul.md` y los §248-§257 de `docs/ARCHITECTURE.md` (índice en `Obsidian/PermaLocke Brain/Índice de ARCHITECTURE.md`).

**Qué estamos haciendo.** Cada vez que se supera una prueba, su cristal Z llega al panel del abismo con una animación propia de su tipo (con guiño a una mecánica de Pokémon) y se queda para siempre en un sitio de las ruinas de Gimmighoul que marco yo, cambiando el entorno según su tipo. Hechos y aprobados: Normastal (Tapu Koko, `CapTapuKoko` + `CapCrystalFall`), Lucharastal (Machamp Gigamax, `CapMachamp`), Hidrostal (Mega Greninja, `CapGreninja`), Pirostal (Ho-Oh, `CapHoOh`), Fitostal (Shaymin, `CapShaymin`, v2 hecha). Entorno por tipo hecho para los cinco (§257).

**Dónde está el Litostal (819, 6.ª prueba).** Prototipo MUY simple, para corregirlo y pulirlo contigo:
- `src/PermaLocke.App/Views/Cap/CapAreaZero.cs`: Terápagos sale del ultraumbral y da vueltas por las dos ventanas; a su paso brotan cristales iridiscentes del Área Cero (dibujados a mano en `Cluster`) en el fondo de los dos mares, y uno grande con el Litostal dentro encima del muro bajo de la ventana grande (`Spot`, pieza 124 de `CapGimmighoulRuins`, x0+131, sitio que marqué); Terápagos se va por la izquierda; sale Miraidon y los rompe uno a uno (`Shatter`), el grande el último en el impacto de la película, y el Litostal queda en el muro con unas esquirlas (`PaintRest`, desde `CapWorldView`). Película propia de 9 s antes del reloj (`CapAreaZero.Lead`, `CapTrial.Lead`).
- Sprites en `tools/critters`: `terapagos.txt`/`terapagos_anim.js` y `miraidon.txt`/`miraidon_anim.js` (sin animar todavía, lo pedí así; `CritterArt` 1024 y 1008). Miraidon tiene un hueco blanco dentro del cuerpo que viene del gráfico de punto de cruz y hay que quitar.
- Laboratorio: `dotnet build tools/PermaLocke.PlaqueLab --artifacts-path .lab-art/ruinas` y `.lab-art/ruinas/bin/PermaLocke.PlaqueLab/debug/PermaLocke.PlaqueLab.exe --secuencia-ventana litostal 0 18 20 1920 <carpeta>` (las secuencias empiezan siempre en 0). Vídeos con ffmpeg (`C:\Program Files (x86)\Soundux\ffmpeg.exe`). Revisión en `Desktop/imagenes/revision/prototipo_ruinas/litostal`.
- **Conflicto por decidir:** el evento de las megas (`CapMega`, aprobado para la 6.ª prueba) coincidía con la película del Litostal; de momento `CapMega.Since` espera a que acaben las películas del cristal y del cap (`Wait`). Pregúntame qué hacer.
- Falta: el efecto del tipo roca en el entorno del Litostal (ahora solo unas esquirlas) y todo el pulido.

**Cómo trabajamos.** Prototipo simple, me pasas SIEMPRE vídeo mp4 y ampliación de cada cambio con SendUserFile, yo te corrijo cosas y vamos puliendo (más detalle, más lento, pixel art hecho a mano sin degradados ni brillos borrosos). Nada de commit ni desplegar sin que lo pida, y no toques «PermaLocke prueba» ni «PermaLocke para amigos». Respóndeme en español. Al acabar algo, compila la app (`dotnet build`), pasa `dotnet test tests/PermaLocke.App.Tests --artifacts-path ".testart-tmp"` (y borra `.testart-tmp`) y actualiza ARCHITECTURE y el cerebro.

Ahora te voy a corregir cosas del prototipo del Litostal para mejorarlo.

---
