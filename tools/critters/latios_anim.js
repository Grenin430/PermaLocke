// Latios: swept wings bank lightly; lower tail and both feet trail on separate phases.
// Keep the reconstructed 74x74 chart intact and reduce only display frames to 80 percent.
const C=require('./critter');
const {pal,rows}=C.load('latios');
const N=8,sourcePad=10,t=f=>f/N*2*Math.PI;
const sourceFrames=C.frames(rows,{count:N,pad:sourcePad,ink:'BIKP',parts:[
 {test:(x,y)=>y<=9,move:(f,x,y)=>[.7*Math.sin(t(f)-1.2)*Math.max(0,(9-y)/9),0]},
 {test:(x,y)=>x<=33&&y<=44,move:(f,x)=>[0,2*Math.sin(t(f))*Math.max(0,(33-x)/33)]},
 {test:(x,y)=>x>=50&&y<=45,move:(f,x)=>[0,2.2*Math.sin(t(f)-.2)*Math.min(1,(x-50)/23)]},
 {test:(x,y)=>x<=30&&y>=56,move:(f,x)=>[0,1.2*Math.sin(t(f)-1.4)*Math.max(0,(30-x)/17)]},
 {test:(x,y)=>x>=32&&x<=47&&y>=59,move:(f,x,y)=>[Math.sin(t(f)-1.1)*Math.min(1,(y-59)/14),0]},
 {test:(x,y)=>x>=54&&y>=54,move:(f,x,y)=>[1.1*Math.sin(t(f)-1.6)*Math.min(1,(y-54)/14),0]},
 {test:()=>true}
],seam:(x,y)=>(y>=7&&y<=11)||(x>=31&&x<=36&&y>=34&&y<=46)||(x>=48&&x<=52&&y>=30&&y<=47)||(y>=54&&y<=60)});
const displayScale=.8,pad=sourcePad*displayScale;
const scaled=sourceFrames.map(g=>Array.from({length:Math.floor(g.length*displayScale)},(_,y)=>Array.from({length:Math.floor(g[0].length*displayScale)},(_,x)=>g[Math.floor((y+.5)/displayScale)][Math.floor((x+.5)/displayScale)])));
for(const g of scaled)for(let y=1;y<g.length-1;y++)for(let x=1;x<g[0].length-1;x++)if(g[y][x]==='.'){
 const near=[g[y][x-1],g[y][x+1],g[y-1][x],g[y+1][x]];if(near.every(c=>c!=='.'))g[y][x]=near.find(c=>!'BIKP'.includes(c))||near[0];
}
// Forward-map the red iris and its one-cell white shine after reduction: centre sampling skips that source row.
scaled.forEach((g,f)=>{for(let y=16;y<=19;y++)for(let x=38;x<=42;x++){
 const c=sourceFrames[f][y+sourcePad][x+sourcePad];
 if('DMC'.includes(c))g[Math.floor((y+sourcePad+.5)*displayScale)][Math.floor((x+sourcePad+.5)*displayScale)]=c;
}});
const fr=scaled.map(g=>g.map(r=>r.join('')));
console.log('latios native',JSON.stringify(C.check(sourceFrames)));
console.log('latios display',JSON.stringify(C.check(fr)));
module.exports={name:'latios',pal,rows,sourceFrames,fr,fps:7,angles:[-2,0,2],pose:0,pad,displayScale,layoutHeight:61,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+26&&x<=pad+36&&y>=pad+10&&y<=pad+21};
