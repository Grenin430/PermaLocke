// Compare the approved flight pose against its detail pass, with identical scale and timing.
const fs=require('fs'),assert=require('assert/strict'),{execFileSync}=require('child_process');
const C=require('./critter'),before=require('./pidgey_flight_before_detail.json'),after=require('./pidgey_flight_anim');
const OUT=process.argv[2]||'C:/Users/javie/Desktop/imagenes/revision/vuelo_piloto/detalle_pidgey';
const FF='C:/Program Files (x86)/Soundux/ffmpeg.exe';
fs.mkdirSync(OUT,{recursive:true});
assert.deepEqual(after.pal,before.pal);assert.deepEqual(after.poses,before.poses);
assert.deepEqual(after.parts,before.parts);assert.equal(after.fps,before.fps);assert.deepEqual(after.angles,before.angles);
const changed=after.fr.map((g,f)=>{const points=[];g.forEach((r,y)=>[...r].forEach((c,x)=>{if(c!==before.fr[f][y][x])points.push([x,y]);}));return points;});
// All changes must belong to the stationary torso; head, wings, feet and tail remain intact.
assert(changed.flat().every(([x,y])=>x>=15&&x<48&&y>=29&&y<40));
const bodyCoordinates=new Map(changed.flat().map(([x,y])=>[x+','+y,[x,y]]));
const stability=[];
for(const [x,y] of bodyCoordinates.values()){
 const expected=after.torso[y-24]?.[x-15];
 if(!expected||expected==='.')continue;
 const frames=[];
 after.fr.forEach((g,f)=>{
  // Foreground wing and shoulder coverts can naturally occlude breast detail.
  if(after.parts[f].near[y][x]!=='.'||(x>=24&&x<=29&&y>=27&&y<=30))return;
  assert.equal(g[y][x],expected,`Body detail moved at ${x},${y}, frame ${f}`);frames.push(f);
 });
 if(frames.length)stability.push({x,y,color:expected,visibleFrames:frames});
}
function write(fr,file,scale,labels=[]){
 const w=fr[0][0].length,h=fr[0].length,b=Buffer.alloc(w*h*3*fr.length);
 fr.forEach((g,f)=>g.forEach((r,y)=>[...r].forEach((c,x)=>{const hex=c==='.'?'2A6A90':after.pal[c];[0,2,4].forEach((p,k)=>b[((f*h+y)*w+x)*3+k]=parseInt(hex.slice(p,p+2),16));})));
 const raw='rev/pidgey_detail.rgb';fs.writeFileSync(raw,b);
 const filter=[`scale=iw*${scale}:ih*${scale}:flags=neighbor`,...labels.map(([text,x,y])=>`drawtext=fontfile='C\\:/Windows/Fonts/arial.ttf':text='${text}':x=${x}:y=${y}:fontsize=19:fontcolor=white`)];
 const input=['-v','error','-y','-f','rawvideo','-pixel_format','rgb24','-video_size',`${w}x${h}`,'-framerate',String(after.fps),'-i',raw];
 const output=file.endsWith('.gif')?['-vf',filter.join(',')+',split[a][b];[a]palettegen=max_colors=48[p];[b][p]paletteuse=dither=none','-loop','0']:['-vf',filter.join(','),'-frames:v','1'];
 execFileSync(FF,[...input,...output,OUT+'/'+file]);
}
const pairs=after.fr.map((g,f)=>{
 const canvas=Array.from({length:72},()=>Array(148).fill('.'));
 [before.fr[f],g].forEach((rows,i)=>rows.forEach((r,y)=>[...r].forEach((c,x)=>canvas[y+8][x+76*i]=c)));
 return canvas.map(r=>r.join(''));
});
const labels=[['VUELO ANTERIOR',12,9],['DETALLE CORREGIDO',468,9]];
write([pairs[0]],'pidgey_vuelo_antes_y_corregido.png',6,labels);
write(pairs,'pidgey_vuelo_comparacion_fija.gif',6,labels);
write(after.fr,'pidgey_vuelo_corregido_fijo.gif',6);
// A compact panel at native resolution and x3, with no resampling of the palette.
write([pairs[0]],'pidgey_comparacion_x3.png',3);
const report={poseUnchanged:true,wingLayersUnchanged:true,paletteUnchanged:true,fps:after.fps,frameCount:after.fr.length,canvas:[72,64],
 changedPixelsPerFrame:changed.map(p=>p.length),changedBounds:[Math.min(...changed.flat().map(p=>p[0])),Math.min(...changed.flat().map(p=>p[1])),Math.max(...changed.flat().map(p=>p[0])),Math.max(...changed.flat().map(p=>p[1]))],
 stableBodyCoordinates:stability.length,stableVisibleSamples:stability.reduce((n,p)=>n+p.visibleFrames.length,0),stability};
fs.writeFileSync(OUT+'/auditoria_detalle.json',JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({...report,stability:undefined},null,2));
