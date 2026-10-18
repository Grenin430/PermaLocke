// Gigantamax Machamp for the second trial's crystal (2026-10-18, prototype), from gmaxmachamp.txt (the sprite the player sent, rebuilt with
// recon_plain.js), turned to face right: its long arm then reaches out to the right. Frames picked by the film, not looped: 0 standing on
// guard, 1 winding up (that arm swung up and back over its shoulder, the body leaning back), 2 the punch (that arm thrown out further, the
// body leaning into it).
const C = require("./critter");
const { pal, rows: sprite } = C.load("gmaxmachamp");
const rows = sprite.map(r => [...r].reverse());
const pad = 16;
const arm = (x, y) => x >= 47 && y >= 5 && y <= 18;
const SHOULDER = [46, 13];
const swingOf = [0, -65, 0];
const reach = [0, 0, 4];
const lean = [0, -1, 2];
// the top of the body leans with the frame, less towards the hips
const top = (f, y) => [y < 44 ? Math.round(lean[f] * (44 - y) / 44) : 0, 0];

const fr = C.frames(rows, { count: 3, pad, ink: "A", parts: [
  {
    test: arm,
    // thrown out: drawn again a cell further each time, so the arm stretches rather than coming off
    sample: (f, put) => {
      for (let r = 0; r <= reach[f]; r++) C.swing(rows, arm, SHOULDER, () => swingOf[f])(f, (x, y, c) => { const [dx, dy] = top(f, Math.min(y, 18)); put(x + dx + r, y + dy, c); });
    },
  },
  { test: () => true, move: (f, x, y) => top(f, y) },
], seam: (x, y) => x >= 40 && x <= 56 && y >= 2 && y <= 22 });

// a cell or two the turn leaves on their own goes
for (const g of fr) {
  const cells = g.map(r => [...r]);
  const seen = new Set();
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

console.log("gmaxmachamp", JSON.stringify(C.check(fr)));
module.exports = { name: "gmaxmachamp", pal, rows, fr, fps: 1, angles: [0], pose: 0, pad, layoutHeight: rows.length };
