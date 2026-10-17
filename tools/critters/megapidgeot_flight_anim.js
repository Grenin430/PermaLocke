// Mega Pidgeot's flight (2026-10-16): Pidgeot's approved rig and wing beat (flight_batch1_specs.pidgeot), with what makes the Mega its own, after
// imagenes/megas/mega_pidgeot.webp: the crest drawn out far behind it with a third, blue plume; the outer flight feathers tipped blue; the tail
// longer, its feathers crimson tipped blue. The head is Pidgeot's (megapidgeot.txt is pidgeot.txt with the new colours added).
const fs = require("fs");
const base = require("./flight_batch1_specs").pidgeot;
const block = s => s.trim().split("\n").map(r => r.trim());
const unblock = rows => "\n" + rows.join("\n");

// the palette: Pidgeot's, and blues and a crimson
if (!fs.existsSync("megapidgeot.txt") || !fs.readFileSync("megapidgeot.txt", "utf8").includes("Q=")) {
  const [pal, ...rows] = fs.readFileSync("pidgeot.txt", "utf8").trim().split("\n");
  fs.writeFileSync("megapidgeot.txt", pal.trim() + " Q=4E86D8 R=2E4E8E S=A8C8F0 T=B8304A\n" + rows.join("\n") + "\n");
}

// stretches a texture along x by a factor (nearest cell), so a plume grows longer without growing thicker
const stretch = (rows, k) => rows.map(r => Array.from({ length: Math.round(r.length * k) }, (_, x) => r[Math.floor(x / k)] || ".").join(""));

// the crest: half as long again and more; its long lower edge a blue plume
const crest = stretch(block(base.crest), 1.7).map((r, y, all) => y >= all.length - 3 ? r.replace(/[HD]/g, (c, x) => (x > 18 ? (y === all.length - 1 ? "R" : "Q") : c)) : r);

// the wing: the outer feathers (beyond the middle of the span) tipped blue
const wing = block(base.wing).map(r => [...r].map((c, x) => (x >= 20 && c === "M" ? "Q" : x >= 20 && c === "D" ? "R" : x >= 27 && c === "A" ? "R" : c)).join(""));

// the tail: longer, crimson feathers with blue tips
const tail = stretch(block(base.tail), 1.5).map(r => [...r].map((c, x, a) => (c === "N" ? "T" : c === "E" ? "N" : c === "H" && x > a.length * 0.6 ? "Q" : c)).join(""));

const spec = {
  ...base, name: "megapidgeot", canvas: [150, 74],
  crest: unblock(crest), wing: unblock(wing), tail: unblock(tail),
  shade: { ...base.shade, Q: "R", T: "N" },
};
module.exports = require("./flight_rig").build(spec);
