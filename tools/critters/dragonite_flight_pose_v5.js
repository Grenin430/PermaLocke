// Dragonite only: flight base v5, the v4 construction redone to the user's list of proportions. Flying level to the left (the game
// mirrors it when it crosses the other way), as in the Black Star Promo 5 card: a compact round torso with a wide cream belly in
// horizontal bands, the original head at its own size slightly raised on a short thick neck, the arms tucked to the chest, muscular
// hind legs stretched back, a tail thick at the root that tapers and curls a little up to a cream tip, a big green near wing with
// three fingers growing from the back and the small brown far wing close behind it. Original palette, black outline, scale 1.
// This standalone base is not imported by the production pipelines.
const C = require("./critter");
const { pal, rows } = C.load("dragonite");
const W = 124, H = 72, TOP = 12; // drawing rows run from -TOP to H

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
  for (let y = -TOP; y < H; y++) for (let x = 0; x < W; x++) {
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
  for (let y = -TOP; y < H; y++) for (let x = 0; x < W; x++) {
    const dx = x + 0.5 - to[0], dy = y + 0.5 - to[1], ux = co * dx + si * dy;
    const c = big[Math.floor((-si * dx + co * dy + pivot[1]) * S)]?.[Math.floor(((flip ? -ux : ux) + pivot[0]) * S)];
    if (c && c !== ".") out.push([x, y, c]);
  }
  return out;
}

// Source regions of the original (64x80, standing, facing the viewer's left).
const isHead = (x, y) => (y <= 27 && x >= 5 && x <= 31 && !(x <= 13 && y >= 14 && y <= 23)) || (y <= 10 && x >= 30 && x <= 34);
const isHandSrc = (x, y) => x <= 9 && y >= 39 && y <= 48;          // left hand with its white claws
const isFootSrc = (x, y) => x <= 17 && y >= 68;                     // left foot, toes and claws pointing left

// Layout on the canvas (facing left).
const TORSO = ellipse(60, 42, 18, 16);
const NECK = limb([37, 30], [46, 36], 7);
const TAIL = poly([[76, 33], [88, 35], [98, 34], [106, 30], [112, 25], [116, 20], [118, 19], [118, 23], [111, 31], [102, 39], [92, 45], [80, 51]]);
const TAIL_TIP = (x, y) => TAIL(x, y) && Math.hypot(x + 0.5 - 117, y + 0.5 - 20) <= 6;
const ARM = limb([47, 44], [46, 51], 4);
const NEAR_LEG = union(ellipse(73, 50, 9, 8), limb([79, 53], [90, 57], 4.2));
const FAR_LEG = union(ellipse(77, 45, 8, 7), limb([83, 48], [95, 51], 3.6));
// wide cream belly over the lower half of the torso
const BELLY = (x, y) => TORSO(x, y) && ((x + 0.5 - 57) / 18) ** 2 + ((y + 0.5 - 52) / 13) ** 2 <= 1;

// Wings, built: a bone along the leading edge from the root to the wrist and on to the tip, three fingers from the wrist to the
// points of the scalloped trailing edge, the membrane between them.
function wing({ root, wrist, tips, scallops }, { bone, finger, membrane, shade, deep }) {
  const outline = [root, wrist, tips[0], scallops[0], tips[1], scallops[1], tips[2], scallops[2]];
  const skin = poly(outline), out = new Map();
  const put = (x, y, c) => out.set(x + "," + y, [x, y, c]);
  for (let y = -TOP; y < H; y++) for (let x = 0; x < W; x++) if (skin(x, y)) {
    // lighter near the leading edge, deeper towards the trailing edge
    const d = Math.min(...[[root, wrist], [wrist, tips[0]]].map(([a, b]) => {
      const dx = b[0] - a[0], dy = b[1] - a[1], u = Math.max(0, Math.min(1, ((x - a[0]) * dx + (y - a[1]) * dy) / (dx * dx + dy * dy)));
      return Math.hypot(x - a[0] - u * dx, y - a[1] - u * dy);
    }));
    put(x, y, d < 4 ? membrane : d < 8 ? shade : deep);
  }
  const line = (a, b, c, r) => { for (let y = -TOP; y < H; y++) for (let x = 0; x < W; x++) if (limb(a, b, r)(x, y)) put(x, y, c); };
  for (const t of tips.slice(1)) line(wrist, t, finger, 0.6);
  line(root, wrist, bone, 1.6); line(wrist, tips[0], bone, 1.2);
  // the outline: every wing pixel with background beside it
  const cells = [...out.values()], has = (x, y) => out.has(x + "," + y);
  return cells.map(([x, y, c]) => [x, y, [[1, 0], [-1, 0], [0, 1], [0, -1]].some(([dx, dy]) => !has(x + dx, y + dy)) ? "H" : c]);
}
const NEAR_WING = { root: [61, 30], wrist: [73, 13], tips: [[94, 8], [92, 19], [83, 27]], scallops: [[88, 15], [86, 23], [70, 28]] };
const FAR_WING = { root: [54, 30], wrist: [59, 19], tips: [[68, 14], [67, 21], [62, 27]], scallops: [[64, 19], [62, 24], [57, 28]] };

