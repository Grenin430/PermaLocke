// Hand-posed wing landmarks. The shoulders remain fixed; wrist, fingers and membrane change independently.
const {shapes}=require('./charizard_flight_pose_v3');
const nearIndices=[0,2,7,10,17,23,28,32,34,35];
const farIndices=[0,3,7,9,13,14,15];
const nearUp=nearIndices.map(i=>shapes.nearWing[i]);
const farUp=farIndices.map(i=>shapes.farWing[i]);
const keys=[
 {f:0,near:nearUp,nh:[76,16],far:farUp,fh:[45,26]},
 {f:3,near:[[56,38],[71,29],[80,18],[89,23],[154,58],[123,62],[95,56],[72,46],[61,43],[58,42]],nh:[86,29],
  far:[[53,39],[35,34],[22,32],[32,32],[88,46],[93,52],[61,38]],fh:[54,40]},
 {f:7,near:[[56,38],[68,43],[76,50],[86,57],[133,94],[105,88],[83,75],[69,54],[61,43],[58,42]],nh:[81,59],
  far:[[53,39],[42,53],[33,63],[43,64],[106,83],[101,88],[61,38]],fh:[64,65]},
 {f:11,near:[[56,38],[66,39],[72,34],[83,38],[123,52],[105,66],[84,61],[68,49],[61,43],[58,42]],nh:[79,43],
  far:[[53,39],[40,42],[29,39],[39,40],[86,56],[84,62],[61,38]],fh:[55,46]},
 {f:14,near:[[56,38],[65,28],[63,7],[80,14],[135,23],[113,40],[88,43],[69,43],[61,43],[58,42]],nh:[73,20],
  far:[[53,39],[25,26],[12,16],[23,20],[65,31],[67,38],[61,38]],fh:[41,30]},
 {f:16,near:nearUp,nh:[76,16],far:farUp,fh:[45,26]}
];
function pose(f){
 const t=((f%16)+16)%16;
 let k=0;while(keys[k+1].f<t)k++;
 const a=keys[k],b=keys[k+1],u=(t-a.f)/(b.f-a.f);
 const mix=(p,q)=>p.map((v,i)=>Array.isArray(v)?mix(v,q[i]):v+(q[i]-v)*u);
 return {near:mix(a.near,b.near),far:mix(a.far,b.far),nh:mix(a.nh,b.nh),fh:mix(a.fh,b.fh),phase:t<=7?'downstroke':'recovery'};
}
function outline(source,indices,anchors){
 return source.map((p,i)=>{
  let j=indices.findIndex((v,k)=>v<=i&&(k===indices.length-1||indices[k+1]>=i));
  if(j<0)j=indices.length-1;
  const a=indices[j],next=(j+1)%indices.length,b=next===0?source.length:indices[next],u=(i-a)/(b-a||1);
  const q=source[b%source.length],aa=anchors[j],bb=anchors[next];
  return [p[0]+(aa[0]-source[a][0])*(1-u)+(bb[0]-q[0])*u,p[1]+(aa[1]-source[a][1])*(1-u)+(bb[1]-q[1])*u];
 });
}
// Each membrane fan is sampled independently; the mesh changes chord, camber and finger spacing, not just rotation.
function paint(rows,isFar,f,put){
 const p=pose(f),src=isFar?shapes.farWing:shapes.nearWing,indices=isFar?farIndices:nearIndices;
 const dst=outline(src,indices,isFar?p.far:p.near),a=isFar?[45,26]:[76,16],ta=isFar?p.fh:p.nh;
 function tri(b,c,tb,tc){
  const den=(tb[1]-tc[1])*(ta[0]-tc[0])+(tc[0]-tb[0])*(ta[1]-tc[1]);if(Math.abs(den)<.01)return;
  for(let y=Math.floor(Math.min(ta[1],tb[1],tc[1]))-1;y<=Math.ceil(Math.max(ta[1],tb[1],tc[1]))+1;y++)
   for(let x=Math.floor(Math.min(ta[0],tb[0],tc[0]))-1;x<=Math.ceil(Math.max(ta[0],tb[0],tc[0]))+1;x++){
    const u=((tb[1]-tc[1])*(x-tc[0])+(tc[0]-tb[0])*(y-tc[1]))/den,
          v=((tc[1]-ta[1])*(x-tc[0])+(ta[0]-tc[0])*(y-tc[1]))/den,w=1-u-v;
    if(Math.min(u,v,w)<-.025)continue;
    const sx=Math.round(u*a[0]+v*b[0]+w*c[0]),sy=Math.round(u*a[1]+v*b[1]+w*c[1]),ch=rows[sy]?.[sx];
    if(ch&&ch!=='.'){
     // Replace sampled inner rib pixels with the adjoining shadow, then redraw continuous anatomical ribs below.
     const boundary=src.some((v,i)=>{const q=src[(i+1)%src.length],dx=q[0]-v[0],dy=q[1]-v[1],u=Math.max(0,Math.min(1,((sx-v[0])*dx+(sy-v[1])*dy)/(dx*dx+dy*dy||1)));return Math.hypot(sx-v[0]-u*dx,sy-v[1]-u*dy)<1.2;});
     put(x,y,!isFar&&ch==='E'&&!boundary?'K':ch);
    }
   }
 }
 for(let i=0;i<src.length;i++)tri(src[i],src[(i+1)%src.length],dst[i],dst[(i+1)%src.length]);
 // Redraw the supporting fingers from the wrist hub, rather than retaining broken resampling marks.
 if(!isFar){
  const stroke=(from,to,c,offset=0)=>{const n=Math.ceil(Math.max(Math.abs(to[0]-from[0]),Math.abs(to[1]-from[1])));for(let k=0;k<=n;k++)put(Math.round(from[0]+(to[0]-from[0])*k/n),Math.round(from[1]+(to[1]-from[1])*k/n)+offset,c);};
  for(const i of [23,28,32]){stroke(ta,dst[i],'M',1);stroke(ta,dst[i],'E');}
 }
 // Rasterise the perimeter continuously, preserving its stepped dark contour.
 for(let i=0;i<src.length;i++){
  const j=(i+1)%src.length,b=dst[i],c=dst[j],n=Math.ceil(Math.max(Math.abs(c[0]-b[0]),Math.abs(c[1]-b[1])));
  for(let k=0;k<=n;k++){const u=k/(n||1),sx=Math.round(src[i][0]*(1-u)+src[j][0]*u),sy=Math.round(src[i][1]*(1-u)+src[j][1]*u);
   const ch=rows[sy]?.[sx];if(ch&&ch!=='.')put(Math.round(b[0]*(1-u)+c[0]*u),Math.round(b[1]*(1-u)+c[1]*u),ch);
  }
 }
 // A fleshy articulated upper arm keeps a broad continuous attachment when the membrane turns edge-on.
 const chain=isFar?[[57,37],p.far[1],p.far[2]]:[[59,40],[p.near[1][0]+1,p.near[1][1]+2],[p.near[2][0]+7,p.near[2][1]+8]];
 function tube(radius,color){
  for(let i=1;i<chain.length;i++){
   const a=chain[i-1],b=chain[i],dx=b[0]-a[0],dy=b[1]-a[1],d=dx*dx+dy*dy;
   for(let y=Math.floor(Math.min(a[1],b[1])-radius);y<=Math.ceil(Math.max(a[1],b[1])+radius);y++)
    for(let x=Math.floor(Math.min(a[0],b[0])-radius);x<=Math.ceil(Math.max(a[0],b[0])+radius);x++){
     const u=Math.max(0,Math.min(1,((x-a[0])*dx+(y-a[1])*dy)/(d||1)));
     if(Math.hypot(x-a[0]-u*dx,y-a[1]-u*dy)<=radius)put(x,y,color);
    }
  }
 }
 tube(isFar?1.6:2.1,'G');tube(isFar?.8:1.1,'J');
 return {outline:dst,hub:ta,anchors:isFar?p.far:p.near};
}
module.exports={paint,pose,keys};