// Static review only: never updates production sheets, animation modules, META or the C# catalogue.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),assert=require('assert/strict'),{execFileSync}=require('child_process'),C=require('./critter');
const OUT='C:/Users/javie/Desktop/imagenes/revision/vuelo_tanda2_bases_rehechas';
const ROOT=path.resolve('../..'),REF='C:/Users/javie/Desktop/imagenes',FF='C:/Program Files (x86)/Soundux/ffmpeg.exe';
fs.mkdirSync(OUT,{recursive:true});
const bg='2A6A90',rgb=hex=>[0,2,4].map(p=>parseInt(hex.slice(p,p+2),16));
function canvas(w,h,color='182B3A'){const data=Buffer.alloc(w*h*3);for(let i=0;i<w*h;i++)data.set(rgb(color),i*3);return {w,h,data};}
function fill(c,x,y,w,h,color){for(let yy=y;yy<y+h;yy++)for(let xx=x;xx<x+w;xx++)c.data.set(rgb(color),(yy*c.w+xx)*3);}
function blit(c,g,pal,x,y,k=1){g.forEach((r,yy)=>[...r].forEach((ch,xx)=>{if(ch==='.')return;for(let j=0;j<k;j++)for(let i=0;i<k;i++)c.data.set(rgb(pal[ch]),((y+yy*k+j)*c.w+x+xx*k+i)*3);}));}
function save(c,file,labels=[]){const filters=labels.map(([text,x,y,size=18])=>`drawtext=fontfile='C\\:/Windows/Fonts/arial.ttf':text='${text}':x=${x}:y=${y}:fontsize=${size}:fontcolor=white:expansion=none`);execFileSync(FF,['-v','error','-y','-f','rawvideo','-pixel_format','rgb24','-video_size',`${c.w}x${c.h}`,'-i','pipe:0',...(filters.length?['-vf',filters.join(',')]:[]),'-frames:v','1',path.join(OUT,file)],{input:c.data});}
function bounds(g){const points=[];g.forEach((r,y)=>[...r].forEach((c,x)=>{if(c!=='.')points.push([x,y]);}));return [Math.min(...points.map(p=>p[0])),Math.min(...points.map(p=>p[1])),Math.max(...points.map(p=>p[0])),Math.max(...points.map(p=>p[1]))];}
function trim(g){const [x,y,xx,yy]=bounds(g);return g.slice(y,yy+1).map(r=>[...r].slice(x,xx+1).join(''));}
function regions(g,on,diagonal){const h=g.length,w=g[0].length,seen=new Set(),out=[];for(let y=0;y<h;y++)for(let x=0;x<w;x++){
 const id=y*w+x;if(seen.has(id)||(g[y][x]!=='.')!==on)continue;const q=[[x,y]],points=[];seen.add(id);
 while(q.length){const [xx,yy]=q.pop();points.push([xx,yy]);for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1],...(diagonal?[[1,1],[-1,-1],[1,-1],[-1,1]]:[])]){const a=xx+dx,b=yy+dy,id=b*w+a;if(a<0||b<0||a>=w||b>=h||seen.has(id)||(g[b][a]!=='.')!==on)continue;seen.add(id);q.push([a,b]);}}
 out.push({size:points.length,bounds:[Math.min(...points.map(p=>p[0])),Math.min(...points.map(p=>p[1])),Math.max(...points.map(p=>p[0])),Math.max(...points.map(p=>p[1]))],touchesEdge:points.some(([a,b])=>a===0||b===0||a===w-1||b===h-1)});
 }return out;}
