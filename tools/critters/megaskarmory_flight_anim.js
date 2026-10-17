// Mega Skarmory's flight (2026-10-16): Skarmory's approved flight (skarmory_approved_flight.js), the same pose and wing beat, recoloured piece
// by piece after imagenes/megas/mega_skarmory.png: head, body and tail gold, the neck a dark indigo collar, the wings dark indigo blades with a
// gold leading edge, the feet indigo, the eye red. Each pixel takes the map of the piece drawn on top of it (compose's layers).
const C = require("./critter"), P = require("./skarmory_flight_pose_v3"), base = require("./skarmory_approved_flight");

const pal = {
  // gold (light to dark) and its brown outline
  M: "FFF6A8", E: "FFFDE8", H: "FFE45A", N: "F2C630", A: "D49A18", L: "9A6410", G: "5E3A0C", C: "1A1206",
  // red eye
  B: "F03838", I: "8E1414",
  // indigo (light to dark) and its outline
  Q: "8A8AD8", R: "6262B4", S: "4A4A96", T: "363678", U: "28285E", V: "1C1C46", W: "12122E",
};
const STEEL_TO_INDIGO = { M: "Q", E: "Q", H: "R", N: "S", A: "T", L: "U", G: "W", C: "C" };
const RED_TO_INDIGO = { F: "Q", J: "R", P: "S", D: "T", K: "U", O: "V" };
const MAP = {
  core: { ...RED_TO_INDIGO },
  neck: { ...STEEL_TO_INDIGO, ...RED_TO_INDIGO },
  wing: { ...RED_TO_INDIGO, G: "W" },
  foot: { ...STEEL_TO_INDIGO, ...RED_TO_INDIGO, E: "N", M: "N" },
};
const [OX, OY] = [50, 40]; // where the pose puts the source origin; the core is not turned, so its source point is canvas - O
// the striped neck between the head and the shoulder (the pose's isNeck, without the far wing's plating above it)
const isNeck = (sx, sy) => !(sx <= 21 && sy >= 13 && sy <= 39 && !(sx >= 18 && sy >= 30)) && sx >= 15 && sx <= 31 && sy >= 26 && sy <= 45
  && !(sx >= 22 && sx <= 37 && sy < 27 + (sx - 21) * 0.75);

// Mega Skarmory's legs hang long under it: an indigo shin and a long thin gold talon each, the near one swept forward and down.
// Frame coordinates of the block's corner; the block bobs with the body.
const LEGS_AT = [30, 46], LEGS = `
..........CTSC....CTSC
..........CTSC....CTSC
.........CTSUC....CTSC
.........CTSC.....CUSTC
........CTSUC.....CUSTC
........CTSC.......CTSC
.......CUTSC.......CTSC
.......CLMMC.......CLMC
......CLMHC........LMHC
......LMHC.........LMC.
.....LMHC..........LMC.
.....LMC...........LMC.
....LMC............LMC.
....LMC............LMC.
...LMC.............LMC.
...LMC.............LMC.
..LMC..............LMC.
..LC...............LC..
.LC................LC..
.L..................L..`.trim().split("\n");
// two indigo blades standing up from the back between the near wing and the tail (they show behind the wing's root)
const BLADES_AT = [46, 26], BLADES = `
.....W.......
....WR.....W.
....WRT...WR.
...WRTT...WRT
...WRTT..WRTT
..WRTTU..WRTT
..WRTTU.WRTTU
.WRTTUU.WRTTU
.WRTTUU.WRTUU
WRTTTUUWRTTUU
WRTTUUUWRTUUU
WWWWWWWWWWWWW`.trim().split("\n");
const EXTRA = 2;
// the crop the approved flight used: its first frame's origin in canvas coordinates
const approved = require("./skarmory_flight_approved.json");
const offset = [approved.origin[0] - base.origin[0], approved.origin[1] - base.origin[1]];
const fr = base.fr.map((rows, f) => {
  const { g, layers } = P.compose({ near: base.nearSweep * base.sweep(f), far: base.farSweep * base.sweep(f) });
  const owner = g.map(r => r.map(() => null));
  for (const id of ["farWing", "backLeg", "core", "frontLeg", "nearWing"]) for (const [x, y] of layers[id]) if (owner[y]?.[x] !== undefined) owner[y][x] = id;
  const kind = (x, y) => {
    const o = owner[y]?.[x];
    if (!o) return null;
    if (o === "core") return isNeck(x - OX, y - OY) ? "neck" : "core";
    return o.endsWith("Wing") ? "wing" : "foot";
  };
  // pinholes compose filled have no owner: they take a neighbour's
  const kindAt = (x, y) => kind(x, y) || [[1, 0], [-1, 0], [0, 1], [0, -1]].map(([dx, dy]) => kind(x + dx, y + dy)).find(k => k) || "core";
  // the approved frame is this canvas moved by the bob and cropped: map each frame pixel back to the canvas
  const dy = base.bob(f);
  const grid = rows.map((r, y) => [...r].map((c, x) => {
    if (c === ".") return c;
    const cx = x + offset[0], cy = y + offset[1] - dy;
    const o = owner[cy]?.[cx];
    // the folded feet of Skarmory's flight go: the Mega's long legs replace them
    if (o === "frontLeg" || o === "backLeg") return ".";
    return MAP[kindAt(cx, cy)][c] || c;
  }));
  const out = [...grid, ...Array.from({ length: EXTRA }, () => Array(grid[0].length).fill("."))];
  // the back blades go behind everything, the legs in front of the far wing and behind nothing else they meet
  const put = (block, [bx, by], under) => block.forEach((r, y) => [...r].forEach((c, x) => {
    const row = out[by + y + dy];
    if (c !== "." && row && row[bx + x] !== undefined && (!under || row[bx + x] === ".")) row[bx + x] = c;
  }));
  put(BLADES, BLADES_AT, true);
  put(LEGS, LEGS_AT, false);
  return out.map(r => r.join(""));
});
console.log("megaskarmory", JSON.stringify({ ...C.check(fr), size: [fr[0][0].length, fr[0].length] }));
module.exports = { ...base, name: "megaskarmory", pal, fr, layoutHeight: 66 };
