// Display-only nearest-neighbour reduction. Source frames and palette remain unchanged.
// Protected source features are projected after sampling, before tilting the sheet.
module.exports=(source,scale,ink,features=[])=>{
 let sealed=0,isolated=0;
 const frames=source.map(src=>{
  const g=Array.from({length:Math.floor(src.length*scale)},(_,y)=>Array.from({length:Math.floor(src[0].length*scale)},(_,x)=>src[Math.floor((y+.5)/scale)][Math.floor((x+.5)/scale)]));
  for(const [x,y,w,h,colors] of features)for(let sy=y;sy<y+h;sy++)for(let sx=x;sx<x+w;sx++){
   const c=src[sy]?.[sx];if(c&&c!=='.'&&colors.includes(c))g[Math.floor((sy+.5)*scale)][Math.floor((sx+.5)*scale)]=c;
  }
  for(let y=1;y<g.length-1;y++)for(let x=1;x<g[0].length-1;x++)if(g[y][x]==='.'){
   const near=[g[y][x-1],g[y][x+1],g[y-1][x],g[y+1][x]];
   if(near.every(c=>c!=='.')){g[y][x]=near.find(c=>!ink.includes(c))||near[0];sealed++;}
  }
  const before=g.map(r=>[...r]);
  for(let y=0;y<g.length;y++)for(let x=0;x<g[0].length;x++)if(before[y][x]!=='.'&&![-1,0,1].some(dy=>[-1,0,1].some(dx=>(dx||dy)&&before[y+dy]?.[x+dx]&&before[y+dy][x+dx]!=='.'))){g[y][x]='.';isolated++;}
  return g.map(r=>r.join(''));
 });
 console.log('display sampling',JSON.stringify({scale,sealed,isolated}));return frames;
};
