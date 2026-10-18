// Wingull: long straight wings beat round their shoulders (both tips up together, then down), turned RotSprite-style each frame so their
// outline stays clean; the body rises a pixel on the downstroke and sinks on the way up; head, beak and eye ride on the body.
const C = require("./critter.js");
const { pal, rows } = C.load("wingull");
const N = 8, t = f => (f / N) * 2 * Math.PI;
const isLeft = (x, y) => (y <= 15 && x <= 23) || (y <= 17 && x <= 19);
const isRight = (x, y) => (y >= 25 && x >= 32) || (y >= 23 && x >= 37);
const beat = f => 17 * Math.cos(t(f));                     // + raises the tips
const lift = f => Math.round(-0.9 * Math.sin(t(f)));        // the body answers the downstroke
const left = C.swing(rows, isLeft, [22.5, 16.5], f => beat(f), f => 1 - 0.06 * Math.abs(Math.sin(t(f))));
const right = C.swing(rows, isRight, [34.5, 24.5], f => -beat(f), f => 1 - 0.1 * Math.abs(Math.sin(t(f))));
const spec = {
  count: N, pad: 8, ink: "AEKN",
  parts: [
    { test: isLeft, sample: (f, put) => left(f, (x, y, c) => put(x, y + lift(f), c)) },
    { test: isRight, sample: (f, put) => right(f, (x, y, c) => put(x, y + lift(f), c)) },
    { test: (x, y) => !isLeft(x, y) && !isRight(x, y), move: f => [0, lift(f)] },
  ],
  seam: (x, y) => (x >= 17 && x <= 26 && y >= 13 && y <= 20) || (x >= 30 && x <= 40 && y >= 20 && y <= 28),
};
const fr = C.frames(rows, spec);
console.log("wingull", JSON.stringify(C.check(fr)));
module.exports = { name: "wingull", pal, fr, fps: 9, angles: [-8, -4, 0, 4, 8], pose: 0,
  protect: (g, x, y) => x >= 8 + 33 && x <= 8 + 44 && y >= 8 + 11 && y <= 8 + 18 && "EKN".includes(g[y][x]) };
