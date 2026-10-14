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
// One-pixel whiskers at the membrane's scalloped tips, left by resampling a bent wing, flicker in and out between frames: trimmed
// back to where the tip is at least two pixels thick.
function trimTips(L,passes){for(let p=0;p<passes;p++){const cut=[];
 for(let y=1;y<L.length-1;y++)for(let x=1;x<L[0].length-1;x++){if(L[y][x]==='.')continue;let n=0;
  for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++)if((dx||dy)&&L[y+dy][x+dx]!=='.')n++;if(n<=1)cut.push([x,y]);}
 for(const [x,y] of cut)L[y][x]='.';}}
// Scale2x, as critter.js uses inside C.swing (it does not export it).
function scale2x(g){const h=g.length,w=g[0].length,o=Array.from({length:h*2},()=>Array(w*2).fill('.')),at=(x,y)=>y<0||y>=h||x<0||x>=w?'.':g[y][x];
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){const P=at(x,y),A=at(x,y-1),B=at(x+1,y),C=at(x-1,y),D=at(x,y+1);
  o[y*2][x*2]=C===A&&C!==D&&A!==B?A:P;o[y*2][x*2+1]=A===B&&A!==C&&B!==D?B:P;o[y*2+1][x*2]=D===C&&D!==B&&C!==A?C:P;o[y*2+1][x*2+1]=B===D&&B!==A&&D!==C?D:P;}
 return o;}
