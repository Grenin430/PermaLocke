// Pelipper: slow, broad strokes of the hanging wings. The bill and pouch retain the reference pose.
// The near wing is cut along its white edge, then inverse-sampled with a bend that vanishes at its attachment.
const C=require('./critter');
const {pal,rows}=C.load('pelipper');
const N=8,pad=7,t=f=>f/N*2*Math.PI;
const bob=f=>Math.round(0.8*Math.sin(t(f)));
const left=(x,y)=>x<=21&&y>=43;
const edges=rows.map((r,y)=>{const x=r.findIndex((c,x)=>x>=38&&c==='B');return x<0?43:x-1;});
const right=(x,y)=>y>=30&&x>=(y<=48?edges[y]:43);
function bend(top,root,span,amp,delay) {
 return (f,put,at,owns)=>{
  const displacement=(x,y)=>amp*Math.sin(t(f)+delay)*Math.min(1,Math.max(0,Math.abs(x-root)/span))*Math.min(1,Math.max(0,(y-top)/12));
  for(let y=top-5;y<rows.length+6;y++) for(let x=0;x<rows[0].length;x++) {
   let sy=y;for(let i=0;i<4;i++)sy=y-displacement(x,sy);
   const Y=Math.round(sy);if(owns(x,Y)&&at(x,Y)!=='.')put(x,y+bob(f),at(x,Y));
  }
 };
}
const fr=C.frames(rows,{
 count:N,pad,ink:'DFG',parts:[
  {test:left,sample:bend(43,20,18,3,0.25)},

  {test:(x,y)=>!right(x,y),move:f=>[0,bob(f)]},
  {test:right,sample:bend(30,40,20,3.5,0)}
 ],seam:(x,y)=>(x>=36&&x<=45&&y>=28&&y<=49)||(x>=8&&x<=23&&y>=41&&y<=48)
});
console.log('pelipper',JSON.stringify(C.check(fr)));
module.exports={name:'pelipper',pal,rows,fr,fps:7,angles:[-6,-3,0,3,6],pose:0,pad,
 protect:(g,x,y)=>g[y][x]!=='.'&&((x>=pad+22&&x<=pad+31&&y>=pad+5&&y<=pad+14)||(x>=pad+5&&x<=pad+34&&y>=pad+16&&y<=pad+23&&'BJ'.includes(g[y][x])))};
