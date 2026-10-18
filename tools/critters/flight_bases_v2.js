// Base-only reconstruction. Native source parts retain their pixel area: no horizontal compression.
// These proposals are deliberately not imported by build_batch.js or the animation rigs.
const C=require('./critter');
const W=170,H=120;
const empty=()=>Array.from({length:H},()=>Array(W).fill('.'));
function inside(x,y,pts){let hit=false;for(let i=0,j=pts.length-1;i<pts.length;j=i++)if((pts[i][1]>y)!==(pts[j][1]>y)&&x<(pts[j][0]-pts[i][0])*(y-pts[i][1])/(pts[j][1]-pts[i][1])+pts[i][0])hit=!hit;return hit;}
function make(name){const original=require('./'+name+'_before_flight.json'),g=empty(),layers=[];
 function put(x,y,c){x=Math.round(x);y=Math.round(y);if(g[y]?.[x]!==undefined)g[y][x]=c;}
 function line(points,c,width=1){for(let j=1;j<points.length;j++){let [x,y]=points[j-1], [xx,yy]=points[j],n=Math.max(Math.abs(xx-x),Math.abs(yy-y));for(let i=0;i<=n;i++)for(let a=0;a<width;a++)for(let b=0;b<width;b++)put(x+(xx-x)*i/(n||1)+a,y+(yy-y)*i/(n||1)+b,c);}}
 function poly(pts,c,edge){for(let y=0;y<H;y++)for(let x=0;x<W;x++)if(inside(x+.5,y+.5,pts))put(x,y,c);if(edge)line([...pts,pts[0]],edge);}
 function patch(text,at){text.trim().split('\n').forEach((r,y)=>[...r.trim()].forEach((c,x)=>{if(c!=='.')put(at[0]+x,at[1]+y,c);}));}
 function part(id,rect,pivot,target,angle=0,mask=()=>true,shade={}){
  const [rx,ry,rw,rh]=rect;
  const rows=Array.from({length:rh},(_,y)=>Array.from({length:rw},(_,x)=>mask(x+rx,y+ry,original.rows[y+ry]?.[x+rx])?original.rows[y+ry]?.[x+rx]||'.':'.').join(''));
  const cell=160,rotated=angle?C.rotate(rows,angle,[pivot[0]-rx,pivot[1]-ry],cell):null;
  const layer=empty();
  if(rotated)rotated.forEach((r,y)=>r.forEach((c,x)=>{const xx=x+target[0]-cell/2,yy=y+target[1]-cell/2;if(c!=='.'&&g[yy]?.[xx]!==undefined)layer[yy][xx]=g[yy][xx]=shade[c]||c;}));
  else rows.forEach((r,y)=>[...r].forEach((c,x)=>{const xx=x+rx-pivot[0]+target[0],yy=y+ry-pivot[1]+target[1];if(c!=='.'&&g[yy]?.[xx]!==undefined)layer[yy][xx]=g[yy][xx]=shade[c]||c;}));
  layers.push({id,sourcePixels:rows.join('').replace(/\./g,'').length,pixels:layer.flat().filter(c=>c!=='.').length,angle,scale:1,rows:layer.map(r=>r.join(''))});
 }
 return {name,original,g,layers,put,line,poly,patch,part};
}
function skarmory(){const a=make('skarmory'),{part,poly,line,patch}=a;
 // Blades spread from separate shoulder joints, retaining the original metal/red facets.
 part('far-wing',[21,0,15,38],[28,35],[45,40],35,(x,y)=>x<35);
 part('tail',[44,35,22,12],[45,42],[64,51],-12);
 // Bent hock joints and curling claws are rebuilt at the source's full thickness.
 patch(`
 GACCC................
 .CAANG...............
 ..LANNGL.............
 ...GNNNAGG...........
 ....GAANNNG..........
 .....GCCNNG..GEL.....
 .......GCNGGMEMG.....
 ........GANHMMG......
 .........GAMMG.......
 ..........GGL........`,[41,49]);
 patch(`
 .GANNGL...............
 ..GHNNNG..............
 ...GANNAGG............
 ....GCNNNNAGG.........
 .....GCCANNNNG........
 ........GLNNNG..GEG...
 .........GCNNGGMEML...
 ..........GCNHMMML....
 ...........GAAMMG.....
 ............GCLG......`,[54,50]);
 line([[66,56],[68,54],[70,54]],'C');
 line([[67,57],[69,56],[71,56]],'M');
 line([[68,59],[70,58],[71,58]],'C');
 // Preserve the neck's articulated collar and each independent chest plate.
 part('body-neck',[16,30,33,22],[16,30],[26,32],0,(x,y)=>y<38?x<33:(x<46||(y<47&&x<49)));
 part('near-wing',[32,7,34,32],[34,37],[47,40],35,(x,y)=>x>=35||(y>=20&&x>=32));
 poly([[44,39],[48,38],[51,42],[48,46],[45,45]],'N','G');
 line([[45,40],[47,40],[49,42]],'M');line([[46,43],[48,44]],'A');
 part('head',[0,13,22,26],[0,13],[10,15]);
 return a;}
