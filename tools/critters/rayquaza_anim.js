// Rayquaza: a travelling coil wave; horn tips and the distant tail trail behind the head.
const C=require('./critter');const {pal,rows}=C.load('rayquaza');
// Pure white inside the coils is the reference background. Lavender claws (M/P) remain.
for(const row of rows)for(let x=0;x<row.length;x++)if(row[x]==='B')row[x]='.';
const N=8,sourcePad=8,t=f=>f/N*2*Math.PI;
const wave=(f,x)=>Math.round(1.5*Math.sin(t(f)-(x-25)*.09)*Math.min(1,Math.max(0,(x-25)/42)));
const sourceFrames=C.frames(rows,{count:N,pad:sourcePad,ink:'AGK',parts:[
 {test:(x,y)=>y<18&&x<16,move:(f,x,y)=>[1.1*Math.sin(t(f)-.7)*(18-y)/18,0]},
 {test:(x,y)=>y<19&&x>=25,move:(f,x,y)=>[0,1.1*Math.sin(t(f)-1.1)*(19-y)/19]},
 {test:(x,y)=>x>=74&&y>=62,move:(f,x,y)=>[.8*Math.sin(t(f)-1.4)*(x-74)/14,wave(f,x)]},
 {test:()=>true,move:(f,x)=>[0,wave(f,x)]}
],seam:(x,y)=>(y>=16&&y<=20)||(x>=24&&x<=76)});
const displayScale=.75,pad=6,fr=require('./batch10_display')(sourceFrames,displayScale,'AGK',[[sourcePad+10,sourcePad+27,5,5,'G'],[sourcePad+10,sourcePad+27,5,5,'C']]);
console.log('rayquaza native',JSON.stringify(C.check(sourceFrames)));console.log('rayquaza display',JSON.stringify(C.check(fr)));
module.exports={name:'rayquaza',pal,rows,sourceFrames,fr,fps:7,angles:[-1,0,1],pose:0,pad,displayScale,layoutHeight:61,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+5&&x<=pad+17&&y>=pad+19&&y<=pad+33};
