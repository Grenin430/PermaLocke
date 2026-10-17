// Shiny sparkles drawn into a critter's frames (2026-10-16, Mega Latias and Mega Latios are shiny): four-pointed stars round the drawing,
// each growing from a dot to a long cross (with a pale gold ring of corners at its largest) and back on its own beat, white with pale gold arms. Pixel art: no blur, no glow.
// require("./sparkle")(critterModule) returns the module with the stars added to every frame and their two colours to the palette.
const SIZE = [0, 1, 2, 3, 4, 3, 2, 0]; // over eight beats
// where they shine, as fractions of the drawing's box, and the beat each starts on
const SPOTS = [[0.12, 0.18, 0], [0.82, 0.06, 2], [0.95, 0.62, 4], [0.38, 0.92, 6], [0.55, 0.3, 5]];

module.exports = m => {
  const pal = { ...m.pal, S: "FFFFFF", T: "FFF2A8" };
  let x0 = 1e9, x1 = -1, y0 = 1e9, y1 = -1;
  m.fr[0].forEach((r, y) => [...r].forEach((c, x) => { if (c !== ".") { x0 = Math.min(x0, x); x1 = Math.max(x1, x); y0 = Math.min(y0, y); y1 = Math.max(y1, y); } }));
  const n = m.fr.length;
  const fr = m.fr.map((rows, f) => {
    const g = rows.map(r => [...r]);
    const set = (x, y, c) => { if (g[y]?.[x] !== undefined) g[y][x] = c; };
    for (const [fx, fy, start] of SPOTS) {
      const s = SIZE[Math.floor(((f / n) * 8 + start)) % 8];
      if (!s) continue;
      const cx = Math.round(x0 + fx * (x1 - x0)), cy = Math.round(y0 + fy * (y1 - y0));
      for (let k = 1; k <= s; k++) for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) set(cx + dx * k, cy + dy * k, k < s || s === 1 ? "S" : "T");
      if (s >= 3) for (const [dx, dy] of [[1, 1], [-1, 1], [1, -1], [-1, -1]]) set(cx + dx, cy + dy, "T");
      set(cx, cy, "S");
    }
    return g.map(r => r.join(""));
  });
  return { ...m, pal, fr };
};
