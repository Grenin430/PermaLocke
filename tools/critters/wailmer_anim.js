// Wailmer: a round, slow whale. The body bobs a pixel; the small front flipper (left) strokes, the big flipper on the right answers half a
// beat later with a wave running out to its fingers, each column moving whole so the outlines keep. The two little eyes ride on the body.
const C = require("./critter.js");
const { pal, rows } = C.load("wailmer");
const N = 8, t = f => (f / N) * 2 * Math.PI;
const BOB = [0, 0, -1, -1, -1, 0, 0, 0];
const isLeft = (x, y) => x <= 13 && y >= 12 && y <= 25;
const isRight = (x, y) => x >= 46 && y >= 20;
const spec = {
  count: N, pad: 6, ink: "DK",
  parts: [
    { test: isRight, move: (f, x) => { const u = Math.min(1, (x - 46) / 25); return [0, BOB[(f + N - 1) % N] + Math.round(2.0 * Math.sin(t(f) + Math.PI - (x - 46) * 0.12) * u)]; } },
    { test: (x, y) => !isLeft(x, y) && !isRight(x, y), move: f => [0, BOB[f]] },
    { test: isLeft, move: (f, x) => [0, BOB[f] + Math.round(1.6 * Math.sin(t(f)) * Math.max(0, (13 - x) / 13))] },
  ],
  seam: (x, y) => (x >= 42 && y >= 18) || (x <= 16 && y >= 10 && y <= 27),
};
const fr = C.frames(rows, spec);
console.log("wailmer", JSON.stringify(C.check(fr)));
module.exports = { name: "wailmer", pal, fr, fps: 6, angles: [-4, 0, 4], pose: 0,
  protect: (g, x, y) => x >= 6 + 18 && x <= 6 + 38 && y >= 6 + 7 && y <= 6 + 12 && g[y][x] === "D" };
