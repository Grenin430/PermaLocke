// Dragonite: the reference grid stays native; only the display frames are reduced, as approved.
// Small wing membranes beat independently, while the curled tail and antennae lag behind.
const C=require('./critter');
const {pal,rows}=C.load('dragonite');
const N=8,sourcePad=8,t=f=>f/N*2*Math.PI;
const sourceFrames=C.frames(rows,{count:N,pad:sourcePad,ink:'AH',parts:[
 {test:(x,y)=>y<=10,move:(f,x,y)=>[.9*Math.sin(t(f)-1.4)*Math.max(0,(10-y)/10)*(x<20?-1:1),0]},
 {test:(x,y)=>x<=13&&y>=15&&y<=22,move:(f,x)=>[0,1.8*Math.sin(t(f))*(13-x)/13]},
 {test:(x,y)=>x>=31&&y>=13&&y<=29,move:(f,x)=>[0,2.3*Math.sin(t(f)-.3)*Math.min(1,(x-31)/24)]},
 {test:(x,y)=>x>=44&&y>=23,move:(f,x,y)=>[1.8*Math.sin(t(f)-1.2-(63-y)*.05)*Math.max(0,(63-y)/40),0]},
 {test:(x,y)=>x<=13&&y>=34&&y<=47,move:(f,x)=>[0,.8*Math.sin(t(f)-.8)*Math.max(0,(13-x)/13)]},
 {test:()=>true}
],seam:(x,y)=>(y>=8&&y<=12)||(x>=11&&x<=15&&y<=24)||(x>=29&&x<=34&&y>=12&&y<=31)||(x>=42&&x<=46)||(y>=33&&y<=38&&x<=15)});
// Nearest-neighbour 3/4 sampling retains palette colours and never introduces smoothing.
const displayScale=.75,pad=sourcePad*displayScale;
const scaled=sourceFrames.map(g=>Array.from({length:g.length*displayScale},(_,y)=>Array.from({length:g[0].length*displayScale},(_,x)=>g[Math.floor((y+.5)/displayScale)][Math.floor((x+.5)/displayScale)])));
// Seal only fully enclosed one-cell sampling holes, using the existing neighbour colour.
for(const g of scaled)for(let y=1;y<g.length-1;y++)for(let x=1;x<g[0].length-1;x++)if(g[y][x]==='.'){
 const near=[g[y][x-1],g[y][x+1],g[y-1][x],g[y+1][x]];
 if(near.every(c=>c!=='.'))g[y][x]=near.find(c=>!'AH'.includes(c))||near[0];
}
const fr=scaled.map(g=>g.map(r=>r.join('')));
console.log('dragonite native',JSON.stringify(C.check(sourceFrames)));
console.log('dragonite display',JSON.stringify(C.check(fr)));
module.exports={name:'dragonite',pal,rows,sourceFrames,fr,fps:8,angles:[-2,0,2],pose:0,pad,displayScale,layoutHeight:61,
 protect:(g,x,y)=>g[y][x]!=='.'&&((x>=pad+15&&x<=pad+21&&y>=pad+10&&y<=pad+17)||(x>=pad+10&&x<=pad+23&&y>=pad+17&&y<=pad+22))};
