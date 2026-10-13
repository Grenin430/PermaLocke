// Rebuilds the reference's pixel grid: one colour per cell (median of its inside, away from the grid lines), background removed by a flood
// from the border, colours snapped to a small palette to clean the JPEG. Writes the grid as letters plus a PPM.
const fs = require("fs");
const W = 198, H = 254, P = 4.3, PH = 3.9;
const b = fs.readFileSync("ref.rgb");
const pix = (x, y) => { const i = (y * W + x) * 3; return [b[i], b[i + 1], b[i + 2]]; };

// cells: include a partial column/row before the first full one, in case the drawing touches it
const x0 = PH - P, y0 = PH - P;
const C = Math.ceil((W - x0) / P), R = Math.ceil((H - y0) / P);
const cells = [];
for (let cy = 0; cy < R; cy++) for (let cx = 0; cx < C; cx++) {
  const samples = [];
  for (let y = Math.ceil(y0 + cy * P + 1.2); y <= Math.floor(y0 + (cy + 1) * P - 1.0); y++)
    for (let x = Math.ceil(x0 + cx * P + 1.2); x <= Math.floor(x0 + (cx + 1) * P - 1.0); x++)
      if (x >= 0 && y >= 0 && x < W && y < H) samples.push(pix(x, y));
  if (!samples.length) { cells.push(null); continue; }
  const med = [0, 1, 2].map(k => samples.map(s => s[k]).sort((a, c) => a - c)[samples.length >> 1]);
  cells.push(med);
}
const cell = (x, y) => (x < 0 || y < 0 || x >= C || y >= R ? null : cells[y * C + x]);
const light = c => c && Math.min(...c) > 200 && Math.max(...c) - Math.min(...c) < 30;

// background: flood from the border through near-white cells
const bg = new Set(), stack = [];
for (let x = 0; x < C; x++) stack.push([x, 0], [x, R - 1]);
for (let y = 0; y < R; y++) stack.push([0, y], [C - 1, y]);
while (stack.length) {
  const [x, y] = stack.pop(), k = x + "," + y;
  if (x < 0 || y < 0 || x >= C || y >= R || bg.has(k)) continue;
  const c = cell(x, y);
  if (c && !light(c)) continue;
  bg.add(k);
  stack.push([x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]);
}

// palette: k-means on the character's cells
const pts = [];
for (let y = 0; y < R; y++) for (let x = 0; x < C; x++) if (!bg.has(x + "," + y) && cell(x, y)) pts.push(cell(x, y));
const K = Number(process.argv[2] || 18);
// farthest-point start, so dark, light and every hue get their own centre
let cent = [[...pts[0]]];
while (cent.length < K) {
  let best = null, bd = -1;
  for (const p of pts) { const d = Math.min(...cent.map(c => (p[0] - c[0]) ** 2 + (p[1] - c[1]) ** 2 + (p[2] - c[2]) ** 2)); if (d > bd) { bd = d; best = p; } }
  cent.push([...best]);
}
for (let it = 0; it < 40; it++) {
  const sum = cent.map(() => [0, 0, 0, 0]);
  for (const p of pts) {
    let bi = 0, bd = 1e9;
    cent.forEach((c, i) => { const d = (p[0] - c[0]) ** 2 + (p[1] - c[1]) ** 2 + (p[2] - c[2]) ** 2; if (d < bd) { bd = d; bi = i; } });
    sum[bi][0] += p[0]; sum[bi][1] += p[1]; sum[bi][2] += p[2]; sum[bi][3]++;
  }
  cent = cent.map((c, i) => (sum[i][3] ? [sum[i][0] / sum[i][3], sum[i][1] / sum[i][3], sum[i][2] / sum[i][3]] : c));
}
const letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
const nearest = p => { let bi = 0, bd = 1e9; cent.forEach((c, i) => { const d = (p[0] - c[0]) ** 2 + (p[1] - c[1]) ** 2 + (p[2] - c[2]) ** 2; if (d < bd) { bd = d; bi = i; } }); return bi; };

// crop to the character
let minX = C, maxX = 0, minY = R, maxY = 0;
for (let y = 0; y < R; y++) for (let x = 0; x < C; x++) if (!bg.has(x + "," + y) && cell(x, y)) { minX = Math.min(minX, x); maxX = Math.max(maxX, x); minY = Math.min(minY, y); maxY = Math.max(maxY, y); }
const rows = [];
for (let y = minY; y <= maxY; y++) {
  let r = "";
  for (let x = minX; x <= maxX; x++) r += bg.has(x + "," + y) || !cell(x, y) ? "." : letters[nearest(cell(x, y))];
  rows.push(r);
}
const hex = c => c.map(v => Math.round(v).toString(16).padStart(2, "0")).join("").toUpperCase();
const palette = cent.map((c, i) => `${letters[i]}=${hex(c)}`).join(" ");
fs.writeFileSync("recon.txt", palette + "\n" + rows.join("\n"));
const out = [];
for (const r of rows) for (const ch of r) { if (ch === ".") out.push(0x2a, 0x6a, 0x90); else out.push(...cent[letters.indexOf(ch)].map(Math.round)); }
fs.writeFileSync("recon.ppm", Buffer.concat([Buffer.from(`P6 ${rows[0].length} ${rows.length} 255\n`), Buffer.from(out)]));
console.log(`${C}x${R} cells, character ${rows[0].length}x${rows.length} at (${minX},${minY}); palette ${palette}`);
console.log(rows.join("\n"));
