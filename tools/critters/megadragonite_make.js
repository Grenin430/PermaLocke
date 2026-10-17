// Writes Mega Dragonite's flight (2026-10-16, the approved version: «v2») from Dragonite's approved one: megadragonite_flight_pose.js is
// dragonite_flight_pose_v6.js with the Mega's own parts drawn into the source sprite before it is leaned into flight, and
// megadragonite_flight_anim.js is dragonite_approved_flight.js on that pose (the same beat, bob and 75 % reduction; no check against
// Dragonite's approved base, which it no longer is). Drawn from the official design, not from the reference in imagenes/megas (its author does
// not allow reposting it): the antennae become two head-sized wings of cream feathers on the crown (a tall plume and a broader arched wing with
// its feather tips hanging), the small teal wings on the back stay and beat as Dragonite's, the horn on the forehead is white, a teal orb ringed
// in violet sits at the tip of the tail. node megadragonite_make.js
const fs = require("fs");
const swap = (s, a, b) => { if (s.split(a).length !== 2) throw new Error("not once: " + a.slice(0, 60)); return s.replace(a, () => b); };

let pose = fs.readFileSync("dragonite_flight_pose_v6.js", "utf8");
pose = swap(pose, `const { pal, rows } = C.load("dragonite");`, `const src = C.load("dragonite");
const pal = { ...src.pal, Q: "8E4FA8" };
const rows = src.rows.map(r => [...r, ...Array(24).fill(".")]);
// the feathered wings on the head, in place of the antennae: a tall plume rises back from the left of the crown and a broader wing arches
// back over it to the right, its feather tips hanging; cream, a white upper edge, beige below, each with an ink outline
const inHeadTop = (x, y) => y <= 10 && x >= 0 && x <= 46;
rows.forEach((r, y) => r.forEach((c, x) => { if (y <= 10 && ((x >= 3 && x <= 11) || (x >= 30 && x <= 35)) && c !== ".") r[x] = "."; }));
const segDist = (x, y, [ax, ay], [bx, by]) => { const vx = bx - ax, vy = by - ay, k = Math.max(0, Math.min(1, ((x - ax) * vx + (y - ay) * vy) / (vx * vx + vy * vy))); return [Math.hypot(x - ax - vx * k, y - ay - vy * k), k, (x - ax) * vy - (y - ay) * vx]; };
const put = (x, y, c) => { if (inHeadTop(x, y) && rows[y]?.[x] !== undefined) rows[y][x] = c; };
// back to front: the arched wing's hanging feathers, its arch, then the tall plume
const FEATHERS = [
  [[30, 7], [40, 9], 1.6], [[29, 6], [43, 6], 1.8], [[27, 5], [44, 2], 2.0],
  [[24, 6], [38, 1], 2.4],
  [[16, 8], [9, 0], 2.2], [[18, 8], [13, 0], 1.8],
];
for (const [base, tip, width] of FEATHERS) {
  const body = [];
  for (let y = 0; y <= 10; y++) for (let x = 0; x <= 46; x++) {
    const [d, k, side] = segDist(x + 0.5, y + 0.5, base, tip);
    const w = width * (k < 0.8 ? 1 : 1 - (k - 0.8) / 0.2 * 0.6);
    if (d <= w) body.push([x, y, d, side]);
  }
  for (const [x, y] of body) for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) if (rows[y + dy]?.[x + dx] === ".") put(x + dx, y + dy, "H");
  for (const [x, y, d, side] of body) put(x, y, side < 0 && d > 0.9 ? "B" : side > 0 && d > 0.8 ? "E" : "L");
}
// the horn on the forehead white
for (let y = 3; y <= 8; y++) for (let x = 17; x <= 24; x++) { const c = rows[y][x]; if (c === "C") rows[y][x] = "B"; else if (c === "M" || c === "N" || c === "F") rows[y][x] = "K"; }
// the orb at the tail's tip: teal, lit from the upper left, a violet ring, an ink outline where it meets the sky
const OX = 58, OY = 28;
for (let y = OY - 6; y <= OY + 6; y++) for (let x = OX - 6; x <= OX + 6; x++) {
  const d = Math.hypot(x + 0.5 - OX, y + 0.5 - OY);
  if (d <= 3.2) rows[y][x] = x + y < OX + OY - 2 ? "D" : x + y > OX + OY + 2 ? "O" : "G";
  else if (d <= 4.2) rows[y][x] = "Q";
  else if (d <= 5 && rows[y][x] === ".") rows[y][x] = "H";
}`);
// the head's piece reaches over the feathered wings
pose = swap(pose, `const isHead = (x, y) => ((y <= 27 && x >= 14 && x <= 31) || (y <= 10 && x >= 5 && x <= 34)) && !(x <= 13 && y >= 13);`,
  `const isHead = (x, y) => ((y <= 27 && x >= 14 && x <= 31) || (y <= 10 && x >= 0 && x <= 46)) && !(x <= 13 && y >= 13);`);
// more canvas on top
pose = swap(pose, `const W = 160, H = 110, TOP = 12;`, `const W = 160, H = 110, TOP = 34;`);
pose = pose.replace("// Dragonite only: flight base v6.", "// Mega Dragonite (written by megadragonite_make.js from dragonite_flight_pose_v6.js). Dragonite's flight base v6.");
fs.writeFileSync("megadragonite_flight_pose.js", pose);

let anim = fs.readFileSync("dragonite_approved_flight.js", "utf8");
anim = swap(anim, `P = require("./dragonite_flight_pose_v6");`, `P = require("./megadragonite_flight_pose");`);
anim = swap(anim, `const approved = require("./dragonite_flight_approved.json");`, `const approved = P.build();`);
anim = swap(anim, `approved.rows.forEach((r, y) => [...r].forEach((c, x) => assert.equal(full[0][oy + y][ox + x], c, \`frame 0 differs at \${x},\${y}\`)));`, ``);
anim = swap(anim, `full[0].forEach((r, y) => r.forEach((c, x) => {
  if (c !== "." && !(y >= oy && y < oy + approved.rows.length && x >= ox && x < ox + approved.rows[0].length)) assert.fail("frame 0 outside the base");
}));`, ``);
anim = swap(anim, `console.log("dragonite",`, `console.log("megadragonite",`);
anim = swap(anim, `name: "dragonite", pal: approved.pal,`, `name: "megadragonite", pal: P.pal,`);
anim = "// Mega Dragonite's flight, written by megadragonite_make.js from dragonite_approved_flight.js.\n" + anim;
fs.writeFileSync("megadragonite_flight_anim.js", anim);
console.log("written");
