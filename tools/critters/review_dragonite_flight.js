// Structural and visual checks of Dragonite's flight cycle, and the review images.
const fs = require("fs"), assert = require("assert/strict"), { execFileSync } = require("child_process"), C = require("./critter");
const m = require("./dragonite_approved_flight"), N = m.fr.length;
const OUT = "C:/Users/javie/Desktop/imagenes/revision/dragonite_vuelo";
fs.mkdirSync(OUT, { recursive: true });
const tilt = Math.max(...m.angles.map(Math.abs)) * Math.PI / 180, w = m.fr[0][0].length, h = m.fr[0].length;
let cell = Math.ceil(Math.max(w * Math.cos(tilt) + h * Math.sin(tilt), h * Math.cos(tilt) + w * Math.sin(tilt))); cell += cell % 2;
const rgba = fs.readFileSync("dragonite_sheet.rgba"), colours = Object.fromEntries(Object.entries(m.pal).map(([c, x]) => [x.toUpperCase(), c]));
const cells = [];
for (let r = 0; r < m.angles.length; r++) for (let f = 0; f < N; f++) cells.push(Array.from({ length: cell }, (_, y) => Array.from({ length: cell }, (_, x) => {
  const i = ((r * cell + y) * cell * N + f * cell + x) * 4; if (!rgba[i + 3]) return ".";
  const c = colours[rgba.subarray(i, i + 3).toString("hex").toUpperCase()]; assert.ok(c); return c;
}).join("")));
const pieces = g => { const seen = new Set(), out = []; g.forEach((r, y) => [...r].forEach((c, x) => {
  if (c === "." || seen.has(x + "," + y)) return; const q = [[x, y]]; seen.add(x + "," + y); let n = 0;
  while (q.length) { const [a, b] = q.pop(); n++; for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) { const X = a + dx, Y = b + dy;
    if (g[Y]?.[X] && g[Y][X] !== "." && !seen.has(X + "," + Y)) { seen.add(X + "," + Y); q.push([X, Y]); } } }
  out.push(n); })); return out; };
const checks = { frames: C.check(m.fr), sheet: C.check(cells) };
for (const v of Object.values(checks)) { assert.equal(v.iso, 0); assert.equal(v.holes, 0); }
const onePiece = { frames: m.fr.every(g => pieces(g).length === 1), sheet: cells.every(g => pieces(g).length === 1) };
assert.ok(onePiece.frames && onePiece.sheet);
// the eye (yellow and its pupil) in every cell of the sheet
const sx = (cell - w) / 2, sy = (cell - h) / 2;
const eyes = cells.map(g => { let yellow = 0; for (let y = Math.floor(m.face[1] + sy) - 2; y < m.face[1] + sy + m.face[3] + 2; y++) for (let x = Math.floor(m.face[0] + sx) - 2; x < m.face[0] + sx + m.face[2] + 2; x++) if (g[y]?.[x] === "B") yellow++; return yellow; });
assert.ok(eyes.every(n => n >= 1));
// room in the sky, as the panel places it
const round = v => { const a = Math.floor(v); return v - a === 0.5 ? (a % 2 ? a + 1 : a) : Math.round(v); };
const sky = cells.map(g => { const ys = g.map((r, y) => (/[^.]/.test(r) ? y : null)).filter(v => v !== null);
  return [Math.min(...ys) + round(3 + m.layoutHeight / 2 - cell / 2), Math.max(...ys) + round(65 - m.layoutHeight / 2 - cell / 2)]; });
assert.ok(sky.every(([a, b]) => a >= 0 && b < 69));
const transitions = m.fr.map((g, f) => g.reduce((n, r, y) => n + [...r].filter((c, x) => c !== m.fr[(f + 1) % N][y][x]).length, 0));
assert.ok(transitions.at(-1) <= Math.max(...transitions.slice(0, -1)));
C.previews("dragonite", m.pal, m.fr, m.fps, 4, OUT);
C.previews("dragonite_layout", m.pal, m.fr, m.fps, 1, OUT);
C.previews("dragonite_nativo", m.pal, m.nativeFrames, m.fps, 3, OUT);
fs.copyFileSync("dragonite_sheet.png", OUT + "/dragonite_hoja_definitiva.png");
execFileSync(process.execPath, ["rostro.js", "dragonite", String(cell), String(N), [Math.floor(m.face[0] + sx) - 3, Math.floor(m.face[1] + sy) - 3, 16, 16].join(","), "5", OUT + "/dragonite_rostros_24_celdas.png"]);
const report = { frames: N, fps: m.fps, seconds: N / m.fps, cell, angles: m.angles, canvas: [w, h], wingSweep: m.wingSweep, displayScale: m.displayScale,
  checks, onePiece, eyes, skyRange: [Math.min(...sky.map(v => v[0])), Math.max(...sky.map(v => v[1]))], transitions, loopEdge: transitions.at(-1),
  approvedBaseSha256: require("crypto").createHash("sha256").update(fs.readFileSync("dragonite_flight_approved.json")).digest("hex"),
  limits: ["La prueba holes solo detecta huecos unitarios", "Se muestra al 75 % por vecino mas proximo; el dibujo nativo queda en nativeFrames"] };
fs.writeFileSync(OUT + "/auditoria.json", JSON.stringify(report, null, 2) + "\n");
console.log(JSON.stringify({ ...report, transitions: undefined, eyes: Math.min(...eyes) + ".." + Math.max(...eyes) }));
