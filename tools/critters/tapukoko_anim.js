// Shiny Tapu Koko in flight (2026-10-18), from tapukoko_shiny.txt (tapukoko_shiny.js). The two shell halves are its wings here: each turns
// whole round its hinge at the shoulder (RotSprite), opening and closing together; the crest streams and flickers; the tail feathers trail
// and sway after the beat; the body rises a cell on the downstroke. The sheet is turned forward (its top leading to the left) by the angles.
const C = require("./critter");
const { pal, rows } = C.load("tapukoko_shiny");
const N = 10, pad = 14, t = f => (f / N) * 2 * Math.PI;
const beat = f => Math.sin(t(f));
const bob = f => Math.round(-Math.max(0, beat(f)) * 1.5);

const warm = new Set("CQOJ");
const crest = (x, y) => y <= 28 && x >= 24 && x <= 62;
const tail = (x, y) => y >= 60 && x >= 20 && x <= 54 && (warm.has(rows[y][x]) || y >= 70);
const left = (x, y) => !crest(x, y) && !tail(x, y) && x <= 32 && y >= 18;
const right = (x, y) => !crest(x, y) && !tail(x, y) && x >= 50 && y >= 18;
const LEFT_HINGE = [32, 44], RIGHT_HINGE = [50, 42];

const fr = C.frames(rows, { count: N, pad, ink: "Bh", parts: [
  { test: left, sample: (f, put) => C.swing(rows, left, LEFT_HINGE, () => -9 * beat(f))(f, (x, y, c) => put(x, y + bob(f), c)) },
  { test: right, sample: (f, put) => C.swing(rows, right, RIGHT_HINGE, () => 9 * beat(f))(f, (x, y, c) => put(x, y + bob(f), c)) },
  { test: crest, move: (f, x, y) => [Math.sin(t(f) - 0.8 + (y * 0.12)) * 1.6 * Math.max(0, (28 - y) / 28), bob(f) + (Math.sin(t(f) - 1) * 0.6 * Math.max(0, (20 - y) / 20))] },
  { test: tail, move: (f, x, y) => [Math.sin(t(f) - 1.6 - (y * 0.06)) * 2.2 * Math.min(1, (y - 60) / 30), bob(f)] },
  { test: () => true, move: f => [0, bob(f)] },
], seam: (x, y) => (x >= 28 && x <= 36 && y >= 18) || (x >= 46 && x <= 54 && y >= 18) || (y >= 26 && y <= 31) || (y >= 58 && y <= 63) });

console.log("tapukoko", JSON.stringify(C.check(fr)));
module.exports = { name: "tapukoko", pal, rows, fr, fps: 12, angles: [-30, -26, -22], pose: -26, pad, layoutHeight: rows.length,
  // the eye keeps its pixels when the sheet turns
  protect: (g, x, y) => g[y][x] !== "." && x >= pad + 36 && x <= pad + 46 && y >= pad + 30 && y <= pad + 42 };
