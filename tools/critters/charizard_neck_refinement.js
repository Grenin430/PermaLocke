// Local anatomical correction of the approved v3 pose; no production imports.
const {build:original}=require('./charizard_flight_pose_v3');
function build(){
 const a=original(true),g=a.rows.map(r=>r.split(''));
 const put=(x,y,c)=>{g[y][x]=c;};
 const line=(points,c)=>{for(let j=1;j<points.length;j++){const [x,y]=points[j-1],[xx,yy]=points[j],n=Math.max(Math.abs(xx-x),Math.abs(yy-y));for(let k=0;k<=n;k++)put(Math.round(x+(xx-x)*k/n),Math.round(y+(yy-y)*k/n),c);}};
 // Remove the square underside of the old head crop, forming a rising jaw and a longer throat.
 const edges=[[35,26],[36,28],[37,30],[38,32],[39,34],[40,35],[41,36],[42,37],[43,38]];
 for(const [y,left] of edges){
  for(let x=24;x<left;x++)put(x,y,'.');
  put(left,y,'E');put(left+1,y,'G');put(left+2,y,'M');
 }
 // Join the cheek to the shoulder with continuous light and shadow bands.
 const spans=[[30,32,34],[31,31,37],[32,30,40],[33,28,43],[34,27,47],[35,28,51],
  [36,30,53],[37,32,54],[38,34,53],[39,36,49],[40,37,49],[41,38,49],[42,39,49],[43,40,48]];
 for(const [y,left,right] of spans)for(let x=left;x<=right;x++){
  if(g[y][x]!=='.')put(x,y,x<left+3?'M':'J');
 }
 // The nape descends into the back; keep the far wing and the air gap above untouched.
 line([[34,29],[35,30],[37,31],[40,32],[43,33],[47,34],[51,35]],'G');
 line([[35,31],[38,32],[41,33],[45,34],[49,35],[53,36]],'J');
 // Short jaw crease, then a widening lower neck shadow rather than a vertical pasted edge.
 line([[23,34],[25,34],[27,35],[29,36],[31,37],[33,38],[35,40],[37,42],[38,43]],'G');
 line([[25,33],[27,33],[29,32]],'M');
 for(const [y,l,r] of [[35,28,31],[36,30,34],[37,32,36],[38,34,39],[39,36,42],[40,37,44],[41,38,46],[42,39,48],[43,40,48]])for(let x=l;x<=r;x++)put(x,y,'M');

 return {...a,rows:g.map(r=>r.join(''))};
}
module.exports={build};