function dragonite(){const a=make('dragonite'),{part,poly,line}=a;
 part('far-wing',[31,13,28,17],[33,28],[46,46],-64,(x,y)=>x>=32&&(y<=22||x<=43),{D:'G',G:'O',C:'N',M:'P'});
 part('tail',[40,23,24,44],[42,62],[68,66],65,(x,y)=>x>=44||(y>=54&&x>=40));
 part('far-leg',[2,50,17,26],[12,52],[59,66],-78,(x,y)=>x<=(y<67?18:18));
 // Fill areas originally hidden by arms, then reorient the full belly and flank without scaling.
 poly([[35,46],[43,44],[53,48],[66,56],[73,66],[74,73],[70,79],[63,83],[54,84],[43,80],[33,73],[28,64],[29,55]],'C','A');
 poly([[51,49],[62,54],[70,62],[72,72],[65,80],[54,82],[44,78],[39,73]],'N');
 poly([[62,73],[69,69],[69,76],[63,81],[54,82],[48,79]],'P');
 part('abdomen',[8,28,34,41],[25,47],[48,62],-50,(x,y)=>x>=(y<35?17:y<40?14:y<47?11:8)&&x<=(y<36?28:y<43?29:y<49?29:y<54?26:y<61?24:y<65?27:31));
 for(let y=82;y<90;y++)for(let x=43;x<62;x++)if(y>84-Math.abs(x-54)*.24)a.put(x,y,'.');
 // A complete far arm with its claws remains visible below the chest.
 part('far-arm',[0,33,18,15],[15,34],[34,55],-60,(x,y)=>x<=(y<39?14:12),{C:'N',M:'P'});
 part('near-thigh',[26,49,16,20],[33,51],[62,66],-15,(x,y)=>x>=(y<54?28:25));
 part('near-foot',[30,68,14,12],[36,69],[66,82],-95);
 part('near-wing',[31,13,28,17],[33,28],[52,48],-24,(x,y)=>x>=32&&(y<=22||x<=43));
 part('near-arm',[27,32,17,20],[31,33],[38,51],-35,(x,y)=>inside(x+.5,y+.5,[[30,32],[34,35],[41,41],[44,46],[44,51],[34,51],[33,46],[31,41],[28,36]]));
 // The neck overlaps the shoulder, with a continuous creamy throat under the jaw.
 poly([[27,42],[35,43],[40,48],[39,54],[34,53],[29,49]],'N');
 line([[27,43],[28,47],[30,50]],'A');
 poly([[28,44],[32,46],[36,50],[34,54],[31,51]],'E');line([[30,47],[34,50]],'F');
 part('head',[4,0,31,29],[4,0],[8,18],0,(x,y)=>!(y>=15&&y<=22&&x<13)&&(y<12||x<=31));
 return a;}
