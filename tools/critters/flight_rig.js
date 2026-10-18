// Pixel-exact articulated wings for the flight correction batch. Each species supplies its own anatomy and texture.
const C=require('./critter');
const block=s=>s.trim().split('\n').map(r=>r.trim());
function build(s){
 const original=C.load(s.name),pal=original.pal,[W,H]=s.canvas,empty=()=>Array.from({length:H},()=>Array(W).fill('.'));
 const blit=(g,rows,ox,oy,shift=()=>[0,0])=>rows.forEach((r,y)=>[...r].forEach((c,x)=>{const [dx,dy]=shift(x,y),xx=ox+x+Math.round(dx),yy=oy+y+Math.round(dy);if(c!=='.'&&g[yy]?.[xx]!==undefined)g[yy][xx]=c;}));
 const head=original.rows.slice(0,s.headHeight).map((r,y)=>r.map((c,x)=>x<s.headWidth(y)&&!(s.cleanHead&&s.cleanHead(x,y,c))?c:'.').join(''));
 const torso=block(s.torso),wing=block(s.wing),tail=block(s.tail),feet=block(s.feet),fr=[],layers=[];let sealed=0;
 function drawWing(g,pose,far){
  const root=s.root,pts=pose.slice(0,4).map(([x,y])=>far?[root[0]-4+(x-root[0])*.65,root[1]-2+(y-root[1])*.85]:[x,y]);
  const span=Math.max(...wing.map(r=>r.length)),chord=wing.length,stations=[0,span*.31,span*.64,span],spread=pose[4]*(far?.72:1);
  const edges=pts.map((p,j)=>{const a=pts[Math.max(0,j-1)],b=pts[Math.min(3,j+1)],l=Math.hypot(b[0]-a[0],b[1]-a[1]);return [-chord*.46,chord*.54].map(v=>[p[0]-(b[1]-a[1])/l*v*spread,p[1]+(b[0]-a[0])/l*v*spread]);});
  function triangle(a,b,c,ta,tb,tc){
   const den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1]);if(Math.abs(den)<.001)return;
   for(let y=Math.max(0,Math.floor(Math.min(a[1],b[1],c[1])));y<=Math.min(H-1,Math.ceil(Math.max(a[1],b[1],c[1])));y++)for(let x=Math.max(0,Math.floor(Math.min(a[0],b[0],c[0])));x<=Math.min(W-1,Math.ceil(Math.max(a[0],b[0],c[0])));x++){
    const u=((b[1]-c[1])*(x+.5-c[0])+(c[0]-b[0])*(y+.5-c[1]))/den,v=((c[1]-a[1])*(x+.5-c[0])+(a[0]-c[0])*(y+.5-c[1]))/den,w=1-u-v;if(Math.min(u,v,w)<-.00001)continue;
    const sx=Math.floor(u*ta[0]+v*tb[0]+w*tc[0]),sy=Math.floor(u*ta[1]+v*tb[1]+w*tc[1]),ch=wing[sy]?.[sx];if(ch&&ch!=='.')g[y][x]=far?(s.shade[ch]||ch):ch;
   }
  }
  for(let j=0;j<3;j++){const p=[edges[j][0],edges[j+1][0],edges[j+1][1],edges[j][1]],uv=[[stations[j],0],[stations[j+1],0],[stations[j+1],chord],[stations[j],chord]];triangle(p[0],p[1],p[2],uv[0],uv[1],uv[2]);triangle(p[0],p[2],p[3],uv[0],uv[2],uv[3]);}
 }
 for(let f=0;f<s.poses.length;f++){
  const g=empty(),near=empty(),far=empty(),t=f/s.poses.length*Math.PI*2;
  drawWing(far,s.poses[f],true);drawWing(near,s.poses[f],false);blit(g,far,0,0);
  blit(g,tail,...s.tailAt,x=>[0,Math.sin(t-1)*x/Math.max(...tail.map(r=>r.length))]);
  blit(g,torso,...s.torsoAt);blit(g,feet,...s.feetAt);blit(g,near,0,0);blit(g,block(s.cover),s.root[0]-3,s.root[1]-2);
  if(s.crest)blit(g,block(s.crest),...s.crestAt,x=>[0,.8*Math.sin(t-1.2)*x/40]);
  blit(g,head,...s.headAt);
  for(let pass=0;pass<2;pass++)for(let y=1;y<H-1;y++)for(let x=1;x<W-1;x++)if(g[y][x]==='.'){
   const adj=[g[y-1][x],g[y+1][x],g[y][x-1],g[y][x+1]];if(adj.every(c=>c!=='.')){g[y][x]=adj.find(c=>!s.ink.includes(c))||adj[0];sealed++;}
  }
  fr.push(g.map(r=>r.join('')));layers.push({near:near.map(r=>r.join('')),far:far.map(r=>r.join(''))});
 }
 const eye=s.eye,protect=(g,x,y)=>x>=eye[0]&&x<eye[0]+eye[2]&&y>=eye[1]&&y<eye[1]+eye[3]&&(eye[4]+eye[5]).includes(g[y][x]);
 return {name:s.name,pal,rows:fr[0],fr,fps:s.fps,angles:s.angles||[-2,0,2],pose:0,pad:0,layoutHeight:s.height||60,sealRotatedHoles:true,protect,head,headAt:s.headAt,layers,spec:s,sealed};
}
module.exports={build};
