// Mega Greninja for the third trial's crystal (2026-10-18, prototype), from the sprites the player sent: standing with its water shuriken on
// its back (megagreninja.txt, recon_plain at 4 px) and hanging upside down mid-flip (megagreninjasalto.txt, at 5 px). The white the pictures
// had round them is taken away from their border. Frames picked by the film; the flip is drawn turned through the sheet's twelve angles.
const C = require("./critter");
function clean(name) {
  const { pal, rows } = C.load(name);
  // white, or nearly (a JPEG leaves it a little off)
  const whites = Object.keys(pal).filter(k => [0, 2, 4].every(i => parseInt(pal[k].slice(i, i + 2), 16) >= 236));
  const g = rows.map(r => [...r]);
  const H = g.length, W = g[0].length, todo = [];
  for (let x = 0; x < W; x++) todo.push([x, 0], [x, H - 1]);
  for (let y = 0; y < H; y++) todo.push([0, y], [W - 1, y]);
  while (todo.length) {
    const [x, y] = todo.pop();
    if (x < 0 || y < 0 || x >= W || y >= H || (!whites.includes(g[y][x]) && g[y][x] !== ".")) continue;
    if (g[y][x] === "." && !(x === 0 || y === 0 || x === W - 1 || y === H - 1)) continue;
    g[y][x] = "#";
    todo.push([x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]);
  }
  return { pal, rows: g.map(r => r.map(c => (c === "#" ? "." : c)).join("")) };
}
module.exports = { clean };
