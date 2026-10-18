// Primal Kyogre (2026-10-16): the reference is a chart (cells of 6.5 px, `node recon2.js imagenes/megas/kyogre_primigenio.webp primalkyogre 18`).
// As Kyogre: the two broad paddles, the one up at the left and the one down at the right, stroke against each other; the forked tail at the top
// right sweeps a beat later; the dorsal fin leans; the whole bobs. The glowing markings ride on each part; the eye stays.
const C = require("./critter");
const { pal, rows } = C.load("primalkyogre");
const N = 8, pad = 8, t = f => (f / N) * 2 * Math.PI, bob = f => Math.round(0.6 * Math.sin(t(f)));
const fr = C.frames(rows, { count: N, pad, ink: "BI", parts: [
  { test: (x, y) => x <= 26 && y <= 27, move: (f, x) => [0, bob(f) + 3 * Math.sin(t(f)) * (26 - x) / 26] },
  { test: (x, y) => x >= 36 && y >= 52, move: (f, x) => [0, bob(f) + 3.2 * Math.sin(t(f) - 0.3) * (x - 36) / 40] },
  { test: (x, y) => x >= 50 && y <= 41, move: (f, x, y) => [2 * Math.sin(t(f) - 1.2) * Math.max(0, (41 - y) / 30), bob(f)] },
  { test: (x, y) => x >= 24 && x <= 37 && y >= 16 && y <= 28, move: (f, x, y) => [0.8 * Math.sin(t(f) - 0.6) * Math.max(0, (28 - y) / 12), bob(f)] },
  { test: () => true, move: f => [0, bob(f)] },
], seam: (x, y) => (x >= 22 && x <= 30 && y <= 32) || (x >= 34 && x <= 40 && y >= 48) || (y >= 38 && y <= 44 && x >= 46) || (y >= 14 && y <= 30 && x >= 22 && x <= 40) });
console.log("primalkyogre", JSON.stringify(C.check(fr)));
module.exports = { name: "primalkyogre", sealRotatedHoles: true, pal, rows, fr, fps: 6, angles: [0, 6, 12, 18, 24], pose: 12, pad, layoutHeight: 77,
  protect: (g, x, y) => g[y][x] !== "." && x >= pad + 9 && x <= pad + 20 && y >= pad + 46 && y <= pad + 52 };
