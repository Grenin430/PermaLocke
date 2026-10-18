// Miraidon for the sixth trial's crystal (2026-10-18), from the cross-stitch chart the player sent (miraidon.txt, recon2 at 21.3 px): the
// white round it taken away from the border, the chart's own bits (its legend, its labels) dropped, and the white gap that showed through
// between its body and its leg made empty. Animated to fly with its chest full of energy (asked): eight frames in which bands of light run
// down the stripes of its chest (white, then yellow), the lightning of its crest crackles, its tail sways from the root, its foreleg paddles
// and the whole body floats up and down a cell. Tilted for its dashes.
const { sheet, clean } = require("./shaymin_anim");
const { pal, rows } = clean("miraidon");
const g0 = rows.map(r => [...r]);
const H0 = g0.length, W0 = g0[0].length, seen = new Set(), blobs = [];
for (let y = 0; y < H0; y++) for (let x = 0; x < W0; x++) {
  if (g0[y][x] === "." || seen.has(y * 1000 + x)) continue;
  const blob = [[x, y]]; seen.add(y * 1000 + x);
  for (let i = 0; i < blob.length; i++) for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, -1], [1, -1], [-1, 1]]) {
    const [bx, by] = [blob[i][0] + dx, blob[i][1] + dy];
    if (g0[by]?.[bx] && g0[by][bx] !== "." && !seen.has(by * 1000 + bx)) { seen.add(by * 1000 + bx); blob.push([bx, by]); }
  }
  blobs.push(blob);
}
const keep = new Set(blobs.filter(b => b.length > 150).flat().map(([x, y]) => y * 1000 + x));
const bare = g0.map((r, y) => r.map((c, x) => (keep.has(y * 1000 + x) ? c : ".")).join(""));
const base = sheet("miraidon", bare, pal, 1, () => [0, 0], [0], 1).fr[0].map(r => [...r]);
const H = base.length, W = base[0].length, N = 8;

// the gap between body and leg (white from the chart): the white cells joined to the one in its middle become empty
{
  const start = [[56, 60], [55, 60], [56, 61], [57, 62], [56, 63]].find(([x, y]) => base[y][x] === "B");
  if (start) {
    const todo = [start], done = new Set();
    while (todo.length) {
      const [x, y] = todo.pop();
      if (done.has(y * 1000 + x) || base[y]?.[x] !== "B") continue;
      done.add(y * 1000 + x); base[y][x] = ".";
      todo.push([x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]);
    }
  }
}

const at = (x, y) => (base[y] && base[y][x]) || ".";
const chest = (x, y) => x >= 30 && x <= 62 && y >= 36 && y <= 58;
const crest = (x, y) => y <= 28;
const tail = (x, y) => x >= 66 && y >= 34;
const leg = (x, y) => x <= 30 && y >= 60;
const hash = (a, b) => { let h = (a * 374761393 + b * 668265263) | 0; h = Math.imul(h ^ (h >>> 13), 1274126177); return (h ^ (h >>> 16)) >>> 0; };

const fr = [];
for (let f = 0; f < N; f++) {
  const a = (f / N) * 2 * Math.PI, bob = Math.round(Math.sin(a));
  const g = Array.from({ length: H }, () => Array(W).fill("."));
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    let sx = x, sy = y - bob;
    // the tail sways from its root, more towards its tip; the foreleg paddles
    if (tail(x, sy)) sx = Math.round(x - (Math.sin(a + ((sy - 34) * 0.12)) * 2 * Math.min(1, (sy - 34) / 40)));
    else if (leg(sx, sy)) sy = Math.round(sy - (Math.sin(a + 1) * 1.2));
    let c = at(sx, sy);
    // the chest's energy: bands of light running down its stripes
    if (c !== "." && chest(sx, sy) && "JDI".includes(c)) {
      const band = ((((sx * 0.6) - sy + (f * 3)) % 12) + 12) % 12;
      c = band < 1.5 ? "B" : band < 3 ? "I" : c;
    }
    // the crest's lightning crackles
    if (crest(sx, sy) && (c === "B" || c === "D") && hash(f, (sy * 100) + sx) % 3 === 0) c = c === "B" ? "D" : "B";
    g[y][x] = c;
  }
  fr.push(g.map(r => r.join("")));
}

module.exports = { name: "miraidon", pal, rows: bare, fr, fps: 10, angles: [-20, -10, 0, 10, 20], pose: 0, pad: 6, layoutHeight: 85 };