function charizard(){const a=make('charizard'),{part,poly,line}=a;
 // The far membrane opens above the shoulder with its own elbow and three fingers.
 poly([[40,46],[31,33],[20,16],[26,6],[31,13],[35,16],[44,13],[53,20],[56,31],[52,42],[48,33],[44,34]],'K','E');
 poly([[32,17],[36,20],[43,18],[49,22],[51,31],[48,28],[43,26],[41,34]],'C');
 line([[40,44],[33,31],[24,16],[27,8]],'M',2);
 line([[31,17],[37,20],[43,17],[51,23],[54,33]],'J');
 line([[35,21],[43,28],[48,33]],'E');
 // A full tapered tail with a visible cream underside, not a thin line.
 poly([[71,61],[85,62],[99,57],[112,48],[123,43],[130,38],[134,38],[132,43],[123,49],[113,57],[101,66],[86,73],[73,74]],'J','E');
 poly([[77,67],[89,66],[102,61],[114,53],[126,46],[130,42],[128,47],[116,58],[101,68],[86,73],[76,73]],'M');
 poly([[78,72],[92,69],[106,63],[116,56],[110,63],[97,72],[86,76],[77,75]],'I','G');
 part('flame',[43,0,22,21],[52,17],[131,39],0,(x,y,c)=>'ADNO'.includes(c));
 part('far-thigh-foot',[47,56,20,22],[53,57],[67,65],-76);
 // The original full-sized abdomen keeps its highlight transition and checker-edge shading.
 poly([[37,41],[48,42],[61,48],[72,56],[78,66],[76,76],[68,84],[58,86],[47,82],[37,75],[30,63],[30,51]],'J','E');
 poly([[61,49],[72,57],[76,68],[72,78],[63,83],[53,83],[47,78],[58,73],[65,64]],'M');
 poly([[36,51],[44,50],[54,54],[64,64],[67,72],[63,80],[54,83],[44,78],[35,69],[32,60]],'I');
 part('abdomen',[28,42,28,30],[42,51],[51,60],-48,(x,y,c)=>'HI'.includes(c));
 // Neck is re-posed independently; the head itself is not tilted with the abdomen.
 poly([[27,30],[36,31],[39,39],[45,45],[43,53],[35,54],[30,46],[27,40]],'M','E');
 poly([[35,33],[37,39],[43,45],[41,52],[38,50],[35,44]],'J');
 part('near-thigh',[15,52,17,18],[24,54],[64,68],-20);
 part('near-foot',[12,69,15,10],[21,70],[67,84],-95);
 poly([[51,46],[59,24],[70,9],[76,5],[76,11],[83,16],[91,26],[99,40],[101,55],[96,48],[91,44],[87,44],[82,49],[77,45],[70,44],[66,47],[61,42],[57,43]],'K','E');
 poly([[61,28],[72,14],[79,20],[86,30],[94,43],[88,40],[82,43],[76,39],[68,41]],'C');
 line([[52,45],[60,24],[71,11],[75,8]],'G',3);
 line([[53,44],[61,24],[72,11],[75,8]],'J');
 line([[73,13],[82,19],[90,29],[97,42],[99,52]],'M',2);
 line([[73,13],[79,25],[82,36],[82,47]],'E');
 line([[63,25],[67,33],[67,44]],'E');
 line([[73,14],[80,27],[83,36]],'M');
 line([[53,44],[55,40],[58,41]],'J',2);
 // Fingers and bent elbows retain the full hand and all visible claws.
 part('far-arm',[55,37,12,16],[56,39],[30,48],-10,(x,y,c)=>x>=56&&!('CK'.includes(c)),{J:'M'});
 part('near-arm',[19,36,16,19],[25,38],[47,53],-25,(x,y,c)=>!('CK'.includes(c))&&inside(x+.5,y+.5,[[20,36],[27,37],[33,40],[29,44],[32,48],[31,54],[23,54],[20,49],[21,43],[19,40]]));
 // Claw tips lost by the part rotation are reconstructed as connected pairs, never isolated sparkles.
 line([[32,59],[32,60]],'L');line([[35,57],[35,58]],'L');line([[38,59],[38,60]],'L');
 line([[47,64],[47,65]],'L');line([[51,66],[51,67]],'L');line([[54,64],[54,65]],'L');
 part('head',[20,1,25,25],[20,1],[7,13],0,(x,y,c)=>!'ADNO'.includes(c));
 return a;}
