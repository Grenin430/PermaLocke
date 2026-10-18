// Pidgey: a small bird with quick wingbeats. The reference is perched with the wing folded, so the wing (the brown feathers on its side)
// swings up round its shoulder and back down to the folded pose each beat, turned RotSprite-style; the body rises a pixel on the
// downstroke, the tail fans a pixel late and the feet trail. Under the wing its side is filled from the body round it. Eye, beak and the markings ride on the head.
const C = require("./critter.js");
const { pal, rows } = C.load("pidgey");
const N = 6, t = f => (f / N) * 2 * Math.PI;
const isWing = (x, y, c) => y >= 14 && y <= 27 && x >= 13 && x <= 25 && "KLOJP".includes(c);
const isTail = (x, y) => x >= 26 && y >= 18 && y <= 27;
const isFeet = (x, y) => y >= 34;
const raise = f => 24 * (1 + Math.cos(t(f)));               // 0 = folded as drawn, 48 = raised over the back
const lift = f => Math.round(-1.0 * Math.sin(t(f)));
const wing = C.swing(rows, isWing, [15.5, 15.5], f => -raise(f));
// under the wing: its side, filled row by row with the nearest body colour to its left (past the outline), so a raised wing shows no hole
const side = rows.map((r, y) => r.map((c, x) => { if (!isWing(x, y, c)) return c; for (let k = x - 1; k >= 0; k--) if (!isWing(k, y, r[k]) && !"DJP.".includes(r[k])) return r[k]; return "K"; }));
const spec = {
  count: N, pad: 8, ink: "DJP",
  parts: [
    { test: isTail, move: (f, x) => [0, lift((f + N - 1) % N) + Math.round(0.9 * Math.sin(t(f) - 1.5) * Math.min(1, (x - 26) / 8))] },
    { test: isFeet, move: f => [0, lift((f + N - 1) % N)] },
    { test: (x, y, c) => !isWing(x, y, c), move: f => [0, lift(f)] },
    { test: isWing, sample: (f, put, at, mine) => { for (let y = 0; y < rows.length; y++) for (let x = 0; x < rows[y].length; x++) if (mine(x, y)) put(x, y + lift(f), side[y][x]); } },
    { test: isWing, sample: (f, put) => wing(f, (x, y, c) => put(x, y + lift(f), c)) },
  ],
  seam: (x, y) => (x >= 11 && x <= 28 && y >= 12 && y <= 30) || y >= 30 || y <= 6,
};
const fr = C.frames(rows, spec);
console.log("pidgey", JSON.stringify(C.check(fr)));
module.exports = { name: "pidgey", pal, fr, fps: 12, angles: [-8, -4, 0, 4, 8], pose: 0,
  protect: (g, x, y) => x >= 8 + 8 && x <= 8 + 15 && y >= 8 + 8 && y <= 8 + 12 && "DBJ".includes(g[y][x]) };
