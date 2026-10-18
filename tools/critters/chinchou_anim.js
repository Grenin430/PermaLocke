// Chinchou: round and upright, it bobs. The body rises and sinks a pixel, the two long antennae follow it and their lamps swing a pixel
// late, like weights on a line, glowing brighter at the top of each swing; the little side fins paddle. Base rebuilt from the reference;
// the white trapped inside the right antenna's loop (x >= 36, rows 0-27) is background and goes. Eyes and the red spot ride on the body.
const C = require("./critter.js");
const { pal, rows } = C.load("chinchou");
for (let y = 0; y <= 27; y++) for (let x = 36; x < rows[y].length; x++) if (rows[y][x] === "B") rows[y][x] = ".";
const N = 8, t = f => (f / N) * 2 * Math.PI;
const BOB = [0, 0, -1, -1, -1, 0, 0, 0];
const late = (f, k) => BOB[(f + N - k) % N];
const isLampL = (x, y) => x <= 10 && y >= 13;
const isLampR = (x, y) => x >= 44 && y >= 15 && y <= 33;
const isFinL = (x, y) => y >= 24 && y <= 28 && x >= 10 && x <= 17;
const isFinR = (x, y) => y >= 24 && y <= 30 && x >= 38 && x <= 46 && !isLampR(x, y);
const glow = f => late(f, 2) === -1;                         // the lamp brightens as it swings up
// the glow widens the pale core by one ring of its own colour, keeping the shading round it
const lit = (f, at, x, y) => { const c = at(x, y); return glow(f) && c === "L" && [[1, 0], [-1, 0], [0, 1], [0, -1]].some(([a, b]) => at(x + a, y + b) === "P") ? "P" : c; };
const spec = {
  count: N, pad: 4, ink: "JM",
  parts: [
    { test: isLampL, sample: (f, put, at, mine) => { const dx = Math.round(0.9 * Math.sin(t(f) - 1.8)); for (let y = 0; y < 40; y++) for (let x = 0; x < 12; x++) if (mine(x, y)) put(x + dx, y + late(f, 2), lit(f, at, x, y)); } },
    { test: isLampR, sample: (f, put, at, mine) => { const dx = Math.round(0.9 * Math.sin(t(f) - 2.2)); for (let y = 0; y < 40; y++) for (let x = 40; x < 60; x++) if (mine(x, y)) put(x + dx, y + late(f, 2), lit(f, at, x, y)); } },
    { test: isFinL, move: (f, x) => [0, BOB[f] + Math.round(0.9 * Math.sin(t(f) * 2 - 1) * Math.max(0, (17 - x) / 6))] },
    { test: isFinR, move: (f, x) => [0, BOB[f] + Math.round(0.9 * Math.sin(t(f) * 2 - 1.4) * Math.max(0, (x - 38) / 7))] },
    // antennae arcs: their tips (over the lamps) lag a frame behind the body
    { test: (x, y) => y <= 15 && !isLampL(x, y) && !isLampR(x, y), move: (f, x) => [0, x <= 8 || x >= 44 ? late(f, 1) : BOB[f]] },
    { test: () => true, move: f => [0, BOB[f]] },
  ],
  seam: (x, y) => x <= 13 || x >= 40 || y <= 16 || (y >= 22 && y <= 31),
};
const fr = C.frames(rows, spec);
console.log("chinchou", JSON.stringify(C.check(fr)));
module.exports = { name: "chinchou", pal, fr, fps: 8, angles: [-6, -3, 0, 3, 6], pose: 0,
  protect: (g, x, y) => x >= 4 + 19 && x <= 4 + 37 && y >= 4 + 17 && y <= 4 + 24 && "JEH".includes(g[y][x]) };
