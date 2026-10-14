// Skarmory only: a flight base built from the pieces of the original sprite, following the flight illustration of the
// Phantom Forces card (XY4 59/119, Sanosuke Sakuma): body level and heading left, neck lowered so the head leads, near wing
// raised up and back, far wing swept down and forward under the chest showing its red inner face, tail fan straight back and
// legs folded back under the belly. Each piece is turned RotSprite-style at scale 1; nothing is shrunk or redrawn from nothing.
// This standalone base is not imported by the production sprite or animation pipelines.
const C = require("./critter");
const { pal, rows } = C.load("skarmory");
const at = (x, y) => (rows[y] && rows[y][x]) || ".";
for (let y = 58; y <= 61; y++) for (let x = 19; x <= 24; x++) if (rows[y][x] === "E") rows[y][x] = ".";
rows[31][60] = ".";

// Pieces of the original, by region (66x68 source).
const red = c => "DFJKOP".includes(c);
const isHead = (x, y) => x <= 21 && y >= 13 && y <= 39 && !(x >= 18 && y >= 30);
const isNeck = (x, y) => !isHead(x, y) && x >= 15 && x <= 37 && y >= 26 && y <= 45 && !(x >= 34 && red(at(x, y)));
const isFarBlades = (x, y) => x >= 22 && x <= 40 && y <= 25;
const isTail = (x, y) => x >= 46 && y >= 35 && y <= 48 && !(x <= 50 && y <= 37);
const isNearWing = (x, y) => y <= 38 && !isHead(x, y) && !isNeck(x, y) && !isFarBlades(x, y) && !isTail(x, y) && x >= 33;
const isFrontLeg = (x, y) => y >= 49 && x <= 35;
const isBackLeg = (x, y) => y >= 50 && x >= 38;
const isBody = (x, y) => y >= 38 && y <= 53 && x >= 22 && x <= 50 && !isTail(x, y) && !isNeck(x, y) && !isFrontLeg(x, y) && !isBackLeg(x, y);
// The far wing's silver blades are left out of this pose; where they rose from the neck its top is closed with the dark
// outline the rest of the neck has.
for (let y = 26; y <= 30; y++) for (let x = 22; x <= 37; x++)
  if (isNeck(x, y) && rows[y][x] !== "." && isFarBlades(x, y - 1) && rows[y - 1][x] !== "." && !"CGL".includes(rows[y][x])) rows[y][x] = "G";

function scale2x(g) {
  const h = g.length, w = g[0].length, o = Array.from({ length: h * 2 }, () => Array(w * 2).fill("."));
  const q = (x, y) => (y < 0 || y >= h || x < 0 || x >= w ? "." : g[y][x]);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const P = q(x, y), A = q(x, y - 1), B = q(x + 1, y), Cc = q(x - 1, y), D = q(x, y + 1);
    o[y * 2][x * 2] = Cc === A && Cc !== D && A !== B ? A : P;
    o[y * 2][x * 2 + 1] = A === B && A !== Cc && B !== D ? B : P;
    o[y * 2 + 1][x * 2] = D === Cc && D !== B && Cc !== A ? Cc : P;
    o[y * 2 + 1][x * 2 + 1] = B === D && B !== A && D !== Cc ? D : P;
  }
  return o;
}
// A piece turned `deg` clockwise round `pivot` (source coords) and its pivot moved to `to` (canvas coords).
// `squash` shortens it along the canvas axes round the pivot (a far part seen in depth).
function piece(mask, pivot, deg, to, recolour = {}, squash = [1, 1]) {
  const part = rows.map((r, y) => r.map((c, x) => (mask(x, y) && c !== "." ? recolour[c] || c : ".")));
  const big = scale2x(scale2x(scale2x(part))), S = 8, a = (deg * Math.PI) / 180, co = Math.cos(a), si = Math.sin(a);
  const out = [];
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    const dx = (x + 0.5 - to[0]) / squash[0], dy = (y + 0.5 - to[1]) / squash[1];
    const sx = co * dx + si * dy + pivot[0], sy = -si * dx + co * dy + pivot[1];
    const c = big[Math.floor(sy * S)]?.[Math.floor(sx * S)];
    if (c && c !== ".") out.push([x, y, c]);
  }
  return out;
}

