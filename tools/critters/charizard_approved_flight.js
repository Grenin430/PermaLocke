// Flight from the approved Charizard base: articulated wings and restrained secondary motion.
const assert=require('assert/strict'),C=require('./critter');
const approved=require('./charizard_neck_refinement').build(),{shapes}=require('./charizard_flight_pose_v3');
const snapshot=require('./charizard_flight_approved.json');
assert.deepEqual(approved.rows,snapshot.rows);
const base=approved.rows,SW=174,SH=92,N=16,OX=10,OY=12,W=194,H=136;
function polygon(x,y,p){let inside=false;for(let i=0,j=p.length-1;i<p.length;j=i++)if((p[i][1]>y)!==(p[j][1]>y)&&x<(p[j][0]-p[i][0])*(y-p[i][1])/(p[j][1]-p[i][1])+p[i][0])inside=!inside;return inside;}
function mask(p){const g=Array.from({length:SH},()=>Array(SW).fill(false));for(let y=0;y<SH;y++)for(let x=0;x<SW;x++)g[y][x]=polygon(x,y,p);for(let j=0;j<p.length;j++){const a=p[j],b=p[(j+1)%p.length],n=Math.max(Math.abs(b[0]-a[0]),Math.abs(b[1]-a[1]));for(let k=0;k<=n;k++)g[Math.round(a[1]+(b[1]-a[1])*k/n)][Math.round(a[0]+(b[0]-a[0])*k/n)]=true;}return g;}
const near=mask(shapes.nearWing),far=mask(shapes.farWing),torso=mask(shapes.torso),arm=mask(shapes.nearArm);
const head=approved.head,originalPose=require('./charizard_flight_pose_v3').build(true);

