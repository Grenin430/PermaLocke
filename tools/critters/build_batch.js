// Builds the sheets of a batch and the C# entries for them. node build_batch.js horsea goldeen ...
const fs = require("fs");
const C = require("./critter.js");
const APP = "C:/Users/javie/Desktop/Permalocke singleplayer/src/PermaLocke.App/Views/Cap/Critters/";
// species number, motion of the path: depth drift, short bob, push per beat
const META = {
  aerodactyl: { id: 142, drift: 3, bob: 0.5, surge: 0.9 },
  togekiss: { id: 468, drift: 5, bob: 1, surge: 0.7 },
  pidgeot: { id: 18, drift: 1, bob: 0.3, surge: 0.5 },
  relicanth: { id: 369, drift: 4, bob: 1, surge: 0.6 },
  wishiwashi: { id: 746, drift: 7, bob: 2, surge: 1.1 },
  altaria: { id: 334, drift: 2, bob: 0.5, surge: 0.5 },
  toucannon: { id: 733, drift: 3, bob: 0.8, surge: 0.6 },
  kingdra: { id: 230, drift: 4, bob: 1.5, surge: 0.5 },
  lanturn: { id: 171, drift: 5, bob: 2, surge: 0.6 },
  remoraid: { id: 223, drift: 6, bob: 1.5, surge: 1.0 },
  skarmory: { id: 227, drift: 2, bob: 0.5, surge: 0.8 },
  pikipek: { id: 731, drift: 7, bob: 2, surge: 0.6 },
  lumineon: { id: 457, drift: 6, bob: 1.5, surge: 0.6 },
  seadra: { id: 117, drift: 4, bob: 2, surge: 0.7 },
  luvdisc: { id: 370, drift: 5, bob: 1.5, surge: 0.5 },
  crobat: { id: 169, drift: 9, bob: 2.5, surge: 1.2 },
  pelipper: { id: 279, drift: 7, bob: 2, surge: 0.8 },
  manaphy: { id: 490, drift: 4, bob: 2.5, surge: 0.5 },
  milotic: { id: 350, drift: 5, bob: 1, surge: 0.7 },
  finneon: { id: 456, drift: 6, bob: 1.5, surge: 0.8 },
  horsea: { id: 116, drift: 5, bob: 3, surge: 0.5 },
  goldeen: { id: 118, drift: 8, bob: 1.5, surge: 1.4 },
  lapras: { id: 131, drift: 4, bob: 1, surge: 1.0 },
  wingull: { id: 278, drift: 10, bob: 1.5, surge: 0.8 },
  zubat: { id: 41, drift: 7, bob: 3.5, surge: 1.0 },
  gyarados: { id: 130, drift: 6, bob: 1.5, surge: 1.2 },
  mantine: { id: 226, drift: 7, bob: 2, surge: 0.9 },
  chinchou: { id: 170, drift: 4, bob: 3, surge: 0.4 },
  pidgey: { id: 16, drift: 9, bob: 2.5, surge: 1.0 },
  butterfree: { id: 12, drift: 8, bob: 3.5, surge: 0.6 },
  sharpedo: { id: 319, drift: 7, bob: 1.5, surge: 1.6 },
  wailmer: { id: 320, drift: 4, bob: 1.5, surge: 0.6 },
  qwilfish: { id: 211, drift: 5, bob: 2.5, surge: 0.5 },
  spearow: { id: 21, drift: 10, bob: 2, surge: 1.2 },
  swablu: { id: 333, drift: 8, bob: 3, surge: 0.6 },
};
const lines = [];
for (const name of process.argv.slice(2)) {
  const m = require(`./${name}_anim.js`), meta = META[name];
  const w = m.fr[0][0].length, h = m.fr[0].length;
  const maxTilt = Math.max(...m.angles.map(Math.abs)) * Math.PI / 180;
  let cell = Math.ceil(Math.max(w * Math.cos(maxTilt) + h * Math.sin(maxTilt), h * Math.cos(maxTilt) + w * Math.sin(maxTilt)));
  cell += cell % 2;
  const pivot = [w / 2, h / 2];
  const { cells } = C.sheet(name, m.pal, m.fr, m.angles, pivot, cell, m.protect);
  if (m.sealRotatedHoles) require("./seal_sheet")(name, m.pal, cells);
  // the drawing's real height (rows with something in them), for the room it needs
  const filled = m.layoutHeight ?? m.fr[0].filter(r => /[^.]/.test(r)).length;
  fs.copyFileSync(`${name}_sheet.png`, APP + `${name}.png`);
  lines.push({ id: meta.id, entry: `Fps: ${m.fps}, Sheet: "${name}", Cell: ${cell}, Angles: [${m.angles.join(", ")}], Pose: ${m.pose}, Height: ${filled}, Drift: ${meta.drift}, Bob: ${meta.bob}, Surge: ${meta.surge}` });
  console.log(`${name}: frames ${m.fr.length}, ${w}x${h}, cell ${cell}, tilts ${m.angles.length}`);
}
fs.writeFileSync("batch_entries.json", JSON.stringify(lines));