const W = 160, H = 130, O = [50, 40]; // canvas and where the source origin lands
const T = (x, y) => [x + O[0], y + O[1]];
// The far wing is the near one swung down and forward, its inner red face in shade.
const SHADE = { J: "P", F: "J", P: "D", D: "K", O: "O", K: "O", H: "A", M: "H", E: "N", N: "A" };
// Head, neck, body and tail stay one rigid core, tilted forward so the head leads level with the back.
const TILT = 0, CORE = [36, 45];
const core = (x, y) => isHead(x, y) || isNeck(x, y) || isBody(x, y) || isTail(x, y);
// where a source point of the core lands on the canvas
const onCore = ([x, y]) => {
  const a = (TILT * Math.PI) / 180, dx = x - CORE[0], dy = y - CORE[1];
  return T(CORE[0] + Math.cos(a) * dx - Math.sin(a) * dy, CORE[1] + Math.sin(a) * dx + Math.cos(a) * dy);
};
const SHOULDER = [37, 36];
const layers = {
  farWing: piece(isNearWing, SHOULDER, TILT + 170, (([x, y]) => [x - 8, y + 9])(onCore(SHOULDER)), SHADE, [0.85, 0.7]),
  backLeg: piece(isBackLeg, [44, 51], TILT - 45, onCore([45, 50])),
  core: piece(core, CORE, TILT, T(...CORE)),
  frontLeg: piece(isFrontLeg, [31, 50], TILT - 95, onCore([36, 50])),
  nearWing: piece(isNearWing, SHOULDER, TILT - 16, onCore([38, 37])),
};

function build() {
  const g = Array.from({ length: H }, () => Array(W).fill("."));
  for (const id of ["farWing", "backLeg", "core", "frontLeg", "nearWing"])
    for (const [x, y, c] of layers[id]) if (g[y]?.[x] !== undefined) g[y][x] = c;
  // Pieces turned apart leave pinholes where they met (wing root, folded legs): small pockets of background shut in by the
  // drawing take the colour most of their neighbours have. Larger pockets are air between parts and stay.
  const seen = g.map(r => r.map(() => false));
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    if (g[y][x] !== "." || seen[y][x]) continue;
    const pocket = [[x, y]], open = { edge: false };
    seen[y][x] = true;
    for (let i = 0; i < pocket.length; i++) {
      const [px, py] = pocket[i];
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const nx = px + dx, ny = py + dy;
        if (g[ny]?.[nx] === undefined) { open.edge = true; continue; }
        if (g[ny][nx] === "." && !seen[ny][nx]) { seen[ny][nx] = true; pocket.push([nx, ny]); }
      }
    }
    if (open.edge || pocket.length > 4) continue;
    for (const [px, py] of pocket) {
      const n = {};
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, -1], [1, -1], [-1, 1]]) { const c = g[py + dy]?.[px + dx]; if (c && c !== ".") n[c] = (n[c] || 0) + 1; }
      g[py][px] = Object.entries(n).sort((p, q) => q[1] - p[1])[0][0];
    }
  }
  // cropped to the drawing with a two-pixel margin
  const ys = g.map((r, y) => (r.some(c => c !== ".") ? y : -1)).filter(y => y >= 0);
  const xs = g[0].map((_, x) => (g.some(r => r[x] !== ".") ? x : -1)).filter(x => x >= 0);
  const x0 = Math.min(...xs) - 2, y0 = Math.min(...ys) - 2;
  const out = g.slice(y0, Math.max(...ys) + 3).map(r => r.slice(x0, Math.max(...xs) + 3).join(""));
  return { pal, rows: out, layers, origin: [x0, y0] };
}
module.exports = { build, pal, W, H };
if (require.main === module) {
  const b = build();
  console.log(JSON.stringify(C.check([b.rows.map(r => [...r])])));
}
