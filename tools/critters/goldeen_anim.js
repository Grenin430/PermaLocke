// Goldeen: a flowing swimmer. The long tail (everything behind x=31: the white lobes and the orange base) waves with a wave that runs from its
// root to its tips and grows on the way; head and body follow a frame ahead of the tail by a pixel; the side fin under the chin beats, the
// lower fins trail and the little dorsal ripples. Horn, eye and mouth ride on the head unchanged.
const C = require("./critter.js");
const { pal, rows } = C.load("goldeen");
const N = 8, t = f => (f / N) * 2 * Math.PI;
const HEAD = [0, -1, -1, 0, 0, 1, 1, 0];
const isTail = (x, y) => x >= 31 && y <= 30 && !(y >= 26 && x <= 42);
const isPec = (x, y) => x <= 11 && y >= 27;
const isPelvic = (x, y) => x >= 12 && x <= 29 && y >= 30;
const isAnal = (x, y) => x >= 29 && x <= 42 && y >= 26;
const isDorsal = (x, y) => x >= 16 && x <= 30 && y >= 7 && y <= 14 && !(x <= 20 && y >= 11);
const spec = {
  count: N, pad: 5, ink: "AFH",
  parts: [
    // the tail behind everything: a wave from the root out, riding on the body
    { test: isTail, move: (f, x, y) => { const u = (x - 31) / 32; return [0, HEAD[(f + N - 1) % N] + 2.6 * Math.sin(t(f) - (x - 31) * 0.22) * Math.pow(u, 0.8)]; } },
    { test: isDorsal, move: (f, x, y) => [0, HEAD[f] + Math.round(0.8 * Math.sin(t(f) * 2 - (x - 16) * 0.5) * Math.max(0, (14 - y) / 7))] },
    // the lower fins, late on the beat, their free edges most
    { test: isAnal, move: (f, x, y) => [0, HEAD[(f + N - 1) % N] + 1.2 * Math.sin(t(f) - 1.4 - (x - 29) * 0.3) * Math.max(0, (y - 26) / 11)] },
    { test: isPelvic, move: (f, x, y) => [Math.round(0.9 * Math.sin(t(f) - 2.0) * Math.max(0, (y - 30) / 6)), HEAD[f]] },
    // the body and head
    { test: (x, y) => !isPec(x, y), move: f => [0, HEAD[f]] },
    // the side fin in front: its free end (left) beats by a pixel
    { test: isPec, move: (f, x, y) => [0, HEAD[f] + Math.round(1.1 * Math.sin(t(f) + 0.8) * Math.max(0, (11 - x) / 11))] },
  ],
  seam: (x, y) => (x >= 28 && x <= 34) || (y >= 25 && y <= 31 && x <= 14) || (y >= 26 && x >= 28 && x <= 44) || (y >= 28 && y <= 31) || (y >= 12 && y <= 15),
};
const fr = C.frames(rows, spec);
console.log("goldeen", JSON.stringify(C.check(fr)));
module.exports = { name: "goldeen", pal, fr, fps: 7, angles: [0, 4, 8, 12, 16, 20], pose: 10,
  protect: (g, x, y) => x >= 5 + 15 && x <= 5 + 22 && y >= 5 + 20 && y <= 5 + 27 && "EFI".includes(g[y][x]) };
