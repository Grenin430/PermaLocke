// Reproducible visual review and structural audits for the first flight correction batch.
const fs=require('fs'),assert=require('assert/strict'),{execFileSync}=require('child_process'),C=require('./critter'),specs=require('./flight_batch2_specs');
const OUT='C:/Users/javie/Desktop/imagenes/revision/vuelo_tanda2',FF='C:/Program Files (x86)/Soundux/ffmpeg.exe';
fs.mkdirSync(OUT,{recursive:true});
function components(g){const seen=new Set(),sizes=[];for(let y=0;y<g.length;y++)for(let x=0;x<g[0].length;x++){const key=x+','+y;if(g[y][x]==='.'||seen.has(key))continue;let q=[[x,y]],n=0;seen.add(key);while(q.length){const [xx,yy]=q.pop();n++;for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){const a=xx+dx,b=yy+dy,k=a+','+b;if(g[b]?.[a]&&g[b][a]!=='.'&&!seen.has(k)){seen.add(k);q.push([a,b]);}}}sizes.push(n);}return sizes;}
const round=v=>{const a=Math.floor(v);return v-a===.5?(a%2?a+1:a):Math.round(v);};
function render(fr,pal,name,scale=4,labels=[]){const w=fr[0][0].length,h=fr[0].length,buf=Buffer.alloc(w*h*3*fr.length);fr.forEach((g,f)=>g.forEach((r,y)=>[...r].forEach((c,x)=>{const hex=c==='.'?'2A6A90':pal[c];[0,2,4].forEach((p,k)=>buf[((f*h+y)*w+x)*3+k]=parseInt(hex.slice(p,p+2),16));})));fs.writeFileSync('rev/flight_batch2.rgb',buf);const filters=[`scale=iw*${scale}:ih*${scale}:flags=neighbor`,...labels.map(([text,x,y])=>`drawtext=fontfile='C\\:/Windows/Fonts/arial.ttf':text='${text}':x=${x}:y=${y}:fontsize=18:fontcolor=white`)];execFileSync(FF,['-v','error','-y','-f','rawvideo','-pixel_format','rgb24','-video_size',`${w}x${h}`,'-i','rev/flight_batch2.rgb','-vf',filters.join(','),'-frames:v','1',OUT+'/'+name]);}
const reports=[];
for(const name of Object.keys(specs)){
 const m=require('./'+name+'_flight_anim'),w=m.fr[0][0].length,h=m.fr[0].length,maxTilt=Math.max(...m.angles.map(Math.abs))*Math.PI/180;
 let cell=Math.ceil(Math.max(w*Math.cos(maxTilt)+h*Math.sin(maxTilt),h*Math.cos(maxTilt)+w*Math.sin(maxTilt)));cell+=cell%2;
 const {cells}=C.sheet(name+'_flight',m.pal,m.fr,m.angles,[w/2,h/2],cell,m.protect);require('./seal_sheet')(name+'_flight',m.pal,cells);
 fs.copyFileSync(name+'_flight_sheet.png',OUT+'/'+name+'_hoja_definitiva.png');
 C.previews(name+'_flight',m.pal,m.fr,m.fps,4,OUT);
 const old=require('./'+name+'_before_flight.json'),before=old.rows.map(r=>[...r].join('')),pw=160,ph=104;
 const pair=Array.from({length:ph},()=>Array(pw*2).fill('.'));
 [before,m.fr[0]].forEach((g,i)=>{const ox=i*pw+8,oy=12;g.forEach((r,y)=>[...r].forEach((c,x)=>{if(c!=='.'&&pair[y+oy]?.[x+ox]!==undefined)pair[y+oy][x+ox]=c;}));});
 render([pair.map(r=>r.join(''))],m.pal,name+'_antes_y_despues.png',4,[[name.toUpperCase()+' - ANTES',12,7],['VUELO CORREGIDO',652,7]]);
 const flat=cells.flat().map(g=>g.map(r=>r.join(''))),eye=m.spec.eye,eyes=[];
 for(let r=0;r<m.angles.length;r++)for(let f=0;f<m.fr.length;f++){
  const a=m.angles[r]*Math.PI/180,hits=[];for(let y=eye[1];y<eye[1]+eye[3];y++)for(let x=eye[0];x<eye[0]+eye[2];x++){const dx=x+.5-w/2,dy=y+.5-h/2,xx=Math.floor(Math.cos(a)*dx-Math.sin(a)*dy+cell/2),yy=Math.floor(Math.sin(a)*dx+Math.cos(a)*dy+cell/2);hits.push(cells[r][f][yy][xx]);}
  eyes.push({r,f,dark:hits.filter(c=>eye[4].includes(c)).length,light:hits.filter(c=>eye[5].includes(c)).length});
 }
 const head=m.fr.map(g=>{let total=0,kept=0;m.head.forEach((r,y)=>[...r].forEach((c,x)=>{if(c!=='.'){total++;if(g[y+m.headAt[1]][x+m.headAt[0]]===c)kept++;}}));return {total,kept};});
 const transitions=m.fr.map((g,f)=>g.reduce((n,r,y)=>n+[...r].filter((c,x)=>c!==m.fr[(f+1)%m.fr.length][y][x]).length,0));
 const bounds=flat.map(g=>{let lo=cell,hi=0;g.forEach((r,y)=>{if(/[^.]/.test(r)){lo=Math.min(lo,y);hi=Math.max(hi,y);}});return [lo+round(3+m.layoutHeight/2-cell/2),hi+round(65-m.layoutHeight/2-cell/2)];});
 const fixedWings=name==='salamence'?m.layers.every(l=>JSON.stringify(l)===JSON.stringify(m.layers[0])):undefined;
 const nativeClipped=m.fr.filter(g=>/[^.]/.test(g[0]+g.at(-1))||g.some(r=>r[0]!=='.'||r.at(-1)!=='.')).length;
 const report={name,fixedWings,nativeClipped,canvas:[w,h],cell,fps:m.fps,frames:m.fr.length,cells:flat.length,paletteIdentical:JSON.stringify(m.pal)===JSON.stringify(old.pal),native:C.check(m.fr),sheet:C.check(flat),components:m.fr.map(components),rotatedComponents:flat.map(g=>components(g).length),head,eyes,clipped:flat.filter(g=>/[^.]/.test(g[0]+g.at(-1))||g.some(r=>r[0]!=='.'||r.at(-1)!=='.')).length,sky:{min:Math.min(...bounds.map(v=>v[0])),max:Math.max(...bounds.map(v=>v[1])),range:62-m.layoutHeight},transitions,loopPassed:transitions.at(-1)<=Math.max(...transitions.slice(0,-1))*1.05,sealed:m.sealed};
 reports.push(report);console.log(JSON.stringify({...report,eyes:undefined,head:head[0],sheet:{iso:report.sheet.iso,holes:report.sheet.holes}}));
}
fs.writeFileSync(OUT+'/auditoria.json',JSON.stringify(reports,null,2)+'\n');
if(reports.some(r=>r.fixedWings===false||r.nativeClipped||!r.paletteIdentical||r.native.iso||r.native.holes||r.sheet.iso||r.sheet.holes||r.components.some(c=>c.length!==1)||r.rotatedComponents.some(n=>n!==1)||r.head.some(h=>h.total!==h.kept)||r.eyes.some(e=>!e.dark||!e.light)||r.clipped||r.sky.min<0||r.sky.max>=69||r.sky.range<=0||!r.loopPassed))process.exitCode=1;
