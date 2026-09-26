---
tipo: contexto
revisado: 2026-09-24
---
# Usuario y forma de trabajar

Usuario: Grenin430 (git). Escribe en español; se le responde en español. En el texto visible se usan pronombres neutros o no se usan. Participa en una competición de Nuzlocke entre amigos (la de la carpeta `Locke/`, que no es suya) y hace PermaLocke para él y sus amigos.

## Reglas que ha dado (vigentes)
- **TORNEO EN CURSO (desde el 2026-09-26): solo se cambia el repo `Permalocke definitivo`.** Nada de `desplegar.ps1`, ni copiar `Data/` a `PermaLocke prueba` (ahí juega el usuario), ni tocar la carpeta de amigos. Lo que se haga es para el siguiente torneo, más grande. Hasta que diga que acabó.
- **«No hagas nada directamente, si estoy testeando para decirte errores y tú arreglarlos.»** Cuando está probando, arreglar lo que reporta y no emprender cosas por iniciativa propia.
- **«Si hay que borrar cosas me lo dices y yo te lo confirmo.»** Todo borrado se pregunta antes y se hace solo tras un sí explícito.
- **«Aún no actualices la carpeta de amigos.»** `C:\Users\javie\Desktop\PermaLocke para amigos` no se toca hasta que lo diga.
- Commit o push solo cuando lo pida. La atribución la marca el recordatorio del sistema (hoy `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`).
- El cerebro (este vault) es para Claude: denso y útil, no bonito ([[00 - Inicio]]).

## Carpetas y ficheros intocables
- `Locke/` y `C:\Users\javie\Desktop\Locke`: solo lectura. Es material ajeno, BxnnyLocke con una partida del usuario.
- `ROM/`: la ROM vanilla no se modifica nunca.
- `Desktop\PermaLocke.zip`: lo hizo el usuario, contiene la ROM. Ni tocar ni repartir.
- `Desktop\PermaLocke prueba`: la carpeta donde el usuario juega. Solo se despliega el exe en ella, y **solo tras comprobar que no corre ni PermaLocke ni Azahar**:
  `Get-Process | Where {($_.ProcessName -like 'PermaLocke*' -or $_.ProcessName -like 'azahar*') -and $_.Threads.Count -gt 0}`
- Copia aislada de verificación: `scratchpad\gacha-e2e-211902` (en el scratchpad de la sesión, que cambia de una sesión a otra). Dentro, `Expansion` es una **unión** (junction) a `Desktop\PermaLocke prueba\Expansion`. **Nunca se borra recursivamente**: solo se quita la unión.
- **Aislar la run no aísla la partida**: `PlayerSave` localiza el save de Azahar de verdad. Una copia aislada que escribe la partida escribe la real. Por eso las pruebas visuales van con `--sin-juego`, y nada destructivo en copias.

## Gustos que ha dejado claros
- Lo que más le gusta: **pixel art con animación de juego y datos reales**. La máquina de cápsulas del gacha es el listón. Lo difuminado y los degradados «parecen hechos por una IA» y se tiraron.
- Acento **violeta** (el del ultraespacio). La paleta `Px*` está en `Themes/Pixel.xaml`.
- No quiere listas de ideas en texto: prefiere ver cosas hechas.
- Quiere saber qué está verificado y qué no, sin adornos.
- Se le dicen los números medidos, no estimaciones.

## Cómo se ha trabajado (patrones que funcionaron)
- Verificar en la app real con la copia aislada y `--sin-juego`, capturando con `secciones-e2e.ps1` y `visor-e2e.ps1` (ver [[UI y kit pixel]]).
- Probar las escrituras destructivas sobre **copias** de la partida (las sondas `Probe --x --probar`).
- Documentar cada cambio como un § nuevo en `docs/ARCHITECTURE.md` más una línea en `CLAUDE.md`.
