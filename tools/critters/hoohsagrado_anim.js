// Ho-Oh for the fourth trial's crystal (2026-10-18, prototype), from the sprite the player sent (hoohsagrado.txt, recon_plain at 6 px; the
// white round it taken away from its border). Flying: both wings beat from the shoulders together (turned whole, RotSprite), down hard and
// up slower, the body rising a cell on the downstroke and the tail streaming after. Tilted a little for banking.
const C = require("./critter");
const { pal, rows } = require("./megagreninja_clean").clean("hoohsagrado");
const N = 10, t = f => (f / N) * 2 * Math.PI;
// the stroke: 0 wings up, 1 wings down; quicker down than up
const down = f => { const u = f / N; return u < 0.4 ? Math.sin((u / 0.4) * Math.PI / 2) : Math.cos(((u - 0.4) / 0.6) * Math.PI / 2); };
const bob = f => Math.round(-down(f) * 2);
const left = (x, y) => x <= 44 && y <= 50;
const right = (x, y) => x >= 58 && y <= 52;
const tail = (x, y) => y >= 70 && (x <= 44 || x >= 60);
const LEFT = [44, 50], RIGHT = [58, 50];
const fr = C.frames(rows, { count: N, pad: 18, ink: "B", parts: [
  { test: left, sample: (f, put) => C.swing(rows, left, LEFT, () => -62 * down(f))(f, (x, y, c) => put(x, y + bob(f), c)) },
  { test: right, sample: (f, put) => C.swing(rows, right, RIGHT, () => 62 * down(f))(f, (x, y, c) => put(x, y + bob(f), c)) },
  { test: tail, move: (f, x, y) => [Math.sin(t(f) - 1.2) * 1.5 * ((y - 70) / 15), bob(f) + (Math.sin(t(f) - 1.2) * 0.8)] },
  { test: () => true, move: f => [0, bob(f)] },
], seam: (x, y) => (x >= 38 && x <= 64 && y >= 40 && y <= 58) });
// a cell or two the turn leaves on their own goes
for (const g of fr) {
  const cells = g.map(r => [...r]), seen = new Set();
  for (let y = 0; y < cells.length; y++) for (let x = 0; x < cells[0].length; x++) {
    if (cells[y][x] === "." || seen.has(y * 1000 + x)) continue;
    const blob = [[x, y]]; seen.add(y * 1000 + x);
    for (let i = 0; i < blob.length; i++) for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
      const [bx, by] = [blob[i][0] + dx, blob[i][1] + dy];
      if (cells[by]?.[bx] && cells[by][bx] !== "." && !seen.has(by * 1000 + bx)) { seen.add(by * 1000 + bx); blob.push([bx, by]); }
    }
    if (blob.length < 8) for (const [bx, by] of blob) cells[by][bx] = ".";
  }
  cells.forEach((r, y) => (g[y] = r.join("")));
}

console.log("hoohsagrado", JSON.stringify(C.check(fr)));
module.exports = { name: "hoohsagrado", pal, rows, fr, fps: 12, angles: [-10, -5, 0, 5, 10], pose: 0, pad: 18, layoutHeight: rows.length };
