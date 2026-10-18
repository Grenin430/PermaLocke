// Luvdisc: the two lobes of its flat body flex gently behind a steady face.
// Whole-column offsets keep the edge continuous without inventing fins absent from the reference.
const C=require('./critter');
const {pal,rows}=C.load('luvdisc');
const N=8,pad=5,t=f=>f/N*2*Math.PI,bob=f=>Math.round(0.6*Math.sin(t(f)));
const fr=C.frames(rows,{count:N,pad,ink:'DMFN',parts:[
 {test:(x,y)=>x>=12&&y<=16,move:(f,x)=>[0,bob(f)+Math.round(Math.sin(t(f)-0.6)*Math.max(0,(x-12)/9))]},
 {test:(x,y)=>x>=12&&y>=19,move:(f,x)=>[0,bob(f)+Math.round(Math.sin(t(f)+0.6)*Math.max(0,(x-12)/9))]},
 {test:()=>true,move:f=>[0,bob(f)]}
],seam:(x,y)=>x>=11&&x<=14});
console.log('luvdisc',JSON.stringify(C.check(fr)));
module.exports={sealRotatedHoles:true,name:'luvdisc',pal,rows,fr,fps:7,angles:[-8,-4,0,4,8],pose:0,pad,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad&&x<=pad+11&&y>=pad+12&&y<=pad+21};
