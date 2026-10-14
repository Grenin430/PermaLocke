// Manaphy: upright paddling, with the arms answering one another and the long antenna trailing behind the head.
// Bending by whole columns keeps the shoulders and the narrow antenna connected without turning attached pieces away from their roots.
const C=require('./critter');
const {pal,rows}=C.load('manaphy');
const N=8,pad=6,t=f=>f/N*2*Math.PI;
const bob=f=>Math.round(0.8*Math.sin(t(f)));
const antenna=(x,y)=>(y<=7&&x>=14)||(x>=40&&y<=14);
const left=(x,y)=>x<=12&&y>=26&&y<=33;
const right=(x,y)=>x>=24&&y>=28&&y<=34;
const fr=C.frames(rows,{
 count:N,pad,ink:'DGP',parts:[
  {test:antenna,move:(f,x)=>[0,bob(f)+Math.round(1.8*Math.sin(t(f)-1.0-(x-14)*0.06)*Math.max(0,(x-14)/39))]},
  {test:left,move:(f,x)=>[0,bob(f)+Math.round(2.0*Math.sin(t(f))*Math.max(0,(12-x)/12))]},
  {test:right,move:(f,x)=>[0,bob(f)+Math.round(2.0*Math.sin(t(f)+Math.PI)*Math.min(1,Math.max(0,(x-24)/11)))]},
  {test:()=>true,move:f=>[0,bob(f)]}
 ],seam:(x,y)=>(y>=5&&y<=10&&x>=13&&x<=28)||(y>=26&&y<=34&&((x>=10&&x<=15)||(x>=22&&x<=27)))
});
console.log('manaphy',JSON.stringify(C.check(fr)));
module.exports={name:'manaphy',pal,rows,fr,fps:8,angles:[-6,-3,0,3,6],pose:0,pad,
 protect:(g,x,y)=>x>=pad+7&&x<=pad+26&&y>=pad+16&&y<=pad+25&&g[y][x]!=='.'};
