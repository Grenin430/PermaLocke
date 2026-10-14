// Seaking: a broad flowing tail, rippling dorsal sail and two delayed ventral fins.
// Every column of a fin moves whole; its root stays joined to the reference outline.
const C=require('./critter');
const {pal,rows}=C.load('seaking');
const N=8,pad=7,t=f=>f/N*2*Math.PI,bob=f=>Math.round(.65*Math.sin(t(f)));
const dorsal=(x,y)=>x>=11&&x<=38&&y<=(x<20?14:x<28?11:Math.min(17,12+(x-28)*.7));
const fr=C.frames(rows,{count:N,pad,ink:'CFH',parts:[
 {test:(x,y)=>x>=39&&y<=34,move:(f,x)=>[0,bob(f)+2.4*Math.sin(t(f)-.16*(x-39))*Math.min(1,(x-39)/19)]},
 {test:dorsal,move:(f,x,y)=>[1.1*Math.sin(t(f)-.6)*Math.max(0,(17-y)/17),bob(f)]},
 {test:(x,y)=>x>=29&&y>=35,move:(f,x,y)=>[1.6*Math.sin(t(f)-1.4)*Math.min(1,(y-35)/14),bob(f)]},
 {test:(x,y)=>x<=14&&y>=40,move:(f,x)=>[0,bob(f)+1.3*Math.sin(t(f)+.8)*Math.max(0,(14-x)/10)]},
 {test:()=>true,move:f=>[0,bob(f)]}
],seam:(x,y)=>(x>=37&&x<=41)||(y>=10&&y<=18&&x>=12)||(y>=33&&y<=38&&x>=28)||(y>=38&&y<=42&&x<=15)});
console.log('seaking',JSON.stringify(C.check(fr)));
module.exports={name:'seaking',sealRotatedHoles:true,pal,rows,fr,fps:7,angles:[-6,-3,0,3,6],pose:0,pad,
 protect:(g,x,y)=>g[y][x]!=='.'&&((x>=pad+16&&x<=pad+25&&y>=pad+24&&y<=pad+34)||(x>=pad+4&&x<=pad+13&&y>=pad+31&&y<=pad+38))};
