// Shaymin Sky Forme (see shaymin_anim.js): the chart's light blue ground (A) and its grid lines (M) round it taken away from the border in;
// a grid line left inside the drawing takes the colour of the cell beside it. Flying, eight frames: both wings beat from the shoulders (turned
// whole), the red scarf streams and flutters after the beat, the legs swing a little, the body rises a cell on the downstroke.
const { sheet, C } = require("./shaymin_anim");
const { pal, rows } = C.load("shaymincielo");
const g = rows.map(r => [...r]);
const H = g.length, W = g[0].length, ground = new Set("AM.");
const todo = [];
for (let x = 0; x < W; x++) todo.push([x, 0], [x, H - 1]);
for (let y = 0; y < H; y++) todo.push([0, y], [W - 1, y]);
while (todo.length) {
  const [x, y] = todo.pop();
  if (x < 0 || y < 0 || x >= W || y >= H || !ground.has(g[y][x])) continue;
  g[y][x] = "#";
  todo.push([x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]);
}
for (let pass = 0; pass < 3; pass++) for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
  if (g[y][x] !== "M" && g[y][x] !== "A") continue;
  const near = [g[y][x - 1], g[y - 1]?.[x], g[y][x + 1], g[y + 1]?.[x]].find(c => c && !"#AM.".includes(c));
  if (near) g[y][x] = near;
}
const bare = g.map(r => r.map(c => (c === "#" || c === "A" || c === "M" ? "." : c)).join(""));

const N = 8, t = f => (f / N) * 2 * Math.PI;
const down = f => (1 - Math.cos(t(f))) / 2;
const bob = f => Math.round(-down(f) * 1.4);
const left = (x, y) => x <= 12 && y >= 5 && y <= 17;
const right = (x, y) => x >= 26 && y <= 16;
const scarf = (x, y) => x >= 27 && y >= 17 && y <= 28 && "FCK".includes(bare[y][x]);
const legs = (x, y) => y >= 33;
const fr = C.frames(bare, { count: N, pad: 8, parts: [
  { test: left, sample: (f, put) => C.swing(bare, left, [13, 11], () => -50 * (down(f) - 0.45))(f, (x, y, c) => put(x, y + bob(f), c)) },
  { test: right, sample: (f, put) => C.swing(bare, right, [25, 11], () => 50 * (down(f) - 0.45))(f, (x, y, c) => put(x, y + bob(f), c)) },
  { test: scarf, move: (f, x, y) => [Math.sin(t(f) - 1 - (x * 0.25)) * 1.4 * ((x - 26) / 15), bob(f) + (Math.sin(t(f) - (x * 0.3)) * 1.2 * ((x - 26) / 15))] },
  { test: legs, move: (f, x, y) => [Math.sin(t(f) - 1.5) * 0.9 * ((y - 33) / 8), bob(f)] },
  { test: () => true, move: f => [0, bob(f)] },
], seam: (x, y) => (x >= 10 && x <= 16 && y >= 5 && y <= 18) || (x >= 22 && x <= 29 && y >= 3 && y <= 18) });
console.log("shaymincielo", JSON.stringify(C.check(fr)));
module.exports = { name: "shaymincielo", pal, rows: bare, fr, fps: 10, angles: [-15, -8, 0, 8, 15], pose: 0, pad: 8, layoutHeight: bare.length };
