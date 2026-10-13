// Qwilfish: a round puffer with a paddle tail. The tail bends from its root and beats (drawn backwards so the bend never cracks), the body
// bobs a pixel against it. Eye and red lips ride on the body.
const C = require("./critter.js");
const { pal, rows } = C.load("qwilfish");
const N = 8, t = f => (f / N) * 2 * Math.PI;
const BOB = [0, 0, -1, -1, -1, 0, 0, 0];
const isTail = (x, y) => y >= 26 && x >= 23;
// the tail runs down and right from its root (25, 26); its tip moves across it, up-right then down-left
const bend = (f, x, y) => { const s = Math.max(0, ((x - 25) * 0.55 + (y - 26) * 0.83) / 19); const k = 2.6 * Math.sin(t(f)) * Math.min(1, s); return [0.83 * k, -0.55 * k]; };
const spec = {
  count: N, pad: 6, ink: "GA",
  parts: [
    { test: isTail, sample: (f, put, at, mine) => { const b = BOB[(f + N - 1) % N]; for (let y = 20; y < 52; y++) for (let x = 16; x < 46; x++) { let sx = x, sy = y; for (let i = 0; i < 3; i++) { const [dx, dy] = bend(f, sx, sy); sx = x - dx; sy = y - dy; } const X = Math.round(sx), Y = Math.round(sy); if (mine(X, Y) && at(X, Y) !== ".") put(x, y + b, at(X, Y)); } } },
    { test: () => true, move: f => [0, BOB[f]] },
  ],
  seam: (x, y) => (y >= 22 && x >= 20) || false,
};
const fr = C.frames(rows, spec);
console.log("qwilfish", JSON.stringify(C.check(fr)));
module.exports = { name: "qwilfish", pal, fr, fps: 8, angles: [-6, -3, 0, 3, 6], pose: 0,
  protect: (g, x, y) => x >= 6 + 15 && x <= 6 + 21 && y >= 6 + 8 && y <= 6 + 12 && "BGJ".includes(g[y][x]) };
