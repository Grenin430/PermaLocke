// node rostro.js <name> <cell> <frames> <x,y,w,h> <scale> <out.png>: the same crop (the face) from every cell of <name>_sheet.rgba, tiled
// as in the sheet (rows = tilts, columns = frames) on the water colour, to check by eye that the eye and the small features are in every cell.
const fs = require("fs"); const { execFileSync } = require("child_process");
const [name, cellArg, nArg, box, k, out] = process.argv.slice(2);
const cell = +cellArg, n = +nArg, [bx, by, bw, bh] = box.split(",").map(Number);
const b = fs.readFileSync(`${name}_sheet.rgba`), W = cell * n, rows = b.length / 4 / W / cell;
const OW = n * (bw + 1) - 1, OH = rows * (bh + 1) - 1, o = Buffer.alloc(OW * OH * 3, 0x10);
for (let r = 0; r < rows; r++) for (let f = 0; f < n; f++) for (let y = 0; y < bh; y++) for (let x = 0; x < bw; x++) {
  const i = ((r * cell + by + y) * W + f * cell + bx + x) * 4, j = ((r * (bh + 1) + y) * OW + f * (bw + 1) + x) * 3;
  const a = b[i + 3]; o[j] = a ? b[i] : 0x2a; o[j + 1] = a ? b[i + 1] : 0x6a; o[j + 2] = a ? b[i + 2] : 0x90;
}
fs.writeFileSync("z.rgb", o);
execFileSync("C:/Program Files (x86)/Soundux/ffmpeg.exe", ["-hide_banner", "-loglevel", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", `${OW}x${OH}`, "-i", "z.rgb", "-vf", `scale=iw*${k}:ih*${k}:flags=neighbor`, out]);
