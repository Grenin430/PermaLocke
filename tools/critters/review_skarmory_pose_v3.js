// Skarmory flight base for visual approval: the reference, the original sprite and the new base side by side, plus the base
// alone. Nothing here touches the production sheets, CapCritterArt.cs or the animation pipeline.
const fs = require("fs"), path = require("path"), { execFileSync } = require("child_process");
const C = require("./critter"), pose = require("./skarmory_flight_pose_v3");
const OUT = "C:/Users/javie/Desktop/imagenes/revision/skarmory_pose_v3", FF = "C:/Program Files (x86)/Soundux/ffmpeg.exe";
const REF = path.join(OUT, "referencias/phantom_forces_59.png");
fs.mkdirSync(OUT, { recursive: true });
const b = pose.build(), { pal } = b, base = b.rows, orig = C.load("skarmory").rows;
const rgb = h => [0, 2, 4].map(i => parseInt(h.slice(i, i + 2), 16));

function raw(rows, k, bg) {
  const h = rows.length, w = rows[0].length, out = Buffer.alloc(w * k * h * k * 4);
  rows.forEach((r, y) => [...r].forEach((c, x) => {
    const col = c === "." ? bg : [...rgb(pal[c]), 255];
    for (let v = 0; v < k; v++) for (let u = 0; u < k; u++) out.set(col, ((y * k + v) * w * k + x * k + u) * 4);
  }));
  return { out, w: w * k, h: h * k };
}
function png(rows, k, file, bg = [0, 0, 0, 0]) {
  const r = raw(rows, k, bg);
  execFileSync(FF, ["-v", "error", "-y", "-f", "rawvideo", "-pixel_format", "rgba", "-video_size", `${r.w}x${r.h}`, "-i", "pipe:0", "-frames:v", "1", file], { input: r.out });
}
const water = [0x2a, 0x6a, 0x90, 255];
png(base, 1, path.join(OUT, "skarmory_base_nativa.png"));
png(base, 6, path.join(OUT, "skarmory_base_ampliada.png"), water);
png(orig, 3, path.join(OUT, "_original_x3.png"), water);
png(base, 3, path.join(OUT, "_base_x3.png"), water);
fs.writeFileSync(path.join(OUT, "skarmory_base.txt"), base.join("\n") + "\n");
fs.writeFileSync(path.join(OUT, "skarmory_base.json"), JSON.stringify({ pal, rows: base }) + "\n");

// One sheet: reference, original x3 and base x3 on the sea colour, each with its label.
const font = "fontfile='C\\:/Windows/Fonts/arial.ttf'";
const label = (t, x) => `drawtext=${font}:text='${t}':x=${x}:y=14:fontsize=20:fontcolor=white`;
execFileSync(FF, ["-v", "error", "-y", "-i", REF, "-i", path.join(OUT, "_original_x3.png"), "-i", path.join(OUT, "_base_x3.png"),
  "-filter_complex",
  "[0]scale=-1:300:flags=lanczos,pad=iw+40:360:20:50:0x2A6A90[a];[1]pad=iw+40:360:20:50:0x2A6A90[b];[2]pad=iw+40:360:20:50:0x2A6A90[c];" +
  "[a][b][c]hstack=inputs=3," + [label("REFERENCIA DE VUELO", 20), label("SPRITE ORIGINAL x3", 522), label("NUEVA BASE x3", 760)].join(","),
  path.join(OUT, "skarmory_pose_y_referencia.png")]);
for (const f of ["_original_x3.png", "_base_x3.png"]) fs.unlinkSync(path.join(OUT, f));

const check = C.check([base.map(r => [...r])]);
const used = new Set(base.join("").replace(/\./g, ""));
const report = {
  size: [base[0].length, base.length], opaque: check.pixels[0], iso: check.iso, holes: check.holes,
  paletteOnly: [...used].every(c => c in pal), originalSize: [orig[0].length, orig.length],
  limits: ["La prueba holes solo detecta huecos unitarios", "Base estatica: sin animacion, integracion ni medicion del layout"],
};
fs.writeFileSync(path.join(OUT, "auditoria.json"), JSON.stringify(report, null, 2) + "\n");
console.log(JSON.stringify(report));
