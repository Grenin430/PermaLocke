// Swablu: a little bird on cotton-cloud wings. Both clouds beat round their shoulders together (tips up, then down), turned RotSprite-style
// so their soft outlines stay whole; the body rises a pixel on the downstroke, the two crest feathers trail and the tail feathers hang a
// beat late. Eye and beak ride on the body.
const C = require("./critter.js");
const { pal, rows } = C.load("swablu");
const N = 8, t = f => (f / N) * 2 * Math.PI;
const isLeft = (x, y) => x <= 24 && y >= 3 && y <= 26;
const isRight = (x, y) => x >= 41 && y >= 9 && y <= 31 && !(x <= 44 && y >= 27);
const isCrest = (x, y) => y <= 11 && x >= 25;
const isTail = (x, y) => y >= 29 && x >= 38 && x <= 45;
const beat = f => 15 * Math.cos(t(f));                     // + : tips up
const lift = f => Math.round(-1.0 * Math.sin(t(f)));
const left = C.swing(rows, isLeft, [24.5, 19.5], f => beat(f));
const right = C.swing(rows, isRight, [41.5, 21.5], f => -beat(f));
const spec = {
  count: N, pad: 9, ink: "BIE",
  parts: [
    { test: isRight, sample: (f, put) => right(f, (x, y, c) => put(x, y + lift(f), c)) },
    { test: isCrest, move: (f, x, y) => [Math.round(0.9 * Math.sin(t(f) - 1.6) * Math.max(0, (11 - y) / 11)), lift((f + N - 1) % N)] },
    { test: isTail, move: f => [0, lift((f + N - 1) % N)] },
    { test: (x, y) => !isLeft(x, y) && !isRight(x, y) && !isCrest(x, y) && !isTail(x, y), move: f => [0, lift(f)] },
    { test: isLeft, sample: (f, put) => left(f, (x, y, c) => put(x, y + lift(f), c)) },
  ],
  seam: (x, y) => (x >= 20 && x <= 46 && y >= 8 && y <= 32),
};
const fr = C.frames(rows, spec);
console.log("swablu", JSON.stringify(C.check(fr)));
module.exports = { name: "swablu", pal, fr, fps: 8, angles: [-6, -3, 0, 3, 6], pose: 0,
  protect: (g, x, y) => x >= 9 + 25 && x <= 9 + 33 && y >= 9 + 16 && y <= 9 + 22 && "BCPH".includes(g[y][x]) };
