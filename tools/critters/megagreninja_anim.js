// The standing Mega Greninja (see megagreninja_clean.js), without the water shuriken on its back: the film draws that one itself, turning all
// the time (CapGreninja). The shuriken is every light-blue cell outside the head, and its edge outside the box of the head and body.
const C = require("./critter");
const { pal, rows } = require("./megagreninja_clean").clean("megagreninja");
const light = new Set("QJLW"), edge = new Set("CRVF");
const body = (x, y) => x >= 31 && x <= 57 && y >= 30;
const head = (x, y) => x >= 33 && x <= 50 && y >= 31;
const bare = rows.map((r, y) => [...r].map((c, x) => ((light.has(c) && !head(x, y)) || (edge.has(c) && !body(x, y)) ? "." : c)).join(""));
const fr = C.frames(bare, { count: 1, pad: 4, parts: [{ test: () => true }] });
// a cell or two left on their own goes
for (const g of fr) {
  const cells = g.map(r => [...r]);
  cells.forEach((r, y) => r.forEach((c, x) => {
    if (c === "." ) return;
    let n = 0;
    for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) if ((dx || dy) && cells[y + dy]?.[x + dx] && cells[y + dy][x + dx] !== ".") n++;
    if (n < 2) cells[y][x] = ".";
  }));
  cells.forEach((r, y) => (g[y] = r.join("")));
}
console.log("megagreninja", JSON.stringify(C.check(fr)));
module.exports = { name: "megagreninja", pal, rows: bare, fr, fps: 1, angles: [0], pose: 0, pad: 4, layoutHeight: rows.length };
