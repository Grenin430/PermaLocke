// Mega Sharpedo (2026-10-16): the reference is an enlarged sprite with no grid and a watermark (`node recon_plain.js imagenes/megas/mega_sharpedo.png
// megasharpedo 16`, then `node shrink_grid.js megasharpedo megasharpedo_small 2.4`, which drops its noise). A torpedo seen three quarters from the
// front: the body bobs; the tall dorsal fin leans with the beat (its tip most), the long fin to the right strokes up and down from its root, the
// small one at the left a beat later, the lower fin under the jaw sways. Eye and teeth stay.
const C = require("./critter.js");
const { pal, rows } = C.load("megasharpedo_small");
const N = 8, t = f => (f / N) * 2 * Math.PI;
const BOB = [0, 0, 1, 1, 0, 0, -1, -1];
const spec = {
  count: N, pad: 5, ink: "A",
  parts: [
    { test: (x, y) => y < 31, move: (f, x, y) => [Math.round(1.4 * Math.sin(t(f)) * (31 - y) / 31), BOB[f]] },
    { test: (x, y) => x >= 52 && y >= 49 && y <= 59, move: (f, x) => [0, BOB[f] + Math.round(1.6 * Math.sin(t(f) - 0.8) * (x - 52) / 22)] },
    { test: (x, y) => x <= 9 && y >= 53 && y <= 57, move: (f, x) => [0, BOB[f] + Math.round(1.1 * Math.sin(t(f) - 1.6) * (9 - x) / 9)] },
    { test: (x, y) => y >= 71, move: (f, x, y) => [Math.round(1.2 * Math.sin(t(f) - 1) * (y - 71) / 8), BOB[f]] },
    { test: () => true, move: f => [0, BOB[f]] },
  ],
  seam: (x, y) => y >= 25 && y <= 75,
};
const fr = C.frames(rows, spec);
console.log("megasharpedo", JSON.stringify(C.check(fr)));
module.exports = { name: "megasharpedo", pal, fr, fps: 9, angles: [-6, -3, 0, 3, 6], pose: 0,
  protect: (g, x, y) => x >= 5 + 34 && x <= 5 + 47 && y >= 5 + 42 && y <= 5 + 51 && "NJPD".includes(g[y][x]) };
