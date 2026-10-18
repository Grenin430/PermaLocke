// The pipeline that made the approved Magikarp, for any species: a grid rebuilt from its reference, parts moved per frame on that grid,
// seams filled from the body round them, checks, RotSprite-style tilts with protected small features, a PNG sheet and previews.
const fs = require("fs");
const { execFileSync } = require("child_process");
const FF = "C:/Program Files (x86)/Soundux/ffmpeg.exe";

function load(name) {
  const [palLine, ...rows] = fs.readFileSync(`${name}.txt`, "utf8").trim().split("\n");
  const pal = Object.fromEntries(palLine.split(" ").map(e => [e[0], e.slice(2)]));
  return { pal, rows: rows.map(r => [...r]) };
}

/**
 * Builds the frames. `spec.parts` is a list drawn in order: { test(x, y, ch) -> bool, move(f, x, y) -> [dx, dy] } on base coordinates; a cell
 * belongs to the first part whose test takes it. `spec.under` rebuilds what a moving part uncovered: (x, y) -> letter or null.
 */
function frames(base, spec) {
  const BH = base.length, BW = Math.max(...base.map(r => r.length));
  const at = (x, y) => (y >= 0 && y < BH && x >= 0 && x < BW ? base[y][x] || "." : ".");
  const pad = spec.pad ?? 4, CW = BW + 2 * pad, CH = BH + 2 * pad;
  const owner = (x, y) => spec.parts.findIndex(p => p.test(x, y, at(x, y)));
  const out = [];
  for (let f = 0; f < spec.count; f++) {
    const g = Array.from({ length: CH }, () => Array(CW).fill("."));
    const put = (x, y, c) => { x += pad; y += pad; if (c !== "." && y >= 0 && y < CH && x >= 0 && x < CW) g[y][x] = c; };
    spec.parts.forEach((part, i) => {
      if (part.sample) { part.sample(f, (x, y, c) => put(x, y, c), at, (x, y) => owner(x, y) === i); return; }
      for (let y = 0; y < BH; y++) for (let x = 0; x < BW; x++) {
        const c = at(x, y);
        if (c === "." || owner(x, y) !== i) continue;
        const [dx, dy] = part.move ? part.move(f, x, y) : [0, 0];
        put(x + Math.round(dx), y + Math.round(dy), c);
      }
    });
    // seams: an empty cell walled in by the drawing (four sides, or three near a seam the spec names) takes the colour of a body neighbour
    const on = (x, y) => g[y]?.[x] && g[y][x] !== ".";
    for (let pass = 0; pass < 3; pass++) for (let y = 1; y < CH - 1; y++) for (let x = 1; x < CW - 1; x++) {
      if (g[y][x] !== ".") continue;
      const near = [[x - 1, y], [x + 1, y], [x, y - 1], [x, y + 1]];
      const need = spec.seam && spec.seam(x - pad, y - pad) ? 3 : 4;
      if (near.filter(([a, b]) => on(a, b)).length < need) continue;
      g[y][x] = near.map(([a, b]) => g[b][a]).find(c => c !== "." && !(spec.ink || "").includes(c)) ?? near.map(([a, b]) => g[b][a]).find(c => c !== ".");
    }
    out.push(g.map(r => r.join("")));
  }
  return out;
}

function check(fr) {
  const on = (g, x, y) => g[y] && g[y][x] && g[y][x] !== ".";
  let iso = 0, holes = 0;
  fr.forEach(g => g.forEach((r, y) => [...r].forEach((c, x) => {
    if (c !== ".") {
      let n = 0;
      for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) if ((dx || dy) && on(g, x + dx, y + dy)) n++;
      if (!n) iso++;
    } else if ([[1, 0], [-1, 0], [0, 1], [0, -1]].every(([dx, dy]) => on(g, x + dx, y + dy))) holes++;
  })));
  return { iso, holes, pixels: fr.map(g => g.join("").replace(/\./g, "").length) };
}

function scale2x(g) {
  const h = g.length, w = g[0].length, o = Array.from({ length: h * 2 }, () => Array(w * 2).fill("."));
  const at = (x, y) => (y < 0 || y >= h || x < 0 || x >= w ? "." : g[y][x]);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const P = at(x, y), A = at(x, y - 1), B = at(x + 1, y), C = at(x - 1, y), D = at(x, y + 1);
    o[y * 2][x * 2] = C === A && C !== D && A !== B ? A : P;
    o[y * 2][x * 2 + 1] = A === B && A !== C && B !== D ? B : P;
    o[y * 2 + 1][x * 2] = D === C && D !== B && C !== A ? C : P;
    o[y * 2 + 1][x * 2 + 1] = B === D && B !== A && D !== C ? D : P;
  }
  return o;
}

