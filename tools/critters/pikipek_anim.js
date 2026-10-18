// Pikipek: a perched pose with a small head nod, a folded-wing flutter and toes following the body's motion.
const C=require('./critter');
const {pal,rows}=C.load('pikipek');
const N=8,pad=6,t=f=>f/N*2*Math.PI,bob=f=>Math.round(.6*Math.sin(t(f)));
const fr=C.frames(rows,{count:N,pad,ink:'DNHO',parts:[
 {test:(x,y)=>y<=23,move:f=>[Math.round(.6*Math.sin(t(f)-.4)),bob(f)]},
 {test:(x,y)=>x>=17&&y>=25&&y<=33,move:(f,x)=>[0,bob(f)-Math.round(1.4*(1+Math.sin(t(f)))/2*Math.min(1,(x-17)/11))]},
 {test:(x,y)=>y>=35,move:(f,x,y)=>[Math.round(.8*Math.sin(t(f)-.9)*Math.max(0,(y-35)/6)),bob(f)]},
 {test:()=>true,move:f=>[0,bob(f)]}
],seam:(x,y)=>(y>=22&&y<=26)||(x>=15&&x<=20&&y>=25&&y<=34)||(y>=33&&y<=37)});
console.log('pikipek',JSON.stringify(C.check(fr)));
module.exports={name:'pikipek',pal,rows,fr,fps:9,angles:[-6,-3,0,3,6],pose:0,pad,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad&&x<=pad+23&&y>=pad+13&&y<=pad+23};
