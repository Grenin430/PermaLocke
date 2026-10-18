// Crobat: four wings bend from fixed roots; the lower pair follows the upper pair with a phase delay.
// JPEG fringe cells outside the mouth are pale background, not white anatomy.
const C=require('./critter');
const {pal,rows}=C.load('crobat');
for(let y=0;y<rows.length;y++)for(let x=0;x<rows[0].length;x++)
 if(rows[y][x]==='B'&&!(x>=30&&x<=41&&y>=28&&y<=32))rows[y][x]='.';
const N=8,pad=8,t=f=>f/N*2*Math.PI,bob=f=>Math.round(-Math.sin(t(f)));
const upperLeft=(x,y)=>x<=29&&y<=32;
const upperRight=(x,y)=>x>=49&&y<=32;
const lowerLeft=(x,y)=>x<=28&&y>=33;
const lowerRight=(x,y)=>x>=49&&y>=35;
function bend(root,span,amp,phase) {
 return (f,put,at,mine)=>{
  for(let x=0;x<rows[0].length;x++) {
   const dy=Math.round(amp*Math.sin(t(f)+phase)*Math.min(1,Math.abs(x-root)/span));
   for(let y=-7;y<rows.length+7;y++)if(mine(x,y-dy)&&at(x,y-dy)!=='.')put(x,y+bob(f),at(x,y-dy));
  }
 };
}
const fr=C.frames(rows,{count:N,pad,ink:'AGMNO',parts:[
 {test:upperLeft,sample:bend(29,28,5,0)},
 {test:upperRight,sample:bend(49,37,5,0.2)},
 {test:lowerLeft,sample:bend(28,20,3,-1.2)},
 {test:lowerRight,sample:bend(49,21,3,-1.0)},
 {test:()=>true,move:f=>[0,bob(f)]}
],seam:(x,y)=>(x>=26&&x<=32&&y>=26&&y<=39)||(x>=46&&x<=52&&y>=27&&y<=41)});
console.log('crobat',JSON.stringify(C.check(fr)));
module.exports={name:'crobat',layoutHeight:61,pal,rows,fr,fps:11,angles:[-8,-4,0,4,8],pose:0,pad,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+28&&x<=pad+45&&y>=pad+24&&y<=pad+33};
