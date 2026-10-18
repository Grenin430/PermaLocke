// Gyarados: a sea serpent, coiled. The whole length waves a pixel at a time, the wave starting at the head (bottom left) and growing along
// the coil to the tail fin (top right), column by column so the body keeps its thickness; the head nods with the neck; the two long
// whiskers trail late. Eye, fangs and the open mouth ride on the head unchanged.
const C = require("./critter.js");
const { pal, rows } = require("./gyarados_base.js");
const N = 8, t = f => (f / N) * 2 * Math.PI;
const HEAD = [0, 0, 1, 1, 1, 0, 0, 0];
const isWhiskerR = (x, y) => x >= 36 && x <= 46 && y >= 55;
const isWhiskerL = (x, y) => x >= 12 && x <= 20 && y >= 64;
// the wave grows from the neck (x 22) to the tail (x 80); each column moves as one, never more than a pixel from its neighbour
const wave = (f, x) => Math.round(1.6 * Math.sin(t(f) - (x - 22) * 0.11) * Math.min(1, Math.max(0, (x - 22) / 40)));
const spec = {
  count: N, pad: 5, ink: "AO",
  parts: [
    { test: isWhiskerR, move: (f, x, y) => [Math.round(1.3 * Math.sin(t(f) - 1.6) * Math.min(1, (y - 55) / 16)), HEAD[(f + N - 1) % N]] },
    { test: isWhiskerL, move: (f, x, y) => [Math.round(1.2 * Math.sin(t(f) - 2.2) * Math.min(1, (y - 64) / 10)), HEAD[(f + N - 1) % N]] },
    { test: () => true, move: (f, x) => [0, wave(f, x) + (x <= 40 ? HEAD[(f + N - 1) % N] : 0)] },
  ],
  seam: (x, y) => (x >= 20 && x <= 44) || (y >= 38 && y <= 44) || x >= 30,
};
const fr = C.frames(rows, spec);
console.log("gyarados", JSON.stringify(C.check(fr)));
module.exports = { name: "gyarados", pal, fr, fps: 7, angles: [-6, -3, 0, 3, 6], pose: 0,
  protect: (g, x, y) => x >= 5 + 19 && x <= 5 + 27 && y >= 5 + 46 && y <= 5 + 55 && "DJGB".includes(g[y][x]) };
