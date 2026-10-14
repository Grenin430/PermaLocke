# Render only the isolated laboratory. No installed app or emulator is launched.
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$lab = Join-Path $project '.lab-art/bin/PermaLocke.PlaqueLab/debug/PermaLocke.PlaqueLab.exe'
$frames = Join-Path $project '.lab-art/vuelo-tanda1-secuencia'
$out = 'C:/Users/javie/Desktop/imagenes/revision/vuelo_tanda1'
$ffmpeg = 'C:/Program Files (x86)/Soundux/ffmpeg.exe'
& $lab --especies 21,731,18,733,398 --secuencia-ventana completo 0 28 12 1920 $frames
if ($LASTEXITCODE -ne 0) { throw 'Falló la exportación del laboratorio.' }
& $ffmpeg -hide_banner -loglevel error -y -framerate 12 -i (Join-Path $frames 'frame-%04d.png') -vf 'palettegen=max_colors=256' (Join-Path $frames 'palette.png')
if ($LASTEXITCODE -ne 0) { throw 'Falló la paleta.' }
& $ffmpeg -hide_banner -loglevel error -y -framerate 12 -i (Join-Path $frames 'frame-%04d.png') -i (Join-Path $frames 'palette.png') -lavfi 'paletteuse=dither=none' -loop 0 (Join-Path $out 'tanda1_juntos_en_el_layout_tamano_real.gif')
if ($LASTEXITCODE -ne 0) { throw 'Falló el GIF.' }
& $lab --especies 21,731,18,733,398 --medir | Set-Content -LiteralPath (Join-Path $out 'rendimiento.txt') -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Falló la medición.' }
