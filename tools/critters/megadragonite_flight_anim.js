// Mega Dragonite's flight, written by megadragonite_make.js from dragonite_approved_flight.js.
// Dragonite's flight from its approved base (dragonite_flight_approved.json), the original sprite leaned into flight. Its wings are
// small, so they beat short and quick: the near wing turns whole round its shoulder, RotSprite-style, from the piece the base was
// built from, sweeping back and down and up again; the body rises a pixel on the downstroke. Head, body, arms, legs and tail do
// not change.
const assert = require("assert/strict"), C = require("./critter"), P = require("./megadragonite_flight_pose");
const approved = P.build();
const N = 8, FPS = 12, WING_SWEEP = 34;
const t = f => (f / N) * 2 * Math.PI;
// 0 at the approved pose (wing up), 1 at the bottom of the stroke
const sweep = f => (1 - Math.cos(t(f))) / 2;
const bob = f => Math.round(-Math.sin(t(f)));

const shift = (g, dy) => { const out = g.map(r => r.map(() => ".")); g.forEach((r, y) => r.forEach((c, x) => { if (c !== "." && out[y + dy]) out[y + dy][x] = c; })); return out; };
// still (no bob) and bobbing frames: the panel's reduced frames are taken from the still ones and bob afterwards, so the reduction
// picks the same rows of the face in every frame
const still = Array.from({ length: N }, (_, f) => shift(P.compose({ wing: WING_SWEEP * sweep(f) }).g, 0));
const full = still.map((g, f) => shift(g, bob(f)));
// The first frame is the approved base, pixel for pixel.
const [ox, oy] = approved.origin;



// One box for every frame, so the body keeps its place through the loop.
let x0 = 1e9, x1 = -1, y0 = 1e9, y1 = -1;
full.forEach(g => g.forEach((r, y) => r.forEach((c, x) => { if (c !== ".") { x0 = Math.min(x0, x); x1 = Math.max(x1, x); y0 = Math.min(y0, y); y1 = Math.max(y1, y); } })));
x0 -= 2; y0 -= 2; x1 += 2; y1 += 2;
const nativeFrames = full.map(g => g.slice(y0, y1 + 1).map(r => r.slice(x0, x1 + 1).join("")));
// At its own size the leaned sprite is taller than the sky's room, so the panel gets it at 75 %, nearest neighbour (as Charizard
// at 60 %); single stray pixels and one-pixel holes that the reduction leaves are tidied.
const DISPLAY = 0.75, dh = Math.ceil(nativeFrames[0].length * DISPLAY), dw = Math.ceil(nativeFrames[0][0].length * DISPLAY);
function tidy(g) {
  for (let y = 1; y < g.length - 1; y++) for (let x = 1; x < g[0].length - 1; x++) {
    const n = [g[y - 1][x], g[y + 1][x], g[y][x - 1], g[y][x + 1]];
    if (g[y][x] === "." && n.every(c => c !== ".")) g[y][x] = n.find(c => c !== "H") || n[0];
  }
  const copy = g.map(r => [...r]);
  for (let y = 0; y < g.length; y++) for (let x = 0; x < g[0].length; x++)
    if (copy[y][x] !== "." && ![-1, 0, 1].some(dy => [-1, 0, 1].some(dx => (dx || dy) && copy[y + dy]?.[x + dx] && copy[y + dy][x + dx] !== "."))) g[y][x] = ".";
  // around the head, pixels held only corner to corner (the antenna's thin curl) get a side neighbour, or the tilt breaks them
  for (let y = 0; y < g.length * 0.5; y++) for (let x = 0; x < g[0].length * 0.35; x++) for (const dx of [-1, 1]) {
    if (g[y][x] === "." || g[y + 1]?.[x + dx] === undefined || g[y + 1][x + dx] === ".") continue;
    if (g[y][x + dx] === "." && g[y + 1][x] === ".") g[y + 1][x] = g[y][x];
  }
  // a thin part the reduction cut off (the antenna's curl) is joined back by the shortest bridge, in its own colour
  const pieces = [], seen = g.map(r => r.map(() => false));
  g.forEach((r, y) => r.forEach((c, x) => {
    if (c === "." || seen[y][x]) return;
    const q = [[x, y]]; seen[y][x] = true;
    for (let i = 0; i < q.length; i++) for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
      const X = q[i][0] + dx, Y = q[i][1] + dy;
      if (g[Y]?.[X] && g[Y][X] !== "." && !seen[Y][X]) { seen[Y][X] = true; q.push([X, Y]); }
    }
    pieces.push(q);
  }));
  pieces.sort((p, q) => q.length - p.length);
  for (const bit of pieces.slice(1)) {
    let best = [1e9];
    for (const [ax, ay] of bit) for (const [bx, by] of pieces[0]) { const d = Math.hypot(ax - bx, ay - by); if (d < best[0]) best = [d, ax, ay, bx, by]; }
    const [, ax, ay, bx, by] = best, n = Math.max(Math.abs(bx - ax), Math.abs(by - ay));
    // stepped with orthogonal moves and a pixel to spare, so the panel's small tilts cannot break it again
    let [px, py] = [ax, ay];
    for (let k = 1; k <= n; k++) {
      const X = Math.round(ax + ((bx - ax) * k) / n), Y = Math.round(ay + ((by - ay) * k) / n);
      for (const [cx, cy] of [[X, py], [X, Y], [X, Y + 1]]) if (g[cy]?.[cx] === ".") g[cy][cx] = g[ay][ax];
      [px, py] = [X, Y];
    }
  }
  // the joins can shut a pixel of background in; it takes its neighbours' colour like the others
  for (let y = 1; y < g.length - 1; y++) for (let x = 1; x < g[0].length - 1; x++) {
    const n = [g[y - 1][x], g[y + 1][x], g[y][x - 1], g[y][x + 1]];
    if (g[y][x] === "." && n.every(c => c !== ".")) g[y][x] = n.find(c => c !== "H") || n[0];
  }
  return g;
}
const stillFrames = still.map(g => g.slice(y0 - 1, y1 + 2).map(r => r.slice(x0, x1 + 1).join("")));
const fr = stillFrames.map((g, f) => tidy(shift(Array.from({ length: dh }, (_, y) => Array.from({ length: dw }, (_, x) =>
  g[Math.min(g.length - 1, Math.floor((y + 0.5) / DISPLAY))][Math.min(g[0].length - 1, Math.floor((x + 0.5) / DISPLAY))])), bob(f))).map(r => r.join("")));
// the eye, in display coordinates (it rises with the bob, so the box takes a pixel either way)
const face = [Math.floor((ox - x0 + 19) * DISPLAY), Math.floor((oy - y0 + 22) * DISPLAY), 9, 8];
console.log("megadragonite", JSON.stringify({ native: C.check(nativeFrames), display: C.check(fr), size: [fr[0].length, fr.length] }));
module.exports = {
  name: "megadragonite", pal: P.pal, fr, fps: FPS, angles: [-2, 0, 2], pose: 0, pad: 0, layoutHeight: 60, sealRotatedHoles: true,
  nativeFrames, displayScale: DISPLAY, face, sweep, bob, wingSweep: WING_SWEEP, origin: [ox - x0, oy - y0],
  protect: (g, x, y) => x >= face[0] && x < face[0] + face[2] && y >= face[1] && y < face[1] + face[3] && "BKH".includes(g[y][x]),
};
