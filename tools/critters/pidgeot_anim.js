// Pidgeot: the perched reference keeps its outline; folded wing, long crest, tail and toes move separately.
const C=require('./critter');
const {pal,rows}=C.load('pidgeot');
// White behind the head and among the crest tips is trapped background; eye and pale claws remain.
for(let y=0;y<60;y++)for(let x=20;x<rows[0].length;x++)if(rows[y][x]==='B')rows[y][x]='.';
const N=8,pad=7,t=f=>f/N*2*Math.PI;
const crestStart=rows.map((row,y)=>{const x=row.findIndex((c,x)=>x>=22&&'EN'.includes(c));return x<0?49:x-2;});
const fr=C.frames(rows,{count:N,pad,ink:'CPI',parts:[
 {test:(x,y)=>y>=13&&y<=44&&x>=crestStart[y],move:(f,x)=>[0,Math.round(1.3*Math.sin(t(f)-1)*Math.max(0,(x-28)/26))]},
 {test:(x,y)=>y<=20,move:(f,x,y)=>[Math.round(.7*Math.sin(t(f))*Math.max(0,(21-y)/21)),0]},
 {test:(x,y)=>y>=59,move:(f,x,y)=>[Math.round(.8*Math.sin(t(f)-.9)*Math.min(1,(y-59)/7)),0]},
 {test:(x,y)=>x>=34&&y>=45,move:(f,x)=>[0,Math.round(Math.sin(t(f)-1.4)*Math.max(0,(x-34)/14))]},
 {test:(x,y)=>x>=16&&x<=36&&y>=21&&y<=43,move:(f,x)=>[0,-Math.round((1-Math.cos(t(f)))/2*Math.min(1,(x-16)/17))]},
 {test:()=>true}
],seam:(x,y)=>(y>=19&&y<=23)||(x>=14&&x<=19&&y>=20&&y<=44)||(y>=41&&y<=45)||(y>=57&&y<=61)});
console.log('pidgeot',JSON.stringify(C.check(fr)));
module.exports={name:'pidgeot',pal,rows,fr,fps:8,angles:[-1,0,1],pose:0,pad,layoutHeight:61,sealRotatedHoles:true,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+2&&x<=pad+20&&y>=pad+9&&y<=pad+20};