const nearRows=base.map((r,y)=>[...r].map((c,x)=>((near[y][x]||x>=56&&'CK'.includes(c))&&!(arm[y][x]&&y>=40))?c:'.').join(''));
const farRows=base.map((r,y)=>[...r].map((c,x)=>{
 const h=head[y+13]?.[x+15];
 return far[y][x]&&!torso[y][x]&&nearRows[y][x]==='.'&&(!h||h==='.')?c:'.';
}).join(''));
// The approved neck covers the original wing at its bottom boundary.
for(let y=29;y<=43;y++)for(let x=23;x<=54;x++)if(base[y][x]!==originalPose.rows[y][x]){
 const r=farRows[y].split('');r[x]='.';farRows[y]=r.join('');
}
const body=base.map((r,y)=>[...r].map((c,x)=>nearRows[y][x]!=='.'||farRows[y][x]!=='.'?'.':c).join(''));
// Reconstruct the far membrane behind the head, so a moving wing is not made from disconnected visible fragments.
const farTexture=Array.from({length:SH},()=>Array(SW).fill('.'));
const putFar=(x,y,c)=>{if(farTexture[y]?.[x]!==undefined)farTexture[y][x]=c;};
const farLine=(p,c)=>{for(let j=1;j<p.length;j++){const a=p[j-1],b=p[j],n=Math.max(Math.abs(a[0]-b[0]),Math.abs(a[1]-b[1]));for(let k=0;k<=n;k++)putFar(Math.round(a[0]+(b[0]-a[0])*k/n),Math.round(a[1]+(b[1]-a[1])*k/n),c);}};
for(let y=0;y<SH;y++)for(let x=0;x<SW;x++)if(far[y][x])putFar(x,y,'M');
farLine([...shapes.farWing,shapes.farWing[0]],'E');
const dorsal=[[22,14],[25,16],[33,17],[43,23],[51,27],[58,29],[62,27],[62,32],[58,36],[52,35],[45,28],[37,26]];
for(let y=0;y<SH;y++)for(let x=0;x<SW;x++)if(polygon(x,y,dorsal))putFar(x,y,'J');
farLine([[15,11],[18,11],[23,14],[25,17]],'J');
farLine([[30,17],[34,21],[37,25]],'G');farLine([[40,18],[44,23],[49,26]],'G');
for(let y=0;y<SH;y++)for(let x=0;x<SW;x++)if(farRows[y][x]!=='.')putFar(x,y,farRows[y][x]);
// Four dorsal edge pixels were overlaid by the old head crop; they belong to the wing, not the face.
for(const [x,y] of [[40,18],[40,19],[41,19],[42,20]]){
 putFar(x,y,body[y][x]);const r=body[y].split('');r[x]='.';body[y]=r.join('');
}
const completeFar=farTexture.map(r=>r.join(''));
function clean(g){let sealed=0,isolated=0;
 for(let pass=0;pass<2;pass++)for(let y=1;y<g.length-1;y++)for(let x=1;x<g[0].length-1;x++)if(g[y][x]==='.'){
 const n=[g[y-1][x],g[y+1][x],g[y][x-1],g[y][x+1]];if(n.every(c=>c!=='.')){g[y][x]=n.find(c=>!'EGK'.includes(c))||n[0];sealed++;}}
 const copy=g.map(r=>[...r]);for(let y=0;y<g.length;y++)for(let x=0;x<g[0].length;x++)if(copy[y][x]!=='.'&&![-1,0,1].some(dy=>[-1,0,1].some(dx=>(dx||dy)&&copy[y+dy]?.[x+dx]&&copy[y+dy][x+dx]!=='.'))){g[y][x]='.';isolated++;}
 return {sealed,isolated};
}
const frames=[],stats=[],wingLayers=[];
function frame(f){
 const t=f/N*2*Math.PI,bob=Math.round(-.9*Math.sin(t));
 const g=Array.from({length:H},()=>Array(W).fill('.'));
 const put=(x,y,c)=>{if(c!=='.'&&g[y+OY+bob]?.[x+OX]!==undefined)g[y+OY+bob][x+OX]=c;};
 const wings={near:Array.from({length:H},()=>Array(W).fill('.')),far:Array.from({length:H},()=>Array(W).fill('.'))};
 function wing(rows,isFar){
  const layer=wings[isFar?'far':'near'];
  const draw=(x,y,c)=>{put(x,y,c);if(c!=='.'&&layer[y+OY+bob]?.[x+OX]!==undefined)layer[y+OY+bob][x+OX]=c;};
  if(f%N===0){rows.forEach((r,y)=>[...r].forEach((c,x)=>{if(c!=='.')draw(x,y,c);}));return;}
    require('./charizard_wing_poses').paint(rows,isFar,f,draw);
  // The broad shoulder cuff is invariant in local body coordinates throughout both half-strokes.
  const box=isFar?[53,34,61,39]:[56,36,64,43];
  for(let y=box[1];y<=box[3];y++)for(let x=box[0];x<=box[2];x++)if(rows[y][x]!=='.')draw(x,y,rows[y][x]);
 }
 wing(completeFar,true);
 // Recover only torso pixels originally hidden by the near wing.
 for(let y=30;y<=44;y++)for(let x=53;x<=86;x++)if(torso[y]?.[x]&&nearRows[y][x]!=='.')put(x,y,y<39?'J':'M');
 for(let y=0;y<SH;y++)for(let x=0;x<SW;x++){
  const c=body[y][x];if(c==='.')continue;
  const tail=Math.max(0,(x-108)/60),flame='ADNO'.includes(c)&&x>=148;
  const dy=x>=108?Math.round(1.2*Math.sin(t)*tail):0;
  const dx=flame?Math.round(.9*Math.sin(2*t)*Math.max(0,(55-y)/20)):y>=63&&x>=73&&x<=107?Math.round(.65*Math.sin(t)*(y-62)/14):0;
  put(x+dx,y+dy,c);
 }
 wing(nearRows,false);
 // The forearm crosses in front of the membrane root.
 for(let y=39;y<67;y++)for(let x=47;x<=63;x++)if(arm[y]?.[x]&&body[y][x]!=='.')put(x,y,body[y][x]);
 const repairs=clean(g);return {g:g.map(r=>r.join('')),bob,wings,...repairs};
}
for(let f=0;f<N;f++){const a=frame(f);frames.push(a.g);wingLayers.push(a.wings);stats.push({frame:f,bob:a.bob,sealed:a.sealed,isolated:a.isolated});}
assert.deepEqual(frame(N).g,frames[0]);
let baselineChanges=0;for(let y=0;y<SH;y++)for(let x=0;x<SW;x++)if(frames[0][y+OY][x+OX]!==base[y][x])baselineChanges++;
assert.equal(baselineChanges,0);
// All frames share one bounding box, preserving the body's position throughout the loop.
const pts=[];frames.forEach(g=>g.forEach((r,y)=>[...r].forEach((c,x)=>{if(c!=='.')pts.push([x,y]);})));
const minX=Math.min(...pts.map(p=>p[0]))-3,maxX=Math.max(...pts.map(p=>p[0]))+3,minY=Math.min(...pts.map(p=>p[1]))-3,maxY=Math.max(...pts.map(p=>p[1]))+3;
const nativeFrames=frames.map(g=>g.slice(minY,maxY+1).map(r=>r.slice(minX,maxX+1)));
const displayScale=.60,dh=Math.ceil(nativeFrames[0].length*displayScale),dw=Math.ceil(nativeFrames[0][0].length*displayScale);
const fr=nativeFrames.map(g=>{const o=Array.from({length:dh},(_,y)=>Array.from({length:dw},(_,x)=>g[Math.min(g.length-1,Math.floor((y+.5)/displayScale))][Math.min(g[0].length-1,Math.floor((x+.5)/displayScale))]));clean(o);return o.map(r=>r.join(''));});
const face=[Math.round((12+OX-minX)*displayScale),Math.round((17+OY-minY)*displayScale),17,13];
module.exports={name:'charizard',pal:approved.pal,rows:base,fr,nativeFrames,fullFrames:frames,wingLayers,body,nearRows,farRows,fps:12,angles:[-2,0,2],pose:0,pad:0,layoutHeight:60,sealRotatedHoles:true,displayScale,stats,baselineChanges,sourceOffset:[OX-minX,OY-minY],face,
 protect:(g,x,y)=>(x>=face[0]&&x<=face[0]+face[2]&&y>=face[1]&&y<=face[1]+face[3]&&'BCE'.includes(g[y][x]))||(x>=72&&y>=35&&y<=55&&'EGJ'.includes(g[y][x]))};