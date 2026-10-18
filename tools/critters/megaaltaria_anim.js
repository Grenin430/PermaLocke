// Mega Altaria (2026-10-16): the reference is the sprite enlarged four times (`node recon_plain.js imagenes/megas/mega_altaria.png
// megaaltaria_full 16 4`, 95x96), brought down to Altaria's size (`node shrink_grid.js megaaltaria_full megaaltaria 1.35`). As Altaria: the
// great cloud wings swell and settle by whole columns, softer near the body; the long tail streams wave from the body out, a beat late; the head
// with its cloud crest sways a little. The face stays.
const C = require("./critter");
const { pal, rows } = C.load("megaaltaria");
// the cloud's rim a slate grey, as in the official sprite, so it stands out against a pale sky: every cloud cell touching the outside
const rim = [];
for (let y = 0; y < rows.length; y++) for (let x = 0; x < rows[y].length; x++) {
  if (!"LNOGJAP".includes(rows[y][x])) continue;
  if ([[1, 0], [-1, 0], [0, 1], [0, -1]].some(([dx, dy]) => (rows[y + dy]?.[x + dx] ?? ".") === ".")) rim.push([x, y]);
}
for (const [x, y] of rim) rows[y][x] = "F";
const onRim = new Set(rim.map(([x, y]) => y * 1000 + x));
const N = 8, pad = 7, t = f => (f / N) * 2 * Math.PI;
const CLOUD = "LNOGJAP";
// the cloud rises and falls, more the further a column is from the body (x 26)
const cloud = (f, x) => -Math.round(1.6 * (1 - Math.cos(t(f))) / 2 * Math.min(1, Math.max(0, Math.abs(x - 26) - 5) / 20));
const fr = C.frames(rows, { count: N, pad, ink: "BCI", parts: [
  { test: (x, y) => y >= 52, move: (f, x, y) => [0, Math.round(1.4 * Math.sin(t(f) - 1.4) * Math.min(1, Math.max(0, 50 - x) / 40))] },
  { test: (x, y) => x >= 20 && x <= 34 && y <= 15 && !CLOUD.includes(rows[y][x]), move: f => [Math.round(0.6 * Math.sin(t(f) - 0.6)), 0] },
  { test: (x, y, c) => y <= 47 && (CLOUD.includes(c) || onRim.has(y * 1000 + x)), move: (f, x) => [0, cloud(f, x)] },
  { test: () => true },
], seam: (x, y) => (y <= 47 && (x <= 24 || x >= 34)) || (y >= 46 && y <= 54) });
console.log("megaaltaria", JSON.stringify(C.check(fr)));
module.exports = { name: "megaaltaria", pal, rows, fr, fps: 7, angles: [-2, 0, 2], pose: 0, pad, layoutHeight: 72,
  protect: (g, x, y) => g[y][x] !== "." && x >= pad + 20 && x <= pad + 32 && y >= pad + 8 && y <= pad + 14 };
