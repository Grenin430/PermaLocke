// node zoom.js <module> <frames comma> <x,y,w,h> <scale> <out>
const fs = require("fs"); const { execFileSync } = require("child_process");
const [mod, fl, box, k, out] = process.argv.slice(2);
const h = require("./" + mod); const [bx, by, bw, bh] = box.split(",").map(Number); const pick = fl.split(",").map(Number);
const hex = c => [0, 2, 4].map(i => parseInt(h.pal[c].slice(i, i + 2), 16));
const W = bw * pick.length + pick.length - 1, buf = Buffer.alloc(W * bh * 3, 0x10);
pick.forEach((f, n) => { for (let y = 0; y < bh; y++) for (let x = 0; x < bw; x++) { const ch = h.fr[f][by + y]?.[bx + x] ?? "."; const c = ch === "." ? [0x2a, 0x6a, 0x90] : hex(ch); const i = (y * W + n * (bw + 1) + x) * 3; buf[i] = c[0]; buf[i + 1] = c[1]; buf[i + 2] = c[2]; } });
fs.writeFileSync("z.rgb", buf);
execFileSync("C:/Program Files (x86)/Soundux/ffmpeg.exe", ["-hide_banner", "-loglevel", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", `${W}x${bh}`, "-i", "z.rgb", "-vf", `scale=iw*${k}:ih*${k}:flags=neighbor`, out]);
