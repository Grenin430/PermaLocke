// Zubat: fast, deep wingbeats; each wing swings round its own shoulder (the left one out and down, the right one out and down together),
// the body jerks up on the downstroke, and the two long tail streamers trail on a wave a little behind. No eyes; the open mouth rides on
// the body and its teeth are kept when turned.
const C = require("./critter.js");
const { pal, rows } = C.load("zubat");
const N = 6, t = f => (f / N) * 2 * Math.PI;
const isTails = (x, y) => x >= 30 && y >= 23;
const isLeft = (x, y) => y <= 20 && x <= 22 && !isTails(x, y);
const isRight = (x, y) => y <= 24 && x >= 22 && !isTails(x, y) && !(y >= 21 && x <= 27);
const beat = f => 24 * Math.cos(t(f));                      // + : wings up
const lift = f => Math.round(-1.1 * Math.sin(t(f)));
const left = C.swing(rows, isLeft, [19.5, 20.5], f => beat(f) * 0.9);
const right = C.swing(rows, isRight, [25.5, 23.5], f => -beat(f));
const spec = {
  count: N, pad: 9, ink: "AEMG",
  parts: [
    { test: isRight, sample: (f, put) => right(f, (x, y, c) => put(x, y + lift(f), c)) },
    { test: isLeft, sample: (f, put) => left(f, (x, y, c) => put(x, y + lift(f), c)) },
    { test: isTails, move: (f, x, y) => [0, lift(f) + 1.6 * Math.sin(t(f) - 1.6 - (x - 30) * 0.28) * Math.min(1, (x - 30) / 14)] },
    { test: (x, y) => !isLeft(x, y) && !isRight(x, y) && !isTails(x, y), move: f => [0, lift(f)] },
  ],
  seam: (x, y) => (x >= 15 && x <= 31 && y >= 16 && y <= 28) || (x >= 28 && x <= 36 && y >= 22),
};
const fr = C.frames(rows, spec);
console.log("zubat", JSON.stringify(C.check(fr)));
module.exports = { name: "zubat", pal, fr, fps: 12, angles: [-8, -4, 0, 4, 8], pose: 0,
  protect: (g, x, y) => x >= 9 + 5 && x <= 9 + 17 && y >= 9 + 27 && y <= 9 + 35 && "BL".includes(g[y][x]) };
