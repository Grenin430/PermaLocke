// Milotic: a coiled serpent, with a slow wave along the body and a late ripple through the tail fan.
// The long feelers bend from their roots. The cream highlights are retained; enclosed white between the coil and tassel is background.
const C = require('./critter');
const { pal, rows } = C.load('milotic');
for(let y=42;y<=59;y++) for(let x=13;x<=26;x++) if(rows[y][x]==='C') rows[y][x]='.';
for(let y=0;y<rows.length;y++) for(let x=55;x<rows[y].length;x++) if(rows[y][x]==='C') rows[y][x]='.';
const N=8,pad=6,t=f=>f/N*2*Math.PI;
const left=(x,y)=>x<=20&&y<=22;
const right=(x,y)=>x>=34&&x<=53&&y<=23;
const fan=(x,y)=>x>=56&&y<=38;
const wave=(f,x)=>Math.round(1.6*Math.sin(t(f)-(x-28)*0.08)*Math.min(1,Math.abs(x-28)/28));
const fr=C.frames(rows,{
  count:N,pad,ink:'DIJF',
  parts:[
    {test:left,move:(f,x,y)=>[Math.round(1.1*Math.sin(t(f)-1.2)*Math.max(0,(22-y)/22)),0]},
    {test:right,move:(f,x,y)=>[Math.round(1.1*Math.sin(t(f)-1.8)*Math.max(0,(23-y)/23)),0]},
    {test:fan,move:(f,x,y)=>[Math.round(1.2*Math.sin(t(f)-1.1-(38-y)*0.12)*Math.max(0,(38-y)/27)),wave(f,60)]},
    {test:()=>true,move:(f,x)=>[0,wave(f,x)]}
  ],seam:(x,y)=>(y>=20&&y<=25)||(x>=53&&x<=66&&y>=33&&y<=42)
});
console.log('milotic',JSON.stringify(C.check(fr)));
module.exports={sealRotatedHoles:true,name:'milotic',pal,rows,fr,fps:7,angles:[-6,-3,0,3,6],pose:0,pad,
  protect:(g,x,y)=>x>=pad+19&&x<=pad+31&&y>=pad+24&&y<=pad+32&&g[y][x]!=='.'};
