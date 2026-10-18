// Shiny Tapu Koko (2026-10-18): the sprite the player gave (imagenes/tapu_koko/sprite_referencia.png, 96x96 at x3.75:
// `node recon_plain.js <png> tapukoko 20 3.75`, then the white round it flooded away) in the shiny colours of the official art they gave
// (imagenes/tapu_koko/arte_variocolor.png): the two shell halves and the feathered fins on them black, their white teeth kept; the crest a
// salmon orange, the yellow tips of its feathers too; the orange of the body and the tail feathers turned the same salmon.
// Writes tapukoko_shiny.txt.
const fs = require("fs");
const C = require("./critter");
const { pal, rows } = C.load("tapukoko");

// the shells' yellows become black, lit a little on their top
const shell = { P: "5E5A68", F: "48444F", L: "38343F", T: "2B2731", I: "1F1B24", N: "131016" };
// the crest's yellow tips become orange
const crest = { P: "FFC08E", F: "FB9C68", L: "E8834E", T: "E07442", I: "B85432", N: "5A2D0C" };
// the orange, a little more salmon
const orange = { C: "F2784C", Q: "F89C6E", O: "C25A3C", J: "B86452" };
const inCrest = (x, y) => y <= 20 && x >= 26 && x <= 60;

const out = { ...pal, ...orange };
// new letters for the crest's colours and the shells' (the old letters keep the black)
const crestKeys = { P: "a", F: "b", L: "c", T: "d", I: "e", N: "f" };
for (const [k, v] of Object.entries(crest)) out[crestKeys[k]] = v;
for (const [k, v] of Object.entries(shell)) out[k] = v;
// the brown outlines on the shells go black; next to the orange they stay
const warm = new Set(["C", "Q", "O", "J"]);
const nearOrange = (x, y) => [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, -1], [1, -1], [-1, 1]].some(([dx, dy]) => warm.has(rows[y + dy]?.[x + dx]));
out.h = "140F14";
const recoloured = rows.map((r, y) => r.map((c, x) => (inCrest(x, y) && crestKeys[c] ? crestKeys[c] : c === "H" && !inCrest(x, y) && !nearOrange(x, y) ? "h" : c)).join(""));
fs.writeFileSync("tapukoko_shiny.txt", Object.entries(out).map(([k, v]) => `${k}=${v}`).join(" ") + "\n" + recoloured.join("\n") + "\n");
console.log("tapukoko_shiny", recoloured[0].length + "x" + recoloured.length);
