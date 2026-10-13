// Lapras: a big, slow paddler. The front flipper (bottom left) strokes down and back, the rear flipper (bottom right) answers half a beat
// later, the head nods a pixel on its long neck a little behind the stroke; the shell and the body stay.
const C = require("./critter.js");
const { pal, rows } = C.load("lapras");
const N = 8, t = f => (f / N) * 2 * Math.PI;
const NOD = [0, 0, 1, 1, 1, 0, 0, 0];
const isFront = (x, y) => y >= 53 && x <= 14;
const isRear = (x, y) => y >= 47 && x >= 50;
const isHead = (x, y) => y <= 15 || (y <= 22 && x >= 20);
const spec = {
  count: N, pad: 5, ink: "EA",
  parts: [
    { test: isRear, move: (f, x, y) => { const u = Math.max(0, x - 50) / 17; return [0, 2.0 * Math.sin(t(f) + Math.PI) * u]; } },
    { test: (x, y) => !isFront(x, y) && !isRear(x, y) && !isHead(x, y) },
    { test: isHead, move: f => [0, NOD[f]] },
    { test: isFront, move: (f, x, y) => { const u = Math.max(0, 14 - x) / 14; return [Math.round(0.8 * Math.cos(t(f)) * u), 2.2 * Math.sin(t(f)) * u]; } },
  ],
  seam: (x, y) => (y >= 13 && y <= 24) || (y >= 50 && (x <= 16 || x >= 48)),
};
const fr = C.frames(rows, spec);
console.log("lapras", JSON.stringify(C.check(fr)));
module.exports = { name: "lapras", pal, fr, fps: 6, angles: [-4, 0, 4, 8], pose: 0,
  protect: (g, x, y) => x >= 5 + 16 && x <= 5 + 22 && y >= 5 + 11 && y <= 5 + 16 && "BGE".includes(g[y][x]) };
