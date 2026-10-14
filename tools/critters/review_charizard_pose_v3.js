// Review a single static Charizard pose. No animation files or app resources are written.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),assert=require('assert/strict'),{execFileSync}=require('child_process');
const {build}=require('./charizard_flight_pose_v3'),C=require('./critter');
const OUT='C:/Users/javie/Desktop/imagenes/revision/charizard_pose_v3',ROOT=path.resolve('../..');
const FF='C:/Program Files (x86)/Soundux/ffmpeg.exe';
const a=build(true),sil=build(false),rgb=h=>[0,2,4].map(p=>parseInt(h.slice(p,p+2),16));
const points=[];a.rows.forEach((r,y)=>[...r].forEach((c,x)=>{if(c!=='.')points.push([x,y]);}));
const bounds=[Math.min(...points.map(p=>p[0])),Math.min(...points.map(p=>p[1])),Math.max(...points.map(p=>p[0])),Math.max(...points.map(p=>p[1]))];
const [ox,oy,xx,yy]=bounds,rows=a.rows.slice(oy,yy+1).map(r=>r.slice(ox,xx+1)),w=rows[0].length,h=rows.length;
function raster(g,pal,bg='DCE6EB',alpha=false){const W=g[0].length,H=g.length,channels=alpha?4:3,b=Buffer.alloc(W*H*channels);g.forEach((r,y)=>[...r].forEach((c,x)=>{b.set(c==='.'?(alpha?[0,0,0,0]:rgb(bg)):[...rgb(pal[c]),...(alpha?[255]:[])],(y*W+x)*channels);}));return b;}
function save(b,W,H,file,k=1,alpha=false){execFileSync(FF,['-v','error','-y','-f','rawvideo','-pixel_format',alpha?'rgba':'rgb24','-video_size',`${W}x${H}`,'-i','pipe:0','-vf',`scale=iw*${k}:ih*${k}:flags=neighbor`,'-frames:v','1',path.join(OUT,file)],{input:b});}
save(raster(rows,a.pal,'DCE6EB',true),w,h,'charizard_base_nativa.png',1,true);
save(raster(rows,a.pal),w,h,'charizard_base_ampliada.png',6);
save(raster(sil.rows,{E:'0E1822'}),174,92,'01_silueta.png',6);
save(raster(a.rows,a.pal),174,92,'03_base_detallada.png',6);
fs.writeFileSync(path.join(OUT,'charizard_base.txt'),Object.entries(a.pal).map(([c,v])=>c+'='+v).join(' ')+'\n'+rows.join('\n')+'\n');
fs.writeFileSync(path.join(OUT,'charizard_base.json'),JSON.stringify({pal:a.pal,rows,sourceCanvasOffset:[ox,oy],stage:'Pose base pendiente de aprobacion',poseReference:'https://gamesdb.launchbox-app.com/games/images/13003-pokmon-ranger'},null,2)+'\n');
// Same character-height range for the complete reference and the native sprite at x4.
const W=1336,H=452,buffer=Buffer.alloc(W*H*3);for(let i=0;i<W*H;i++)buffer.set(rgb('182B3A'),i*3);
const reference=execFileSync(FF,['-v','error','-i',path.join(OUT,'referencias/ranger.png'),'-vf','scale=640:360:flags=lanczos','-frames:v','1','-f','rawvideo','-pix_fmt','rgb24','pipe:1']);
for(let y=0;y<360;y++)reference.copy(buffer,((y+48)*W+16)*3,y*640*3,(y+1)*640*3);
for(let y=48;y<408;y++)for(let x=676;x<1320;x++)buffer.set(rgb('DCE6EB'),(y*W+x)*3);
const dx=676+Math.floor((644-w*4)/2),dy=48+Math.floor((360-h*4)/2);
rows.forEach((r,y)=>[...r].forEach((c,x)=>{if(c!=='.')for(let v=0;v<4;v++)for(let u=0;u<4;u++)buffer.set(rgb(a.pal[c]),((dy+y*4+v)*W+dx+x*4+u)*3);}));
const labels=[['REFERENCIA DE VUELO - POKEMON RANGER',16,14,19],['CHARIZARD - NUEVA POSE BASE x4',676,14,19],['Ilustracion de 2006 conservada en LaunchBox. Referencia de postura.',16,420,15],['Paleta del sprite original. Sin animar ni integrar. Pendiente de aprobacion.',676,420,15]];
const filters=labels.map(([t,x,y,z])=>`drawtext=fontfile='C\\:/Windows/Fonts/arial.ttf':text='${t}':x=${x}:y=${y}:fontsize=${z}:fontcolor=white:expansion=none`).join(',');
execFileSync(FF,['-v','error','-y','-f','rawvideo','-pixel_format','rgb24','-video_size',`${W}x${H}`,'-i','pipe:0','-vf',filters,'-frames:v','1',path.join(OUT,'charizard_pose_y_referencia.png')],{input:buffer});
const seen=new Set(),components=[];for(const [x,y] of points){const id=x+','+y;if(seen.has(id))continue;const q=[[x,y]];seen.add(id);let count=0;while(q.length){const [px,py]=q.pop();count++;for(let vy=-1;vy<=1;vy++)for(let vx=-1;vx<=1;vx++){const nx=px+vx,ny=py+vy,key=nx+','+ny;if(!a.rows[ny]?.[nx]||a.rows[ny][nx]==='.'||seen.has(key))continue;seen.add(key);q.push([nx,ny]);}}components.push(count);}
const check=C.check([a.rows]);assert.equal(check.iso,0);assert.equal(check.holes,0);assert.equal(components.length,1);
assert.ok([...new Set(rows.join('').replace(/\./g,''))].every(c=>a.pal[c]));
const source=require('./charizard_before_flight.json');
const headPixels=a.head.flat().filter(c=>c!=='.').length;
let headKept=0;a.head.forEach((r,y)=>r.forEach((c,x)=>{if(c!=='.'&&a.rows[y+27-40]?.[x+25-40]===c)headKept++;}));
assert.equal(headKept,headPixels);
const audit={stage:'Solo base; no hay ciclos nuevos',bounds,size:[w,h],palette:source.pal,check,components,headComposition:{pixels:headPixels,preserved:headKept,partRotation:20,scale:1},sealedOneCellSeams:a.sealed,landmarks:{neck:[37,35],thorax:[54,47],pelvis:[83,51],tailRoot:[91,54],nearElbow:[61,50],nearKnee:[86,58],nearFoot:[101,69]},reference:{page:'https://gamesdb.launchbox-app.com/games/images/13003-pokmon-ranger',image:'https://images.launchbox-app.com/d4b1eb5d-8a70-48ef-845f-a9572b83e701.png',label:'Ilustracion de Pokemon Ranger; copyright 2006 visible en la imagen',file:'referencias/ranger.png'},limits:['No prueba trayectorias, inclinaciones ni fotogramas','Cola completa reconstruida: el entrenador oculta su extremo en la referencia','La aprobacion de la pose es visual, no queda demostrada por los chequeos de pixeles']};
fs.writeFileSync(path.join(OUT,'auditoria.json'),JSON.stringify(audit,null,2)+'\n');
const prior=JSON.parse(fs.readFileSync('C:/Users/javie/Desktop/imagenes/revision/vuelo_tanda2_bases_rehechas/conservacion.json','utf8'));
const files=[...new Set([...prior.rows.map(r=>r.path),'tools/critters/flight_bases_v2.js','tools/critters/review_flight_bases_v2.js'])];
const sha=b=>crypto.createHash('sha256').update(b).digest('hex'),canonical=(b,p)=>p.endsWith('.png')?b:Buffer.from(b.toString('utf8').replace(/\r\n/g,'\n'));
const preservation=files.map(p=>{const bytes=fs.readFileSync(path.join(ROOT,p)),head=execFileSync('git',['show','HEAD:'+p],{cwd:ROOT,maxBuffer:8*1024*1024});return {path:p,sha256:sha(bytes),unchanged:sha(canonical(bytes,p))===sha(canonical(head,p))};});assert.ok(preservation.every(r=>r.unchanged));
fs.writeFileSync(path.join(OUT,'conservacion.json'),JSON.stringify({commit:execFileSync('git',['rev-parse','HEAD'],{cwd:ROOT,encoding:'utf8'}).trim(),files:preservation.length,unchanged:preservation.filter(r=>r.unchanged).length,comparison:'PNG byte a byte; texto normalizando CRLF a LF',rows:preservation},null,2)+'\n');
console.log(JSON.stringify({size:audit.size,check,components,head:audit.headComposition,protected:preservation.length},null,2));
