const fs = require("fs");
let s = fs.readFileSync("anim.js", "utf8");
function rep(a, b) { if (!s.includes(a)) throw new Error("no " + a.slice(0, 50)); s = s.replace(a, b); }
rep("const PADR = 4, PADB = 2, CW = BW + PADR, CH = BH + PADB;", "const PADT = 2, PADR = 4, PADB = 3, CW = BW + PADR, CH = PADT + BH + PADB;");
rep("// ---- the body layer:", `const SEAM = {13:18,14:19,15:20,16:21,17:21,18:20,19:20,20:20,21:19,22:18,23:17,24:17,25:17,26:17,27:17,28:16,29:14,30:12,31:13,32:14,33:15,34:16,35:17,36:17,37:18,38:18,39:19,40:20,41:20};
const isHead = (x, y) => y < 13 ? true : y > 41 ? false : x < SEAM[y] || ("CJA".includes(at(x, y)) && x <= SEAM[y] + 1);
// head leads, the body follows a frame later: the wave runs from the head back to the tail
const REAR = [0, 0, -1, -1, 0, 0, 1, 1], HEAD = [0, -1, -1, 0, 0, 1, 1, 0];
// ---- the body layer:`);
rep("  const out = Array.from", "  const hd = HEAD[f], rd = REAR[f];\n  const out = Array.from");
rep("const put = (x, y, c) => { if (c !== \".\" && y >= 0", "const put = (x, y, c) => { y += PADT; if (c !== \".\" && y >= 0");
rep("const srcY = Math.round(y - lift * Math.max(0, u));", "const srcY = Math.round(y - rd - lift * Math.max(0, u));");
rep("for (let y = 0; y < CH; y++) for (let x = rootX - 2; x < CW; x++) {", "for (let y = -PADT; y < CH; y++) for (let x = rootX - 2; x < CW; x++) {");
rep("  body.forEach((r, y) => r.forEach((c, x) => put(x, y, c)));", "  body.forEach((r, y) => r.forEach((c, x) => put(x, y + (isHead(x, y) ? hd : rd), c)));");
rep("if (isPelvic(x, y)) put(x + Math.round(pel * Math.max(0, y - 40) / 8), y, at(x, y));", "if (isPelvic(x, y)) put(x + Math.round(pel * Math.max(0, y - 40) / 8), y + rd, at(x, y));");
rep("if (isPec(x, y)) put(x, y + Math.round(fin * Math.max(0, 28 - x) / 8), at(x, y));", "if (isPec(x, y)) put(x, y + rd + Math.round(fin * Math.max(0, 28 - x) / 8), at(x, y));");
rep("    put(x + Math.round(sway * Math.max(0, y - start) / 15), y, at(x, y));", "    put(x + Math.round(sway * Math.max(0, y - start) / 15), y + hd, at(x, y));");
s = s.replace(/anim_frames\.txt/g, "anim2_frames.txt").replace(/`anim_\$\{i\}/g, "`anim2_${i}");
fs.writeFileSync("anim2.js", s);
console.log("ok");
