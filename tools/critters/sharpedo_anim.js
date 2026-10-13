// Sharpedo: a shark. Rebuilt from a small chart (cells of 3.3 px: `node recon2.js .../sharpedo.jpg sharpedo 16 3.3`); one stray white
// cell left of the body (row 29) is JPEG noise and goes. The tail fin waves from its root out, the head and body a frame ahead by a pixel,
// the tall dorsal fin leans with the beat (its tip most), the side fin strokes and the lower jaw snaps shut and open. Eye and teeth stay.
const C = require("./critter.js");
const { pal, rows } = C.load("sharpedo");
rows[29][6] = ".";
// the cells are so small that the chart's grid lines leak into the two body blues (K, L) as a checker the reference does not have: each K/L
// cell takes the majority of K and L around it (3x3), which keeps the broad light and shade and drops the checker
const src = rows.map(r => [...r]);
for (let y = 0; y < src.length; y++) for (let x = 0; x < src[y].length; x++) { if (!"KL".includes(src[y][x])) continue; let k = 0, l = 0; for (let b = -1; b <= 1; b++) for (let a = -1; a <= 1; a++) { const c = src[y + b]?.[x + a]; if (c === "K") k++; else if (c === "L") l++; } rows[y][x] = k >= l ? "K" : "L"; }
const N = 8, t = f => (f / N) * 2 * Math.PI;
const HEAD = [0, -1, -1, 0, 0, 1, 1, 0];
const isTail = (x, y) => x >= 44 && y >= 38;
const isDorsal = (x, y) => y <= 24;
const isPec = (x, y) => x <= 10 && y >= 29 && y <= 36;
const isJaw = (x, y) => y >= 57 && x >= 25;
const JAW = [0, 0, 1, 1, 0, 0, 0, 0];
const spec = {
  count: N, pad: 6, ink: "FN",
  parts: [
    { test: isTail, move: (f, x) => [0, HEAD[(f + N - 1) % N] + 2.0 * Math.sin(t(f) - (x - 44) * 0.25) * Math.min(1, (x - 44) / 16)] },
    { test: isDorsal, move: (f, x, y) => [Math.round(1.2 * Math.sin(t(f) - 1.2) * Math.max(0, (25 - y) / 24)), HEAD[f]] },
    { test: isJaw, move: f => [0, HEAD[f] + JAW[f]] },
    { test: (x, y) => !isPec(x, y), move: f => [0, HEAD[f]] },
    { test: isPec, move: (f, x) => [0, HEAD[f] + Math.round(1.2 * Math.sin(t(f) + 0.6) * Math.max(0, (10 - x) / 10))] },
  ],
  seam: (x, y) => (x >= 40 && y >= 36) || (y >= 22 && y <= 27) || (y >= 55 && y <= 60) || (x <= 13 && y >= 27 && y <= 38),
};
const fr = C.frames(rows, spec);
console.log("sharpedo", JSON.stringify(C.check(fr)));
module.exports = { name: "sharpedo", pal, fr, fps: 9, angles: [-6, -3, 0, 3, 6], pose: 0,
  protect: (g, x, y) => x >= 6 + 21 && x <= 6 + 28 && y >= 6 + 43 && y <= 6 + 47 && "BCJF".includes(g[y][x]) };