/** One frame turned by `deg` (clockwise) round `pivot`, into a `cell`-square; `protect(g, x, y)` marks single pixels to put back after. */
function rotate(lines, deg, pivot, cell, protect) {
  const g = lines.map(r => [...r]);
  const big = scale2x(scale2x(scale2x(g)));
  const S = 8, H = big.length, W = big[0].length, a = (deg * Math.PI) / 180, c = Math.cos(a), s = Math.sin(a);
  const out = Array.from({ length: cell }, () => Array(cell).fill("."));
  for (let y = 0; y < cell; y++) for (let x = 0; x < cell; x++) {
    const dx = (x + 0.5 - cell / 2) * S, dy = (y + 0.5 - cell / 2) * S;
    const ix = Math.floor(c * dx + s * dy + pivot[0] * S), iy = Math.floor(-s * dx + c * dy + pivot[1] * S);
    if (ix >= 0 && iy >= 0 && ix < W && iy < H) out[y][x] = big[iy][ix];
  }
  for (let y = 1; y < g.length - 1; y++) for (let x = 1; x < g[0].length - 1; x++) {
    if (!protect || !protect(g, x, y)) continue;
    const dx = x + 0.5 - pivot[0], dy = y + 0.5 - pivot[1];
    const ox = Math.floor(c * dx - s * dy + cell / 2), oy = Math.floor(s * dx + c * dy + cell / 2);
    if (out[oy]?.[ox] !== undefined) out[oy][ox] = g[y][x];
  }
  for (let y = 0; y < cell; y++) for (let x = 0; x < cell; x++) {
    if (out[y][x] === ".") continue;
    let n = 0;
    for (let yy = -1; yy <= 1; yy++) for (let xx = -1; xx <= 1; xx++) if ((xx || yy) && out[y + yy]?.[x + xx] && out[y + yy][x + xx] !== ".") n++;
    if (!n) out[y][x] = ".";
  }
  return out;
}

/** A pupil: one or two dark pixels walled in by the white of an eye. */
function pupilOf(dark, white) {
  return (g, x, y) => dark.includes(g[y][x]) && [g[y][x + 1], g[y][x - 1], g[y + 1]?.[x], g[y - 1]?.[x]].filter(v => white.includes(v)).length >= 3;
}

function sheet(name, pal, fr, angles, pivot, cell, protect) {
  const cells = angles.map(d => fr.map(f => rotate(f, d, pivot, cell, protect)));
  const W = cell * fr.length, H = cell * angles.length, rgba = Buffer.alloc(W * H * 4);
  const hex = ch => [0, 2, 4].map(i => parseInt(pal[ch].slice(i, i + 2), 16));
  cells.forEach((row, r) => row.forEach((g, f) => g.forEach((line, y) => line.forEach((ch, x) => {
    if (ch === "." || !pal[ch]) return;
    const i = ((r * cell + y) * W + f * cell + x) * 4, [R, G, B] = hex(ch);
    rgba[i] = R; rgba[i + 1] = G; rgba[i + 2] = B; rgba[i + 3] = 255;
  }))));
  fs.writeFileSync(`${name}_sheet.rgba`, rgba);
  execFileSync(FF, ["-hide_banner", "-loglevel", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgba", "-s", `${W}x${H}`, "-i", `${name}_sheet.rgba`, `${name}_sheet.png`]);
  return { W, H, cells };
}

/** Frames as PPMs on the water colour, a strip, a GIF of the loop at `fps`, each scaled `k` times with no smoothing. */
function previews(name, pal, fr, fps, k, outDir) {
  const hex = ch => [0, 2, 4].map(i => parseInt(pal[ch].slice(i, i + 2), 16));
  const w = fr[0][0].length, h = fr[0].length;
  const all = Buffer.alloc(w * h * 3 * fr.length);
  fr.forEach((g, f) => g.forEach((line, y) => [...line].forEach((ch, x) => {
    const i = ((f * h + y) * w + x) * 3, c = ch === "." || !pal[ch] ? [0x2a, 0x6a, 0x90] : hex(ch);
    all[i] = c[0]; all[i + 1] = c[1]; all[i + 2] = c[2];
  })));
  fs.writeFileSync(`${name}_frames.rgb`, all);
  const input = ["-hide_banner", "-loglevel", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", `${w}x${h}`, "-framerate", String(fps), "-i", `${name}_frames.rgb`];
  execFileSync(FF, [...input, "-vf", `scale=iw*${k}:ih*${k}:flags=neighbor,split[a][b];[a]palettegen=max_colors=48[p];[b][p]paletteuse=dither=none`, "-loop", "0", `${outDir}/${name}_bucle_fijo.gif`]);
  const cols = Math.min(fr.length, 6);
  execFileSync(FF, [...input, "-vf", `scale=iw*${k}:ih*${k}:flags=neighbor,pad=iw+8:ih+8:4:4:0x101820,tile=${cols}x${Math.ceil(fr.length / cols)}`, "-frames:v", "1", `${outDir}/${name}_fotogramas.png`]);
}

module.exports = { load, frames, check, rotate, sheet, previews, pupilOf };

/**
 * A part turned round its own pivot, RotSprite-style, for parts that swing (wings): returns a `sample` hook for `frames`. `angle(f)` in degrees,
 * clockwise; `scale(f)` shortens it along its length (a wing seen edge-on) if given. `mine(x, y)` picks the part's cells on the base.
 */
function swing(base, mine, pivot, angle, scale) {
  const BH = base.length, BW = Math.max(...base.map(r => r.length));
  const part = Array.from({ length: BH }, (_, y) => Array.from({ length: BW }, (_, x) => (mine(x, y, base[y][x] || ".") ? base[y][x] || "." : ".")));
  const big = scale2x(scale2x(scale2x(part)));
  const S = 8;
  return (f, put) => {
    const a = (angle(f) * Math.PI) / 180, c = Math.cos(a), s = Math.sin(a), k = scale ? scale(f) : 1;
    for (let y = -BH; y < 2 * BH; y++) for (let x = -BW; x < 2 * BW; x++) {
      const dx = x + 0.5 - pivot[0], dy = y + 0.5 - pivot[1];
      const sx = (c * dx + s * dy) / k + pivot[0], sy = -s * dx + c * dy + pivot[1];
      const ix = Math.floor(sx * S), iy = Math.floor(sy * S);
      if (ix < 0 || iy < 0 || ix >= BW * S || iy >= BH * S) continue;
      const ch = big[iy][ix];
      if (ch !== ".") put(x, y, ch);
    }
  };
}
module.exports.swing = swing;
