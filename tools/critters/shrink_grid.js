// Shrinks a letter grid (<in>.txt, as recon2.js writes it) by a factor, each new cell the letter most of its block has (background when most of
// it is background), then evens out lone cells (a cell whose eight neighbours hold another letter at least six times takes it): the
// reference's noise and watermark go, edges stay. node shrink_grid.js <in> <out> <factor>
const fs = require("fs");
const [src, dst, fArg] = process.argv.slice(2);
const f = +fArg;
const [pal, ...rows] = fs.readFileSync(`${src}.txt`, "utf8").trim().split("\n").map(l => l.replace(/\r$/, ""));
const H = rows.length, W = Math.max(...rows.map(r => r.length));
const at = (x, y) => (rows[y] && rows[y][x]) || ".";
const R = Math.ceil(H / f), C = Math.ceil(W / f);
let out = [];
for (let Y = 0; Y < R; Y++) {
  let r = "";
  for (let X = 0; X < C; X++) {
    const count = {};
    let n = 0;
    for (let y = Math.floor(Y * f); y < Math.min(H, Math.floor((Y + 1) * f)); y++)
      for (let x = Math.floor(X * f); x < Math.min(W, Math.floor((X + 1) * f)); x++) { const c = at(x, y); count[c] = (count[c] || 0) + 1; n++; }
    if ((count["."] || 0) * 2 > n) { r += "."; continue; }
    delete count["."];
    r += Object.entries(count).sort((a, b) => b[1] - a[1])[0][0];
  }
  out.push(r);
}
for (let pass = 0; pass < 2; pass++) {
  const g = out.map(r => [...r]);
  for (let y = 1; y < R - 1; y++) for (let x = 1; x < C - 1; x++) {
    if (g[y][x] === ".") continue;
    const count = {};
    for (let b = -1; b <= 1; b++) for (let a = -1; a <= 1; a++) if (a || b) { const c = g[y + b][x + a]; count[c] = (count[c] || 0) + 1; }
    const [top, k] = Object.entries(count).sort((a, b) => b[1] - a[1])[0];
    if (top !== g[y][x] && top !== "." && k >= 6) out[y] = out[y].slice(0, x) + top + out[y].slice(x + 1);
  }
}
fs.writeFileSync(`${dst}.txt`, pal + "\n" + out.join("\n") + "\n");
console.log(`${C}x${R}`);
