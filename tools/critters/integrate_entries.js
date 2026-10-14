// Puts the entries that build_batch.js wrote (batch_entries.json) into src/PermaLocke.App/Views/Cap/CapCritterArt.cs, replacing the old
// entry of each species (the hand-drawn one or a previous sheet one). Safe to run again: the result is the same.
// Run from tools/critters after build_batch.js:  node integrate_entries.js
const fs = require("fs");
const path = require("path");
const file = path.join(__dirname, "../../src/PermaLocke.App/Views/Cap/CapCritterArt.cs");
const entries = JSON.parse(fs.readFileSync(path.join(__dirname, "batch_entries.json"), "utf8"));
let s = fs.readFileSync(file, "utf8");
const crlf = s.includes("\r\n");
s = s.replace(/\r\n/g, "\n");
for (const { id, entry } of entries) {
  // The last catalog entry can omit its trailing comma.
  // an old hand-drawn entry spans several lines up to its closing """) or """, "up", 3); a sheet entry is one line with Sheet:
  const sheetLine = new RegExp(String.raw`            new\(${id}, "([^"]+)",[^\n]*Sheet:[^\n]*\n`);
  const drawn = new RegExp(String.raw`            new\(${id}, "([^"]+)"[\s\S]*?"""(, "(up|sym)", \d+)?\),?\n`);
  const m = s.match(sheetLine) ?? s.match(drawn);
  if (!m) throw new Error("no entry for species " + id);
  s = s.replace(m[0], `            new(${id}, "${m[1]}", CritterMotion.Fish, "", "", ${entry}),\n`);
  console.log(`${m[1]} (${id}) -> sheet`);
}
fs.writeFileSync(file, crlf ? s.replace(/\n/g, "\r\n") : s);
