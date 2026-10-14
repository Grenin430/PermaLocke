// Wailord: a slow body lift, forked tail sway and alternating strokes of the small flippers.
const C=require('./critter');const {pal,rows}=C.load('wailord');
// Pale samples outside the upper silhouette are JPEG/background residue; belly and back patches remain.
for(let y=0;y<24;y++)for(let x=0;x<rows[y].length;x++)if(rows[y][x]==='A')rows[y][x]='.';
const N=8,pad=8,t=f=>f/N*2*Math.PI,bob=f=>Math.round(.7*Math.sin(t(f)));
const fr=C.frames(rows,{count:N,pad,ink:'BPEI',parts:[
 {test:(x,y)=>x>=60&&y<=14,move:(f,x,y)=>[1.6*Math.sin(t(f)-1.1)*Math.max(0,(14-y)/14),bob(f)]},
 {test:(x,y)=>x>=75&&y>=21&&y<=35,move:(f,x)=>[0,bob(f)+1.3*Math.sin(t(f)+Math.PI)*Math.max(0,(x-75)/11)]},
 {test:(x,y)=>x>=69&&y>=37&&y<=43,move:(f,x)=>[0,bob(f)+1.6*Math.sin(t(f)-.4)*Math.max(0,(x-69)/17)]},
 {test:(x,y)=>x>=68&&y>=45&&y<=55,move:(f,x)=>[0,bob(f)+1.8*Math.sin(t(f)-1.1)*Math.max(0,(x-68)/16)]},
 {test:()=>true,move:f=>[0,bob(f)]}
],seam:(x,y)=>(y>=12&&y<=16&&x>=58)||(x>=73&&x<=77&&y>=19&&y<=37)||(x>=66&&x<=71&&y>=35&&y<=57)});
console.log('wailord',JSON.stringify(C.check(fr)));
module.exports={name:'wailord',pal,rows,fr,fps:5,angles:[-4,0,4],pose:0,pad,layoutHeight:84,
 protect:(g,x,y)=>g[y][x]==='B'&&x>=pad+49&&x<=pad+55&&y>=pad+37&&y<=pad+43};
