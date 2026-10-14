// Assemble fixed cycles at native pixel scale x3, retaining each species' own timing.
const fs=require('fs'),{execFileSync}=require('child_process');
const specs=require('./flight_batch2_specs'),names=Object.keys(specs),mods=names.map(n=>require('./'+n+'_flight_anim'));
const OUT='C:/Users/javie/Desktop/imagenes/revision/vuelo_tanda2',FF='C:/Program Files (x86)/Soundux/ffmpeg.exe';
const w=320,h=400,count=192,rate=24,b=Buffer.alloc(w*h*3*count);
for(let i=0;i<b.length;i+=3){b[i]=42;b[i+1]=106;b[i+2]=144;}
function put(g,pal,f,ox,oy){g.forEach((r,y)=>[...r].forEach((c,x)=>{if(c==='.')return;const hex=pal[c],i=((f*h+y+oy)*w+x+ox)*3;[0,2,4].forEach((p,k)=>b[i+k]=parseInt(hex.slice(p,p+2),16));}));}
for(let f=0;f<count;f++)mods.forEach((m,i)=>{const old=require('./'+names[i]+'_before_flight.json');put(old.rows,m.pal,f,8,i*100+10);put(m.fr[Math.floor(f/rate*m.fps)%8],m.pal,f,168,i*100+10);});
fs.writeFileSync('rev/flight_batch2_comparison.rgb',b);
const labels=names.flatMap((n,i)=>[[n.toUpperCase()+' - ANTES',12,i*300+4],['VUELO CORREGIDO',504,i*300+4]]).map(([t,x,y])=>`drawtext=fontfile='C\\:/Windows/Fonts/arial.ttf':text='${t}':x=${x}:y=${y}:fontsize=16:fontcolor=white`).join(',');
const input=['-v','error','-y','-f','rawvideo','-pixel_format','rgb24','-video_size',`${w}x${h}`,'-framerate',String(rate),'-i','rev/flight_batch2_comparison.rgb'];
execFileSync(FF,[...input,'-vf','scale=iw*3:ih*3:flags=neighbor,'+labels,'-frames:v','1',OUT+'/tanda2_antes_y_despues.png']);
execFileSync(FF,[...input,'-vf','scale=iw*3:ih*3:flags=neighbor,'+labels+',split[a][b];[a]palettegen=max_colors=128[p];[b][p]paletteuse=dither=none','-loop','0',OUT+'/tanda2_ciclos_comparados.gif']);
for(let i=0;i<names.length;i++){
 const m=mods[i],n=names[i],a=JSON.parse(fs.readFileSync(OUT+'/auditoria.json','utf8')).find(r=>r.name===n),shift=[(a.cell-m.spec.canvas[0])/2,(a.cell-m.spec.canvas[1])/2],eye=m.spec.eye;
 execFileSync(process.execPath,['rostro.js',n+'_flight',String(a.cell),'8',[eye[0]+shift[0]-3,eye[1]+shift[1]-3,eye[2]+6,eye[3]+6].join(','),'5',OUT+'/'+n+'_rostros.png']);
 execFileSync(process.execPath,['zoom.js',n+'_flight_anim','0,1,2,3,4,5,6,7',[m.spec.torsoAt[0]-2,m.spec.torsoAt[1]-2,58,30].join(','),'4',OUT+'/'+n+'_uniones.png']);
}