function salamence(){const a=make('salamence'),{part,poly,line}=a;
 // Far fixed sail: retain the original crescent outline and highlight, never a bat wing.
 poly([[43,44],[34,35],[29,24],[25,13],[23,7],[33,8],[44,13],[54,22],[60,33],[60,43],[54,37],[49,37]],'J','H');
 poly([[26,9],[33,10],[43,15],[52,23],[57,33],[55,30],[49,22],[39,16]],'A');
 line([[24,8],[32,9],[43,14],[52,22]],'G');
 line([[43,43],[37,34],[33,24]],'A',2);
 // Thick tail follows the hindquarters, with the characteristic red underside.
 poly([[80,57],[94,59],[107,58],[120,53],[133,46],[139,42],[136,48],[126,57],[113,65],[98,70],[85,68]],'B','P');
 poly([[90,61],[105,61],[120,56],[131,49],[125,56],[112,63],[98,66],[90,65]],'M');
 poly([[88,66],[101,67],[117,62],[128,55],[119,64],[106,70],[96,72],[87,69]],'A','H');
 poly([[28,51],[34,52],[29,61],[31,66],[38,66],[41,69],[38,73],[31,72],[25,68],[21,60]],'K','P');
 poly([[24,53],[27,54],[25,60],[28,66],[33,69],[38,69],[36,71],[30,70],[24,65]],'B');
 poly([[36,68],[39,69],[38,72],[35,71]],'N','E');line([[36,69],[37,70]],'C');
 poly([[79,50],[85,53],[85,59],[89,64],[95,64],[98,67],[96,71],[89,71],[82,66],[78,59]],'K','P');
 poly([[81,53],[83,55],[83,61],[89,67],[94,66],[95,69],[89,69],[81,63]],'B');
 poly([[94,65],[97,66],[96,69],[93,69]],'F','P');line([[94,66],[95,67]],'N');
 // Broad shoulders, lumbar region and the uncompressed plated belly.
 poly([[27,42],[42,40],[60,41],[76,47],[87,56],[87,64],[78,71],[58,74],[39,67],[28,56]],'M','E');
 poly([[30,53],[45,58],[61,63],[77,62],[86,58],[87,64],[78,71],[58,74],[40,67]],'B');
 poly([[32,43],[44,42],[58,43],[73,48],[80,54],[72,56],[63,53],[53,53],[43,51]],'M');
 poly([[32,54],[41,58],[52,60],[64,59],[75,56],[84,56],[84,63],[76,68],[60,72],[43,65]],'B');
 line([[43,44],[44,48],[45,51]],'O');line([[64,45],[65,49],[67,53]],'O');
 line([[69,49],[71,52],[73,53]],'K');
 part('back-surface',[30,43,35,8],[30,43],[35,45],0,(x,y,c)=>'MOK'.includes(c));
 // Five overlapping plates turn beneath the ribcage instead of becoming a single white patch.
 poly([[33,56],[39,56],[44,62],[48,67],[44,68],[38,65],[34,61]],'C','E');
 poly([[40,57],[46,57],[51,63],[55,70],[49,70],[44,66]],'C','E');
 poly([[47,58],[54,58],[60,64],[63,71],[56,72],[52,68]],'C','E');
 poly([[55,59],[61,59],[66,64],[69,70],[64,73],[61,69]],'N','E');
 poly([[62,60],[67,59],[72,64],[74,68],[70,71],[67,68]],'N','E');
 line([[40,64],[44,67]],'N');line([[47,65],[50,69]],'N');
 line([[54,67],[57,71]],'F');line([[62,68],[65,71]],'F');
 // The near foreleg folds at the elbow, with a rounded upper arm and three full toe claws.
 poly([[44,56],[50,58],[50,64],[47,69],[50,74],[56,74],[60,77],[58,81],[52,81],[46,78],[40,72],[40,64]],'B','P');
 poly([[44,58],[48,59],[48,64],[44,69],[45,72],[50,77],[53,77],[52,79],[46,76],[42,71],[42,64]],'M');
 poly([[47,69],[48,73],[52,76],[57,76],[59,78],[56,80],[52,80],[47,77],[43,72]],'K');
 poly([[55,75],[58,76],[59,78],[57,80],[54,79]],'F','P');
 line([[55,76],[56,77]],'C');line([[57,77],[58,78]],'N');line([[55,79],[56,79]],'C');
 line([[47,60],[47,62],[45,65],[44,68]],'O');
 line([[46,74],[48,76],[51,78]],'O');
 // Rounded hind thigh, flexed hock, and a separated foot behind the abdomen.
 poly([[74,60],[82,61],[84,65],[82,70],[79,74],[83,78],[90,77],[94,80],[91,84],[84,85],[78,82],[73,77],[70,70],[70,65]],'B','P');
 poly([[74,62],[79,62],[81,65],[79,69],[75,71],[75,76],[80,80],[85,82],[82,82],[77,79],[73,73],[72,67]],'M');
 poly([[79,72],[79,75],[84,79],[90,79],[92,81],[88,84],[83,83],[78,79],[75,76]],'K');
 poly([[89,78],[92,79],[93,81],[90,84],[87,83],[87,80]],'F','P');
 line([[89,79],[90,80]],'C');line([[91,80],[91,81]],'N');line([[88,82],[89,83]],'C');
 line([[80,64],[80,66],[78,69],[76,70]],'O');
 line([[73,69],[74,73],[77,77]],'O');
 // Neck is longer and slopes into the shoulders; its red underside remains recognisable.
 poly([[24,31],[31,31],[38,39],[46,45],[43,53],[36,53],[30,44]],'B','P');
 poly([[24,34],[29,35],[34,43],[38,48],[35,50],[29,44]],'A','H');
 line([[30,33],[34,38],[39,43]],'M',2);
 poly([[55,46],[53,34],[52,22],[48,13],[43,7],[53,7],[66,11],[79,18],[90,27],[99,39],[103,51],[103,62],[98,55],[92,50],[85,48],[79,49],[72,54],[64,56]],'A','H');
 poly([[47,8],[54,9],[67,13],[79,20],[89,29],[97,41],[100,51],[97,47],[91,36],[82,26],[70,18],[58,13]],'J');
 line([[46,8],[54,8],[67,12],[79,19],[90,28],[98,40]],'G');
 line([[55,45],[60,49],[65,51],[71,51],[78,46],[85,45],[92,48],[98,53],[102,60]],'J',2);
 // Restore the broad muscular saddle under the fixed wing root.
 poly([[48,43],[53,40],[58,42],[62,47],[62,50],[58,48],[54,45],[50,45]],'B','E');
 line([[50,43],[53,42],[57,44]],'M');
 line([[46,50],[50,52],[52,55]],'O');
 part('head',[3,5,31,28],[3,5],[6,13],0,(x,y,c)=>!(x>=14&&y<=12&&'AJGHLD'.includes(c))&&!(x>=27&&y<=17&&'AJGHLD'.includes(c)));
 return a;}
function finish(a){
 const ink={skarmory:'CGL',dragonite:'AH',charizard:'EG',salamence:'DPH'}[a.name],sealed=[];
 for(let pass=0;pass<2;pass++)for(let y=1;y<H-1;y++)for(let x=1;x<W-1;x++)if(a.g[y][x]==='.'){
  const near=[a.g[y-1][x],a.g[y+1][x],a.g[y][x-1],a.g[y][x+1]];
  if(near.every(c=>c!=='.')){const c=near.find(c=>!ink.includes(c))||near[0];a.put(x,y,c);sealed.push([x,y,c]);}
 }
 // Two detached dark end cells resulted from the 95-degree heel rotation.
 if(a.name==='dragonite'){a.put(65,86,'.');a.put(65,87,'.');}
 a.sealed=sealed;return a;
}
module.exports={buildAll:()=>[skarmory(),dragonite(),charizard(),salamence()].map(finish),inside};
