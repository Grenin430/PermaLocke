// The Max Mushrooms for the second trial's crystal (2026-10-18, prototype): no pixel model exists, so they are built here after the render the
// player sent: a cluster of three with flat hexagonal caps seen from a little above (red, a pink rim, a magenta spiral with a light band, white
// dots at the corners) on pale pink stems, the biggest in front, each outlined over the ones behind. Frames picked by the film as Machamp
// eats them: 0 whole, 1 the back left one gone, 2 the back right one too, 3 a bite out of the front cap, 4 only the front stem left.
// Tilted for their tumble out of the Ultra Wormhole. Sheet with build_batch.js.
const W = 32, H = 27;
const blank = () => Array.from({ length: H }, () => Array(W).fill("."));
let out = blank();
let g = blank();
const put = (x, y, c) => { x = Math.round(x); y = Math.round(y); if (x >= 0 && y >= 0 && x < W && y < H) g[y][x] = c; };

// a stem from under a cap down to the ground, a little bent, lit on its left and shaded on its right
function stem(x0, y0, x1, y1, r) {
  const n = Math.ceil(Math.abs(y1 - y0)) + 1;
  for (let i = 0; i <= n; i++) {
    const u = i / n, x = x0 + (x1 - x0) * u * u, y = y0 + (y1 - y0) * u, w = r * (1 + 0.35 * u);
    for (let dx = -Math.ceil(w); dx <= Math.ceil(w); dx++) {
      if (Math.abs(dx) > w) continue;
      put(x + dx, y, dx <= -w + 1 ? "S" : dx >= w - 1.2 ? "d" : dx > w * 0.2 ? "s" : "S");
    }
  }
}

// a flat hexagonal cap: its top face (red, the spiral as a magenta line with a light band beside it, a white dot at each corner), a pink rim
// and the pale gills under it
function cap(cx, cy, rx, ry, turn, turns) {
  // flat-topped: corners at (+-1, 0) and (+-0.5, +-1)
  const inside = (x, y, k) => { const dx = (x - cx) / (rx * k), dy = (y - cy) / (ry * k); return Math.abs(dy) <= 1 && Math.abs(dx) + (Math.abs(dy) * 0.5) <= 1; };
  for (let y = Math.floor(cy - ry - 1); y <= cy + ry + 3; y++) for (let x = Math.floor(cx - rx - 1); x <= cx + rx + 1; x++) {
    if (inside(x, y, 1)) continue;
    if (inside(x, y - 1, 1)) put(x, y, "P");
    else if (inside(x, y - 2, 0.92)) put(x, y, "G");
  }
  for (let y = Math.floor(cy - ry - 1); y <= cy + ry + 1; y++) for (let x = Math.floor(cx - rx - 1); x <= cx + rx + 1; x++) {
    if (!inside(x, y, 1)) continue;
    const dx = (x - cx) / rx, dy = (y - cy) / ry, r = Math.hypot(dx, dy * 0.9), ang = Math.atan2(dy, dx);
    const spiral = ((((r * turns) - ((ang + turn) / (2 * Math.PI))) % 1) + 1) % 1;
    put(x, y, r > 0.8 ? "R" : spiral < 0.3 ? "M" : spiral < 0.5 ? "L" : "R");
  }
  for (const [vx, vy] of [[1, 0], [-1, 0], [0.5, 1], [-0.5, 1], [0.5, -1], [-0.5, -1]]) put(cx + (vx * (rx - 1.6)), cy + (vy * (ry - 1.1)), "W");
}

// each mushroom on its own, outlined, laid over the ones behind it (back to front), so the caps stay apart
function mushroom(draw, bite = () => false) {
  g = blank();
  draw();
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) if (bite(x, y)) g[y][x] = ".";
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    if (g[y][x] !== ".") out[y][x] = g[y][x];
    else if ([[1, 0], [-1, 0], [0, 1], [0, -1]].some(([dx, dy]) => g[y + dy]?.[x + dx] && g[y + dy][x + dx] !== ".")) out[y][x] = "K";
  }
}

const pad = 6;
const fr = [];
for (let f = 0; f < 5; f++) {
  out = blank();
  // the bite out of the front cap: two round mouthfuls from its upper right
  const bite = (x, y) => f >= 3 && (Math.hypot(x - 23, (y - 9) * 1.2) < 6.5 || Math.hypot(x - 18, (y - 7) * 1.2) < 4.5 || (f >= 4 && y < 17));
  if (f < 1) mushroom(() => { stem(8, 9, 9, 25, 1.5); cap(8, 6, 7, 3.5, 0.4, 1.3); });
  if (f < 2) mushroom(() => { stem(22, 9, 21, 25, 1.5); cap(22, 6, 7, 3.5, 2.2, 1.3); });
  mushroom(() => { stem(15, 16, 15, 25, 2.4); cap(15, 13, 10, 5, 1.0, 1.6); }, bite);
  fr.push(Array.from({ length: H + (2 * pad) }, (_, y) => ".".repeat(pad) + (out[y - pad]?.join("") ?? ".".repeat(W)) + ".".repeat(pad)));
}

const pal = { K: "5A1434", R: "EE4058", r: "C8304A", P: "FFA0BC", G: "F6D4DE", M: "C42A8E", m: "E25AA8", L: "FFC6E2", W: "FFF4FA", S: "FFC8D6", s: "F2A4BA", d: "C87090" };
module.exports = { name: "setasdinamax", pal, fr, fps: 1, angles: [-60, -30, 0, 30, 60], pose: 0, pad, layoutHeight: H };
