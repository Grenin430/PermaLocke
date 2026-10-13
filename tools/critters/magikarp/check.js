// Checks a frames file: isolated pixels, holes inside the silhouette, pixel count per frame.
const fs = require("fs");
const fr = fs.readFileSync(process.argv[2], "utf8").split("\n\n").map(f => f.split("\n"));
const on = (g, x, y) => g[y] && g[y][x] && g[y][x] !== ".";
let iso = 0, holes = 0;
fr.forEach((g, i) => g.forEach((r, y) => [...r].forEach((c, x) => {
  if (c !== ".") {
    let n = 0;
    for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) if ((dx || dy) && on(g, x + dx, y + dy)) n++;
    if (n === 0) { iso++; console.log("isolated", i, x, y); }
  } else if ([[1, 0], [-1, 0], [0, 1], [0, -1]].every(([dx, dy]) => on(g, x + dx, y + dy))) { holes++; console.log("hole", i, x, y); }
})));
console.log("isolated", iso, "holes", holes, "pixels", fr.map(g => g.join("").replace(/\./g, "").length).join(" "));
