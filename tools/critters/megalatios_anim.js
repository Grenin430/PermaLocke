// Mega Latios (2026-10-16): the reference sprite itself, animated (imagenes/megas/mega_latias_latios.png, the green one, bottom right (the user wants this one), 1:1:
// `node recon_plain.js <crop> megalatios 16 1`, then the white around it flooded from the border). As Mega Latias: the big fin above beats from
// its root, the lower wing answers a little later, the fins below trail, the ears sway.
const C = require("./critter");
const { pal, rows } = C.load("megalatios");
const N = 8, pad = 7, t = f => (f / N) * 2 * Math.PI, bob = f => Math.round(0.7 * Math.sin(t(f)));
const tip = (x, y) => x >= 78 && y >= 26 && y <= 36;
const upper = (x, y) => x >= 38 && y <= 30 && !tip(x, y), lower = (x, y) => y >= 42 && x <= 48;
// where a moving part meets the body, its own cells stay in place underneath, so it never tears off when it swings away
const ghost = (mine, band) => (f, put) => rows.forEach((r, y) => [...r].forEach((c, x) => { if (c !== "." && mine(x, y) && band(y)) put(x, y + bob(f), c); }));
const fr = C.frames(rows, { count: N, pad, ink: "BEL", parts: [
  { test: () => false, sample: ghost(upper, y => y >= 18) },
  { test: () => false, sample: ghost(lower, y => y <= 45) },
  { test: (x, y) => y <= 22 && x <= 24, move: (f, x, y) => [0.7 * Math.sin(t(f) - 1) * (22 - y) / 10, bob(f)] },
  { test: upper, move: (f, x) => [0, bob(f) + 2.4 * Math.sin(t(f)) * Math.min(1, Math.max(0, (x - 40) / 40))] },
  { test: lower, move: (f, x) => [0, bob(f) + 1.8 * Math.sin(t(f) - 0.4) * Math.max(0, (48 - x) / 31)] },
  { test: (x, y) => y >= 56 && x >= 50 && x <= 72, move: (f, x, y) => [1.3 * Math.sin(t(f) - 1.4) * (y - 56) / 11, bob(f)] },
  { test: (x, y) => x >= 68 && y >= 38, move: (f, x) => [0, bob(f) + 1.3 * Math.sin(t(f) - 0.7) * (x - 68) / 22] },
  { test: () => true, move: f => [0, bob(f)] },
], seam: (x, y) => (y >= 20 && y <= 24 && x <= 25) || (y >= 28 && y <= 32 && x >= 36) || (x >= 76 && x <= 80 && y >= 24) || (x >= 46 && x <= 50 && y >= 40)
  || (y >= 54 && y <= 58 && x >= 48) || (x >= 66 && x <= 70 && y >= 36) });
console.log("megalatios", JSON.stringify(C.check(fr)));
// Both Megas are shiny: sparkles round it (sparkle.js).
module.exports = require("./sparkle")({ name: "megalatios", pal, rows, fr, fps: 7, angles: [-2, 0, 2], pose: 0, pad, layoutHeight: rows.length,
  // the eye and the white of the face keep their pixels when the sheet tilts
  protect: (g, x, y) => g[y][x] !== "." && x >= pad && x <= pad + 13 && y >= pad + 24 && y <= pad + 32 });
