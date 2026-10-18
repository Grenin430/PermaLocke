// Magikarp swim cycle from the approved base (recon.txt). The base's pixels are kept; only the tail, the side fin, the pelvic fins and the
// whiskers are moved, each as its own layer, and what they uncover is rebuilt from the body round it.
const fs = require("fs");
const [palLine, ...base] = fs.readFileSync("recon.txt", "utf8").trim().split("\n");
const PAL = Object.fromEntries(palLine.split(" ").map(e => [e[0], e.slice(2)]));
const BW = base[0].length, BH = base.length;
const PADT = 2, PADR = 4, PADB = 3, CW = BW + PADR, CH = PADT + BH + PADB;   // room for the tail opening out and dipping
const at = (x, y) => (y >= 0 && y < BH && x >= 0 && x < BW ? base[y][x] : ".");

// ---- the parts, by coordinates on the base
const tailFrom = y => (y >= 4 && y <= 9 ? 29 : y <= 19 ? 28 : y === 20 ? 31 : y <= 22 ? 30 : y <= 29 ? 29 : y === 30 ? 40 : 99);
const isTail = (x, y) => at(x, y) !== "." && x >= tailFrom(y);
const isPec = (x, y) => x >= 19 && x <= 31 && y >= 14 && y <= 33 && "BKIN".includes(at(x, y)) && !isTail(x, y);
const isPelvic = (x, y) => y >= 39 && y <= 49 && x >= 19 && "CFAD".includes(at(x, y));
const isWhisker = (x, y) => "JAF".includes(at(x, y)) && ((x <= 12 && y >= 37) || (x >= 15 && x <= 19 && y >= 41 && !isPelvic(x, y)));

const SEAM = {13:18,14:19,15:20,16:21,17:21,18:20,19:20,20:20,21:19,22:18,23:17,24:17,25:17,26:17,27:17,28:16,29:14,30:12,31:13,32:14,33:15,34:16,35:17,36:17,37:18,38:18,39:19,40:20,41:20};
const isHead = (x, y) => y < 13 ? true : y > 41 ? false : x < SEAM[y] || ("CJA".includes(at(x, y)) && x <= SEAM[y] + 1);
// head leads, the body follows a frame later: the wave runs from the head back to the tail
const REAR = [0, 0, -1, -1, 0, 0, 1, 1], HEAD = [0, -1, -1, 0, 0, 1, 1, 0];
// ---- the body layer: the base without the moving parts; under the side fin, the body is rebuilt from its nearest pixel to the left
const body = base.map((r, y) => [...r].map((c, x) => (isTail(x, y) || isPelvic(x, y) || isWhisker(x, y) ? "." : c)));
for (let y = 0; y < BH; y++) for (let x = 0; x < BW; x++) {
  if (!isPec(x, y)) continue;
  let k = x - 1;
  while (k > 0 && (isPec(k, y) || "DN".includes(at(k, y)))) k--;
  body[y][x] = "BKIN".includes(at(k, y)) || at(k, y) === "." ? "G" : at(k, y);
}

// ---- one frame
function frame(f) {
  const t = (f / 8) * 2 * Math.PI;
  const hd = HEAD[f], rd = REAR[f];
  const out = Array.from({ length: CH }, () => Array(CW).fill("."));
  const put = (x, y, c) => { y += PADT; if (c !== "." && y >= 0 && y < CH && x >= 0 && x < CW) out[y][x] = c; };

  // the tail, behind the body: it narrows as it turns away and widens as it turns towards us, and its far end rises and dips with the beat
  const sx = 1 + 0.16 * Math.sin(t), lift = 2.2 * Math.sin(t - 0.6);
  const rootX = 29, reach = 14;
  for (let y = -PADT; y < CH; y++) for (let x = rootX - 2; x < CW; x++) {
    const u = (x - rootX) / reach;
    const srcX = Math.round(rootX + (x - rootX) / sx);
    const srcY = Math.round(y - rd - lift * Math.max(0, u));
    if (isTail(srcX, srcY)) put(x, y, at(srcX, srcY));
  }

  // the body and head, as they are
  body.forEach((r, y) => r.forEach((c, x) => put(x, y + (isHead(x, y) ? hd : rd), c)));

  // the pelvic fins trail a little behind the beat, their tips more than their roots
  const pel = 1.3 * Math.sin(t - 1.4);
  for (let y = 0; y < BH; y++) for (let x = 0; x < BW; x++) if (isPelvic(x, y)) put(x + Math.round(pel * Math.max(0, y - 40) / 8), y + rd, at(x, y));

  // the side fin: its free tip (to the left) lifts and lowers by a pixel, its root against the body stays
  const fin = Math.sin(t + 0.9);
  for (let y = 0; y < BH; y++) for (let x = 0; x < BW; x++) if (isPec(x, y)) put(x, y + rd + Math.round(fin * Math.max(0, 28 - x) / 8), at(x, y));

  // whiskers: each row moves sideways by its distance from the lips, late on the beat; the rows keep their pixels, so the thickness stays
  const sway = 1.6 * Math.sin(t - 2.0);
  for (let y = 0; y < BH; y++) for (let x = 0; x < BW; x++) if (isWhisker(x, y)) {
    const start = x <= 12 ? 41 : 44;
    put(x + Math.round(sway * Math.max(0, y - start) / 15), y + hd, at(x, y));
  }
  // the seam between head and body opens by a cell where they move apart: fill it from the body round it (not from an outline)
  const on = (x, y) => out[y] && out[y][x] && out[y][x] !== ".";
  for (let pass = 0; pass < 3; pass++) for (let y = 1; y < CH - 1; y++) for (let x = 1; x < CW - 1; x++) {
    if (out[y][x] !== ".") continue;
    const near = [[x - 1, y], [x + 1, y], [x, y - 1], [x, y + 1]];
    const by = y - PADT, nearSeam = by >= 12 && by <= 42 && Math.abs(x - (SEAM[Math.min(41, Math.max(13, by))] ?? 99)) <= 2;
    if (near.filter(([nx, ny]) => on(nx, ny)).length < (nearSeam ? 3 : 4)) continue;
    const pick = near.map(([nx, ny]) => out[ny][nx]).find(c => !"DHPN".includes(c)) ?? out[y][x - 1];
    out[y][x] = pick;
  }
  return out.map(r => r.join(""));
}

const frames = Array.from({ length: 8 }, (_, f) => frame(f));
fs.writeFileSync("anim2_frames.txt", frames.map(f => f.join("\n")).join("\n\n"));
const hex = c => [parseInt(PAL[c].slice(0, 2), 16), parseInt(PAL[c].slice(2, 4), 16), parseInt(PAL[c].slice(4, 6), 16)];
frames.forEach((rows, i) => {
  const px = [];
  for (const r of rows) for (const c of r) px.push(...(c === "." ? [0x2a, 0x6a, 0x90] : hex(c)));
  fs.writeFileSync(`anim2_${i}.ppm`, Buffer.concat([Buffer.from(`P6 ${CW} ${CH} 255\n`), Buffer.from(px)]));
  // transparent PNG source: RGBA raw
  const rgba = [];
  for (const r of rows) for (const c of r) rgba.push(...(c === "." ? [0, 0, 0, 0] : [...hex(c), 255]));
  fs.writeFileSync(`anim2_${i}.rgba`, Buffer.from(rgba));
});
console.log(`${CW}x${CH}, 8 frames`);
