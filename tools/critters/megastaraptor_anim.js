// Mega Staraptor (2026-10-16): the reference sprite itself (imagenes/megas/mega_staraptor.png is 1:1;
// `node recon_plain.js ... megastaraptor 16 1`, then the white around it flooded from the border, the white chest kept: megastaraptor.txt),
// animated: both raised wings beat down and back up, each turned whole round its shoulder (RotSprite), the near one further; the tail
// fan sways after them, the body rises on the downstroke. Head, crest, chest and feet are the sprite's own.
const C = require("./critter");
// shrunk to fit the sky (node shrink_grid.js megastaraptor megastaraptor_small 1.35); the masks below are in the full sprite's cells
const K = 1.35, { pal, rows } = C.load("megastaraptor_small");
const N = 8, PAD = 24, t = f => (f / N) * 2 * Math.PI;
// 0 with the wings raised (the sprite), 1 at the bottom of the stroke
const down = f => (1 - Math.cos(t(f))) / 2;
const bob = f => -Math.round(2 * down(f));

const farWing = (x, y) => (x *= K, y *= K, x <= 18 && y <= 50);
const nearWing = (x, y) => (x *= K, y *= K, (y <= 58 && x >= 44) || (y > 58 && y <= 71 && x >= 37));
const tail = (x, y) => (x *= K, y *= K, y >= 86 && x >= 40);
const [FAR_PIVOT, NEAR_PIVOT, TAIL_PIVOT] = [[14, 47], [45, 66], [42, 90]].map(p => p.map(v => v / K));

const fr0 = C.frames(rows, { count: N, pad: PAD, ink: "BIP", parts: [
  { test: farWing, sample: (f, put) => C.swing(rows, farWing, FAR_PIVOT, () => -34 * down(f))(f, (x, y, c) => put(x, y + bob(f), c)) },
  { test: tail, sample: (f, put) => C.swing(rows, tail, TAIL_PIVOT, () => 5 * Math.sin(t(f) - 1.4))(f, (x, y, c) => put(x, y + bob(f), c)) },
  { test: (x, y) => !nearWing(x, y), move: f => [0, bob(f)] },
  { test: nearWing, sample: (f, put) => C.swing(rows, nearWing, NEAR_PIVOT, () => 46 * down(f))(f, (x, y, c) => put(x, y + bob(f), c)) },
], seam: (x, y) => (x *= K, y *= K, (x >= 10 && x <= 20 && y >= 40 && y <= 52) || (x >= 34 && x <= 48 && y >= 56 && y <= 72) || (x >= 38 && x <= 46 && y >= 84 && y <= 94)) });

// one box for every frame, two cells of margin
let x0 = 1e9, x1 = -1, y0 = 1e9, y1 = -1;
fr0.forEach(g => g.forEach((r, y) => [...r].forEach((c, x) => { if (c !== ".") { x0 = Math.min(x0, x); x1 = Math.max(x1, x); y0 = Math.min(y0, y); y1 = Math.max(y1, y); } })));
x0 -= 2; y0 -= 2; x1 += 2; y1 += 2;
const fr = fr0.map(g => g.slice(y0, y1 + 1).map(r => r.slice(x0, x1 + 1)));
const ox = PAD - x0, oy = PAD - y0;
console.log("megastaraptor", JSON.stringify({ ...C.check(fr), size: [fr[0][0].length, fr[0].length] }));
module.exports = { name: "megastaraptor", pal, rows, fr, fps: 9, angles: [-2, 0, 2], pose: 0, pad: 0, layoutHeight: rows.length,
  // the eye and the white of the face keep their pixels when the sheet tilts
  protect: (g, x, y) => x >= ox + 12 && x <= ox + 22 && y >= oy + 40 && y <= oy + 47 && g[y][x] !== "." };
