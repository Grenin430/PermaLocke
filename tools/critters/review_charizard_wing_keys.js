// Key poses at one fixed scale, with both wings isolated over a subdued body guide to inspect their shape and their root on the back.
const fs=require('fs'),path=require('path'),{execFileSync}=require('child_process'),m=require('./charizard_approved_flight');
const OUT='C:/Users/javie/Desktop/imagenes/revision/charizard_batido_v3',FF='C:/Program Files (x86)/Soundux/ffmpeg.exe';
const picks=[0,3,7,9,11,14],titles=['ALAS ALTAS','GOLPE DESCENDENTE','ALAS BAJAS','INICIO DE RECUPERACION','RECUPERACION FLEXIONADA','APERTURA ANTES DEL GOLPE'];
const pw=680,ph=600,W=pw*3,H=ph*2,b=Buffer.alloc(W*H*3);
const rgb=h=>[0,2,4].map(k=>parseInt(h.slice(k,k+2),16));
for(let i=0;i<W*H;i++)b.set(rgb('182B3A'),i*3);
function rect(x,y,w,h,color){for(let yy=y;yy<y+h;yy++)for(let xx=x;xx<x+w;xx++)b.set(rgb(color),(yy*W+xx)*3);}
function draw(rows,ox,oy,k,grey=false){rows.forEach((r,y)=>[...r].forEach((c,x)=>{if(c==='.')return;const color=grey?'536876':m.pal[c];for(let v=0;v<k;v++)for(let u=0;u<k;u++)b.set(rgb(color),((oy+y*k+v)*W+ox+x*k+u)*3);}));}
const labels=[];
for(let j=0;j<picks.length;j++){
 const f=picks[j],x=(j%3)*pw,y=Math.floor(j/3)*ph;
 rect(x+8,y+40,pw-16,ph-48,'DCE6EB');
 labels.push([String(f).padStart(2,'0')+' - '+titles[j],x+14,y+12,18,'white']);
 // All full poses use the same source canvas and magnification.
 draw(m.fullFrames[f].slice(12,112).map(r=>r.slice(0,194)),x+9,y+46,3);
 const bob=m.stats[f].bob,guide=m.body.slice(0,100).map(r=>r.slice(0,160));
 // Each wing alone, at twice its size, over the grey body it hangs from.
 for(const [side,ox] of [['near',x+12],['far',x+342]]){
  draw(guide,ox,y+380,2,true);
  draw(m.wingLayers[f][side].slice(12+bob,12+bob+100).map(r=>r.slice(10,170)),ox,y+380,2);
 }
 labels.push(['ALA CERCANA AISLADA x2',x+12,y+356,14,'0x182B3A']);
 labels.push(['ALA LEJANA AISLADA x2',x+342,y+356,14,'0x182B3A']);
}
const filters=labels.map(([t,x,y,z,c])=>"drawtext=fontfile='C\\:/Windows/Fonts/arial.ttf':text='"+t+"':x="+x+":y="+y+":fontsize="+z+":fontcolor="+c+":expansion=none").join(',');
execFileSync(FF,['-v','error','-y','-f','rawvideo','-pixel_format','rgb24','-video_size',W+'x'+H,'-i','pipe:0','-vf',filters,'-frames:v','1',path.join(OUT,'charizard_fotogramas_clave.png')],{input:b});