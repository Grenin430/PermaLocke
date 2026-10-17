// Recolours a finished critter sheet colour for colour (2026-10-16): a Mega form that keeps its base form's approved flight. Every pixel of a
// colour in the map takes the new one; the rest stay. node recolor_sheet.js <in.png> <out.png> <map.json>
const fs = require("fs");
const { execFileSync } = require("child_process");
const FF = "C:/Program Files (x86)/Soundux/ffmpeg.exe";
const [src, dst, mapFile] = process.argv.slice(2);
const map = JSON.parse(fs.readFileSync(mapFile, "utf8"));
let W, H;
try { execFileSync(FF, ["-hide_banner", "-i", src]); } catch (e) { const m = e.stderr.toString().match(/, (\d{2,4})x(\d{2,4})/); W = +m[1]; H = +m[2]; }
const raw = execFileSync(FF, ["-hide_banner", "-loglevel", "error", "-i", src, "-f", "rawvideo", "-pix_fmt", "rgba", "-"], { maxBuffer: 1 << 28 });
let changed = 0;
for (let i = 0; i < W * H; i++) {
  if (raw[i * 4 + 3] === 0) continue;
  const k = [0, 1, 2].map(j => raw[i * 4 + j].toString(16).padStart(2, "0")).join("").toUpperCase();
  const to = map[k];
  if (!to) continue;
  for (let j = 0; j < 3; j++) raw[i * 4 + j] = parseInt(to.slice(j * 2, j * 2 + 2), 16);
  changed++;
}
execFileSync(FF, ["-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgba", "-s", `${W}x${H}`, "-i", "-", dst], { input: raw });
console.log(`${changed} pixels recoloured`);
