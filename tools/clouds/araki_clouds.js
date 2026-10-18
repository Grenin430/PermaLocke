// The sky's clouds for the cap panel, pixelled from a still of JoJo's Bizarre Adventure that the player sent (Araki's style): each cloud
// is cut out of the still, every block of BLOCK x BLOCK pixels becomes one cell, the sky's blue is left out, the block's light gives the
// tone, and a block crossed by enough of Araki's ink lines keeps a line, so the inner contours survive the shrinking. The ink outline goes
// round the outside. Writes src/PermaLocke.App/Views/Cap/CapCloudArt.cs and a review sheet.
// node tools/clouds/araki_clouds.js <still.rgb> <width> <height> <review.png>
const fs = require("fs"), path = require("path"), { execFileSync } = require("child_process");
const [file, W, H, review] = [process.argv[2], +process.argv[3], +process.argv[4], process.argv[5]];
const FF = "C:/Program Files (x86)/Soundux/ffmpeg.exe";
const img = fs.readFileSync(file);
const BLOCK = 15;
// the white streaks across the still (x0, y0, x1, y1, half width): their pixels count neither as sky nor as cloud
const STREAKS = [[704, 0, 377, 372, 22], [1800, 0, 1377, 291, 20], [1915, 156, 694, 934, 40], [836, 760, 678, 852, 16], [439, 1035, 280, 1142, 15], [1489, 943, 933, 1193, 20], [1915, 979, 1499, 1142, 17], [1915, 1204, 1765, 1245, 14]];
function inStreak(x, y) {
  return STREAKS.some(([ax, ay, bx, by, hw]) => {
    const dx = bx - ax, dy = by - ay, t = Math.max(0, Math.min(1, ((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy)));
    return Math.hypot(x - (ax + t * dx), y - (ay + t * dy)) <= hw;
  });
}
// x0, y0, x1, y1 in the still (1915 x 2000); the first ones are the big heaps, the last ones the small ones (the flat wisp of the horizon was taken out, 2026-10-18)
const CROPS = [
  ["grande1", 0, 214, 1163, 546], ["grande2", 1168, 163, 1915, 530], ["grande3", 163, 724, 1169, 1051], ["ancha", 1025, 709, 1561, 893],
  ["media1", 82, 1081, 490, 1285], ["media2", 1326, 1097, 1915, 1341], ["pequena1", 714, 1158, 1097, 1285], ["pequena2", 36, 1341, 270, 1459],
];
const px = (x, y) => { const i = (y * W + x) * 3; return [img[i], img[i + 1], img[i + 2]]; };
const isSky = ([r, g, b]) => b > 150 && b - r > 75 && r < 150;
const lum = ([r, g, b]) => 0.3 * r + 0.59 * g + 0.11 * b;
const isInk = c => !isSky(c) && lum(c) < 165;

function cut([name, x0, y0, x1, y1]) {
  const w = Math.floor((x1 - x0) / BLOCK), h = Math.floor((y1 - y0) / BLOCK);
  const g = Array.from({ length: h }, () => Array(w).fill("."));
  for (let cy = 0; cy < h; cy++) for (let cx = 0; cx < w; cx++) {
    let sky = 0, ink = 0, n = 0, l = 0, m = 0;
    for (let y = 0; y < BLOCK; y++) for (let x = 0; x < BLOCK; x++) {
      const X = x0 + cx * BLOCK + x, Y = y0 + cy * BLOCK + y, c = px(X, Y);
      if (inStreak(X, Y)) continue;
      n++;
      if (isSky(c)) { sky++; continue; }
      if (isInk(c)) ink++;
      l += lum(c); m++;
    }
    // a block mostly under a streak is left for its neighbours to fill
    if (n < BLOCK * BLOCK * 0.35) { g[cy][cx] = "?"; continue; }
    if (sky > n * 0.55 || m === 0) continue;
    const L = l / m;
    g[cy][cx] = ink > m * 0.08 ? "l" : L > 238 ? "w" : L > 224 ? "p" : L > 205 ? "b" : "s";
  }
  // the blocks under a streak take what most of their neighbours have, if most of them are cloud
  for (let pass = 0; pass < 3; pass++) for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    if (g[y][x] !== "?") continue;
    const n = {}; let cloud = 0;
    for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) { const c = g[y + dy]?.[x + dx]; if (c && c !== "." && c !== "?") { n[c] = (n[c] || 0) + 1; cloud++; } }
    if (cloud >= 3) g[y][x] = Object.entries(n).sort((a, b) => b[1] - a[1])[0][0];
    else if (pass === 2) g[y][x] = ".";
  }
  // what is left of a streak is a stair of cells touching only at their corners: those go
  for (let pass = 0; pass < 3; pass++) for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const on = (X, Y) => (g[Y]?.[X] ?? ".") !== ".";
    if (on(x, y) && !on(x - 1, y) && !on(x + 1, y) && !(on(x, y - 1) && on(x, y + 1)) && (!on(x, y - 1) || !on(x, y + 1)) && [[1, 1], [-1, -1], [1, -1], [-1, 1]].some(([dx, dy]) => on(x + dx, y + dy))) g[y][x] = ".";
  }
  // crumbs (bits of the white streaks that cross the sky) go: only pieces of 12 cells or more stay
  const seen = g.map(r => r.map(() => false));
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    if (g[y][x] === "." || seen[y][x]) continue;
    const bit = [[x, y]]; seen[y][x] = true;
    for (let i = 0; i < bit.length; i++) for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
      const X = bit[i][0] + dx, Y = bit[i][1] + dy;
      if (g[Y]?.[X] && g[Y][X] !== "." && !seen[Y][X]) { seen[Y][X] = true; bit.push([X, Y]); }
    }
    if (bit.length < 12) for (const [X, Y] of bit) g[Y][X] = ".";
  }
  // the ink round the outside
  const out = g.map(r => [...r]);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++)
    if (g[y][x] !== "." && [[1, 0], [-1, 0], [0, 1], [0, -1]].some(([dx, dy]) => (g[y + dy]?.[x + dx] ?? ".") === ".")) out[y][x] = "k";
  // trimmed to the drawing
  const rows = out.map(r => r.join(""));
  const ys = rows.map((r, y) => (/[^.]/.test(r) ? y : -1)).filter(y => y >= 0);
  const xs = [...Array(w).keys()].filter(x => rows.some(r => r[x] !== "."));
  return { name, rows: rows.slice(ys[0], ys.at(-1) + 1).map(r => r.slice(xs[0], xs.at(-1) + 1)) };
}

