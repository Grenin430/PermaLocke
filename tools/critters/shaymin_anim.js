// Shaymin for the fifth trial's crystal (2026-10-18, prototype), from the sprites the player sent: Land Forme (shaymintierra.txt, recon2 of
// its chart), Sky Forme (shaymincielo.txt, recon2 at 10 px; the chart's light blue ground and grid lines taken away) and the Gracidea flower
// (gracidea.txt, recon_plain at 22 px; its white taken away from the border). One module per sheet: node build_batch.js shaymintierra ...
const C = require("./critter");
const { clean } = require("./megagreninja_clean");

// cells on their own (left by a grid line) go
function tidy(fr) {
  for (const g of fr) {
    const cells = g.map(r => [...r]), seen = new Set();
    for (let y = 0; y < cells.length; y++) for (let x = 0; x < cells[0].length; x++) {
      if (cells[y][x] === "." || seen.has(y * 1000 + x)) continue;
      const blob = [[x, y]]; seen.add(y * 1000 + x);
      for (let i = 0; i < blob.length; i++) for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const [bx, by] = [blob[i][0] + dx, blob[i][1] + dy];
        if (cells[by]?.[bx] && cells[by][bx] !== "." && !seen.has(by * 1000 + bx)) { seen.add(by * 1000 + bx); blob.push([bx, by]); }
      }
      if (blob.length < 6) for (const [bx, by] of blob) cells[by][bx] = ".";
    }
    cells.forEach((r, y) => (g[y] = r.join("")));
  }
  return fr;
}

function sheet(name, rows, pal, count, move, angles, fps) {
  const fr = tidy(C.frames(rows, { count, pad: 6, parts: [{ test: () => true, move }] }));
  console.log(name, JSON.stringify(C.check(fr)));
  return { name, pal, rows, fr, fps, angles, pose: 0, pad: 6, layoutHeight: rows.length };
}

module.exports = { sheet, clean, C };
