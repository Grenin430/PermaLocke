// Optional repair for enclosed single-cell gaps created by rotation. Original animation frames stay untouched.
const fs=require('fs'),{execFileSync}=require('child_process');
module.exports=function sealSheet(name,pal,cells) {
 let repaired=0;
 for(const row of cells)for(const g of row) {
  const edits=[];
  for(let y=1;y<g.length-1;y++)for(let x=1;x<g[0].length-1;x++) {
   if(g[y][x]!=='.')continue;
   const near=[g[y][x-1],g[y][x+1],g[y-1][x],g[y+1][x]];
   if(near.some(c=>c==='.'))continue;
   const counts=near.reduce((s,c)=>(s[c]=(s[c]||0)+1,s),{});
   const color=near.slice().sort((a,b)=>counts[b]-counts[a])[0];
   edits.push([x,y,color]);
  }
  for(const [x,y,c] of edits)g[y][x]=c;
  repaired+=edits.length;
 }
 const cell=cells[0][0].length,W=cell*cells[0].length,H=cell*cells.length,rgba=Buffer.alloc(W*H*4);
 cells.forEach((row,r)=>row.forEach((g,f)=>g.forEach((line,y)=>line.forEach((ch,x)=>{
  if(ch==='.')return;
  const i=((r*cell+y)*W+f*cell+x)*4;
  for(let c=0;c<3;c++)rgba[i+c]=parseInt(pal[ch].slice(c*2,c*2+2),16);
  rgba[i+3]=255;
 }))));
 fs.writeFileSync(name+'_sheet.rgba',rgba);
 execFileSync('C:/Program Files (x86)/Soundux/ffmpeg.exe',['-hide_banner','-loglevel','error','-y','-f','rawvideo','-pix_fmt','rgba','-s',`${W}x${H}`,'-i',name+'_sheet.rgba',name+'_sheet.png']);
 console.log(`${name}: sealed ${repaired} rotation gaps`);
};
