// Terapagos (Terastal Form) for the sixth trial's crystal (2026-10-18), from the sprite the player sent (terapagos.txt, recon_plain at 8 px),
// animated to fly (asked "que parezca que está volando"): eight frames in which the shell stays whole and rises and sinks a cell, while its
// fringes all round (the crystal tufts and the flippers) wave like fins in a wave that runs round the body, and white glints run over the
// facets of the shell, one after another. Tilted for its circling flight.
const { sheet, clean } = require("./shaymin_anim");
const { pal, rows } = clean("terapagos");
const base = sheet("terapagos", rows, pal, 1, () => [0, 0], [0], 1).fr[0].map(r => [...r]);
const H = base.length, W = base[0].length, N = 8;
// the shell: an oval round its middle; everything outside it is fringe
const CX = 39, CY = 30, RX = 19, RY = 14;
const core = (x, y) => ((x - CX) / RX) ** 2 + ((y - CY) / RY) ** 2 <= 1;
const at = (x, y) => (base[y] && base[y][x]) || ".";
const hash = (a, b) => { let h = (a * 374761393 + b * 668265263) | 0; h = Math.imul(h ^ (h >>> 13), 1274126177); return (h ^ (h >>> 16)) >>> 0; };

const fr = [];
for (let f = 0; f < N; f++) {
  const a = (f / N) * 2 * Math.PI, bob = Math.round(Math.sin(a));
  const g = Array.from({ length: H }, () => Array(W).fill("."));
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    let sx = x, sy = y - bob;
    if (!core(sx, sy)) {
      // a fin wave running round the body, stronger the further out the fringe is
      const th = Math.atan2(sy - CY, sx - CX), k = Math.min(1.6, Math.max(0, Math.hypot((sx - CX) / RX, (sy - CY) / RY) - 1) * 4);
      sx = Math.round(x - (Math.cos(th) * k * 0.9 * Math.sin(a + (th * 2))));
      sy = Math.round(y - bob - (Math.sin(th) * k * 1.4 * Math.sin(a + (th * 2))) - (k * 0.8 * Math.cos(a + th)));
      if (core(sx, sy)) continue;
    }
    g[y][x] = at(sx, sy);
  }
  // glints running over the shell's facets: a white cell with a pale cross, never on the outline
  for (let n = 0; n < 4; n++) {
    const h = hash(f, n + 7), x = CX - 14 + (h % 28), y = CY - 10 + ((h >>> 8) % 20) + bob;
    if (!core(x, y - bob) || "BEPK.".includes(g[y][x])) continue;
    g[y][x] = "C";
    for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) if (!"BEPK.".includes(g[y + dy]?.[x + dx] ?? ".")) g[y + dy][x + dx] = "T";
  }
  fr.push(g.map(r => r.join("")));
}

module.exports = { name: "terapagos", pal, rows, fr, fps: 8, angles: [-30, -20, -10, 0, 10, 20, 30], pose: 0, pad: 6, layoutHeight: 45 };
