// Wishiwashi: a small quick swimmer, with the tail beat following the dorsal and paired ventral fins.
const C=require('./critter');
const {pal,rows}=C.load('wishiwashi');
const N=8,pad=6,t=f=>f/N*2*Math.PI,bob=f=>Math.round(.6*Math.sin(t(f)));
const fr=C.frames(rows,{count:N,pad,ink:'FAH',parts:[
 {test:(x,y)=>x>=33,move:(f,x)=>[0,bob(f)+Math.round(1.8*Math.sin(t(f)-.7)*Math.min(1,(x-33)/9))]},
 {test:(x,y)=>y<=5&&x>=15,move:(f,x,y)=>[Math.round(.9*Math.sin(t(f)+.7)*Math.max(0,(6-y)/6)),bob(f)]},
 {test:(x,y)=>y>=19&&x<=12,move:(f,x,y)=>[Math.round(Math.sin(t(f)+.4)*Math.max(0,(y-19)/5)),bob(f)]},
 {test:(x,y)=>y>=19&&x>=15,move:(f,x,y)=>[Math.round(1.2*Math.sin(t(f)-1)*Math.max(0,(y-19)/5)),bob(f)]},
 {test:()=>true,move:f=>[0,bob(f)]}
],seam:(x,y)=>(x>=31&&x<=35)||(y>=4&&y<=7&&x>=14)||(y>=17&&y<=21)});
console.log('wishiwashi',JSON.stringify(C.check(fr)));
module.exports={name:'wishiwashi',pal,rows,fr,fps:10,angles:[-6,-3,0,3,6],pose:0,pad,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad&&x<=pad+16&&y>=pad+6&&y<=pad+15};
