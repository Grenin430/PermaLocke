// Flight correction; anatomy and original-palette textures are defined separately.
const m = require('./flight_rig').build(require('./flight_batch1_specs').spearow);

// Under the chin the head kept its own black outline when it was joined to the chest: inside the drawing it ran as a black bar between the
// red throat and the body, and at the size the panel shows it that bar read as the head cut off from the body. Those inner black pixels
// (all four neighbours drawn, rows 26-29 under the head) become the dark brown of the chest; the outline on the outside stays.
const soften = g => g.map((r, y) => [...r].map((c, x) => {
  if (c !== 'C' || x < 8 || x > 14 || y < 26 || y > 29) return c;
  return [g[y - 1][x], g[y + 1][x], g[y][x - 1], g[y][x + 1]].every(k => k !== '.') ? 'F' : c;
}).join(''));
m.fr = m.fr.map(soften);
module.exports = m;
