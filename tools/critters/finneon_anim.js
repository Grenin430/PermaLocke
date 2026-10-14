// Finneon: the two tail lobes ripple from their narrow root; the dorsal fin trails the body's beat.
// All masks follow the chart's existing outlines. Each column moves whole to preserve the fin thickness.
const C = require('./critter');
const { pal, rows } = C.load('finneon');
const N = 8, pad = 5, phase = f => f / N * 2 * Math.PI;
const bob = f => Math.round(Math.sin(phase(f)) * 0.7);
const tail = (x,y) => x >= 25 && y <= 28;
const dorsal = (x,y) => x >= 19 && x <= 26 && y <= 17 && !tail(x,y);
const side = (x,y) => x >= 14 && x <= 22 && y >= 29;
const fr = C.frames(rows, {
  count:N, pad, ink:'CHK',
  parts:[
    {test:tail,move:(f,x)=>[0,bob(f)+1.7*Math.sin(phase(f)-(x-25)*0.23)*Math.min(1,(x-25)/9)]},
    {test:dorsal,move:(f,x,y)=>[Math.round(1.0*Math.sin(phase(f)-0.9)*Math.max(0,(17-y)/17)),bob(f)]},
    {test:side,move:(f,x)=>[0,bob(f)+Math.round(0.8*Math.sin(phase(f)+0.7)*Math.max(0,(x-14)/8))]},
    {test:()=>true,move:f=>[0,bob(f)]}
  ],
  seam:(x,y)=>(x>=22&&x<=28)||(x>=17&&y>=15&&y<=19)||(x>=13&&x<=24&&y>=28)
});
console.log('finneon',JSON.stringify(C.check(fr)));
module.exports={sealRotatedHoles:true,name:'finneon',pal,rows,fr,fps:9,angles:[0,4,8,12,16,20,24],pose:12,pad,
  protect:(g,x,y)=>x>=pad+6&&x<=pad+11&&y>=pad+25&&y<=pad+31&&g[y][x]!=='.'};
