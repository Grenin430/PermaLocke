// Butterfree: seen from the front, a butterfly's beat. Each pair of wings (left and right) folds in towards the body and opens again,
// narrowing round its root as wings seen edge-on do, the tips lifting a little as they close; the body bobs a pixel against the beat and
// the antennae trail it. Big red eyes, mouth and feet ride on the body.
const C = require("./critter.js");
const { pal, rows } = C.load("butterfree");
const N = 8, t = f => (f / N) * 2 * Math.PI;
const isAntennae = (x, y) => y <= 17 && x >= 23 && x <= 36;
// the body: head and eyes above, then the trunk and the blue feet between the lower wings (which are lilac and white, not the body)
const isBody = (x, y, c) => y >= 16 && y <= 48 && (y < 33 ? x >= 21 && x <= 41 && "CEJFMKNDHPAOIL".includes(c) && !((x <= 22 || x >= 40) && "AOI".includes(c)) : x >= 22 && x <= 36 && !"BGNL".includes(c));
const wing = (x, y, c) => !isAntennae(x, y) && !isBody(x, y, c);
// fore wings down to their own lower edge (rows 33-35), hind wings from their top edge (row 36 left, 35 right)
const isLeft = (x, y, c) => x <= 30 && y < 36 && wing(x, y, c), isRight = (x, y, c) => x > 30 && y < 35 && wing(x, y, c);
const isLowL = (x, y, c) => x <= 30 && y >= 36 && wing(x, y, c), isLowR = (x, y, c) => x > 30 && y >= 35 && wing(x, y, c);
const close = f => 0.5 - 0.5 * Math.cos(t(f));               // 0 open as drawn, 1 folded
const bob = f => Math.round(0.9 * Math.sin(t(f) + 0.6));
const left = C.swing(rows, (x, y) => isLeft(x, y, rows[y][x] || "."), [24.5, 27.5], f => 7 * close(f), f => 1 - 0.42 * close(f));
const right = C.swing(rows, (x, y) => isRight(x, y, rows[y][x] || "."), [36.5, 27.5], f => -7 * close(f), f => 1 - 0.42 * close(f));
// the hind wings fold with the fore wings (so their edges stay together), round roots at the waist
const late = f => close(f);
const lowL = C.swing(rows, (x, y) => isLowL(x, y, rows[y][x] || "."), [22.5, 37.5], f => -5 * late(f), f => 1 - 0.4 * late(f));
const lowR = C.swing(rows, (x, y) => isLowR(x, y, rows[y][x] || "."), [36.5, 37.5], f => 5 * late(f), f => 1 - 0.4 * late(f));
const spec = {
  count: N, pad: 6, ink: "AOI",
  parts: [
    { test: isLowL, sample: (f, put) => lowL(f, (x, y, c) => put(x, y + bob(f), c)) },
    { test: isLowR, sample: (f, put) => lowR(f, (x, y, c) => put(x, y + bob(f), c)) },
    { test: isLeft, sample: (f, put) => left(f, (x, y, c) => put(x, y + bob(f), c)) },
    { test: isRight, sample: (f, put) => right(f, (x, y, c) => put(x, y + bob(f), c)) },
    { test: isAntennae, move: (f, x, y) => [Math.round(0.8 * Math.sin(t(f) - 1.6) * Math.max(0, (17 - y) / 17) * (x < 30 ? -1 : 1)), bob((f + N - 1) % N)] },
    { test: () => true, move: f => [0, bob(f)] },
  ],
  seam: (x, y) => (x >= 18 && x <= 44 && y >= 14) || (y >= 14 && y <= 20),
};
const fr = C.frames(rows, spec);
console.log("butterfree", JSON.stringify(C.check(fr)));
module.exports = { name: "butterfree", pal, fr, fps: 8, angles: [-6, -3, 0, 3, 6], pose: 0,
  protect: (g, x, y) => x >= 6 + 22 && x <= 6 + 40 && y >= 6 + 18 && y <= 6 + 30 && "EJ".includes(g[y][x]) };
