// Static review and exact change-scope audit for the Charizard neck correction.
const fs=require('fs'),path=require('path'),assert=require('assert/strict'),{execFileSync}=require('child_process');
const C=require('./critter'),before=require('./charizard_flight_pose_v3').build(true),after=require('./charizard_neck_refinement').build();
const OUT='C:/Users/javie/Desktop/imagenes/revision/charizard_cuello',FF='C:/Program Files (x86)/Soundux/ffmpeg.exe';
fs.mkdirSync(OUT,{recursive:true});
const rgb=h=>[0,2,4].map(i=>parseInt(h.slice(i,i+2),16));
function canvas(w,h,color='DCE6EB'){const b=Buffer.alloc(w*h*3);for(let i=0;i<w*h;i++)b.set(rgb(color),i*3);return b;}
function paste(b,w,rows,dx,dy,k){rows.forEach((r,y)=>[...r].forEach((c,x)=>{if(c!=='.')for(let v=0;v<k;v++)for(let u=0;u<k;u++)b.set(rgb(after.pal[c]),((dy+y*k+v)*w+dx+x*k+u)*3);}));}
function save(b,w,h,file,labels=[]){
 const vf=labels.map(([t,x,y,z])=>"drawtext=fontfile='C\\:/Windows/Fonts/arial.ttf':text='"+t+"':x="+x+":y="+y+":fontsize="+z+":fontcolor=0x182B3A:expansion=none").join(',');
 const args=['-v','error','-y','-f','rawvideo','-pixel_format','rgb24','-video_size',w+'x'+h,'-i','pipe:0'];
 if(vf)args.push('-vf',vf);
 execFileSync(FF,[...args,'-frames:v','1',path.join(OUT,file)],{input:b});
}
const crop=a=>a.rows.slice(18,51).map(r=>r.slice(10,60));
const b=canvas(1280,480);paste(b,1280,crop(before),28,56,12);paste(b,1280,crop(after),648,56,12);
save(b,1280,480,'charizard_cuello_antes_despues.png',[['ANTES - x12',28,16,24],['DESPUES - x12',648,16,24]]);
const full=after.rows.slice(5,77).map(r=>r.slice(12,170)),f=canvas(948,432);paste(f,948,full,0,0,6);
save(f,948,432,'charizard_completo.png');
const raw=Buffer.alloc(158*72*4);full.forEach((r,y)=>[...r].forEach((c,x)=>{raw.set(c==='.'?[0,0,0,0]:[...rgb(after.pal[c]),255],(y*158+x)*4);}));
execFileSync(FF,['-v','error','-y','-f','rawvideo','-pixel_format','rgba','-video_size','158x72','-i','pipe:0','-frames:v','1',path.join(OUT,'charizard_base_nativa.png')],{input:raw});
const changes=[];
before.rows.forEach((r,y)=>[...r].forEach((c,x)=>{if(c!==after.rows[y][x])changes.push({x,y,before:c,after:after.rows[y][x]});}));
assert.deepEqual(before.pal,after.pal);
assert.ok(changes.every(p=>p.x>=23&&p.x<=54&&p.y>=29&&p.y<=43));
const check=C.check([after.rows]);
assert.equal(check.iso,0);assert.equal(check.holes,0);
const audit={scope:'Solo mandibula, nuca, cuello y transicion al pecho; resto identico pixel a pixel a v3',changes:changes.length,changedBounds:[Math.min(...changes.map(p=>p.x)),Math.min(...changes.map(p=>p.y)),Math.max(...changes.map(p=>p.x)),Math.max(...changes.map(p=>p.y))],check,paletteUnchanged:true,pixelsOutsideRegion:174*92-32*15,allOutsideRegionUnchanged:true,details:changes};
fs.writeFileSync(path.join(OUT,'auditoria.json'),JSON.stringify(audit,null,2)+'\n');
fs.writeFileSync(path.join(OUT,'charizard_base.json'),JSON.stringify({pal:after.pal,rows:full,sourceCanvasOffset:[12,5],stage:'Union cabeza-cuello pendiente de revision'},null,2)+'\n');
fs.writeFileSync(path.join(OUT,'charizard_base.txt'),Object.entries(after.pal).map(([c,v])=>c+'='+v).join(' ')+'\n'+full.join('\n')+'\n');
console.log(JSON.stringify({...audit,details:undefined},null,2));