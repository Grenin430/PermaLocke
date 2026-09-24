#!/bin/sh
# Regenera "Mapa de código/*.md" y "Mapa de código/Tests.md". Ejecutar desde la raíz del repo con Git Bash:
#   sh "Obsidian/PermaLocke Brain/_herramientas/regenerar-mapa.sh"
V="Obsidian/PermaLocke Brain"; T="$V/_herramientas"; D=$(date +%F)
for p in src/*/; do n=$(basename "$p"); short=${n#PermaLocke.}
  { printf -- "---\ntipo: mapa-codigo\nproyecto: %s\ngenerado: %s\n---\n# %s — mapa de ficheros\n\n" "$n" "$D" "$n"
    printf "Generado de la primera frase del \`<summary>\` de cada fichero (\`src/%s/\`). Tipos en negrita. Vuelve a [[00 - Inicio]] · [[Arquitectura]].\n\n" "$n"
    find "$p" -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' | sort | xargs awk -f "$T/mapa.awk"; } > "$V/Mapa de código/$n.md"
done
{ printf -- "---\ntipo: mapa-codigo\ngenerado: %s\n---\n# Pruebas — ficheros y número de [Fact]/[Theory]\n\nContado con grep, NO ejecutado. Detalle en [[Pruebas]].\n\n" "$D"
  for p in tests/*/; do echo "## $(basename "$p")"; for f in $(find "$p" -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' | sort); do
    n=$(grep -cE '\[(Fact|Theory)' "$f"); [ "$n" -gt 0 ] && echo "- \`$(basename "$f")\` ($n)"; done; done; } > "$V/Mapa de código/Tests.md"
echo "mapa regenerado"
