// Shaymin Land Forme (see shaymin_anim.js), sitting: six frames of breathing, the grass on its back swaying (more at its top), the body
// swelling a cell as it breathes in. Tilted for its tumble out of the wormhole.
const { sheet, C } = require("./shaymin_anim");
const { pal, rows } = C.load("shaymintierra");
const N = 6, t = f => (f / N) * 2 * Math.PI;
const grass = (x, y) => y <= 13 && "AJMQRF".includes(rows[y][x]);
const fr = C.frames(rows, { count: N, pad: 6, parts: [
  { test: grass, move: (f, x, y) => [Math.round(Math.sin(t(f) + (x * 0.15)) * 1.2 * ((14 - y) / 14)), Math.sin(t(f)) > 0.3 && y < 8 ? -1 : 0] },
  { test: () => true, move: (f, x, y) => [0, y < 20 && Math.sin(t(f)) > 0.3 ? -1 : 0] },
], seam: (x, y) => y >= 10 && y <= 22 });
console.log("shaymintierra", JSON.stringify(C.check(fr)));
module.exports = { name: "shaymintierra", pal, rows, fr, fps: 6, angles: [-60, -40, -20, 0, 20, 40, 60], pose: 0, pad: 6, layoutHeight: rows.length };
