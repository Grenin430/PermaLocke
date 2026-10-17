// Rebuilds an enlarged sprite with no grid lines (2026-10-16, Mega Sharpedo's reference): finds the size of its pixels and their phase (the
// split whose cells are the most even inside), takes the middle of each cell, keeps the transparent ones (or, with no alpha, those flooded from
// the border through light cells) as background, snaps the colours to a palette. Writes <out>.txt like recon2.js.
// Usage: node recon_plain.js <image> <out-prefix> [colours] [period]
const fs = require("fs");
const { execFileSync } = require("child_process");
const FF = "C:/Program Files (x86)/Soundux/ffmpeg.exe";
const [img, outp, kArg, pArg] = process.argv.slice(2);
let W, H;
try { execFileSync(FF, ["-hide_banner", "-i", img]); } catch (e) { const m = e.stderr.toString().match(/, (\d{2,4})x(\d{2,4})/); W = +m[1]; H = +m[2]; }
const raw = execFileSync(FF, ["-hide_banner", "-loglevel", "error", "-i", img, "-f", "rawvideo", "-pix_fmt", "rgba", "-"], { maxBuffer: 1 << 27 });
const px = (x, y) => { const i = (y * W + x) * 4; return [raw[i], raw[i + 1], raw[i + 2], raw[i + 3]]; };

// how uneven the cells of a split are: the colour spread inside each, sampled on its middle half
function uneven(p, phx, phy) {
  let acc = 0, n = 0;
  for (let cy = phy; cy + p <= H; cy += p * 3) for (let cx = phx; cx + p <= W; cx += p * 3) {
    const xs = [cx + p * 0.3, cx + p * 0.7], ys = [cy + p * 0.3, cy + p * 0.7];
    const s = [];
    for (const y of ys) for (const x of xs) s.push(px(Math.floor(x), Math.floor(y)));
    if (s.some(q => q[3] < 128)) continue;
    for (let k = 0; k < 3; k++) acc += Math.max(...s.map(q => q[k])) - Math.min(...s.map(q => q[k]));
    n++;
  }
  return n ? acc / n : 1e9;
}

let best = { s: 1e9 };
const lo = pArg ? +pArg : 2, hi = pArg ? +pArg : 9;
for (let p = lo; p <= hi + 1e-9; p += 0.05) {
  for (let phx = 0; phx < p; phx += 0.5) for (let phy = 0; phy < p; phy += 0.5) {
    const s = uneven(p, phx, phy);
    if (s < best.s - 0.5 || (Math.abs(s - best.s) <= 0.5 && p > best.p * 1.9)) best = { s, p, phx, phy };
  }
}

const { p, phx, phy } = best;
const C = Math.floor((W - phx) / p), R = Math.floor((H - phy) / p);
const cells = [];
for (let cy = 0; cy < R; cy++) for (let cx = 0; cx < C; cx++) {
  const s = [];
  for (let b = 0.35; b <= 0.65; b += 0.15) for (let a = 0.35; a <= 0.65; a += 0.15) s.push(px(Math.floor(phx + (cx + a) * p), Math.floor(phy + (cy + b) * p)));
  const med = [0, 1, 2, 3].map(k => s.map(q => q[k]).sort((u, v) => u - v)[s.length >> 1]);
  cells.push(med[3] < 128 ? null : med.slice(0, 3));
}

const pts = cells.filter(Boolean);
const K = Number(kArg || 16);
const d2 = (a, b) => (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2;
let cent = [[...pts[0]]];
while (cent.length < K) { let far = null, fd = -1; for (const q of pts) { const d = Math.min(...cent.map(c => d2(q, c))); if (d > fd) { fd = d; far = q; } } cent.push([...far]); }
for (let it = 0; it < 40; it++) {
  const sum = cent.map(() => [0, 0, 0, 0]);
  for (const q of pts) { let bi = 0, bd = 1e9; cent.forEach((c, i) => { const d = d2(q, c); if (d < bd) { bd = d; bi = i; } }); for (let k = 0; k < 3; k++) sum[bi][k] += q[k]; sum[bi][3]++; }
  cent = cent.map((c, i) => (sum[i][3] ? sum[i].slice(0, 3).map(v => v / sum[i][3]) : c));
}
const L = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
const hex = c => c.map(v => Math.round(v).toString(16).padStart(2, "0")).join("").toUpperCase();
const rows = [];
for (let y = 0; y < R; y++) {
  let r = "";
  for (let x = 0; x < C; x++) {
    const c = cells[y * C + x];
    if (!c) { r += "."; continue; }
    let bi = 0, bd = 1e9;
    cent.forEach((q, i) => { const d = d2(c, q); if (d < bd) { bd = d; bi = i; } });
    r += L[bi];
  }
  rows.push(r);
}
// trim empty borders
const used = rows.map(r => /[^.]/.test(r));
const top = used.indexOf(true), bottom = used.lastIndexOf(true);
let left = 1e9, right = -1;
rows.forEach(r => { const a = r.search(/[^.]/); if (a >= 0) { left = Math.min(left, a); right = Math.max(right, r.length - 1 - [...r].reverse().join("").search(/[^.]/)); } });
const out = rows.slice(top, bottom + 1).map(r => r.slice(left, right + 1));
fs.writeFileSync(`${outp}.txt`, cent.map((c, i) => `${L[i]}=${hex(c)}`).join(" ") + "\n" + out.join("\n") + "\n");
console.log(`${img}: pixels of ${p.toFixed(2)} px (phase ${phx}, ${phy}, spread ${best.s.toFixed(1)}), sprite ${out[0].length}x${out.length}`);
