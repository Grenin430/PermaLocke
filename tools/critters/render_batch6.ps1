# Batch 6: reproducible export from tools/critters, using only the isolated lab.
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$lab = Join-Path $project '.lab-art/bin/PermaLocke.PlaqueLab/debug/PermaLocke.PlaqueLab.exe'
$frames = Join-Path $project '.lab-art/lote6-secuencia'
$out = 'C:/Users/javie/Desktop/imagenes/revision/lote6'
$ffmpeg = 'C:/Program Files (x86)/Soundux/ffmpeg.exe'
& $lab --especies 223,171,230,733,334 --secuencia-ventana completo 0 70 12 1920 $frames
if ($LASTEXITCODE -ne 0) { throw 'Falló la exportación del laboratorio.' }
# Two passes avoid buffering the full sequence in RAM. Both panels remain at native size.
& $ffmpeg -hide_banner -loglevel error -y -framerate 12 -i (Join-Path $frames 'frame-%04d.png') -vf 'palettegen=max_colors=256' (Join-Path $frames 'palette.png')
if ($LASTEXITCODE -ne 0) { throw 'Falló la paleta.' }
& $ffmpeg -hide_banner -loglevel error -y -framerate 12 -i (Join-Path $frames 'frame-%04d.png') -i (Join-Path $frames 'palette.png') -lavfi 'paletteuse=dither=none' -loop 0 (Join-Path $out 'lote6_juntos_en_el_layout_tamano_real.gif')
if ($LASTEXITCODE -ne 0) { throw 'Falló la creación del GIF.' }
& $lab --especies 223,171,230,733,334 --medir | Set-Content -LiteralPath (Join-Path $out 'rendimiento.txt') -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Falló la medición.' }
