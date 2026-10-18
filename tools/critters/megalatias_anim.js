// Mega Latias (2026-10-16): the reference sprite itself, animated (imagenes/megas/mega_latias_latios.png, the normal colours, top left, 1:1:
// `node recon_plain.js <crop> megalatias 16 1`, then the white around it flooded from the border). As Latias: a gentle glide, the big jet
// fin above beats from its root, the lower wing and the right fin answer it a little later, the two tail fins trail, the ears sway.
const C = require("./critter");
const { pal, rows } = C.load("megalatias");
const N = 8, pad = 7, t = f => (f / N) * 2 * Math.PI, bob = f => Math.round(0.7 * Math.sin(t(f)));
const upper = (x, y) => x >= 37 && y <= 36, lower = (x, y) => x <= 30 && y >= 37, fin = (x, y) => x >= 54 && y >= 37 && y < 61;
// where a moving part meets the body, its own cells stay in place underneath, so it never tears off when it swings away
const ghost = (mine, band) => (f, put) => rows.forEach((r, y) => [...r].forEach((c, x) => { if (c !== "." && mine(x, y) && band(y)) put(x, y + bob(f), c); }));
const fr = C.frames(rows, { count: N, pad, ink: "BEJ", parts: [
  { test: () => false, sample: ghost(upper, y => y >= 26) },
  { test: () => false, sample: ghost(lower, y => y <= 41) },
  { test: () => false, sample: ghost(fin, y => y <= 41) },
  { test: (x, y) => y <= 17 && x <= 30, move: (f, x, y) => [0.7 * Math.sin(t(f) - 1) * (17 - y) / 9, bob(f)] },
  { test: upper, move: (f, x) => [0, bob(f) + 2.4 * Math.sin(t(f)) * Math.min(1, Math.max(0, (x - 38) / 34))] },
  { test: lower, move: (f, x) => [0, bob(f) + 1.8 * Math.sin(t(f) - 0.4) * (30 - x) / 30] },
  { test: (x, y) => y >= 61, move: (f, x, y) => [1.3 * Math.sin(t(f) - 1.4) * (y - 61) / 12, bob(f)] },
  { test: (x, y) => x >= 54 && y >= 37, move: (f, x) => [0, bob(f) + 1.3 * Math.sin(t(f) - 0.7) * (x - 54) / 19] },
  { test: () => true, move: f => [0, bob(f)] },
], seam: (x, y) => (y >= 15 && y <= 19 && x <= 31) || (x >= 35 && x <= 39 && y <= 38) || (y >= 35 && y <= 39 && x <= 32) || (y >= 59 && y <= 63) || (x >= 52 && x <= 56 && y >= 36) });
console.log("megalatias", JSON.stringify(C.check(fr)));
// Both Megas are shiny: sparkles round it (sparkle.js).
module.exports = require("./sparkle")({ name: "megalatias", pal, rows, fr, fps: 7, angles: [-4, -2, 0, 2, 4], pose: 0, pad, layoutHeight: rows.length,
  // the eye and the white of the face keep their pixels when the sheet tilts
  protect: (g, x, y) => g[y][x] !== "." && x >= pad + 8 && x <= pad + 22 && y >= pad + 20 && y <= pad + 28 });
