// Standalone flight review. Never writes app assets, catalog entries, or the approved animation.
const fs=require('fs'),{execFileSync}=require('child_process'),crypto=require('crypto'),C=require('./critter');
const m=require('./pidgey_flight_anim'),old=C.load('pidgey');
const OUT=process.argv[2]||'C:/Users/javie/Desktop/imagenes/revision/vuelo_piloto',FF='C:/Program Files (x86)/Soundux/ffmpeg.exe';
fs.mkdirSync(OUT,{recursive:true});
function ff(args){execFileSync(FF,['-v','error','-y',...args],{maxBuffer:32<<20});}
function rgb(fr){const w=fr[0][0].length,h=fr[0].length,b=Buffer.alloc(w*h*3*fr.length);fr.forEach((g,f)=>g.forEach((r,y)=>[...r].forEach((c,x)=>{const v=c==='.'?'2A6A90':m.pal[c];[0,2,4].forEach((p,k)=>b[((f*h+y)*w+x)*3+k]=parseInt(v.slice(p,p+2),16));})));return b;}
function image(fr,file,k=6,labels=[]){const w=fr[0][0].length,h=fr[0].length,raw='rev/flight_review.rgb';fs.writeFileSync(raw,rgb(fr));const filters=[`scale=iw*${k}:ih*${k}:flags=neighbor`,...labels.map(([text,x,y])=>`drawtext=fontfile='C\\:/Windows/Fonts/arial.ttf':text='${text}':x=${x}:y=${y}:fontsize=19:fontcolor=white`)];const args=['-f','rawvideo','-pixel_format','rgb24','-video_size',w+'x'+h,'-framerate',String(m.fps),'-i',raw];if(file.endsWith('.gif'))ff([...args,'-vf',filters.join(',')+',split[a][b];[a]palettegen=max_colors=48[p];[b][p]paletteuse=dither=none','-loop','0',OUT+'/'+file]);else ff([...args,'-vf',filters.join(','),'-frames:v','1',OUT+'/'+file]);}
const blank=(w,h)=>Array.from({length:h},()=>Array(w).fill('.'));
function paste(g,src,ox,oy){src.forEach((r,y)=>[...r].forEach((c,x)=>{if(c!=='.'&&g[y+oy]?.[x+ox]!==undefined)g[y+oy][x+ox]=c;}));}
const pairs=m.fr.map(g=>{const canvas=blank(148,70);paste(canvas,old.rows,7,19);paste(canvas,g,76,6);return canvas.map(r=>r.join(''));});
image([pairs[0]],'pidgey_antes_y_despues.png',6,[['ANTES - pose de apoyo',12,8],['PROPUESTA - vuelo con batido',468,8]]);
image(pairs,'pidgey_antes_y_vuelo_fijo.gif',6,[['ANTES - pose de apoyo',12,8],['PROPUESTA - ciclo fijo',468,8]]);
C.previews('pidgey_flight',m.pal,m.fr,m.fps,6,OUT);
const cell=78,{cells}=C.sheet('pidgey_flight',m.pal,m.fr,m.angles,[36,32],cell,m.protect);
fs.copyFileSync('pidgey_flight_sheet.png',OUT+'/pidgey_hoja_propuesta.png');
// Same horizontal inversion as Swimmer: base faces left, dir > 0 mirrors the sheet.
const routes=m.fr.map((g,f)=>{const canvas=blank(156,84);paste(canvas,cells[1][f],0,6);paste(canvas,cells[1][f].map(r=>[...r].reverse().join('')),78,6);return canvas.map(r=>r.join(''));});
image(routes,'pidgey_direcciones.gif',4,[['HACIA LA IZQUIERDA  <---',8,4],['HACIA LA DERECHA  --->',316,4]]);
// Diagnostic motion only: reproduce Swimmer's direction, tilt selection and sky clamps without loading the app.
const simulation=[];
for(let f=0;f<144;f++){
 const canvas=blank(300,162),time=f/12,progress=f/143,world=300;
 [-1,1].forEach((dir,lane)=>{
  const tau=time+lane*.37,beat=2*Math.PI*tau/(m.fr.length/m.fps),speed=(world+2*cell)/12;
  const across=dir>0?-cell+Math.trunc(progress*(world+2*cell)):world+cell-Math.trunc(progress*(world+2*cell));
  const x=across+dir*.8*Math.sin(beat)+(dir>0?-cell/2:cell/2),y=Math.max(32,Math.min(36,33+5*Math.sin(2*Math.PI*tau/13)+.5*Math.sin(2*Math.PI*tau/4.3)));
  const heading=Math.atan2(5*2*Math.PI/13*Math.cos(2*Math.PI*tau/13),speed)*180/Math.PI,lift=Math.max(-4,Math.min(4,-heading));
  let row=0;for(let r=1;r<m.angles.length;r++)if(Math.abs(m.angles[r]-lift)<Math.abs(m.angles[row]-lift))row=r;
  const col=Math.floor(tau*m.fps)%m.fr.length,g=dir>0?cells[row][col].map(r=>[...r].reverse().join('')):cells[row][col];
  paste(canvas,g,Math.round(x-cell/2),Math.round(y-cell/2)+9+lane*81);
 });simulation.push(canvas.map(r=>r.join('')));
}
image(simulation,'pidgey_trayectoria_simulada.gif',3,[['SIMULACION - hacia la izquierda <---',8,4],['SIMULACION - hacia la derecha --->',8,247]]);
function components(g){let seen=new Set(),sizes=[];for(let y=0;y<g.length;y++)for(let x=0;x<g[0].length;x++){if(g[y][x]==='.'||seen.has(x+','+y))continue;let q=[[x,y]],n=0;seen.add(x+','+y);while(q.length){const [a,b]=q.pop();n++;for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){const xx=a+dx,yy=b+dy,key=xx+','+yy;if(g[yy]?.[xx]&&g[yy][xx]!=='.'&&!seen.has(key)){seen.add(key);q.push([xx,yy]);}}}sizes.push(n);}return sizes;}
const rounded=v=>{const a=Math.floor(v);return v-a===.5?(a%2?a+1:a):Math.round(v);};
const flattened=cells.flat().map(g=>g.map(r=>r.join(''))),bounds=flattened.map(g=>{let lo=cell,hi=0;for(let y=0;y<cell;y++)if(/[^.]/.test(g[y])){lo=Math.min(lo,y);hi=Math.max(hi,y);}return [lo+rounded(3+m.layoutHeight/2-cell/2),hi+rounded(65-m.layoutHeight/2-cell/2)];});
const eyes=[];for(let a=0;a<m.angles.length;a++)for(let f=0;f<m.fr.length;f++){
 const angle=m.angles[a]*Math.PI/180,hits=[];for(let y=21;y<=26;y++)for(let x=14;x<=23;x++){
  const dx=x+.5-36,dy=y+.5-32,xx=Math.floor(Math.cos(angle)*dx-Math.sin(angle)*dy+cell/2),yy=Math.floor(Math.sin(angle)*dx+Math.cos(angle)*dy+cell/2);hits.push(cells[a][f][yy][xx]);
 }eyes.push({angle:m.angles[a],frame:f,black:hits.filter(c=>c==='D').length,white:hits.filter(c=>c==='B').length});
}
const face=m.fr.map(g=>{let n=0,kept=0;for(let y=0;y<17;y++)for(let x=0;x<(y<15?18:12);x++){const c=old.rows[y][x];if(c!=='.'){n++;if(g[y+13][x+7]===c)kept++;}}return {n,kept};});
const feet=m.fr.map(g=>g.slice(36,43).reduce((n,r)=>n+[...r.slice(38,49)].filter(c=>'FI'.includes(c)).length,0));
const changes=m.fr.map((g,f)=>g.reduce((n,r,y)=>n+[...r].filter((c,x)=>c!==m.fr[(f+1)%m.fr.length][y][x]).length,0));
const border=flattened.filter(g=>g[0].replaceAll('.','')||g.at(-1).replaceAll('.','')||g.some(r=>r[0]!=='.'||r.at(-1)!=='.')).length;
const report={frames:m.fr.length,fps:m.fps,tilts:m.angles,cells:flattened.length,paletteIdentical:JSON.stringify(m.pal)===JSON.stringify(old.pal),native:C.check(m.fr),sheet:C.check(flattened),components:m.fr.map(components),rotatedComponents:flattened.map(g=>components(g).length),face,eyes,feet,changes,loopPassed:changes.at(-1)<=Math.max(...changes.slice(0,-1))*1.05,clippedCells:border,sky:{minY:Math.min(...bounds.map(b=>b[0])),maxY:Math.max(...bounds.map(b=>b[1])),trajectoryRange:62-m.layoutHeight},wingTipY:m.poses.map(p=>p[3][1]),shoulder:[27,29]};
fs.writeFileSync(OUT+'/auditoria_pidgey.json',JSON.stringify(report,null,2));
console.log(JSON.stringify(report,null,2));
if(report.native.iso||report.native.holes||report.sheet.iso||report.sheet.holes||border||report.components.some(c=>c.length!==1)||report.rotatedComponents.some(n=>n!==1)||face.some(v=>v.n!==v.kept)||eyes.some(e=>!e.black||!e.white)||feet.some(n=>n<1)||!report.loopPassed||report.sky.minY<0||report.sky.maxY>=69||report.sky.trajectoryRange<=0)process.exitCode=1;
