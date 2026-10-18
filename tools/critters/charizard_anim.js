// Charizard: wing membranes bend from the shoulders; the original flame ripples above the tail.
// Remove trapped checkerboard cells, retaining the eye, tooth and claws. Native frames stay available.
const C=require('./critter');
const {pal,rows}=C.load('charizard');
for(let y=18;y<=49;y++)for(let x=0;x<rows[y].length;x++)if('BP'.includes(rows[y][x])&&!(y===22&&x===26))rows[y][x]='.';
const N=8,sourcePad=8,t=f=>f/N*2*Math.PI,down=f=>(1-Math.cos(t(f)))/2;
const flame=(x,y,c)=>x>=43&&y<=20&&'ADNO'.includes(c);
const stem=(x,y)=>x>=44&&x<=54&&y>=17&&y<=32;
const left=(x,y)=>x<=27&&y>=13&&y<=55&&(x<=19||y>=28&&y<=35)&&!(x>=12&&y>=43);
const right=(x,y)=>x>=55&&y>=13&&y<=50;
const sourceFrames=C.frames(rows,{count:N,pad:sourcePad,ink:'EGK',parts:[
 {test:flame,move:(f,x,y)=>[1.1*Math.sin(t(f)*2)*Math.min(1,Math.max(0,(20-y)/16)),0]},
 {test:stem,move:(f,x,y)=>[.7*Math.sin(t(f)-1.2)*Math.max(0,(32-y)/15),0]},
 {test:left,move:(f,x)=>[0,3*down(f)*Math.max(0,(27-x)/27)]},
 {test:right,move:(f,x)=>[0,2.5*down(f)*Math.max(0,(x-55)/20)]},
 {test:(x,y)=>x<=18&&y>=54&&y<=68,move:(f,x,y)=>[.9*Math.sin(t(f)-1.4)*Math.max(0,(68-y)/14),0]},
 {test:(x,y)=>x>=56&&y>=44&&y<=52,move:(f,x,y)=>[.65*Math.sin(t(f)-.8)*(y-44)/8,0]},
 {test:()=>true}
],seam:(x,y)=>(x>=42&&x<=58&&y<=34)||(x>=17&&x<=29&&y>=26&&y<=57)||(x>=53&&x<=58&&y>=30&&y<=52)||(x<=20&&y>=52&&y<=69)});
const displayScale=.75,pad=sourcePad*displayScale;
const scaled=sourceFrames.map(g=>Array.from({length:Math.floor(g.length*displayScale)},(_,y)=>Array.from({length:Math.floor(g[0].length*displayScale)},(_,x)=>g[Math.floor((y+.5)/displayScale)][Math.floor((x+.5)/displayScale)])));
for(const g of scaled)for(let y=1;y<g.length-1;y++)for(let x=1;x<g[0].length-1;x++)if(g[y][x]==='.'){
 const near=[g[y][x-1],g[y][x+1],g[y-1][x],g[y+1][x]];if(near.every(c=>c!=='.'))g[y][x]=near.find(c=>!'EGK'.includes(c))||near[0];
}
// Reduction can detach one-cell flame tips; remove only unsupported isolated samples.
for(const g of scaled){const src=g.map(r=>[...r]);for(let y=0;y<g.length;y++)for(let x=0;x<g[y].length;x++){
 if(src[y][x]==='.')continue;
 if(![-1,0,1].some(dy=>[-1,0,1].some(dx=>(dx||dy)&&src[y+dy]?.[x+dx]&&src[y+dy][x+dx]!=='.')))g[y][x]='.';
}}
const fr=scaled.map(g=>g.map(r=>r.join('')));
console.log('charizard native',JSON.stringify(C.check(sourceFrames)));
console.log('charizard display',JSON.stringify(C.check(fr)));
module.exports={name:'charizard',pal,rows,sourceFrames,fr,fps:8,angles:[-2,0,2],pose:0,pad,displayScale,layoutHeight:61,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+15&&x<=pad+28&&y>=pad+9&&y<=pad+19};
