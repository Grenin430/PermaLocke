// Dragonite only: flight base v4, after v3 (an exact quarter turn of the standing torso) read wrong. Following the Black Star
// Promo 5 card, flying level to the left, the torso, neck, tail, arm and legs are built anew as shapes in the original palette
// and shaded by volume (outline, lit top, shaded underside); the head, the claws of the hand and feet and the wings are pieces of
// the original sprite at scale 1. This standalone base is not imported by the production pipelines.
const C = require("./critter");
const { pal, rows } = C.load("dragonite");
const W = 120, H = 70;

function inside(x, y, p) {
  let hit = false;
  for (let i = 0, j = p.length - 1; i < p.length; j = i++)
    if ((p[i][1] > y) !== (p[j][1] > y) && x < ((p[j][0] - p[i][0]) * (y - p[i][1])) / (p[j][1] - p[i][1]) + p[i][0]) hit = !hit;
  return hit;
}
const ellipse = (cx, cy, rx, ry) => (x, y) => ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1;
const poly = p => (x, y) => inside(x + 0.5, y + 0.5, p);
const union = (...f) => (x, y) => f.some(g => g(x, y));
// a limb: a fat segment from a to b of radius r
const limb = (a, b, r) => (x, y) => {
  const dx = b[0] - a[0], dy = b[1] - a[1], u = Math.max(0, Math.min(1, ((x + 0.5 - a[0]) * dx + (y + 0.5 - a[1]) * dy) / (dx * dx + dy * dy)));
  return Math.hypot(x + 0.5 - a[0] - u * dx, y + 0.5 - a[1] - u * dy) <= r;
};

// A shape painted by volume: dark outline on its edge, lit band along its top, shaded band along its underside.
// tones: [outline, lit, base, shade]
const ORANGE = ["H", "M", "C", "N"], ORANGE_FAR = ["H", "C", "N", "P"];
function shaded(mask, [edge, lit, base, shade], { litRows = 2, shadeRows = 3 } = {}) {
  const out = [];
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    if (!mask(x, y)) continue;
    const near = [[1, 0], [-1, 0], [0, 1], [0, -1]].some(([dx, dy]) => !mask(x + dx, y + dy));
    let c = base;
    if (near) c = edge;
    else if (!mask(x, y - litRows)) c = lit;
    else if (!mask(x, y + shadeRows)) c = shade;
    out.push([x, y, c]);
  }
  return out;
}
// A piece of the original turned `deg` clockwise round `pivot` and landed at `to`, RotSprite-style; `flip` mirrors it first.
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
function piece(mask, pivot, deg, to, { recolour = {}, flip = false } = {}) {
  const part = rows.map((r, y) => r.map((c, x) => (mask(x, y) && c !== "." ? recolour[c] || c : ".")));
  const big = scale2x(scale2x(scale2x(part))), S = 8, a = (deg * Math.PI) / 180, co = Math.cos(a), si = Math.sin(a), out = [];
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    const dx = x + 0.5 - to[0], dy = y + 0.5 - to[1], ux = co * dx + si * dy;
    const c = big[Math.floor((-si * dx + co * dy + pivot[1]) * S)]?.[Math.floor(((flip ? -ux : ux) + pivot[0]) * S)];
    if (c && c !== ".") out.push([x, y, c]);
  }
  return out;
}

// Source regions of the original (64x80, standing, facing the viewer's left).
// the head down to the throat; the neck below is built, so the original chest and the near wing's edge stay out
const isHead = (x, y) => (y <= 27 && x >= 5 && x <= 31 && !(x <= 13 && y >= 14 && y <= 23)) || (y <= 10 && x >= 30 && x <= 34);
const isNearWingSrc = (x, y) => x >= 34 && y >= 12 && y <= 31 && !(x >= 48 && y >= 23);
const isHandSrc = (x, y) => x <= 9 && y >= 39 && y <= 48;          // left hand with its white claws, pointing left
const isFootSrc = (x, y) => x <= 17 && y >= 68;                     // left foot, toes and claws pointing left

// Layout on the canvas (facing left).
const TORSO = ellipse(63, 41, 23, 13);
const NECK = poly([[33, 27], [40, 25], [47, 29], [50, 37], [45, 44], [38, 42], [33, 36]]);
const TAIL = poly([[80, 31], [92, 31], [104, 28], [113, 23], [117, 21], [116, 26], [109, 33], [99, 40], [89, 47], [80, 51]]);
const ARM = union(limb([47, 42], [41, 50], 3.4), limb([41, 50], [36, 54], 2.8));
const NEAR_LEG = union(ellipse(79, 47, 7, 6), limb([80, 50], [90, 58], 3.2));
const FAR_LEG = union(ellipse(84, 44, 6, 5), limb([85, 46], [95, 53], 2.8));
// cream underside: the belly along the torso's underside, running on under the tail
const BELLY = union((x, y) => TORSO(x, y) && ((x + 0.5 - 62) / 24) ** 2 + ((y + 0.5 - 51) / 10) ** 2 <= 1,
  (x, y) => TAIL(x, y) && !TAIL(x + 1, y + 2) && x < 104);

function build() {
  const g = Array.from({ length: H }, () => Array(W).fill("."));
  const draw = cells => { for (const [x, y, c] of cells) if (g[y]?.[x] !== undefined) g[y][x] = c; };
  // far side first
  draw(piece(isNearWingSrc, [36, 27], -52, [58, 28], { recolour: { D: "P", G: "F", O: "A", E: "M", L: "I" } }));
  draw(shaded(FAR_LEG, ORANGE_FAR));
  draw(piece(isFootSrc, [12, 71], 20, [96, 54], { flip: true, recolour: { C: "N", M: "C", N: "P", B: "K" } }));
  draw(shaded(TAIL, ORANGE, { shadeRows: 2 }));
  draw(shaded(union(TORSO, NECK), ORANGE));
  // belly in creams with the segment lines running across it
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    if (!BELLY(x, y) || g[y][x] === "H") continue;
    const edge = [[1, 0], [-1, 0], [0, 1], [0, -1]].some(([dx, dy]) => !BELLY(x + dx, y + dy));
    g[y][x] = edge && TORSO(x, y - 1) && !BELLY(x, y - 1) ? "I" : x % 5 === 0 ? "I" : y >= 49 ? "E" : "L";
  }
  draw(piece(isHead, [24, 28], 0, [38, 33]));
  draw(shaded(NEAR_LEG, ORANGE));
  draw(piece(isFootSrc, [12, 71], 15, [91, 59], { flip: true }));
  draw(shaded(ARM, ORANGE));
  draw(piece(isHandSrc, [8, 43], -35, [37, 55]));
  draw(piece(isNearWingSrc, [36, 27], -20, [63, 30]));
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
  const ys = g.map((r, y) => (r.some(c => c !== ".") ? y : -1)).filter(y => y >= 0);
  const xs = g[0].map((_, x) => (g.some(r => r[x] !== ".") ? x : -1)).filter(x => x >= 0);
  const x0 = Math.min(...xs) - 2, y0 = Math.min(...ys) - 2;
  return { pal, rows: g.slice(y0, Math.max(...ys) + 3).map(r => r.slice(x0, Math.max(...xs) + 3).join("")), origin: [x0, y0] };
}
module.exports = { build, pal, W, H };
if (require.main === module) console.log(JSON.stringify(C.check([build().rows])));
