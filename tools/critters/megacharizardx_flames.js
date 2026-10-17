// Mega Charizard X's sheet (2026-10-16): Charizard's approved flight recoloured (recolor_sheet.js with megacharizardx.map.json), then two
// touches that a colour map cannot make. The eye shares Charizard's red with the tail flame, so in the head (left part of each cell) that red
// stays red. And the blue flames out of the corners of its mouth: in each cell the snout is found (the leftmost pixel in the head's rows) and a
// small tongue of blue fire licks back and up from the corner of the mouth, its shape changing from frame to frame.
// node megacharizardx_flames.js <charizard.png> <megacharizardx.png>
const { execFileSync } = require("child_process");
const FF = "C:/Program Files (x86)/Soundux/ffmpeg.exe";
const [base, out] = process.argv.slice(2);
const CELL = 102;
function read(img) {
  let W, H;
  try { execFileSync(FF, ["-hide_banner", "-i", img]); } catch (e) { const m = e.stderr.toString().match(/, (\d{2,4})x(\d{2,4})/); W = +m[1]; H = +m[2]; }
  return { W, H, raw: execFileSync(FF, ["-hide_banner", "-loglevel", "error", "-i", img, "-f", "rawvideo", "-pix_fmt", "rgba", "-"], { maxBuffer: 1 << 28 }) };
}
const src = read(base), dst = read(out);
const { W, H } = dst;
const hex = (raw, i) => [0, 1, 2].map(j => raw[i * 4 + j].toString(16).padStart(2, "0")).join("").toUpperCase();
const set = (x, y, c) => { if (x < 0 || y < 0 || x >= W || y >= H) return; const i = (y * W + x) * 4; for (let j = 0; j < 3; j++) dst.raw[i + j] = parseInt(c.slice(j * 2, j * 2 + 2), 16); dst.raw[i + 3] = 255; };

// the eye: the head's red back, and the eye itself red
for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
  const i = y * W + x;
  if (x % CELL < 30 && hex(src.raw, i) === "ED2913") set(x, y, "ED2913");
  // Charizard's eye is the blue of its wings; Mega Charizard X's is red
  if (x % CELL < 30 && y % CELL < 50 && hex(src.raw, i) === "115B9C") set(x, y, "EC0000");
}

// the flames at the mouth: three shapes in turn, back and up from the corner, light at the root
const shapes = [
  [[0, 0, "2C86FF"], [1, 0, "46B4FF"], [1, -1, "A8EEFF"], [2, -1, "46B4FF"], [2, -2, "2C86FF"], [3, -2, "46B4FF"], [3, -3, "2C86FF"], [4, -4, "2C86FF"]],
  [[0, 0, "2C86FF"], [1, 0, "A8EEFF"], [2, 0, "46B4FF"], [2, -1, "46B4FF"], [3, -1, "2C86FF"], [4, -2, "46B4FF"], [5, -2, "2C86FF"]],
  [[0, 0, "2C86FF"], [1, -1, "A8EEFF"], [1, 0, "46B4FF"], [2, -2, "46B4FF"], [2, -3, "2C86FF"], [3, -3, "46B4FF"], [3, -5, "2C86FF"]],
];
let placed = 0;
for (let cy = 0; cy * CELL < H; cy++) for (let cx = 0; cx * CELL < W; cx++) {
  // the snout: the leftmost drawn pixel in the head's rows
  let sx = -1, sy = -1;
  for (let x = 0; x < 40 && sx < 0; x++) for (let y = 28; y < 48; y++) {
    if (dst.raw[((cy * CELL + y) * W + cx * CELL + x) * 4 + 3] > 0) { sx = x; sy = y; break; }
  }
  if (sx < 0) continue;
  const shape = shapes[cx % shapes.length];
  for (const [dx, dy, c] of shape) set(cx * CELL + sx + 9 + dx, cy * CELL + sy + 2 + dy, c);
  placed++;
}
execFileSync(FF, ["-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgba", "-s", `${W}x${H}`, "-i", "-", out], { input: dst.raw });
console.log(`flames in ${placed} cells`);
