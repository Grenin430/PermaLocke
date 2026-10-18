// Writes the finished Gimmighoul (tools/clouds/gimmighoul_usuario.json) as CapGimmighoulArt.cs: rows of keys and the colour of each key.
// node tools/clouds/gimmighoul_cs.js <art.json>
const fs = require("fs"), path = require("path");
const art = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
const { pal } = art;
// trimmed to the drawing; the row it stands on (foot) follows the trim
const ys = art.rows.map((r, y) => (/[^.]/.test(r) ? y : -1)).filter(y => y >= 0);
const xs = [...art.rows[0]].map((_, x) => (art.rows.some(r => r[x] !== ".") ? x : -1)).filter(x => x >= 0);
const rows = art.rows.slice(ys[0], ys.at(-1) + 1).map(r => r.slice(xs[0], xs.at(-1) + 1));
const foot = (art.foot ?? ys.at(-1)) - ys[0];
const used = [...new Set(rows.join("").replace(/\./g, ""))].sort();
const cs = [
  "namespace PermaLocke.App.Views;",
  "",
  "/// <summary>",
  "/// Gimmighoul in its chest, upright, with glasses and a snorkel (2026-10-18): the player's own pixel sprite (32 x 40 grid), read cell by cell,",
  "/// with the glasses and the snorkel added (tools/clouds/gimmighoul_usuario.json, gimmighoul_cs.js; do not edit by hand).",
  "/// </summary>",
  "internal static class CapGimmighoulArt",
  "{",
  `    public const string Keys = "${used.join("")}";`,
  "",
  "    /// <summary>The row it stands on: the bottom of the box (the strap's end hangs lower).</summary>",
  `    public const int Foot = ${foot};`,
  "",
  `    public static readonly uint[] Colours = [${used.map(k => "0x" + pal[k]).join(", ")}];`,
  "",
  "    public static readonly string[] Rows =",
  "    [",
  ...rows.map(r => `        "${r}",`),
  "    ];",
  "}",
  "",
];
fs.writeFileSync(path.join(__dirname, "../../src/PermaLocke.App/Views/Cap/CapGimmighoulArt.cs"), cs.join("\n"));
console.log(rows[0].length + "x" + rows.length, used.join(""));
