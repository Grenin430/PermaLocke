// Dragonite only: a flight base built from the pieces of the original (front-facing, standing) sprite, following the flight
// illustration of the Wizards Black Star Promo 5 card (1999, Toshinao Aoki): flying level to the left, head in front, the near
// wing raised over the back showing its green membrane with the far one behind it in shade, the arm reaching forward and down,
// legs trailing back and the tail straight out behind. The torso turns a whole quarter (exact, no resampling) so its belly faces
// down and its legs trail; the head stays upright. This standalone base is not imported by the production pipelines.
const C = require("./critter");
const { pal, rows } = C.load("dragonite");

// Pieces of the original, by region (64x80 source).
const isFarWingSrc = (x, y) => x <= 13 && y >= 14 && y <= 23;
const isNearWing = (x, y) => x >= 34 && y >= 12 && y <= 31 && !(x >= 48 && y >= 23);
const isTail = (x, y) => x >= 44 && y >= 23 && y <= 67 && !isNearWing(x, y);
const isHead = (x, y) => y <= 29 && !isFarWingSrc(x, y) && !isNearWing(x, y) && !isTail(x, y);
// the far (right) hand would stick up out of the back once the torso turns: it is behind the body in a side view
const isFarHand = (x, y) => x >= 33 && x <= 43 && y >= 44 && y <= 51;
// the far (right) foot: once the torso turns it would point up off the back, so it trails beside the near one instead
const isFarLeg = (x, y) => x >= 29 && x <= 44 && y >= 66;
const isTorso = (x, y) => y >= 26 && !isHead(x, y) && !isTail(x, y) && !isNearWing(x, y) && !isFarWingSrc(x, y) && !isFarHand(x, y) && !isFarLeg(x, y);

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
const W = 170, H = 130;
// A piece turned `deg` clockwise round `pivot` (source coords) and its pivot moved to `to` (canvas coords), RotSprite-style.
function piece(mask, pivot, deg, to, recolour = {}) {
  const part = rows.map((r, y) => r.map((c, x) => (mask(x, y) && c !== "." ? recolour[c] || c : ".")));
  const big = scale2x(scale2x(scale2x(part))), S = 8, a = (deg * Math.PI) / 180, co = Math.cos(a), si = Math.sin(a);
  const out = [];
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    const dx = x + 0.5 - to[0], dy = y + 0.5 - to[1];
    const sx = co * dx + si * dy + pivot[0], sy = -si * dx + co * dy + pivot[1];
    const c = big[Math.floor(sy * S)]?.[Math.floor(sx * S)];
    if (c && c !== ".") out.push([x, y, c]);
  }
  return out;
}

// The torso turns a quarter anticlockwise round CORE and lands at BODY; where a torso point of the source ends up:
const CORE = [24, 50], BODY = [80, 70];
const onBody = ([x, y]) => [BODY[0] + (y - CORE[1]), BODY[1] - (x - CORE[0])];
// the far wing shows its outer face, orange in shade
const SHADE = { D: "P", G: "F", O: "A", E: "M", L: "I" };
// Torso on the canvas: x 56-109 (front = the source's top), y 51-94 (back = the source's right side).
const NECK = [24, 28], SHOULDER = [36, 27];
const POS = { head: [60, 64], nearWing: [79, 56], farWing: [72, 53], tail: [103, 73], farLeg: [80, 84] };
const layers = {
  farWing: piece(isNearWing, SHOULDER, -55, POS.farWing, SHADE),
  farLeg: piece(isFarLeg, CORE, -90, POS.farLeg),
  tail: piece(isTail, [46, 62], 52, POS.tail),
  torso: piece(isTorso, CORE, -90, BODY),
  head: piece(isHead, NECK, 0, POS.head),
  nearWing: piece(isNearWing, SHOULDER, -15, POS.nearWing),
};

function compose() {
  const g = Array.from({ length: H }, () => Array(W).fill("."));
  for (const id of ["farWing", "farLeg", "tail", "torso", "head", "nearWing"])
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
  return { g, layers };
}
function build() {
  const { g } = compose();
  const ys = g.map((r, y) => (r.some(c => c !== ".") ? y : -1)).filter(y => y >= 0);
  const xs = g[0].map((_, x) => (g.some(r => r[x] !== ".") ? x : -1)).filter(x => x >= 0);
  const x0 = Math.min(...xs) - 2, y0 = Math.min(...ys) - 2;
  return { pal, rows: g.slice(y0, Math.max(...ys) + 3).map(r => r.slice(x0, Math.max(...xs) + 3).join("")), layers, origin: [x0, y0] };
}
module.exports = { build, compose, pal, W, H };
if (require.main === module) console.log(JSON.stringify(C.check([build().rows])));
