// The Gracidea flower (see shaymin_anim.js), turned through twelve angles so the film can spin it as it falls.
const { sheet, clean } = require("./shaymin_anim");
const { pal, rows } = clean("gracidea");
module.exports = sheet("gracidea", rows, pal, 1, () => [0, 0], [0, 30, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330], 1);
