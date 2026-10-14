// Relicanth: a heavy body, slowly paddling lobed fins and a delayed tail; the closed eye stays intact.
const C=require('./critter');
const {pal,rows}=C.load('relicanth');
const N=8,pad=7,t=f=>f/N*2*Math.PI;
const fr=C.frames(rows,{count:N,pad,ink:'DFM',parts:[
 {test:(x,y)=>x>=25&&x<=35&&y<=17,move:(f,x,y)=>[Math.round(Math.sin(t(f)-.6)*Math.max(0,(18-y)/18)),0]},
 {test:(x,y)=>x>=36&&y<=28,move:(f,x)=>[0,Math.round(1.5*Math.sin(t(f)-1.1)*Math.min(1,(x-36)/14))]},
 {test:(x,y)=>x>=37&&y>=30,move:(f,x)=>[0,Math.round(1.4*Math.sin(t(f)+1)*Math.min(1,(x-37)/12))]},
 {test:(x,y)=>x>=31&&y>=36,move:(f,x)=>[0,Math.round(1.2*Math.sin(t(f)-.5)*Math.min(1,(x-31)/12))]},
 {test:(x,y)=>x>=23&&y>=39,move:(f,x,y)=>[Math.round(1.2*Math.sin(t(f)-1.8)*Math.min(1,(y-39)/8)),0]},
 {test:()=>true}
],seam:(x,y)=>(y>=16&&y<=19&&x>=24&&x<=36)||(x>=34&&x<=38&&y<=29)||(x>=29&&x<=39&&y>=30&&y<=40)||(x>=21&&x<=26&&y>=37&&y<=42)});
console.log('relicanth',JSON.stringify(C.check(fr)));
module.exports={name:'relicanth',pal,rows,fr,fps:6,angles:[16,22,28,34,40],pose:28,pad,sealRotatedHoles:true,
 protect:(g,x,y)=>g[y][x]!=='.'&&((x>=pad+8&&x<=pad+16&&y>=pad+33&&y<=pad+38)||(x>=pad&&x<=pad+9&&y>=pad+39&&y<=pad+44)||(x>=pad+21&&x<=pad+25&&y>=pad+25&&y<=pad+29))};