const species=require('./flight_bases_v2').buildAll(),reports=[],combined=canvas(1120,1360),combinedLabels=[];
const scalePreview=canvas(780,176),scaleLabels=[['PRUEBA ESTATICA DE ESCALA - cuatro cielos de 178 x 69 px',12,8,17],['No es una captura del layout. No comprueba inclinaciones ni batidos.',12,149,15]];
const nativePreview=canvas(600,155),nativeLabels=[['BASES A RESOLUCION NATIVA (1 pixel = 1 pixel)',12,8,17]];
for(const [i,a] of species.entries()){
 const rows=a.g.map(r=>r.join('')),clean=trim(rows),old=a.original.rows.map(r=>r.join('')),w=clean[0].length,h=clean.length;
 const report={name:a.name,stage:'Base estatica pendiente de aprobacion',nativeBounds:bounds(rows),nativeSize:[w,h],nativePixels:C.check([rows]).pixels[0],originalPixels:C.check([old]).pixels[0],check:C.check([rows]),components:regions(rows,true,true),enclosedAir:regions(rows,false,false).filter(r=>!r.touchesEdge),sealedOneCellSeams:a.sealed,sourceParts:a.layers.map(({rows,...m})=>m),paletteExact:Object.keys(a.original.pal).every(c=>/^[0-9A-F]{6}$/.test(a.original.pal[c])),invalidColours:[...new Set(rows.join('').replace(/\./g,''))].filter(c=>!a.original.pal[c])};
 // Exact face/eye preservation after composition; seam filling may only add previously empty pixels.
 const head=a.layers.find(l=>l.id==='head');report.head={pixels:0,preserved:0};head.rows.forEach((r,y)=>[...r].forEach((c,x)=>{if(c!=='.'){report.head.pixels++;if(rows[y][x]===c)report.head.preserved++;}}));
 assert.equal(report.components.length,1,a.name+' detached pieces');assert.equal(report.check.iso,0);assert.equal(report.check.holes,0);assert.deepEqual(report.invalidColours,[]);assert.equal(report.head.preserved,report.head.pixels);
 fs.writeFileSync(path.join(OUT,a.name+'_base.json'),JSON.stringify({pal:a.original.pal,rows,stage:report.stage,sourceParts:report.sourceParts},null,2)+'\n');
 fs.writeFileSync(path.join(OUT,a.name+'_base.txt'),Object.entries(a.original.pal).map(([c,v])=>c+':'+v).join(' ')+'\n'+clean.join('\n')+'\n');
 const rgba=Buffer.alloc(w*h*4);clean.forEach((r,y)=>[...r].forEach((c,x)=>{if(c!=='.')rgba.set([...rgb(a.original.pal[c]),255],(y*w+x)*4);}));
 execFileSync(FF,['-v','error','-y','-f','rawvideo','-pixel_format','rgba','-video_size',`${w}x${h}`,'-i','pipe:0','-frames:v','1',path.join(OUT,a.name+'_base_nativa.png')],{input:rgba});
 const zoom=canvas(w*6+24,h*6+54,bg);blit(zoom,clean,a.original.pal,12,40,6);save(zoom,a.name+'_base_ampliada.png',[[a.name.toUpperCase()+' - BASE NUEVA x6',12,10]]);
 const pair=canvas(1000,420,bg);blit(pair,old,a.original.pal,12,60,4);blit(pair,clean,a.original.pal,420,60,4);save(pair,a.name+'_original_y_base.png',[[a.name.toUpperCase()+' - SPRITE ORIGINAL x4',12,12],['NUEVA POSE BASE x4',420,12]]);
 // Crop the actual supplied reference at its reconstructed cell boundaries; show at the same cell scale.
 const meta=JSON.parse(fs.readFileSync(a.name+'.grid.json','utf8')),sx=Math.max(0,Math.round(meta.x0+meta.minX*meta.px)),sy=Math.max(0,Math.round(meta.y0+meta.minY*meta.py)),cw=Math.round(meta.w*meta.px),ch=Math.round(meta.h*meta.py),rw=meta.w*3,rh=meta.h*3;
 const ref=path.join(REF,a.name+(a.name==='charizard'?'.png':'.jpg'));
 const reference=execFileSync(FF,['-v','error','-i',ref,'-vf',`crop=${cw}:${ch}:${sx}:${sy},scale=${rw}:${rh}:flags=neighbor`,'-frames:v','1','-f','rawvideo','-pix_fmt','rgb24','pipe:1'],{maxBuffer:8*1024*1024});
 const panel=canvas(1120,340,bg),labels=[[a.name.toUpperCase(),12,10,21],['REFERENCIA ORIGINAL',12,43,16],['SPRITE ORIGINAL x3',285,43,16],['NUEVA POSE BASE x3',585,43,16]];
 for(let y=0;y<rh;y++)reference.copy(panel.data,((y+76)*panel.w+12)*3,y*rw*3,(y+1)*rw*3);
 blit(panel,old,a.original.pal,285,76,3);blit(panel,clean,a.original.pal,585,76,3);
 save(panel,a.name+'_referencia_y_base.png',labels);
 for(let y=0;y<340;y++)panel.data.copy(combined.data,((i*340+y)*combined.w)*3,y*panel.w*3,(y+1)*panel.w*3);
 combinedLabels.push(...labels.map(([t,x,y,z])=>[t,x,y+i*340,z]));
 const displayScale={skarmory:1,dragonite:.75,charizard:.75,salamence:5/6}[a.name],dw=Math.round(w*displayScale),dh=Math.round(h*displayScale);
 const display=Array.from({length:dh},(_,y)=>Array.from({length:dw},(_,x)=>clean[Math.min(h-1,Math.floor((y+.5)/displayScale))][Math.min(w-1,Math.floor((x+.5)/displayScale))]).join(''));
 report.displayTrial={scale:displayScale,size:[dw,dh],skyHeight:69,margin:69-dh,appliedToApp:false};
 fill(scalePreview,12+i*192,64,178,69,bg);blit(scalePreview,display,a.original.pal,12+i*192+Math.floor((178-dw)/2),64+Math.floor((69-dh)/2));
 scaleLabels.push([a.name.toUpperCase(),12+i*192,35,15],[String(Math.round(displayScale*100))+' % - '+dw+' x '+dh+' px',12+i*192,49,12]);
 blit(nativePreview,clean,a.original.pal,12+i*145,47);nativeLabels.push([a.name.toUpperCase(),12+i*145,31,12]);
 reports.push(report);
}
save(combined,'cuatro_referencias_y_bases.png',combinedLabels);
save(scalePreview,'prueba_cielo_69px.png',scaleLabels);save(nativePreview,'bases_tamano_nativo.png',nativeLabels);
const previous=JSON.parse(fs.readFileSync(path.join(REF,'revision/vuelo_tanda2/conservacion.json'),'utf8'));
const files=[...new Set([...previous.rows.map(r=>r.path),...species.map(a=>'src/PermaLocke.App/Views/Cap/Critters/'+a.name+'.png'),'src/PermaLocke.App/Views/Cap/CapCritterArt.cs','tools/critters/build_batch.js','tools/critters/flight_rig2.js','tools/critters/flight_batch2_specs.js',...species.map(a=>'tools/critters/'+a.name+'_flight_anim.js')])];
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const canonical=(b,p)=>p.endsWith('.png')?b:Buffer.from(b.toString('utf8').replace(/\r\n/g,'\n'));
const preservation=files.map(p=>{const current=fs.readFileSync(path.join(ROOT,p)),actual=sha(canonical(current,p)),before=sha(canonical(execFileSync('git',['show','HEAD:'+p],{cwd:ROOT,maxBuffer:8*1024*1024}),p));return {path:p,workingTreeSha256:sha(current),comparison:p.endsWith('.png')?'exact bytes':'text with CRLF normalised to LF',unchanged:actual===before};});
assert.ok(preservation.every(p=>p.unchanged));
fs.writeFileSync(path.join(OUT,'auditoria_bases.json'),JSON.stringify(reports,null,2)+'\n');
fs.writeFileSync(path.join(OUT,'conservacion.json'),JSON.stringify({againstCommit:execFileSync('git',['rev-parse','HEAD'],{cwd:ROOT,encoding:'utf8'}).trim(),files:preservation.length,unchanged:preservation.filter(p=>p.unchanged).length,rows:preservation},null,2)+'\n');
console.log(JSON.stringify({bases:reports.map(r=>({name:r.name,size:r.nativeSize,pixels:r.nativePixels,originalPixels:r.originalPixels,check:r.check,components:r.components.length,head:r.head,display:r.displayTrial})),protected:preservation.length},null,2));
