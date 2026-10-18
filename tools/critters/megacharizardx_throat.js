// Mega Charizard X's sheet, after megacharizardx_features.js (2026-10-17): the light blue that runs from under the jaw down the front of the
// neck to the chest, which only the Mega has (Charizard's neck is orange all round, so the recolour left it grey). In each cell, row by row
// from just under the eye (megacharizardx_flames.js's red pixel) down to the top of the cyan chest, the first body pixel from the front of the
// neck and the one behind it turn cyan, the outer one darker, as the chest is.
// node megacharizardx_throat.js <megacharizardx.png>
const { execFileSync } = require("child_process");
const FF = "C:/Program Files (x86)/Soundux/ffmpeg.exe";
const [img] = process.argv.slice(2);
const CELL = 102;
let W, H;
try { execFileSync(FF, ["-hide_banner", "-i", img]); } catch (e) { const m = e.stderr.toString().match(/, (\d{2,4})x(\d{2,4})/); W = +m[1]; H = +m[2]; }
const raw = execFileSync(FF, ["-hide_banner", "-loglevel", "error", "-i", img, "-f", "rawvideo", "-pix_fmt", "rgba", "-"], { maxBuffer: 1 << 28 });
const at = (x, y) => (x < 0 || y < 0 || x >= W || y >= H ? null : [0, 1, 2].map(j => raw[(y * W + x) * 4 + j].toString(16).padStart(2, "0")).join("").toUpperCase());
const set = (x, y, c) => { const i = (y * W + x) * 4; for (let j = 0; j < 3; j++) raw[i + j] = parseInt(c.slice(j * 2, j * 2 + 2), 16); raw[i + 3] = 255; };
const BODY = new Set(["4A4E58", "33363E", "1E2026"]);
const CHEST = new Set(["86E6FF", "2BB4E6"]);
let cells = 0;
for (let cy = 0; cy * CELL < H; cy++) for (let cx = 0; cx * CELL < W; cx++) {
  const ox = cx * CELL, oy = cy * CELL;
  let ex = -1, ey = -1;
  for (let y = 0; y < 50 && ex < 0; y++) for (let x = 0; x < 30; x++) if (at(ox + x, oy + y) === "EC0000") { ex = ox + x; ey = oy + y; break; }
  if (ex < 0) continue;
  let chx = -1, chy = -1;
  for (let y = ey - oy + 7; y < CELL && chx < 0; y++) for (let x = 0; x < 45; x++) if (CHEST.has(at(ox + x, oy + y))) { chx = ox + x; chy = oy + y; break; }
  if (chx < 0) continue;
  for (let y = ey + 3; y < chy; y++) {
    for (let x = ex - 2; x <= chx + 4; x++) {
      if (!BODY.has(at(x, y))) continue;
      set(x, y, "2BB4E6");
      if (BODY.has(at(x + 1, y))) set(x + 1, y, "86E6FF");
      break;
    }
  }
  cells++;
}
execFileSync(FF, ["-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgba", "-s", `${W}x${H}`, "-i", "-", img], { input: raw });
console.log(`throat painted in ${cells} cells`);
