// Spearow: a small, fierce flier with fast beats. The reference has its wing raised, so the wing (the pink feathers) bends down from its
// shoulder and back up each beat; the body rises a pixel on the
// downstroke, the orange tail trails and the feet hang a beat late. Eye, beak and crest ride on the head.
const C = require("./critter.js");
const { pal, rows } = C.load("spearow");
const N = 6, t = f => (f / N) * 2 * Math.PI;
const isWing = (x, y, c) => x >= 14 && y >= 9 && y <= 24 && "KJDGN".includes(c);
const isTail = (x, y) => x >= 22 && y >= 19 && y <= 27 && !"KJDGN".includes(rows[y][x] || ".");
const isFeet = (x, y) => y >= 31;
const drop = f => 19 * (1 - Math.cos(t(f)));                // 0 = raised as drawn, 38 = down
const lift = f => Math.round(-1.0 * Math.sin(t(f) - Math.PI / 2));
// the wing bends down column by column from its root on the shoulder (x 14): each column moves whole, so its outline stays, and what it
// uncovers above is sky, not body
const bend = (f, x) => Math.round((drop(f) / 38) * 7 * Math.min(1, Math.max(0, x - 14) / 17));
const spec = {
  count: N, pad: 9, ink: "CFN",
  parts: [
    { test: isTail, move: (f, x) => [0, lift((f + N - 1) % N) + Math.round(0.9 * Math.sin(t(f) - 1.5) * Math.min(1, (x - 22) / 8))] },
    { test: isFeet, move: f => [0, lift((f + N - 1) % N)] },
    { test: (x, y, c) => !isWing(x, y, c), move: f => [0, lift(f)] },
    { test: isWing, move: (f, x) => [0, lift(f) + bend(f, x)] },
  ],
  seam: (x, y) => (x >= 12 && x <= 28 && y >= 8 && y <= 30) || y >= 29,
};
const fr = C.frames(rows, spec);
console.log("spearow", JSON.stringify(C.check(fr)));
module.exports = { name: "spearow", pal, fr, fps: 12, angles: [-8, -4, 0, 4, 8], pose: 0,
  protect: (g, x, y) => x >= 9 + 5 && x <= 9 + 10 && y >= 9 + 8 && y <= 9 + 12 && "BCO".includes(g[y][x]) };
