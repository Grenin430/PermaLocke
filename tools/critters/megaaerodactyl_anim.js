// Mega Aerodactyl (2026-10-16): the reference is the sprite enlarged four times (`node recon_plain.js imagenes/megas/mega_aerodactyl.png
// megaaerodactyl 16 4`). As Aerodactyl, with a fuller beat (2026-10-16): the two membranes swing up and down from their shoulders; the long tail curled over the top sways, the feet and the
// spike under the jaw lag behind. Eye and open mouth stay.
const C = require("./critter");
const { pal, rows } = C.load("megaaerodactyl");
const N = 8, pad = 8, t = f => (f / N) * 2 * Math.PI, down = f => (1 - Math.cos(t(f))) / 2;
const fr = C.frames(rows, { count: N, pad, ink: "AJNOP", parts: [
  { test: (x, y) => y <= 22 && x >= 18 && x <= 48, move: (f, x, y) => [Math.round(1.3 * Math.sin(t(f) - 1.2) * Math.max(0, (22 - y) / 22)), 0] },
  { test: (x, y) => x <= 22 && y <= 31, move: (f, x) => [0, Math.round(4.5 * Math.sin(t(f) - 0.3) * Math.max(0, (24 - x) / 24))] },
  { test: (x, y) => x >= 48 && y <= 31, move: (f, x) => [0, Math.round(5.5 * Math.sin(t(f) - 0.3) * Math.min(1, (x - 48) / 30))] },
  { test: (x, y) => x >= 44 && y >= 44, move: (f, x, y) => [Math.round(1.2 * Math.sin(t(f) - 1.5) * Math.min(1, (y - 44) / 12)), 0] },
  { test: (x, y) => y >= 58, move: (f, x, y) => [Math.round(Math.sin(t(f) - 0.7) * Math.min(1, (y - 58) / 10)), 0] },
  { test: () => true },
], seam: (x, y) => (x >= 20 && x <= 26 && y >= 25 && y <= 35) || (x >= 46 && x <= 52 && y >= 18 && y <= 36) || (y >= 42 && y <= 48 && x >= 40) || (y >= 56 && y <= 60) || (y >= 20 && y <= 24) });
console.log("megaaerodactyl", JSON.stringify(C.check(fr)));
module.exports = { name: "megaaerodactyl", pal, rows, fr, fps: 9, angles: [-3, 0, 3], pose: 0, pad, layoutHeight: 70,
  protect: (g, x, y) => g[y][x] !== "." && ((x >= pad + 24 && x <= pad + 30 && y >= pad + 35 && y <= pad + 40) || (x >= pad + 22 && x <= pad + 35 && y >= pad + 40 && y <= pad + 57)) };
