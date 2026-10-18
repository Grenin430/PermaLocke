// node ppm.js <module> <out.ppm>: writes the base grid a module exports ({ pal, rows }, or an animation's first frame) as a PPM on the water colour, to compare with the reference.
const fs = require("fs");
const [mod, out] = process.argv.slice(2);
const m = require(`./${mod}`), pal = m.pal;
// an animation module: its first frame, trimmed to the drawing
const rows = m.rows ?? (g => { const ys = g.map((r, y) => (/[^.]/.test(r) ? y : -1)).filter(y => y >= 0), xs = g.flatMap(r => [...r].map((c, x) => (c !== "." ? x : -1))).filter(x => x >= 0); return g.slice(ys[0], ys.at(-1) + 1).map(r => [...r.slice(Math.min(...xs), Math.max(...xs) + 1)]); })(m.fr[0]);
const h = rows.length, w = Math.max(...rows.map(r => r.length)), px = [];
for (const r of rows) for (let x = 0; x < w; x++) { const ch = r[x] || "."; px.push(...(ch === "." ? [0x2a, 0x6a, 0x90] : [0, 2, 4].map(i => parseInt(pal[ch].slice(i, i + 2), 16)))); }
fs.writeFileSync(out, Buffer.concat([Buffer.from(`P6 ${w} ${h} 255\n`), Buffer.from(px)]));
