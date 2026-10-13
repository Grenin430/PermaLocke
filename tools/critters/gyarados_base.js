// Gyarados base: the reference is a small chart (cells of 2.77 px, `node recon2.js .../gyarados.jpg gyarados 16 2.77`). Its pale fins are white
// drawn only with a thin grey outline, broken here and there, so the flood from the border took their insides as background. In the boxes
// of those fins, an empty cell closed in on all four sides within a few cells takes the white of the fins (B) back.
const C = require("./critter.js");
const { pal, rows } = C.load("gyarados");
const FINS = [[18, 3, 27, 17], [6, 18, 21, 31], [58, 20, 74, 31]];
const on = (x, y) => rows[y]?.[x] && rows[y][x] !== ".";
const shut = (x, y) => [[1, 0], [-1, 0], [0, 1], [0, -1]].every(([dx, dy]) => { for (let k = 1; k <= 7; k++) if (on(x + dx * k, y + dy * k)) return true; return false; });
const fill = [];
for (const [x0, y0, x1, y1] of FINS) for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) if (!on(x, y) && shut(x, y)) fill.push([x, y]);
for (const [x, y] of fill) rows[y][x] = "B";
module.exports = { pal, rows };
if (require.main === module) { console.log(fill.length, "cells"); rows.forEach((r, y) => console.log(String(y).padStart(2), r.join(""))); }
