// Mega Charizard X's sheet, last step (2026-10-16, after recolor_sheet.js and megacharizardx_flames.js): what makes its outline its own. Two long
// horns rising back from the back of the head, the far one darker and behind, and three black spikes on the shoulders (2026-10-17: black, as the real one's; they were blue). The horns ride on the head:
// each cell's eye (the red pixel megacharizardx_flames.js set) is their anchor. The spikes ride on the body: their anchor is the top of the
// shoulder above the chest (eleven cells above the top of the cyan chest, down onto the body if that is sky), and they go behind the wings (they only take empty
// cells and the body's greys). An ink outline round them where they stand out against the sky.
// node megacharizardx_features.js <megacharizardx.png> [--marcas]
const { execFileSync } = require("child_process");
const FF = "C:/Program Files (x86)/Soundux/ffmpeg.exe";
const [img] = process.argv.slice(2);
const CELL = 102;
let W, H;
try { execFileSync(FF, ["-hide_banner", "-i", img]); } catch (e) { const m = e.stderr.toString().match(/, (\d{2,4})x(\d{2,4})/); W = +m[1]; H = +m[2]; }
const raw = execFileSync(FF, ["-hide_banner", "-loglevel", "error", "-i", img, "-f", "rawvideo", "-pix_fmt", "rgba", "-"], { maxBuffer: 1 << 28 });
const at = (x, y) => (x < 0 || y < 0 || x >= W || y >= H ? null : [0, 1, 2].map(j => raw[(y * W + x) * 4 + j].toString(16).padStart(2, "0")).join("").toUpperCase());
const empty = (x, y) => x >= 0 && y >= 0 && x < W && y < H && raw[(y * W + x) * 4 + 3] === 0;
const set = (x, y, c) => { if (x < 0 || y < 0 || x >= W || y >= H) return; const i = (y * W + x) * 4; for (let j = 0; j < 3; j++) raw[i + j] = parseInt(c.slice(j * 2, j * 2 + 2), 16); raw[i + 3] = 255; };
const BODY = new Set(["4A4E58", "33363E", "1E2026"]);

// a horn: a curve back and up from behind the head, two cells thick at the root and one at the tip
// (2026-10-16: shorter than the first try, and the last cells blue, as the real one's tips)
const HORN = [[2, -4], [3, -5], [4, -6], [5, -7], [6, -8], [7, -9], [8, -9], [9, -10]];
const TIP = ["2C86FF", "A8EEFF"];
function horn(ax, ay, light, dark, mayCover) {
  const cells = [];
  HORN.forEach(([dx, dy], k) => {
    const tip = k - (HORN.length - TIP.length);
    cells.push([ax + dx, ay + dy, tip >= 0 ? TIP[tip] : light]);
    if (k < 6) cells.push([ax + dx, ay + dy + 1, dark]);
    if (k < 3) cells.push([ax + dx + 1, ay + dy + 1, dark]);
  });
  return cells.filter(([x, y]) => mayCover(x, y));
}

// the shoulder spikes: three, leaning back, blue with a light tip
const SPIKES = [[[-4, 1], [-1, 1], [-3, -4]], [[0, 0], [3, 0], [2, -6]], [[4, 1], [7, 1], [7, -4]]];
function inTri(x, y, t) {
  const side = (a, b, p) => (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0]);
  const d = [side(t[0], t[1], [x, y]), side(t[1], t[2], [x, y]), side(t[2], t[0], [x, y])];
  return !(d.some(v => v < 0) && d.some(v => v > 0));
}

let cells = 0;
for (let cy = 0; cy * CELL < H; cy++) for (let cx = 0; cx * CELL < W; cx++) {
  let ex = -1, ey = -1;
  for (let y = 0; y < 50 && ex < 0; y++) for (let x = 0; x < 30; x++) if (at(cx * CELL + x, cy * CELL + y) === "EC0000") { ex = cx * CELL + x; ey = cy * CELL + y; break; }
  if (ex < 0) continue;
  cells++;
  const added = [];
  // far horn first, darker, only where it is empty (behind the head)
  for (const [x, y, c] of horn(ex + 2, ey + 1, "2A2D34", "1E2026", empty)) { set(x, y, c); added.push([x, y]); }
  for (const [x, y, c] of horn(ex, ey, "5A5F6B", "3E424C", (x, y) => empty(x, y) || BODY.has(at(x, y)))) { set(x, y, c); added.push([x, y]); }
  // the shoulder: the top of the cyan chest, then up its column (eight cells back) to the last of the body
  let chx = -1, chy = -1;
  // (below the head: the underside of the jaw is cyan too)
  for (let y = ey - cy * CELL + 7; y < CELL && chx < 0; y++) for (let x = 0; x < 45; x++) {
    const c = at(cx * CELL + x, cy * CELL + y);
    if (c === "86E6FF" || c === "2BB4E6") { chx = cx * CELL + x; chy = cy * CELL + y; break; }
  }
  let sx = -1, sy = -1;
  if (chx >= 0) {
    sx = chx + 8;
    // a fixed rise above the chest (the column above it runs on up the neck), brought down onto the body when it lands on sky
    sy = chy - 11;
    while (sy < chy && empty(sx, sy + 1)) sy++;
  }
  for (const t of sy < 0 ? [] : SPIKES) {
    const tri = t.map(([dx, dy]) => [sx + dx, sy + dy]);
    const xs = tri.map(p => p[0]), ys = tri.map(p => p[1]);
    for (let y = Math.min(...ys); y <= Math.max(...ys); y++) for (let x = Math.min(...xs); x <= Math.max(...xs); x++) {
      if (!inTri(x, y, tri) || !(empty(x, y) || BODY.has(at(x, y)))) continue;
      const tip = y <= Math.min(...ys) + 1;
      set(x, y, tip ? "4A4E58" : x === Math.min(...xs) ? "33363E" : "1E2026");
      added.push([x, y]);
    }
  }
  // the outline: every empty cell touching what was added
  const mark = new Set(added.map(([x, y]) => y * W + x));
  for (const [x, y] of added) for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
    const nx = x + dx, ny = y + dy;
    if (empty(nx, ny) && !mark.has(ny * W + nx)) set(nx, ny, "000000");
  }
}
execFileSync(FF, ["-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgba", "-s", `${W}x${H}`, "-i", "-", img], { input: raw });
console.log(`horns and spikes in ${cells} cells`);
