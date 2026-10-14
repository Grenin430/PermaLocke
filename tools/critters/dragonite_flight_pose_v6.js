// Dragonite only: flight base v6. The constructed side views (v3-v5) were rejected; at the user's choice this one keeps the original
// sprite nearly whole and leans it forward into flight: the whole figure turns as one piece, RotSprite-style at scale 1, so head,
// face, belly bands, arms, wings and claws keep their own drawing and proportions; the legs, already pointing down, end up trailing
// back. Only the tail is a piece of its own, turned so it streams out behind instead of standing up.
// This standalone base is not imported by the production pipelines.
const C = require("./critter");
const { pal, rows } = C.load("dragonite");
const W = 160, H = 110, TOP = 12; // drawing rows run from -TOP to H

const isTail = (x, y) => x >= 44 && y >= 23 && y <= 67 && !(x >= 34 && x < 48 && y >= 12 && y <= 31);
// pieces that do not simply lean with the body
const isHead = (x, y) => ((y <= 27 && x >= 14 && x <= 31) || (y <= 10 && x >= 5 && x <= 34)) && !(x <= 13 && y >= 13);
// the near wing (the original's right one) turns on its own, tipped a little further back than the body
const isNearWing = (x, y) => x >= 34 && y >= 12 && y <= 31 && !(x >= 48 && y >= 23);
// the small far wing of the original would hang under the chin once the body leans
const isFarWing = (x, y) => x <= 13 && y >= 13 && y <= 24;
// Both legs and both hands stay with the body: turned apart they read as cut off.
const isBody = (x, y) => !isTail(x, y) && !isHead(x, y) && !isNearWing(x, y) && !isFarWing(x, y);

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


// lean forward (degrees, anticlockwise) round the middle of the belly; where a source point lands
const LEAN = -55, CORE = [26, 48], AT = [78, 56];
const onCore = ([x, y]) => {
  const a = (LEAN * Math.PI) / 180, dx = x - CORE[0], dy = y - CORE[1];
  return [AT[0] + Math.cos(a) * dx - Math.sin(a) * dy, AT[1] + Math.sin(a) * dx + Math.cos(a) * dy];
};
const TAIL_ROOT = [45, 60], TAIL_TURN = 110, TAIL_SINK = [-4, 2];
// the head leans less than the body so it looks ahead; the near leg swings further back to trail like the other
const NECK = [23, 27], HEAD_TURN = 40, SHOULDER = [36, 28], WING_TURN = 10;

function build() {
  // canvas row r holds drawing row r - TOP
  const g = Array.from({ length: H + TOP }, () => Array(W).fill("."));
  const draw = cells => { for (const [x, y, c] of cells) if (g[y + TOP]?.[x] !== undefined) g[y + TOP][x] = c; };
  // the tail goes over the body with its thick root sunk into the haunch, so the two run into each other with no seam
  const root = onCore(TAIL_ROOT), tail = piece(isTail, TAIL_ROOT, LEAN + TAIL_TURN, [root[0] + TAIL_SINK[0], root[1] + TAIL_SINK[1]]);
  draw(piece(isBody, CORE, LEAN, AT));
  draw(tail);
  draw(piece(isNearWing, SHOULDER, LEAN + WING_TURN, onCore(SHOULDER)));
  draw(piece(isHead, NECK, LEAN + HEAD_TURN, onCore(NECK)));
  // The tail's root touched the body in the original, so that edge had no outline; turned apart it is open to the sky: every
  // tail pixel that ends up beside background gets the black outline.
  for (const [x, y] of tail) {
    const r = y + TOP;
    if (g[r]?.[x] && !"HA".includes(g[r][x]) && [[1, 0], [-1, 0], [0, 1], [0, -1]].some(([dx, dy]) => g[r + dy]?.[x + dx] === ".")) g[r][x] = "H";
  }
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
