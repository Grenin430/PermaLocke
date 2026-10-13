// Rebuilds a reference pixel chart: finds its grid (period and phase, per axis), takes one colour per cell (median of its inside), removes the
// background by a flood from the border through light cells, snaps colours to a palette. Usage: node recon2.js <image> <out-prefix> [colours]
const fs = require("fs");
const { execFileSync } = require("child_process");
const FF = "C:/Program Files (x86)/Soundux/ffmpeg.exe";
const [img, outp, kArg] = process.argv.slice(2);
let W, H;
try { execFileSync(FF, ["-hide_banner", "-i", img]); } catch (e) { const m = e.stderr.toString().match(/, (\d{2,4})x(\d{2,4})/); W = +m[1]; H = +m[2]; }
const raw = execFileSync(FF, ["-hide_banner", "-loglevel", "error", "-i", img, "-f", "rawvideo", "-pix_fmt", "rgb24", "-"], { maxBuffer: 1 << 26 });
const pix = (x, y) => { const i = (y * W + x) * 3; return [raw[i], raw[i + 1], raw[i + 2]]; };
const lum = (x, y) => { const [r, g, b] = pix(x, y); return (r + g + b) / 3; };

// grid lines are a little darker than the white background: average the light pixels of each column (row), find the period and phase whose
// lines are darkest; of near-equal periods the smallest wins (a multiple of the period scores the same)
function profile(n, len, get) {
  const m = [];
  for (let i = 0; i < n; i++) { let s = 0, c = 0; for (let j = 0; j < len; j++) { const v = get(i, j); if (v > 170) { s += v; c++; } } m.push(c ? s / c : 255); }
  return m;
}
function scoreAt(m, p) {
  const mean = m.reduce((a, b) => a + b) / m.length;
  let best = { score: -1e9, ph: 0 };
  for (let ph = 0; ph < p; ph += 0.1) {
    let acc = 0, k = 0;
    for (let t = ph; t < m.length - 1; t += p) { acc += m[Math.round(t)]; k++; }
    if (mean - acc / k > best.score) best = { score: mean - acc / k, ph };
  }
  return best;
}
// cells are square: one period for both axes, the smallest that scores nearly as well as the best (a multiple scores the same)
const mx = profile(W, H, (i, j) => lum(i, j)), my = profile(H, W, (i, j) => lum(j, i));
const cand = [];
for (let p = 3; p <= 16; p += 0.02) { const a = scoreAt(mx, p), b = scoreAt(my, p); cand.push({ p, s: a.score + b.score, phx: a.ph, phy: b.ph }); }
const top = Math.max(...cand.map(c => c.s));
const pick = cand.find(c => c.s >= top * 0.8);
const gx = { p: pick.p, ph: pick.phx }, gy = { p: pick.p, ph: pick.phy };
const x0 = gx.ph - gx.p, y0 = gy.ph - gy.p;
const C = Math.ceil((W - x0) / gx.p), R = Math.ceil((H - y0) / gy.p);
const cells = [];
for (let cy = 0; cy < R; cy++) for (let cx = 0; cx < C; cx++) {
  const s = [];
  const ix0 = Math.ceil(x0 + cx * gx.p + gx.p * 0.28), ix1 = Math.floor(x0 + (cx + 1) * gx.p - gx.p * 0.24);
  const iy0 = Math.ceil(y0 + cy * gy.p + gy.p * 0.28), iy1 = Math.floor(y0 + (cy + 1) * gy.p - gy.p * 0.24);
  for (let y = iy0; y <= iy1; y++) for (let x = ix0; x <= ix1; x++) if (x >= 0 && y >= 0 && x < W && y < H) s.push(pix(x, y));
  cells.push(s.length ? [0, 1, 2].map(k => s.map(q => q[k]).sort((a, b) => a - b)[s.length >> 1]) : null);
}
const cell = (x, y) => (x < 0 || y < 0 || x >= C || y >= R ? null : cells[y * C + x]);
const light = c => c && Math.min(...c) > 205 && Math.max(...c) - Math.min(...c) < 28;

const bg = new Set(), st = [];
for (let x = 0; x < C; x++) st.push([x, 0], [x, R - 1]);
for (let y = 0; y < R; y++) st.push([0, y], [C - 1, y]);
while (st.length) {
  const [x, y] = st.pop(), k = x + "," + y;
  if (x < 0 || y < 0 || x >= C || y >= R || bg.has(k)) continue;
  const c = cell(x, y);
  if (c && !light(c)) continue;
  bg.add(k); st.push([x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]);
}

const pts = [];
for (let y = 0; y < R; y++) for (let x = 0; x < C; x++) if (!bg.has(x + "," + y) && cell(x, y)) pts.push(cell(x, y));
const K = Number(kArg || 16);
const d2 = (p, c) => (p[0] - c[0]) ** 2 + (p[1] - c[1]) ** 2 + (p[2] - c[2]) ** 2;
let cent = [[...pts[0]]];
while (cent.length < K) { let best = null, bd = -1; for (const p of pts) { const d = Math.min(...cent.map(c => d2(p, c))); if (d > bd) { bd = d; best = p; } } cent.push([...best]); }
for (let it = 0; it < 40; it++) {
  const sum = cent.map(() => [0, 0, 0, 0]);
  for (const p of pts) { let bi = 0, bd = 1e9; cent.forEach((c, i) => { const d = d2(p, c); if (d < bd) { bd = d; bi = i; } }); sum[bi][0] += p[0]; sum[bi][1] += p[1]; sum[bi][2] += p[2]; sum[bi][3]++; }
  cent = cent.map((c, i) => (sum[i][3] ? [sum[i][0] / sum[i][3], sum[i][1] / sum[i][3], sum[i][2] / sum[i][3]] : c));
}
const L = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
const nearest = p => { let bi = 0, bd = 1e9; cent.forEach((c, i) => { const d = d2(p, c); if (d < bd) { bd = d; bi = i; } }); return bi; };
let minX = C, maxX = 0, minY = R, maxY = 0;
for (let y = 0; y < R; y++) for (let x = 0; x < C; x++) if (!bg.has(x + "," + y) && cell(x, y)) { minX = Math.min(minX, x); maxX = Math.max(maxX, x); minY = Math.min(minY, y); maxY = Math.max(maxY, y); }
const rows = [];
for (let y = minY; y <= maxY; y++) { let r = ""; for (let x = minX; x <= maxX; x++) r += bg.has(x + "," + y) || !cell(x, y) ? "." : L[nearest(cell(x, y))]; rows.push(r); }
const hex = c => c.map(v => Math.round(v).toString(16).padStart(2, "0")).join("").toUpperCase();
fs.writeFileSync(outp + ".txt", cent.map((c, i) => `${L[i]}=${hex(c)}`).join(" ") + "\n" + rows.join("\n"));
const px = [];
for (const r of rows) for (const ch of r) px.push(...(ch === "." ? [0x2a, 0x6a, 0x90] : cent[L.indexOf(ch)].map(Math.round)));
fs.writeFileSync(outp + ".ppm", Buffer.concat([Buffer.from(`P6 ${rows[0].length} ${rows.length} 255\n`), Buffer.from(px)]));
fs.writeFileSync(outp + ".grid.json", JSON.stringify({ px: gx.p, phx: gx.ph, py: gy.p, phy: gy.ph, minX, minY, w: rows[0].length, h: rows.length, x0, y0 }));
console.log(`${img}: grid ${gx.p.toFixed(2)}x${gy.p.toFixed(2)} px, sprite ${rows[0].length}x${rows.length}`);
