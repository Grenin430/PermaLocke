// Skarmory's flight from its approved base (skarmory_flight_approved.json). The steel blades are rigid, so each wing turns whole
// round its own root, RotSprite-style, from the pieces the base was built from; neither is bent. Both beat together: the near wing
// sweeps down from raised towards level while the far one swings further down under the chest, then both come back. The body
// rises a pixel on the downstroke. Head, neck, body, tail and feet do not change.
const assert = require("assert/strict"), C = require("./critter"), P = require("./skarmory_flight_pose_v3");
const approved = require("./skarmory_flight_approved.json");
const N = 12, FPS = 10, NEAR_SWEEP = 32, FAR_SWEEP = -18;
const t = f => (f / N) * 2 * Math.PI;
// 0 at the approved pose (wings up), 1 at the bottom of the stroke
const sweep = f => (1 - Math.cos(t(f))) / 2;
const bob = f => Math.round(-Math.sin(t(f)));

const full = Array.from({ length: N }, (_, f) => {
  const { g } = P.compose({ near: NEAR_SWEEP * sweep(f), far: FAR_SWEEP * sweep(f) });
  const dy = bob(f), out = g.map(() => Array(P.W).fill("."));
  g.forEach((r, y) => r.forEach((c, x) => { if (c !== "." && out[y + dy]) out[y + dy][x] = c; }));
  return out;
});
// The first frame is the approved base, pixel for pixel.
const [ox, oy] = approved.origin;
approved.rows.forEach((r, y) => [...r].forEach((c, x) => assert.equal(full[0][oy + y][ox + x], c, `frame 0 differs at ${x},${y}`)));
for (let y = 0; y < P.H; y++) for (let x = 0; x < P.W; x++)
  if (full[0][y][x] !== "." && !(y >= oy && y < oy + approved.rows.length && x >= ox && x < ox + approved.rows[0].length)) assert.fail("frame 0 outside the base");

// One box for every frame, so the body keeps its place through the loop.
let x0 = 1e9, x1 = -1, y0 = 1e9, y1 = -1;
full.forEach(g => g.forEach((r, y) => r.forEach((c, x) => { if (c !== ".") { x0 = Math.min(x0, x); x1 = Math.max(x1, x); y0 = Math.min(y0, y); y1 = Math.max(y1, y); } })));
x0 -= 2; y0 -= 2; x1 += 2; y1 += 2;
const fr = full.map(g => g.slice(y0, y1 + 1).map(r => r.slice(x0, x1 + 1).join("")));
// the eye, in frame coordinates (it rises with the bob, so the box takes a pixel either way)
const face = [ox - x0 + 9, oy - y0 + 25, 10, 10];
console.log("skarmory", JSON.stringify({ ...C.check(fr), size: [fr[0][0].length, fr[0].length] }));
module.exports = {
  name: "skarmory", pal: approved.pal, fr, fps: FPS, angles: [-2, 0, 2], pose: 0, pad: 0, layoutHeight: 60, sealRotatedHoles: true,
  face, sweep, bob, nearSweep: NEAR_SWEEP, farSweep: FAR_SWEEP, origin: [ox - x0, oy - y0],
  protect: (g, x, y) => x >= face[0] && x < face[0] + face[2] && y >= face[1] && y < face[1] + face[3] && "BIC".includes(g[y][x]),
};
