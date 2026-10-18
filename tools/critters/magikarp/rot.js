// RotSprite-style rotation of the swim frames: Scale2x three times (8x), rotate with nearest sampling round a body pivot, take the centre pixel
// of each 8x8 block back down. Writes a sheet: one row per angle, one column per frame, each cell 64x64 with the pivot at its centre.
const fs = require("fs");
const [palLine] = fs.readFileSync("recon.txt", "utf8").split("\n");
const PAL = Object.fromEntries(palLine.split(" ").map(e => [e[0], e.slice(2)]));
const frames = fs.readFileSync("anim2_frames.txt", "utf8").split("\n\n").map(f => f.split("\n").map(r => [...r]));
const ANGLES = [0, 4, 8, 12, 16, 20, 24];       // clockwise, which lifts the nose of a fish facing left
const PIVOT = [18, 32], CELL = 64;

function scale2x(g) {
  const h = g.length, w = g[0].length, o = Array.from({ length: h * 2 }, () => Array(w * 2).fill("."));
  const at = (x, y) => (y < 0 || y >= h || x < 0 || x >= w ? "." : g[y][x]);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const P = at(x, y), A = at(x, y - 1), B = at(x + 1, y), C = at(x - 1, y), D = at(x, y + 1);
    let e0 = P, e1 = P, e2 = P, e3 = P;
    if (C === A && C !== D && A !== B) e0 = A;
    if (A === B && A !== C && B !== D) e1 = B;
    if (D === C && D !== B && C !== A) e2 = C;
    if (B === D && B !== A && D !== C) e3 = D;
    o[y * 2][x * 2] = e0; o[y * 2][x * 2 + 1] = e1; o[y * 2 + 1][x * 2] = e2; o[y * 2 + 1][x * 2 + 1] = e3;
  }
  return o;
}

function rotate(g, deg) {
  const big = scale2x(scale2x(scale2x(g)));
  const S = 8, H = big.length, W = big[0].length, a = (deg * Math.PI) / 180, c = Math.cos(a), s = Math.sin(a);
  const out = Array.from({ length: CELL }, () => Array(CELL).fill("."));
  for (let y = 0; y < CELL; y++) for (let x = 0; x < CELL; x++) {
    // centre of the output pixel, in big-image coordinates, rotated back
    const dx = (x + 0.5 - CELL / 2) * S, dy = (y + 0.5 - CELL / 2) * S;
    const sx = c * dx + s * dy + PIVOT[0] * S, sy = -s * dx + c * dy + PIVOT[1] * S;
    const ix = Math.floor(sx), iy = Math.floor(sy);
    if (ix >= 0 && iy >= 0 && ix < W && iy < H) out[y][x] = big[iy][ix];
  }
  // features too small for the sampling (the pupil: one dark pixel inside the white of the eye) are put back where they turn to, so the face
  // never loses them; a pupil is a dark pixel with the white of the eye on three sides or more
  for (let y = 1; y < g.length - 1; y++) for (let x = 1; x < g[0].length - 1; x++) {
    // and the cream lines of the whiskers and the cheek, one pixel wide, that the sampling can cut
    const pupil = g[y][x] === "D" && [g[y][x + 1], g[y][x - 1], g[y + 1][x], g[y - 1][x]].filter(v => v === "B").length >= 3;
    const whisker = g[y][x] === "J" && y >= 30;
    if (!pupil && !whisker) continue;
    const dx = x + 0.5 - PIVOT[0], dy = y + 0.5 - PIVOT[1];
    const ox = Math.floor(c * dx - s * dy + CELL / 2), oy = Math.floor(s * dx + c * dy + CELL / 2);
    out[oy][ox] = g[y][x];
  }
  // a lone pixel left by the sampling goes
  for (let y = 0; y < CELL; y++) for (let x = 0; x < CELL; x++) {
    if (out[y][x] === ".") continue;
    let n = 0;
    for (let yy = -1; yy <= 1; yy++) for (let xx = -1; xx <= 1; xx++) if ((xx || yy) && out[y + yy]?.[x + xx] && out[y + yy][x + xx] !== ".") n++;
    if (n === 0) out[y][x] = ".";
  }
  return out;
}

const sheet = ANGLES.map(deg => frames.map(f => rotate(f, deg)));
const hex = ch => [0, 2, 4].map(i => parseInt(PAL[ch].slice(i, i + 2), 16));
const W = CELL * frames.length, H = CELL * ANGLES.length, rgba = Buffer.alloc(W * H * 4);
sheet.forEach((row, r) => row.forEach((g, f) => g.forEach((line, y) => line.forEach((ch, x) => {
  if (ch === ".") return;
  const i = ((r * CELL + y) * W + f * CELL + x) * 4, [R, G, B] = hex(ch);
  rgba[i] = R; rgba[i + 1] = G; rgba[i + 2] = B; rgba[i + 3] = 255;
}))));
fs.writeFileSync("sheet.rgba", rgba);
fs.writeFileSync("sheet_meta.json", JSON.stringify({ cell: CELL, frames: frames.length, angles: ANGLES, pivot: [CELL / 2, CELL / 2] }));
console.log(`${W}x${H}`);
