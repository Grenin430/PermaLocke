// Machamp for the second trial's crystal (2026-10-18, prototype), from machamp.txt (the Gen 3 chart the player sent, rebuilt with recon2.js),
// turned to face right; the white the chart left between its arms and its waist is see-through. Frames picked by the film: 0 standing,
// 1 crouched (the top a cell lower, as it lands or chews). Tilted for its tumble out of the Ultra Wormhole.
const C = require("./critter");
const { pal, rows: chart } = C.load("machamp");
const rows = chart.map(r => [...r].reverse().map(c => (c === "B" ? "." : c)));
const fr = C.frames(rows, { count: 2, pad: 4, ink: "AET", parts: [
  { test: () => true, move: (f, x, y) => [0, f === 1 && y < 46 ? 1 : 0] },
] });

console.log("machamp", JSON.stringify(C.check(fr)));
module.exports = { name: "machamp", pal, rows, fr, fps: 1, angles: [-40, -20, 0, 20, 40], pose: 0, pad: 4, layoutHeight: rows.length };
