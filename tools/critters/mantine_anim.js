// Mantine: a ray that flies under water. Both wing-fins beat slow and deep round their shoulders, the tips rising together and falling
// together (turned RotSprite-style so the outlines stay clean); the body rises a pixel as they push down; the two curled head fins trail
// late and the long tail ripples behind. Eyes and mouth ride on the body.
const C = require("./critter.js");
const { pal, rows } = C.load("mantine");
const N = 8, t = f => (f / N) * 2 * Math.PI;
const isTail = (x, y) => x >= 50 && y >= 37;
const isWingR = (x, y) => !isTail(x, y) && ((y <= 22 && x >= 38) || (y <= 25 && x >= 46));
const isWingL = (x, y) => y >= 43 && x <= 22;
const isHornA = (x, y) => y >= 15 && y <= 23 && x >= 12 && x <= 23;
const isHornB = (x, y) => y >= 24 && y <= 34 && x <= 12;
const beat = f => 11 * Math.cos(t(f));                    // + : tips up
const lift = f => Math.round(-0.9 * Math.sin(t(f)));
// the upper wing bends rather than turns: its root stays joined to the body, the tip moves across the wing's length (up-left as it rises)
const bendR = (f, x, y) => { const s = Math.max(0, ((x - 40) * 0.55 - (y - 25) * 0.83) / 30); const k = (beat(f) / 11) * 3.6 * Math.pow(Math.min(1, s), 1.3); return [-0.83 * k, -0.55 * k]; };
// the lower wing (almost level) bends column by column: root on the body, the tip rising most; each column moves whole, so its outline stays
const bendL = (f, x) => [0, -Math.round((beat(f) / 11) * 2.8 * Math.min(1, Math.max(0, 22 - x) / 22))];
const spec = {
  count: N, pad: 8, ink: "ANKHM",
  parts: [
    { test: isTail, move: (f, x, y) => [0, lift(f) + 1.5 * Math.sin(t(f) - 1.5 - (x - 50) * 0.25) * Math.min(1, (x - 50) / 14)] },
    // drawn backwards (each cell asks where it comes from), so the bend never opens a crack
    { test: isWingR, sample: (f, put, at, mine) => { for (let y = -8; y < 40; y++) for (let x = 30; x < 80; x++) { let sx = x, sy = y; for (let i = 0; i < 3; i++) { const [dx, dy] = bendR(f, sx, sy); sx = x - dx; sy = y - dy; } const X = Math.round(sx), Y = Math.round(sy); if (mine(X, Y) && at(X, Y) !== '.') put(x, y + lift(f), at(X, Y)); } } },
    { test: isHornA, move: f => [0, lift(f) + Math.round(0.9 * Math.sin(t(f) - 2.0))] },
    { test: isHornB, move: f => [Math.round(0.9 * Math.sin(t(f) - 2.6)), lift(f)] },
    { test: (x, y) => !isTail(x, y) && !isWingR(x, y) && !isWingL(x, y) && !isHornA(x, y) && !isHornB(x, y), move: f => [0, lift(f)] },
    { test: isWingL, sample: (f, put, at, mine) => { for (let y = 30; y < 70; y++) for (let x = -8; x < 30; x++) { let sx = x, sy = y; for (let i = 0; i < 3; i++) { const [dx, dy] = bendL(f, sx, sy); sx = x - dx; sy = y - dy; } const X = Math.round(sx), Y = Math.round(sy); if (mine(X, Y) && at(X, Y) !== '.') put(x, y + lift(f), at(X, Y)); } } },
  ],
  seam: (x, y) => (x >= 34 && x <= 52 && y >= 14 && y <= 40) || (x >= 8 && x <= 28 && y >= 40) || (y >= 14 && y <= 35 && x <= 26) || (x >= 46 && y >= 34),
};
const fr = C.frames(rows, spec);
console.log("mantine", JSON.stringify(C.check(fr)));
module.exports = { name: "mantine", pal, fr, fps: 6, angles: [-6, -3, 0, 3, 6], pose: 0,
  protect: (g, x, y) => ((x >= 8 + 24 && x <= 8 + 29 && y >= 8 + 27 && y <= 8 + 31) || (x >= 8 + 13 && x <= 8 + 20 && y >= 8 + 34 && y <= 8 + 40)) && "ABEON".includes(g[y][x]) };
