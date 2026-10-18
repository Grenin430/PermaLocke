// Mega Greninja mid-flip (see megagreninja_clean.js): one frame, turned through twelve angles so the film can spin it.
const C = require("./critter");
const { pal, rows } = require("./megagreninja_clean").clean("megagreninjasalto");
const fr = C.frames(rows, { count: 1, pad: 4, parts: [{ test: () => true }] });
console.log("megagreninjasalto", JSON.stringify(C.check(fr)));
module.exports = { name: "megagreninjasalto", pal, rows, fr, fps: 1, angles: [0, 30, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330], pose: 0, pad: 4, layoutHeight: rows.length };
