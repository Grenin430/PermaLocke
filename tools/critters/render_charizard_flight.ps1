# Only the isolated laboratory is launched; installed apps and the emulator remain untouched.
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$lab = Join-Path $project '.lab-art/bin/PermaLocke.PlaqueLab/debug/PermaLocke.PlaqueLab.exe'
$frames = Join-Path $project '.lab-art/charizard-wing-v5-sequence'
$out = 'C:/Users/javie/Desktop/imagenes/revision/charizard_batido_v5'
$ffmpeg = 'C:/Program Files (x86)/Soundux/ffmpeg.exe'
& $lab --especies 6 --secuencia-ventana completo 0 28 12 1920 $frames
if ($LASTEXITCODE -ne 0) { throw 'Falló la exportación del laboratorio.' }
& $ffmpeg -v error -y -framerate 12 -i (Join-Path $frames 'frame-%04d.png') -vf 'palettegen=max_colors=256' (Join-Path $frames 'palette.png')
if ($LASTEXITCODE -ne 0) { throw 'Falló la paleta.' }
& $ffmpeg -v error -y -framerate 12 -i (Join-Path $frames 'frame-%04d.png') -i (Join-Path $frames 'palette.png') -lavfi 'paletteuse=dither=none' -loop 0 (Join-Path $out 'charizard_en_el_layout_tamano_real.gif')
if ($LASTEXITCODE -ne 0) { throw 'Falló el GIF.' }
& $lab --especies 6 --medir | Set-Content -LiteralPath (Join-Path $out 'rendimiento.txt') -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Falló la medición.' }