// Mega Gyarados base (2026-10-16): the reference is a chart (cells of 6 px, `node recon2.js imagenes/megas/mega_gyarados.png megagyarados 18`).
// Its long whiskers curl round and close in a stretch of the white background below the body, which the flood from the border cannot reach. Every
// white patch (B) bigger than a few cells is that background, and goes; the eyes' and teeth's whites are a cell or two.
const C = require("./critter.js");
const { pal, rows } = C.load("megagyarados");
const H = rows.length, W = Math.max(...rows.map(r => r.length));
const seen = new Set();
let cleared = 0;
for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
  if (rows[y][x] !== "B" || seen.has(y * W + x)) continue;
  const patch = [], todo = [[x, y]];
  seen.add(y * W + x);
  while (todo.length) {
    const [px, py] = todo.pop();
    patch.push([px, py]);
    for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
      const nx = px + dx, ny = py + dy;
      if (ny < 0 || ny >= H || nx < 0 || nx >= W || rows[ny][nx] !== "B" || seen.has(ny * W + nx)) continue;
      seen.add(ny * W + nx);
      todo.push([nx, ny]);
    }
  }
  if (patch.length > 6) { for (const [px, py] of patch) rows[py][px] = "."; cleared += patch.length; }
}
module.exports = { pal, rows };
if (require.main === module) {
  console.log(cleared, "cells");
  require("fs").writeFileSync("megagyarados_clean.txt", Object.entries(pal).map(([k, v]) => `${k}=${v}`).join(" ") + "\n" + rows.map(r => r.join("")).join("\n"));
}