const frames=[],stats=[],wingLayers=[];
const NEAR_ROOT=[59,40],CUFF_ABOVE=31,FAR_SHIFT=[-8,-3],FAR_TURN_UP=30,FAR_TURN_DOWN=28,FAR_SPAN=.9,FAR_ROOM=[3,96],FAR_LOW_SHIFT=[5,1],FAR_MIN_PIECE=70,REVERSE={C:'K',J:'M',M:'G',I:'M',H:'J'};
function frame(f){
 const t=f/N*2*Math.PI,bob=Math.round(-.9*Math.sin(t));
 const g=Array.from({length:H},()=>Array(W).fill('.'));
 // Who drew each pixel last: f far wing, n near wing, b body.
 const own=Array.from({length:H},()=>Array(W).fill(''));let who='b';
 const put=(x,y,c)=>{if(c!=='.'&&g[y+OY+bob]?.[x+OX]!==undefined){g[y+OY+bob][x+OX]=c;own[y+OY+bob][x+OX]=who;}};
 const wings={near:Array.from({length:H},()=>Array(W).fill('.')),far:Array.from({length:H},()=>Array(W).fill('.'))};
 // The far wing is the near wing of the same frame seen across the back: same membrane, fingers and stroke, its root set a little
 // forward and up on the far shoulder, its span shortened by the turn of the body and swung towards the head, so it opens ahead of
 // the near wing when both are high and under the chest when both are low. It shows its shaded reverse; the body hides its root.
 function farWing(){
  const layer=wings.far,PX=40,PY=60,L=Array.from({length:SH+2*PY},()=>Array(SW+2*PX).fill('.'));
  const local=(x,y,c)=>{if(c!=='.'&&L[y+PY]?.[x+PX]!==undefined)L[y+PY][x+PX]=c;};
  if(f%N===0)nearRows.forEach((r,y)=>[...r].forEach((c,x)=>local(x,y,c)));else{require('./charizard_wing_poses').paint(nearRows,false,f,local);trimTips(L,2);}
  // The arm decides the turn: the wrist above the shoulder swings the far wing forward and up, below it forward and down.
  const hub=require('./charizard_wing_poses').pose(f).nh,arm=[hub[0]-NEAR_ROOT[0],hub[1]-NEAR_ROOT[1]];
  const turn=(arm[1]<0?FAR_TURN_UP:FAR_TURN_DOWN)*arm[1]/Math.hypot(...arm);
  // Lowered, the far shoulder sits further forward under the chest, so the two wings hang from different sides of the back
  // instead of lying one along the other.
  const down=Math.max(0,arm[1]/Math.hypot(...arm)),sh=[Math.round(FAR_SHIFT[0]-FAR_LOW_SHIFT[0]*down),Math.round(FAR_SHIFT[1]+FAR_LOW_SHIFT[1]*down)];
  // Further away, it is also shorter up and down: squashed towards its root just enough to stay within the height the near wing
  // already takes, so the sky keeps room for it. Turn, span and squash are one backward sampling of the wing smoothed eight times
  // over (as C.swing does), so its outline stays whole instead of losing rows.
  const big=scale2x(scale2x(scale2x(L))),S=8,a=turn*Math.PI/180,co=Math.cos(a),si=Math.sin(a),px=NEAR_ROOT[0]+PX,py=NEAR_ROOT[1]+PY;
  const sample=(x,y,depth)=>{const dx=x+.5-px,dy=(y+.5-py)/depth,sx=(co*dx+si*dy)/FAR_SPAN+px,sy=-si*dx+co*dy+py,
   ix=Math.floor(sx*S),iy=Math.floor(sy*S);return big[iy]?.[ix]&&big[iy][ix]!=='.'?big[iy][ix]:null;};
  let top=1e9,bottom=-1e9;
  for(let y=0;y<L.length;y++)for(let x=0;x<L[0].length;x++)if(sample(x,y,1)){top=Math.min(top,y);bottom=Math.max(bottom,y);}
  const depth=Math.min(1,(py-FAR_ROOM[0]-PY+sh[1])/(py-top),(FAR_ROOM[1]+PY-sh[1]-py)/(bottom-py));
  for(let y=0;y<L.length;y++)for(let x=0;x<L[0].length;x++){
    const c=sample(x,y,depth);if(!c)continue;
    const X=x-PX+sh[0],Y=y-PY+sh[1],k=REVERSE[c]||c;put(X,Y,k);if(layer[Y+OY+bob]?.[X+OX]!==undefined)layer[Y+OY+bob][X+OX]=k;
   }
 }
 function wing(rows){
  const layer=wings.near,PX=40,PY=60,L=Array.from({length:SH+2*PY},()=>Array(SW+2*PX).fill('.'));
  const local=(x,y,c)=>{if(c!=='.'&&L[y+PY]?.[x+PX]!==undefined)L[y+PY][x+PX]=c;};
  if(f%N===0)rows.forEach((r,y)=>[...r].forEach((c,x)=>local(x,y,c)));
  else{
   const poses=require('./charizard_wing_poses');poses.paint(rows,false,f,local);trimTips(L,2);
   if(poses.pose(f).nh[1]<CUFF_ABOVE){
    // While the arm is raised the broad shoulder cuff stays as drawn in the base.
    for(let y=36;y<=43;y++)for(let x=56;x<=64;x++)if(rows[y][x]!=='.')local(x,y,rows[y][x]);
   }else{
    // Lowered, the cuff of the raised wing would point the wrong way: the root is just the shoulder and the arm, and the slivers
    // the resampled leading edge leaves around the elbow go.
    // Nothing of the membrane rises above the arm: it hangs behind it.
    const p=poses.pose(f).near,chain=[NEAR_ROOT,[p[1][0]+1,p[1][1]+2],[p[2][0]+7,p[2][1]+8]];
    for(let y=20;y<=70;y++)for(let x=46;x<=110;x++)for(let i=1;i<chain.length;i++){
     const [ax,ay]=chain[i-1],[bx,by]=chain[i],dx=bx-ax,dy=by-ay,u=((x-ax)*dx+(y-ay)*dy)/(dx*dx+dy*dy);
     if(u<0||u>1)continue;
     const side=(dx*(y-ay)-dy*(x-ax))/Math.hypot(dx,dy);
     if(side<-3.2)L[y+PY][x+PX]='.';
    }
    for(let pass=0;pass<3;pass++)for(let y=26;y<=64;y++)for(let x=50;x<=100;x++){
     const c=L[y+PY][x+PX];if(c==='.')continue;let n=0;
     for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++)if((dx||dy)&&L[y+PY+dy][x+PX+dx]!=='.')n++;
     if(n<=2)L[y+PY][x+PX]='.';
    }
   }
  }
  L.forEach((r,y)=>r.forEach((c,x)=>{if(c==='.')return;const X=x-PX,Y=y-PY;put(X,Y,c);if(layer[Y+OY+bob]?.[X+OX]!==undefined)layer[Y+OY+bob][X+OX]=c;}));
 }
 who='f';farWing();who='b';
 // Recover only torso pixels originally hidden by the near wing.
 for(let y=30;y<=44;y++)for(let x=53;x<=86;x++)if(torso[y]?.[x]&&nearRows[y][x]!=='.')put(x,y,y<39?'J':'M');
 for(let y=0;y<SH;y++)for(let x=0;x<SW;x++){
  const c=body[y][x];if(c==='.')continue;
  const tail=Math.max(0,(x-108)/60),flame='ADNO'.includes(c)&&x>=148;
  const dy=x>=108?Math.round(1.2*Math.sin(t)*tail):0;
  const dx=flame?Math.round(.9*Math.sin(2*t)*Math.max(0,(55-y)/20)):y>=63&&x>=73&&x<=107?Math.round(.65*Math.sin(t)*(y-62)/14):0;
  put(x+dx,y+dy,c);
 }
 who='n';wing(nearRows);who='b';
 // The forearm crosses in front of the membrane root.
 for(let y=39;y<67;y++)for(let x=47;x<=63;x++)if(arm[y]?.[x]&&body[y][x]!=='.')put(x,y,body[y][x]);
 // What shows of the far wing is one piece: shards it leaves round the body's outline go, they would flicker from frame to frame.
 const seen=own.map(r=>r.map(()=>false));
 for(let y=0;y<H;y++)for(let x=0;x<W;x++){if(own[y][x]!=='f'||seen[y][x])continue;
  const piece=[[x,y]];seen[y][x]=true;for(let i=0;i<piece.length;i++){const [a,b]=piece[i];
   for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]])if(own[b+dy]?.[a+dx]==='f'&&!seen[b+dy][a+dx]){seen[b+dy][a+dx]=true;piece.push([a+dx,b+dy]);}}
 if(piece.length<FAR_MIN_PIECE)for(const [a,b] of piece){g[b][a]='.';own[b][a]='';}
 }
 // Where the near wing lies over the far one its edge is drawn dark, so the two outlines do not run into each other.
 const edge=[];
 for(let y=1;y<H-1;y++)for(let x=1;x<W-1;x++)if(own[y][x]==='n'&&!'EG'.includes(g[y][x])&&[[1,0],[-1,0],[0,1],[0,-1]].some(([dx,dy])=>own[y+dy][x+dx]==='f'))edge.push([x,y]);
 for(const [x,y] of edge)g[y][x]='E';
 const repairs=clean(g);return {g:g.map(r=>r.join('')),bob,wings,...repairs};
}
for(let f=0;f<N;f++){const a=frame(f);frames.push(a.g);wingLayers.push(a.wings);stats.push({frame:f,bob:a.bob,sealed:a.sealed,isolated:a.isolated});}
assert.deepEqual(frame(N).g,frames[0]);
// Frame 0 is the approved base except where the old far wing was and where the new one shows.
let baselineChanges=0;for(let y=0;y<SH;y++)for(let x=0;x<SW;x++)if(frames[0][y+OY][x+OX]!==base[y][x]){baselineChanges++;
 assert.ok(completeFar[y][x]!=='.'||wingLayers[0].far[y+OY][x+OX]!=='.',`frame 0 changed outside the far wing at ${x},${y}`);}
