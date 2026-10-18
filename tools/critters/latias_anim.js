// Latias: a shallow bank on long swept wings; feet and lower tail follow the beat.
// Small chart: explicit grid period 3.49 px. Two isolated pale background cells are removed.
const C=require('./critter');
const {pal,rows}=C.load('latias');
rows[28][0]='.';
for(let x=35;x<50;x++)if(rows[22][x]==='D')rows[22][x]='.';
const N=8,pad=7,t=f=>f/N*2*Math.PI,bob=f=>Math.round(.65*Math.sin(t(f)));
const fr=C.frames(rows,{count:N,pad,ink:'BFN',parts:[
 {test:(x,y)=>y<=5,move:(f,x,y)=>[.7*Math.sin(t(f)-1.1)*Math.max(0,(5-y)/5),bob(f)]},
 {test:(x,y)=>x>=39&&y<=33,move:(f,x)=>[0,bob(f)+2*Math.sin(t(f))*Math.min(1,(x-39)/28)]},
 {test:(x,y)=>x<=18&&y>=25&&y<=34,move:(f,x)=>[0,bob(f)+1.6*Math.sin(t(f)-.2)*(18-x)/18]},
 {test:(x,y)=>x<=20&&y>=40,move:(f,x)=>[0,bob(f)+1.1*Math.sin(t(f)-1.4)*Math.max(0,(20-x)/14)]},
 {test:(x,y)=>x>=51&&y>=40,move:(f,x)=>[0,bob(f)+1.1*Math.sin(t(f)-1.4)*Math.min(1,(x-51)/13)]},
 {test:(x,y)=>y>=48&&x<=44,move:(f,x,y)=>[.8*Math.sin(t(f)-1.7)*(y-48)/4,bob(f)]},
 {test:()=>true,move:f=>[0,bob(f)]}
],seam:(x,y)=>(y>=4&&y<=7)||(x>=37&&x<=41&&y>=22&&y<=35)||(x>=16&&x<=21&&y>=25&&y<=35)||(y>=39&&y<=43)||(y>=46&&y<=49)});
console.log('latias',JSON.stringify(C.check(fr)));
module.exports={name:'latias',sealRotatedHoles:true,pal,rows,fr,fps:7,angles:[-4,-2,0,2,4],pose:0,pad,layoutHeight:59,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+3&&x<=pad+21&&y>=pad+8&&y<=pad+22};
