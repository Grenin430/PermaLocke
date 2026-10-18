// Charizard only. Pose landmarks follow the Pokemon Ranger flight illustration.
// This standalone base is not imported by the production sprite or animation pipelines.
const source=require('./charizard_before_flight.json'),C=require('./critter');
const W=174,H=92;
const shapes={
 farWing:[[53,39],[46,30],[37,26],[28,20],[20,16],[15,13],[14,11],[18,10],[23,12],[25,15],[33,16],[43,21],[53,23],[62,25],[65,31],[61,38]],
 tail:[[85,49],[98,52],[112,57],[128,60],[140,59],[150,56],[157,52],[159,53],[155,58],[148,62],[137,66],[123,67],[110,64],[98,61],[87,59]],
 farLeg:[[74,54],[84,54],[86,60],[83,64],[85,67],[87,69],[88,73],[85,75],[82,74],[80,76],[76,75],[74,72],[73,68],[66,66],[64,63],[66,59]],
 farArm:[[44,40],[49,43],[49,48],[46,52],[46,55],[49,57],[50,60],[48,63],[44,64],[40,62],[38,59],[38,54],[36,50],[37,45]],
 torso:[[31,29],[38,31],[46,34],[54,35],[62,36],[71,36],[80,38],[88,42],[93,47],[94,52],[90,58],[84,61],[75,65],[64,65],[55,63],[48,60],[43,56],[40,50],[37,43],[31,39],[27,36]],
 nearLeg:[[80,43],[86,43],[92,47],[95,52],[96,56],[102,58],[104,61],[106,67],[104,70],[100,70],[98,72],[94,72],[92,69],[91,65],[87,62],[80,62],[75,60],[73,56],[74,51],[76,47]],
 nearWing:[[56,38],[63,32],[68,27],[71,20],[71,14],[68,9],[66,7],[68,5],[73,5],[78,8],[82,11],[91,12],[102,15],[114,17],[125,20],[135,25],[144,32],[147,36],[140,33],[133,30],[126,29],[120,31],[115,34],[112,38],[107,35],[101,34],[96,35],[91,38],[89,41],[84,39],[79,38],[73,40],[70,43],[67,42],[61,43],[58,42]],
 nearArm:[[54,39],[58,41],[59,45],[63,49],[63,52],[61,56],[57,59],[56,62],[53,65],[49,64],[47,62],[47,59],[50,56],[53,53],[54,50],[51,47],[50,43]],
 head:[[12,30],[13,28],[18,27],[21,24],[25,23],[26,21],[30,20],[33,19],[36,17],[39,16],[38,19],[35,23],[39,23],[42,23],[44,25],[43,27],[38,26],[35,27],[35,31],[32,34],[29,36],[23,38],[20,38],[17,36],[15,36],[14,34],[12,34]]
};
function inside(x,y,pts){let hit=false;for(let i=0,j=pts.length-1;i<pts.length;j=i++)if((pts[i][1]>y)!==(pts[j][1]>y)&&x<(pts[j][0]-pts[i][0])*(y-pts[i][1])/(pts[j][1]-pts[i][1])+pts[i][0])hit=!hit;return hit;}
function build(detail=false){const g=Array.from({length:H},()=>Array(W).fill('.')),layers=[];
 const put=(x,y,c)=>{x=Math.round(x);y=Math.round(y);if(g[y]?.[x]!==undefined)g[y][x]=c;};
 function line(p,c,width=1){for(let j=1;j<p.length;j++){const [x,y]=p[j-1],[xx,yy]=p[j],n=Math.max(Math.abs(xx-x),Math.abs(yy-y));for(let k=0;k<=n;k++)for(let b=0;b<width;b++)put(x+(xx-x)*k/(n||1),y+(yy-y)*k/(n||1)+b,c);}}
 function poly(p,c,edge){for(let y=0;y<H;y++)for(let x=0;x<W;x++)if(inside(x,y,p))put(x,y,c);if(edge)line([...p,p[0]],edge);}
 for(const [id,points] of Object.entries(shapes)){
  if(id==='head')continue;
  poly(points,detail?({farWing:'M',tail:'J',farLeg:'M',farArm:'M',torso:'J',nearLeg:'J',nearWing:'C',nearArm:'J',head:'J'}[id]):'E',detail?'E':null);layers.push({id,points});
 }
 // The original flame is a separate identity detail; its stem continues directly from the tail.
 const flame=[];
 for(let y=0;y<=20;y++)for(let x=43;x<=64;x++)if('ADNO'.includes(source.rows[y][x])){
  const xx=x-43+148,yy=y+35;put(xx,yy,detail?source.rows[y][x]:'E');flame.push([xx,yy,source.rows[y][x]]);
 }
 if(detail){
  // The far wing shows its dorsal orange surface, as in the flight reference.
  poly([[22,14],[25,16],[33,17],[43,23],[51,27],[58,29],[62,27],[62,32],[58,36],[52,35],[45,28],[37,26]],'J');
  line([[15,11],[18,11],[23,14],[25,17]],'J');
  line([[30,17],[34,21],[37,25]],'G');line([[40,18],[44,23],[49,26]],'G');
  // Torso shading follows the sternum-to-pelvis axis instead of a seated belly.
  poly([[31,33],[37,36],[43,39],[51,40],[56,39],[61,41],[70,40],[79,41],[85,44],[90,48],[90,54],[86,59],[75,63],[63,63],[53,60],[46,54],[41,46],[35,41],[29,37]],'M');
  poly([[35,32],[42,35],[49,37],[54,36],[61,37],[67,37],[74,38],[81,40],[85,43],[78,43],[72,42],[64,43],[57,42],[48,42],[42,39]],'J');
  line([[43,44],[45,46],[48,47]],'G');line([[44,43],[45,44],[47,45]],'M');
  // Cream underside wraps around the chest and narrows at the rear pelvis.
  poly([[43,49],[48,48],[54,49],[61,52],[69,54],[79,55],[87,55],[84,60],[75,64],[64,64],[55,62],[48,59],[44,55]],'I','G');
  poly([[44,50],[48,49],[54,50],[61,53],[69,55],[79,56],[83,56],[78,59],[71,60],[64,60],[56,58],[50,56],[46,53]],'H');
  for(const [x,y] of [[54,57],[56,59],[58,59],[60,60],[63,61],[65,61],[68,61],[72,60],[75,60],[78,59]])put(x,y,'H');
  // Far arm and hind leg remain darker, with complete claw groups.
  poly([[38,47],[40,47],[41,51],[40,55],[41,59],[44,61],[48,60],[48,62],[44,63],[40,61],[39,58],[39,54],[37,50]],'G');
  poly([[40,58],[43,58],[45,60],[44,62],[42,61]],'M');
  poly([[41,61],[42,62],[41,63],[40,62]],'L','E');
  poly([[45,61],[46,62],[45,64],[44,63]],'L','E');
  poly([[48,59],[49,60],[48,62],[47,61]],'L','E');
  poly([[70,63],[77,63],[81,62],[80,66],[83,69],[84,72],[81,73],[78,72],[76,69],[72,68]],'G');
  poly([[76,69],[78,69],[79,73],[78,75],[76,73]],'L','E');
  poly([[80,71],[82,71],[84,74],[82,76],[80,74]],'L','E');
  poly([[84,70],[86,70],[88,73],[86,75],[84,73]],'L','E');
  put(77,71,'B');put(81,73,'B');put(85,72,'B');
  // Tail stays thick at its pelvic root; shade and underside run continuously to the flame.
  poly([[91,54],[100,57],[112,61],[126,64],[138,63],[150,59],[156,55],[154,59],[147,63],[136,66],[123,66],[111,63],[100,60],[91,58]],'M');
  poly([[98,59],[111,62],[123,65],[136,65],[145,63],[137,66],[123,67],[110,64],[99,61]],'I');
  line([[106,63],[117,66],[128,67],[137,66]],'G');
  // Rounded thigh attaches at the rear pelvis, with the shin bending backwards from the knee.
  poly(shapes.nearLeg,'J','E');
  poly([[89,47],[93,51],[94,56],[97,59],[102,60],[104,65],[104,69],[100,70],[97,71],[94,70],[93,65],[89,61],[82,61],[77,59],[75,55],[76,51],[77,55],[81,58],[85,58],[89,56]],'M');
  poly([[78,48],[82,46],[86,47],[87,49],[84,50],[81,49],[79,51]],'J');
  poly([[78,50],[80,50],[81,53],[80,54],[78,53]],'I');
  line([[89,57],[87,60],[84,61]],'G');line([[95,60],[98,62],[99,65]],'G');
  poly([[94,67],[96,67],[98,71],[96,73],[94,70]],'L','E');
  poly([[99,66],[101,66],[103,70],[101,72],[99,69]],'L','E');
  poly([[103,64],[105,65],[107,68],[105,70],[104,68]],'L','E');
  put(95,68,'B');put(100,67,'B');put(104,66,'B');
  // Repaint the near sail over the shoulder; scallops are anatomical negative spaces.
  poly(shapes.nearWing,'C','E');
  poly([[77,14],[92,21],[105,29],[110,36],[105,34],[100,33],[94,35],[89,40],[86,34],[82,25]],'K');
  poly([[73,19],[79,26],[85,35],[88,40],[83,38],[78,37],[72,39],[70,42],[72,31]],'K');
  poly([[69,25],[69,32],[67,38],[69,42],[63,41],[59,41],[58,39],[63,34]],'K');
  poly([[82,12],[92,13],[102,16],[114,18],[125,21],[135,26],[144,33],[146,35],[137,30],[126,24],[114,21],[103,20],[91,16]],'K');
  // Continuous leading-edge arm from the shoulder to the wrist, and the three supporting fingers.
  line([[58,40],[61,36],[66,32],[70,26],[73,20],[74,14],[72,10],[68,7]],'G',3);
  line([[59,39],[63,35],[68,31],[72,25],[75,19],[76,13],[73,9],[69,6],[72,6]],'J',2);
  line([[77,10],[82,12],[92,13],[103,16],[115,18],[126,21],[136,27],[145,34]],'G',2);
  line([[78,10],[83,12],[94,14],[104,16],[115,18],[126,22],[136,28],[145,35]],'J');
  line([[76,15],[91,22],[104,29],[112,38]],'E');
  line([[76,16],[91,23],[104,30]],'M');
  line([[74,19],[81,27],[86,35],[89,41]],'E');
  line([[71,24],[72,32],[70,43]],'E');
  // Near upper arm, elbow, forearm and fist are a separate chain from the wing arm.
  poly(shapes.nearArm,'J','G');
  poly([[57,42],[58,46],[61,49],[61,52],[58,56],[55,58],[54,62],[51,64],[48,62],[48,60],[51,57],[55,54],[56,50],[53,46]],'M');
  poly([[52,41],[54,40],[56,42],[55,45],[57,47],[56,49],[53,47],[51,44]],'J');
  line([[54,40],[56,41],[57,43]],'G');line([[53,56],[51,58],[50,60]],'G');
  poly([[48,61],[50,61],[51,64],[49,65],[48,63]],'L','E');
  poly([[52,62],[54,62],[54,65],[52,66],[51,64]],'L','E');
  poly([[55,59],[57,60],[57,62],[55,64],[54,62]],'L','E');
  put(49,62,'B');put(52,63,'B');put(55,61,'B');

 }
 // The source head is a separate anatomical part; the torso, pelvis and limbs are newly constructed.
 // Turn its gaze forward and keep the original horns, brow, eye, mouth and stepped cheek.
 const headRows=source.rows.slice(1,27).map(r=>r.slice(20,45).map(c=>'ADNO'.includes(c)?'.':c).join(''));
 const head=C.rotate(headRows,20,[12,14],80,(g,x,y)=>x>=7&&x<=14&&y>=11&&y<=17&&'BCE'.includes(g[y][x]));
 head.forEach((r,y)=>r.forEach((c,x)=>{if(c!=='.')put(x+25-40,y+27-40,detail?c:'E');}));
 // Seal only one-cell rasterisation seams. Larger anatomical spaces remain open.
 const sealed=[];for(let pass=0;pass<2;pass++)for(let y=1;y<H-1;y++)for(let x=1;x<W-1;x++)if(g[y][x]==='.'){
  const near=[g[y-1][x],g[y+1][x],g[y][x-1],g[y][x+1]];if(near.every(c=>c!=='.')){const c=near.find(c=>!('EGK'.includes(c)))||near[0];put(x,y,c);sealed.push([x,y,c]);}
 }
 return {name:'charizard',pal:source.pal,rows:g.map(r=>r.join('')),layers,flame,shapes,detail,sealed,head};
}
module.exports={build,shapes};
