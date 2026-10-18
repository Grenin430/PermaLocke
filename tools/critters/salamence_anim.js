// Salamence: reference membranes fold from fixed shoulders, with tail and feet following.
// Preserve the native grid; the approved display reduction is 5/6 for this tall pose.
const C=require('./critter');
const {pal,rows}=C.load('salamence');
for(let y=0;y<rows.length;y++)for(let x=0;x<rows[y].length;x++){
 const c=rows[y][x];
 if(('CFNI'.includes(c)&&x>=32&&x<=55&&y>=13&&y<=35)||('CNF'.includes(c)&&((x<=7&&y<=12)||(x<=25&&y>=40&&y<=48)||(x>=66&&y>=40&&y<=45)||(y>=69))))rows[y][x]='.';
}
const N=8,sourcePad=6,t=f=>f/N*2*Math.PI,down=f=>(1-Math.cos(t(f)))/2;
const front=(x,y)=>y<=42&&(x>=50||(x>=39&&y<=14)||(x>=39&&y>=30));
const rear=(x,y,c)=>x>=14&&x<=49&&y<=44&&'AJGHLD'.includes(c)&&!(x<27&&y>=18&&y<=32);
const sourceFrames=C.frames(rows,{count:N,pad:sourcePad,ink:'DHL',parts:[
 {test:front,move:(f,x)=>[0,4*down(f)*Math.max(0,(x-39)/39)]},
 {test:rear,move:(f,x,y)=>[1.3*Math.sin(t(f)-.2)*Math.max(0,(39-y)/39),0]},
 {test:(x,y)=>x<=18&&y>=44&&y<=55,move:(f,x)=>[0,1.5*Math.sin(t(f)-1.3)*(18-x)/18]},
 {test:(x,y)=>y>=59,move:(f,x,y)=>[.8*Math.sin(t(f)-(x<35?.6:1.5))*Math.min(1,(y-59)/10),0]},
 {test:()=>true}
],seam:(x,y)=>(x>=37&&x<=52&&y<=44)||(y>=38&&y<=45)||(x>=16&&x<=20&&y>=43&&y<=57)||(y>=57&&y<=61)});
const displayScale=5/6,pad=sourcePad*displayScale;
const scaled=sourceFrames.map(g=>Array.from({length:Math.floor(g.length*displayScale)},(_,y)=>Array.from({length:Math.floor(g[0].length*displayScale)},(_,x)=>g[Math.floor((y+.5)/displayScale)][Math.floor((x+.5)/displayScale)])));
for(const g of scaled)for(let y=1;y<g.length-1;y++)for(let x=1;x<g[0].length-1;x++)if(g[y][x]==='.'){
 const near=[g[y][x-1],g[y][x+1],g[y-1][x],g[y+1][x]];if(near.every(c=>c!=='.'))g[y][x]=near.find(c=>!'DHL'.includes(c))||near[0];
}
const fr=scaled.map(g=>g.map(r=>r.join('')));
console.log('salamence native',JSON.stringify(C.check(sourceFrames)));
console.log('salamence display',JSON.stringify(C.check(fr)));
module.exports={name:'salamence',sealRotatedHoles:true,pal,rows,sourceFrames,fr,fps:7,angles:[-2,0,2],pose:0,pad,displayScale,layoutHeight:61,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+4&&x<=pad+22&&y>=pad+12&&y<=pad+29};
