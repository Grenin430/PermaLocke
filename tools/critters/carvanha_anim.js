// Carvanha: short tail strokes and paired fins; the tall dorsal bends from its base.
// The reference grid is 4.34 px; auto detection picks its double.
const C=require('./critter');
const {pal,rows}=C.load('carvanha');
const N=8,pad=6,t=f=>f/N*2*Math.PI,bob=f=>Math.round(.65*Math.sin(t(f)));
const fr=C.frames(rows,{count:N,pad,ink:'GNI',parts:[
 {test:(x,y)=>x>=31&&(y<=23||y>=30),move:(f,x)=>[0,bob(f)+1.7*Math.sin(t(f)-.18*(x-31))*Math.min(1,(x-31)/9)]},
 {test:(x,y)=>y<=18&&x<=28,move:(f,x,y)=>[Math.sin(t(f)-.7)*Math.max(0,(18-y)/18),bob(f)]},
 {test:(x,y)=>y>=40,move:(f,x,y)=>[1.1*Math.sin(t(f)-1.4)*Math.min(1,(y-40)/10),bob(f)]},
 {test:(x,y)=>x<=8&&y>=24&&y<=28,move:(f,x)=>[0,bob(f)+1.2*Math.sin(t(f)+.5)*(8-x)/8]},
 {test:(x,y)=>x>=35&&y>=24&&y<=28,move:(f,x)=>[0,bob(f)+1.3*Math.sin(t(f)-.3)*(x-35)/12]},
 {test:()=>true,move:f=>[0,bob(f)]}
],seam:(x,y)=>(y>=16&&y<=20&&x<=29)||(x>=29&&x<=35)||(y>=38&&y<=42)||(y>=23&&y<=29&&(x<=10||x>=33))});
console.log('carvanha',JSON.stringify(C.check(fr)));
module.exports={name:'carvanha',pal,rows,fr,fps:10,angles:[-6,-3,0,3,6],pose:0,pad,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+9&&x<=pad+29&&y>=pad+21&&y<=pad+36};
