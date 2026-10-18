const fs=require('fs'),{execFileSync}=require('child_process'),C=require('./critter');
const names=['wingull','pidgey','spearow','pikipek','butterfree','swablu','zubat','pelipper','pidgeot','fearow','toucannon','altaria','skarmory','staraptor','crobat','togekiss','dragonite','salamence','charizard','aerodactyl','latias','latios','rayquaza','hooh','articuno','zapdos','moltres','lugia'];
const out='C:/Users/javie/Desktop/imagenes/revision/vuelo_piloto';fs.mkdirSync(out,{recursive:true});
const FF='C:/Program Files (x86)/Soundux/ffmpeg.exe';
for(const phase of [0,1]){
 const W=1000,H=1140,b=Buffer.alloc(W*H*3);for(let i=0;i<b.length;i+=3){b[i]=42;b[i+1]=106;b[i+2]=144;}
 names.forEach((n,i)=>{const m=require('./'+n+'_anim'),g=m.fr[phase?Math.floor(m.fr.length/2):0],k=Math.min(3,Math.floor(174/Math.max(g.length,g[0].length)));const ox=i%5*200+Math.floor((200-g[0].length*k)/2),oy=Math.floor(i/5)*190+25+Math.floor((160-g.length*k)/2);g.forEach((r,y)=>[...r].forEach((c,x)=>{if(c==='.')return;const rgb=[0,2,4].map(v=>parseInt(m.pal[c].slice(v,v+2),16));for(let yy=0;yy<k;yy++)for(let xx=0;xx<k;xx++){const p=((oy+y*k+yy)*W+ox+x*k+xx)*3;b.set(rgb,p);}}));});
 const raw='rev/vuelo_contacto.rgb';fs.writeFileSync(raw,b);
 const labels=names.map((n,i)=>`drawtext=fontfile='C\\:/Windows/Fonts/arial.ttf':text='${n}':x=${i%5*200+8}:y=${Math.floor(i/5)*190+5}:fontsize=18:fontcolor=white`).join(',');
 execFileSync(FF,['-v','error','-y','-f','rawvideo','-pixel_format','rgb24','-video_size',W+'x'+H,'-i',raw,'-vf',labels,'-frames:v','1',out+'/voladores_actuales_'+phase+'.png']);
}