function build() {
  // canvas row r holds drawing row r - TOP
  const g = Array.from({ length: H + TOP }, () => Array(W).fill("."));
  const draw = cells => { for (const [x, y, c] of cells) if (g[y + TOP]?.[x] !== undefined) g[y + TOP][x] = c; };
  // far side first
  draw(wing(FAR_WING, { bone: "N", finger: "A", membrane: "F", shade: "P", deep: "A" }));
  draw(shaded(FAR_LEG, ORANGE_FAR));
  draw(piece(isFootSrc, [12, 71], 15, [98, 53], { flip: true, recolour: { C: "N", M: "C", N: "P" } }));
  for (const [x, y, c] of shaded(TAIL, ORANGE, { shadeRows: 2 })) {
    let k = c;
    if (c === "C") k = TAIL(x, y - 4) ? (x % 2 ? "N" : "P") : "C";
    if (c === "N") k = x % 2 ? "F" : "I";
    if (TAIL_TIP(x, y) && c !== "H") k = c === "M" ? "L" : "E";
    if (g[y + TOP]?.[x] !== undefined) g[y + TOP][x] = k;
  }
  draw(shaded(union(TORSO, NECK), ORANGE));
  // wide cream belly in horizontal bands
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    if (!BELLY(x, y) || g[y + TOP][x] === "H") continue;
    const top = !BELLY(x, y - 1);
    g[y + TOP][x] = top || y % 3 === 0 ? "I" : y >= 55 ? "E" : "L";
  }
  draw(piece(isHead, [24, 28], -6, [37, 31]));
  draw(shaded(NEAR_LEG, ORANGE));
  draw(piece(isFootSrc, [12, 71], 10, [93, 59], { flip: true }));
  draw(shaded(ARM, ORANGE));
  draw(piece(isHandSrc, [8, 43], -60, [45, 54]));
  draw(wing(NEAR_WING, { bone: "C", finger: "N", membrane: "D", shade: "G", deep: "O" }));
  // Pieces turned apart leave pinholes where they met (wing root, folded legs): small pockets of background shut in by the
  // drawing take the colour most of their neighbours have. Larger pockets are air between parts and stay.
  const seen = g.map(r => r.map(() => false));
  for (let y = 0; y < g.length; y++) for (let x = 0; x < W; x++) {
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
  // crumbs of a piece's source edge left apart from the drawing (under 6 pixels) go
  const done = g.map(r => r.map(() => false));
  for (let y = 0; y < g.length; y++) for (let x = 0; x < W; x++) {
    if (g[y][x] === "." || done[y][x]) continue;
    const bit = [[x, y]]; done[y][x] = true;
    for (let i = 0; i < bit.length; i++) for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
      const X = bit[i][0] + dx, Y = bit[i][1] + dy;
      if (g[Y]?.[X] && g[Y][X] !== "." && !done[Y][X]) { done[Y][X] = true; bit.push([X, Y]); }
    }
    if (bit.length < 6) for (const [X, Y] of bit) g[Y][X] = ".";
  }
  const ys = g.map((r, y) => (r.some(c => c !== ".") ? y : -1)).filter(y => y >= 0);
  const xs = g[0].map((_, x) => (g.some(r => r[x] !== ".") ? x : -1)).filter(x => x >= 0);
  const x0 = Math.min(...xs) - 2, y0 = Math.min(...ys) - 2;
  return { pal, rows: g.slice(y0, Math.max(...ys) + 3).map(r => r.slice(x0, Math.max(...xs) + 3).join("")), origin: [x0, y0] };
}
module.exports = { build, pal, W, H };
if (require.main === module) console.log(JSON.stringify(C.check([build().rows])));
