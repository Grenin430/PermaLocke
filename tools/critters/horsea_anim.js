// Horsea: upright; the yellow dorsal fan flutters fast with a ripple from root to tip, the curled tail tightens a little behind it, the head
// and its spines stay. Base rebuilt from the reference; the white trapped between belly and fan (rows 17-23) is background and goes.
const C = require("./critter.js");
const { pal, rows } = C.load("horsea");
for (let y = 17; y <= 23; y++) for (let x = 16; x <= 23; x++) if (rows[y][x] === "B") rows[y][x] = ".";

const N = 8, t = f => (f / N) * 2 * Math.PI;
const yellow = "NIOHMC";
const isFan = (x, y, c) => x >= 19 && y >= 20 && y <= 28 && (yellow.includes(c) || "DL".includes(c));
const isTail = (x, y) => y >= 27 && x <= 13;
const spec = {
  count: N, pad: 4, ink: "ADL",
  parts: [
    // the tail behind: its curl rocks a pixel, late on the beat
    { test: (x, y) => isTail(x, y), move: (f, x, y) => [Math.round(0.9 * Math.sin(t(f) - 1.8) * Math.min(1, (y - 26) / 4)), 0] },
    // the body and head, as they are
    { test: (x, y, c) => !isFan(x, y, c) && !isTail(x, y) },
    // the fan: a ripple runs out from its root, the tip moving most
    { test: isFan, move: (f, x, y) => { const u = Math.max(0, x - 19) / 10; return [0, 1.15 * Math.sin(t(f) * 2 - (x - 19) * 0.3) * Math.min(1, u * 1.4)]; } },
  ],
  seam: (x, y) => (y >= 25 && y <= 29 && x <= 14) || (x >= 18 && x <= 22 && y >= 19 && y <= 29),
};
const fr = C.frames(rows, spec);
console.log("horsea", JSON.stringify(C.check(fr)));
require("fs").writeFileSync("horsea_frames.txt", fr.join("\n\n").replace(/,/g, "\n"));
module.exports = { name: "horsea", pal, fr, fps: 10, angles: [-6, -3, 0, 3, 6], pose: 0,
  // the eye (red, its white shine and its dark pupil) is kept whole when turned
  protect: (g, x, y) => x >= 4 + 14 && x <= 4 + 19 && y >= 4 + 8 && y <= 4 + 14 && g[y][x] !== "." };
