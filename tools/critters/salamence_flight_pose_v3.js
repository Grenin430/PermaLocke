// Salamence only: a gliding flight base. At the user's direction the reference is an anime frame of Salamence in flight (3/4 view
// heading left, mouth open, the far wing raised behind, the near wing low in front, legs hanging and tail curled up behind), which is
// already the posture of the original sprite. So the original stays whole at scale 1 (head, face, body, legs, tail) and only its
// wings come down a little, each turned round its own root RotSprite-style (Scale2x x3, backward sampling). The wings stay fixed:
// Salamence glides, it does not beat them. This standalone base is not imported by the production pipelines.
const C = require("./critter");
const { pal, rows } = C.load("salamence");
const at = (x, y) => (rows[y] && rows[y][x]) || ".";

// The two wings meet in one red mass; the line between them is the white and grey bone along the big wing's leading edge, which
// runs from (47, 8) down to (45, 34). The big wing (raised behind, top right) lies from that line back; the near wing is the red
// in front of it, by the head and down to its tip under the chin.
const edge = y => (y < 8 ? 40 : y <= 21 ? 47 : y <= 34 ? 45 : 50);
const redish = c => "AJGHL".includes(c);
const isCrestOrMouth = (x, y) => (x <= 30 && y <= 13) || (x <= 21 && y <= 33);
const isBigWing = (x, y) => y <= 41 && (x >= edge(y)) && !(y >= 36 && !redish(at(x, y)) && !"DFIN".includes(at(x, y)));
const isNearRed = (x, y) => !isBigWing(x, y) && !isCrestOrMouth(x, y) && x >= 20 && x < edge(y) && y <= 44 && redish(at(x, y));
// The near wing's dark outline goes with it where it faces the sky; where it meets the neck it stays, so the neck keeps its edge.
const near8 = (x, y, f) => [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, -1], [1, -1], [-1, 1]].some(([dx, dy]) => f(x + dx, y + dy));
const isNearOutline = (x, y) => "DI".includes(at(x, y)) && !isBigWing(x, y) && !isCrestOrMouth(x, y) && near8(x, y, isNearRed) &&
  near8(x, y, (a, b) => at(a, b) === ".") && !near8(x, y, (a, b) => "BKMOEPC".includes(at(a, b)));
const isNearWing = (x, y) => isNearRed(x, y) || isNearOutline(x, y);
const isCore = (x, y) => !isBigWing(x, y) && !isNearWing(x, y);

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
const W = 140, H = 110, O = [30, 20]; // canvas and where the source origin lands
const T = (x, y) => [x + O[0], y + O[1]];
// A piece turned `deg` clockwise round `pivot` (source coords) and its pivot moved to `to` (canvas coords).
function piece(mask, pivot, deg, to) {
  const part = rows.map((r, y) => r.map((c, x) => (mask(x, y) && c !== "." ? c : ".")));
  const big = scale2x(scale2x(scale2x(part))), S = 8, a = (deg * Math.PI) / 180, co = Math.cos(a), si = Math.sin(a), out = [];
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    const dx = x + 0.5 - to[0], dy = y + 0.5 - to[1];
    const c = big[Math.floor((-si * dx + co * dy + pivot[1]) * S)]?.[Math.floor((co * dx + si * dy + pivot[0]) * S)];
    if (c && c !== ".") out.push([x, y, c]);
  }
  return out;
}

// Turned apart, the two wings opened a gap of sky between them and left the near one with a straight cut edge: they come down
// together as one rigid piece behind head and body, lowered a few pixels and tipped a little back round the shoulder.
const isWings = (x, y) => isBigWing(x, y) || isNearWing(x, y);
const SHOULDER = [45, 38], WINGS_TURN = 9, WINGS_DROP = 5;

// The whole canvas for a pose, before cropping; `wings` adds degrees to the pair (kept for a later glide sway).
function compose({ wings = 0 } = {}) {
  const g = Array.from({ length: H }, () => Array(W).fill("."));
  const draw = cells => { for (const [x, y, c] of cells) if (g[y]?.[x] !== undefined) g[y][x] = c; };
  const wingCells = piece(isWings, SHOULDER, WINGS_TURN + wings, T(SHOULDER[0], SHOULDER[1] + WINGS_DROP));
  draw(wingCells);
  draw(piece(isCore, [0, 0], 0, T(0, 0)));
  // A wing pixel left beside the sky after the turn gets the dark outline, so no edge of the wing is open.
  for (const [x, y] of wingCells) {
    if (g[y][x] !== "." && !"DHL".includes(g[y][x]) && redish(g[y][x]) && [[1, 0], [-1, 0], [0, 1], [0, -1]].some(([dx, dy]) => g[y + dy]?.[x + dx] === ".")) g[y][x] = "H";
  }
  // Pieces turned apart leave pinholes where they met: small pockets of background shut in by the drawing (up to 4 pixels) take the
  // colour most of their neighbours have. Larger pockets are air between parts and stay.
  const seen = g.map(r => r.map(() => false));
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    if (g[y][x] !== "." || seen[y][x]) continue;
    const pocket = [[x, y]]; let open = false;
    seen[y][x] = true;
    for (let i = 0; i < pocket.length; i++) {
      const [px, py] = pocket[i];
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const nx = px + dx, ny = py + dy;
        if (g[ny]?.[nx] === undefined) { open = true; continue; }
        if (g[ny][nx] === "." && !seen[ny][nx]) { seen[ny][nx] = true; pocket.push([nx, ny]); }
      }
    }
    if (open || pocket.length > 4) continue;
    for (const [px, py] of pocket) {
      const n = {};
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, -1], [1, -1], [-1, 1]]) { const c = g[py + dy]?.[px + dx]; if (c && c !== ".") n[c] = (n[c] || 0) + 1; }
      g[py][px] = Object.entries(n).sort((p, q) => q[1] - p[1])[0][0];
    }
  }
  // crumbs left apart from the drawing (under 6 pixels) go
  const done = g.map(r => r.map(() => false));
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    if (g[y][x] === "." || done[y][x]) continue;
    const bit = [[x, y]]; done[y][x] = true;
    for (let i = 0; i < bit.length; i++) for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
      const X = bit[i][0] + dx, Y = bit[i][1] + dy;
      if (g[Y]?.[X] && g[Y][X] !== "." && !done[Y][X]) { done[Y][X] = true; bit.push([X, Y]); }
    }
    if (bit.length < 6) for (const [X, Y] of bit) g[Y][X] = ".";
  }
  return { g };
}

function build() {
  const { g } = compose();
  const ys = g.map((r, y) => (r.some(c => c !== ".") ? y : -1)).filter(y => y >= 0);
  const xs = g[0].map((_, x) => (g.some(r => r[x] !== ".") ? x : -1)).filter(x => x >= 0);
  const x0 = Math.min(...xs) - 2, y0 = Math.min(...ys) - 2;
  return { pal, rows: g.slice(y0, Math.max(...ys) + 3).map(r => r.slice(x0, Math.max(...xs) + 3).join("")), origin: [x0, y0] };
}
module.exports = { build, compose, pal, W, H };
if (require.main === module) console.log(JSON.stringify(C.check([build().rows])));