// All frames share one bounding box, preserving the body's position throughout the loop.
const pts=[];frames.forEach(g=>g.forEach((r,y)=>[...r].forEach((c,x)=>{if(c!=='.')pts.push([x,y]);})));
const minX=Math.min(...pts.map(p=>p[0]))-3,maxX=Math.max(...pts.map(p=>p[0]))+3,minY=Math.min(...pts.map(p=>p[1]))-3,maxY=Math.max(...pts.map(p=>p[1]))+3;
const nativeFrames=frames.map(g=>g.slice(minY,maxY+1).map(r=>r.slice(minX,maxX+1)));
const displayScale=.60,dh=Math.ceil(nativeFrames[0].length*displayScale),dw=Math.ceil(nativeFrames[0][0].length*displayScale);
const fr=nativeFrames.map(g=>{const o=Array.from({length:dh},(_,y)=>Array.from({length:dw},(_,x)=>g[Math.min(g.length-1,Math.floor((y+.5)/displayScale))][Math.min(g[0].length-1,Math.floor((x+.5)/displayScale))]));clean(o);return o.map(r=>r.join(''));});
const face=[Math.round((12+OX-minX)*displayScale),Math.round((17+OY-minY)*displayScale),17,13];
module.exports={name:'charizard',pal:approved.pal,rows:base,fr,nativeFrames,fullFrames:frames,wingLayers,body,nearRows,farRows,fps:12,angles:[-2,0,2],pose:0,pad:0,layoutHeight:60,sealRotatedHoles:true,displayScale,stats,baselineChanges,sourceOffset:[OX-minX,OY-minY],face,
 protect:(g,x,y)=>(x>=face[0]&&x<=face[0]+face[2]&&y>=face[1]&&y<=face[1]+face[3]&&'BCE'.includes(g[y][x]))||(x>=72&&y>=35&&y<=55&&'EGJ'.includes(g[y][x]))};