const clouds = CROPS.map(cut);
const cs = [
  "namespace PermaLocke.App.Views;",
  "",
  "/// <summary>",
  "/// The clouds of the sky, pixelled from a still of JoJo's Bizarre Adventure in Araki's style (2026-10-18, tools/clouds/araki_clouds.js; do",
  "/// not edit by hand). One character a cell: w white, p pale, b blue, s shade, l Araki's inner line, k the ink outline, . nothing.",
  "/// </summary>",
  "internal static class CapCloudArt",
  "{",
  "    public static readonly string[][] Clouds =",
  "    [",
  ...clouds.map(c => `        // ${c.name}, ${c.rows[0].length}x${c.rows.length}\n        [\n${c.rows.map(r => `            "${r}",`).join("\n")}\n        ],`),
  "    ];",
  "}",
  "",
];
const target = path.join(__dirname, "../../src/PermaLocke.App/Views/Cap/CapCloudArt.cs");
fs.writeFileSync(target, cs.join("\n"));

// the review sheet: every cloud at x6 on the sky's blue, one under the other
const pal = { w: [255, 255, 255], p: [238, 244, 253], b: [200, 218, 242], s: [162, 188, 228], l: [126, 156, 212], k: [62, 90, 160] };
const K = 6, sheetW = Math.max(...clouds.map(c => c.rows[0].length)) * K + 20;
const sheetH = clouds.reduce((a, c) => a + c.rows.length * K + 12, 12);
const buf = Buffer.alloc(sheetW * sheetH * 3);
for (let i = 0; i < sheetW * sheetH; i++) buf.set([38, 110, 205], i * 3);
let top = 12;
for (const c of clouds) {
  c.rows.forEach((r, y) => [...r].forEach((ch, x) => {
    if (ch === ".") return;
    for (let v = 0; v < K; v++) for (let u = 0; u < K; u++) buf.set(pal[ch], ((top + y * K + v) * sheetW + 10 + x * K + u) * 3);
  }));
  top += c.rows.length * K + 12;
}
execFileSync(FF, ["-v", "error", "-y", "-f", "rawvideo", "-pixel_format", "rgb24", "-video_size", `${sheetW}x${sheetH}`, "-i", "pipe:0", "-frames:v", "1", review], { input: buf });
console.log(clouds.map(c => `${c.name} ${c.rows[0].length}x${c.rows.length}`).join(", "));
