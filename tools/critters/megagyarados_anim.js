// Mega Gyarados (2026-10-16): as Gyarados, a sea serpent. The body waves a pixel at a time from the neck (x 40) to the tail fin (right), column
// by column so it keeps its thickness; the head with its crest and open mouth nods; the two long whiskers, curling down to the bottom, trail late.
const C = require("./critter.js");
const { pal, rows } = require("./megagyarados_base.js");
const N = 8, t = f => (f / N) * 2 * Math.PI;
const HEAD = [0, 0, 1, 1, 1, 0, 0, 0];
const isWhiskerL = (x, y, c) => x <= 15 && y >= 62 && "JKB".includes(c);
const isWhiskerR = (x, y, c) => x >= 37 && x <= 72 && y >= 63 && "JKB".includes(c);
const wave = (f, x) => Math.round(1.6 * Math.sin(t(f) - (x - 40) * 0.11) * Math.min(1, Math.max(0, (x - 40) / 35)));
const spec = {
  count: N, pad: 5, ink: "D",
  parts: [
    { test: isWhiskerL, move: (f, x, y) => [Math.round(1.2 * Math.sin(t(f) - 2.2) * Math.min(1, (y - 62) / 14)), HEAD[(f + N - 1) % N]] },
    { test: isWhiskerR, move: (f, x, y) => [Math.round(1.3 * Math.sin(t(f) - 1.6) * Math.min(1, (y - 63) / 16)), HEAD[(f + N - 1) % N]] },
    { test: () => true, move: (f, x) => [0, wave(f, x) + (x <= 42 ? HEAD[(f + N - 1) % N] : 0)] },
  ],
  seam: (x, y) => (x >= 36 && x <= 60) || x >= 40,
};
const fr = C.frames(rows, spec);
console.log("megagyarados", JSON.stringify(C.check(fr)));
module.exports = { name: "megagyarados", pal, fr, fps: 7, angles: [-6, -3, 0, 3, 6], pose: 0,
  // the eyes and the inside of the mouth keep their pixels when the sheet is tilted
  protect: (g, x, y) => x >= 5 + 14 && x <= 5 + 32 && y >= 5 + 54 && y <= 5 + 82 && "CQRIB".includes(g[y][x]) };
