// Pidgey flight pilot: a newly posed torso, tucked feet, spread tail and articulated open wings.
// The original head and palette are retained; this module does not mutate the approved perched sprite.
const C=require('./critter');
const original=C.load('pidgey'),pal=original.pal,W=72,H=64;
const block=s=>s.trim().split('\n').map(r=>r.trimEnd());
// Feather groups follow the horizontal chest volume: warm throat, scalloped breast and shaded belly.
const torso=block(`
........AAKKKAA..................
......AKKLLLLKKAAA...............
....AKKLLOOOOLLKKKAA.............
..AKKLLLOOOOLLLKKKKKAA...........
.AKKKLLOOOOLLLKKKKKKKKAA.........
AKKHHCCCCCCKKKKKKKKKKKKKA........
ACCCCHHHHHCCCKKKKKKKKKKKKJ.......
ACCCHHHHHHHCCCCKKKKKKKKKKCJ......
ACHHHHHHHHHHHHCCCCCCCCCCCCJ......
AHHHHHHHHHHHHHHCCCCCCCCCCCJ......
.AHLHHHHHOLHHHHHCCCCCCLCCCJ......
.AHLOHHHHCLHHHHHCCCCCOLCCCJ......
..AACAHHHHACCHHCCCCCCCLCCJ.......
....ACACCCHLACCCCCCCCLJJ.........
......DACCCACCCCCCOCJJ...........
........DDCJJJJJJJ..............`);
const tail=block(`
..........AAA.AAA...
.......AALLLLALLLA..
....AALLLOOOLLOOOOD.
..AKKLLLOOLLOOOODD..
AKKLLLOOLLOOOKDD....
PKKKLLLLOOOKDDLLOD..
.PKKKKKKKDDKKLOOOD..
..PKKKKDDKKKKKDDD...
...PDDD..DDDDD.....`);
// Open wing in span/chord coordinates: coverts, secondaries and five separated primary tips.
const wing=block(`
....................AAA.........
...............AAAALLLOAA.......
..........AAAALLLLLOOOOLLA......
......AAKKLLLOOOOOOOOLLLLLOD.....
...AKKLLLOOOOOOOOLLLLLOOODDD.....
.AKKLLOOOOLLLLKKLLOOOODDLLLOD....
AKKLLOOLLLKKLLOOOOOODDLOOOOODD...
AKKLLLKKKLLOOOOODDDLOOOOODD.....
AKKKKKLLOOOOODDDLOOOOODD........
.PKKKLLOOODDDKKLOOOODD..........
..PKKLOODDJKLLLOOODD............
...PKKDDJKKLLOOODD..............
....PPJKKLOOODD.................
......PJKLLDD...................
........PDD.....................`);
const farFoot=block(`
JFFJ......
.JFIJ.....
..JFIJJ...
...JFFFEJ.
....JDDDBD`);
const nearFoot=block(`
JFFJ.......
.JFIJ....J.
..JFIJJJFB.
...JFFFFEJ.
....JFFJBD.
.....JD....`);
const poses=[
 [[27,29],[34,20],[38,10],[42,3],1],
 [[27,29],[36,22],[43,15],[49,10],1],
 [[27,29],[37,27],[47,24],[56,23],1],
 [[27,29],[34,34],[40,43],[44,51],1],
 [[27,29],[31,36],[32,46],[33,56],.95],
 [[27,29],[32,33],[37,40],[42,45],.70],
 [[27,29],[34,27],[40,23],[44,19],.55],
 [[27,29],[32,22],[34,13],[35,7],.75]
];
const fr=[],parts=[];
function empty(){return Array.from({length:H},()=>Array(W).fill('.'));}
function blit(g,rows,ox,oy,shift=()=>[0,0]){rows.forEach((r,y)=>[...r].forEach((c,x)=>{if(c==='.')return;const [dx,dy]=shift(x,y),xx=ox+x+Math.round(dx),yy=oy+y+Math.round(dy);if(g[yy]?.[xx]!==undefined)g[yy][xx]=c;}));}
function triangle(g,a,b,c,ta,tb,tc,shade){
 const den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1]);if(Math.abs(den)<.001)return;
 const x0=Math.max(0,Math.floor(Math.min(a[0],b[0],c[0]))),x1=Math.min(W-1,Math.ceil(Math.max(a[0],b[0],c[0])));
 const y0=Math.max(0,Math.floor(Math.min(a[1],b[1],c[1]))),y1=Math.min(H-1,Math.ceil(Math.max(a[1],b[1],c[1])));
 for(let y=y0;y<=y1;y++)for(let x=x0;x<=x1;x++){
  const u=((b[1]-c[1])*(x+.5-c[0])+(c[0]-b[0])*(y+.5-c[1]))/den,v=((c[1]-a[1])*(x+.5-c[0])+(a[0]-c[0])*(y+.5-c[1]))/den,w=1-u-v;
  if(Math.min(u,v,w)<-.00001)continue;
  const sx=Math.floor(u*ta[0]+v*tb[0]+w*tc[0]),sy=Math.floor(u*ta[1]+v*tb[1]+w*tc[1]);const ch=wing[sy]?.[sx];
  if(ch&&ch!=='.')g[y][x]=shade[ch]||ch;
 }
}
function drawWing(g,pose,far){
 const pts=pose.slice(0,4).map(([x,y])=>far?[24+(x-27)*.55,27+(y-29)*.88]:[x,y]);
 const stations=[0,10,20,32],spread=pose[4]*(far?.72:1),edges=[];
 for(let j=0;j<4;j++){
  const a=pts[Math.max(0,j-1)],b=pts[Math.min(3,j+1)],len=Math.hypot(b[0]-a[0],b[1]-a[1]);
  const nx=-(b[1]-a[1])/len,ny=(b[0]-a[0])/len;
  edges.push([-7,8].map(v=>[pts[j][0]+nx*v*spread,pts[j][1]+ny*v*spread]));
 }
 const shade=far?{L:'K',O:'K',K:'A',M:'L',A:'J'}:{};
 for(let j=0;j<3;j++){
  const uv=[[stations[j],0],[stations[j+1],0],[stations[j+1],15],[stations[j],15]],p=[edges[j][0],edges[j+1][0],edges[j+1][1],edges[j][1]];
  triangle(g,p[0],p[1],p[2],uv[0],uv[1],uv[2],shade);triangle(g,p[0],p[2],p[3],uv[0],uv[2],uv[3],shade);
 }
}
function seal(g){let n=0;for(let y=1;y<H-1;y++)for(let x=1;x<W-1;x++)if(g[y][x]==='.'){
 const a=[g[y-1][x],g[y+1][x],g[y][x-1],g[y][x+1]];if(a.every(c=>c!=='.')){g[y][x]=a.find(c=>!'DJP'.includes(c))||a[0];n++;}
}return n;}
let sealed=0;
for(let f=0;f<poses.length;f++){
 const g=empty(),far=empty(),near=empty(),t=f/poses.length*Math.PI*2;
 drawWing(far,poses[f],true);drawWing(near,poses[f],false);
 blit(g,far,0,0);blit(g,tail,38,28,(x)=>[0,Math.sin(t-.8)*Math.max(0,x/19)]);
 blit(g,farFoot,32,37);blit(g,torso,15,24);blit(g,nearFoot,38,36);
 blit(g,near,0,0);
 // Cover the common shoulder root with newly drawn overlapping coverts, not a detached rotating cutout.
 blit(g,block(`.AKKA.\nAKLLKA\nAKLLKP\n.PKKP.`),24,27);
 const head=original.rows.slice(0,17).map((r,y)=>r.map((c,x)=>x<(y<15?18:12)?c:'.').join(''));
 blit(g,head,7,13);
 sealed+=seal(g);fr.push(g.map(r=>r.join('')));parts.push({near:near.map(r=>r.join('')),far:far.map(r=>r.join(''))});
}
console.log('pidgey flight',JSON.stringify({...C.check(fr),sealed}));
module.exports={torso,torsoOrigin:[15,24],name:'pidgey_flight',pal,fr,parts,poses,rows:fr[0],fps:12,angles:[-4,0,4],pose:0,pad:0,layoutHeight:58,
 protect:(g,x,y)=>x>=14&&x<=22&&y>=21&&y<=26&&'DBJ'.includes(g[y][x])};
