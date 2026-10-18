// Moltres: column-bent wings carry their flames; the tall tail flame ripples from its root.
const C=require('./critter');const {pal,rows}=C.load('moltres');
for(let y=0;y<40;y++)for(let x=0;x<rows[y].length;x++)if(rows[y][x]==='B')rows[y][x]='.';
const N=8,sourcePad=7,t=f=>f/N*2*Math.PI,down=f=>(1-Math.cos(t(f)))/2;
const tail=(x,y)=>x>=32&&((x<=58&&y<=24)||(x<=53&&y<=31)||(x<=48&&y<=40));
const shift=(f,y)=>1.5*Math.sin(t(f)-.11*(40-y))*Math.max(0,(40-y)/40);
const sourceFrames=C.frames(rows,{count:N,pad:sourcePad,ink:'CFK',parts:[
 {test:tail,sample:(f,put,at,mine)=>{for(let y=-2;y<=43;y++)for(let x=28;x<=63;x++){const sx=Math.round(x-shift(f,y));if(mine(sx,y))put(x,y,at(sx,y));}}},
 {test:(x,y)=>x<=23&&y<=38,move:(f,x,y)=>[2*down(f)*Math.max(0,(38-y)/38),0]},
 {test:(x,y)=>x>=46&&y<=46,move:(f,x)=>[0,3*down(f)*(x-46)/40]},
 {test:(x,y,c)=>x<=21&&y>=37&&y<=50&&'GNP'.includes(c),move:(f,x,y)=>[.8*Math.sin(t(f)-1)*Math.max(0,(50-y)/13),0]},
 {test:(x,y)=>y>=58&&x<=28,move:(f,x,y)=>[.8*Math.sin(t(f)-.8)*(y-58)/11,0]},
 {test:(x,y)=>y>=59&&x>=37,move:(f,x,y)=>[.8*Math.sin(t(f)-1.4)*(y-59)/10,0]},
 {test:()=>true}
],seam:(x,y)=>(x>=21&&x<=25&&y<=41)||(x>=44&&x<=48&&y<=47)||(y>=38&&y<=43)||(y>=56&&y<=61)||(x>=18&&x<=23&&y>=42&&y<=51)});
const displayScale=6/7,pad=6,fr=require('./batch10_display')(sourceFrames,displayScale,'CFK',[[sourcePad+12,sourcePad+48,4,6,'BCJK'],[sourcePad+18,sourcePad+43,4,3,'BC']]);
console.log('moltres native',JSON.stringify(C.check(sourceFrames)));console.log('moltres display',JSON.stringify(C.check(fr)));
module.exports={name:'moltres',sealRotatedHoles:true,pal,rows,sourceFrames,fr,fps:8,angles:[-2,0,2],pose:0,pad,displayScale,layoutHeight:61,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+9&&x<=pad+20&&y>=pad+35&&y<=pad+48};
