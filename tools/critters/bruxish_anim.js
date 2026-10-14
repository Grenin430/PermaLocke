// Bruxish: broad tail strokes, a swaying dorsal lure and a small pectoral fan.
const C=require('./critter');
const {pal,rows}=C.load('bruxish');
const N=8,pad=6,t=f=>f/N*2*Math.PI,bob=f=>Math.round(.6*Math.sin(t(f)));
const fr=C.frames(rows,{count:N,pad,ink:'AEL',parts:[
 {test:(x,y)=>x>=49,move:(f,x)=>[0,bob(f)+2*Math.sin(t(f)-.2*(x-49))*Math.min(1,(x-49)/10)]},
 {test:(x,y)=>y<=12&&x<=27,move:(f,x,y)=>[1.3*Math.sin(t(f)-1.1)*Math.max(0,(12-y)/12),bob(f)]},
 {test:(x,y)=>x>=29&&x<=40&&y>=24&&y<=30,move:(f,x)=>[0,bob(f)+.85*Math.sin(t(f)+.4)*(x-29)/11]},
 {test:(x,y)=>y>=35&&x>=29,move:(f,x)=>[0,bob(f)+.8*Math.sin(t(f)-1.2)*Math.min(1,(x-29)/10)]},
 {test:()=>true,move:f=>[0,bob(f)]}
],seam:(x,y)=>(x>=47&&x<=51)||(y>=10&&y<=14&&x<=28)||(x>=28&&x<=41&&y>=23&&y<=32)||(y>=33&&y<=36)});
console.log('bruxish',JSON.stringify(C.check(fr)));
module.exports={name:'bruxish',pal,rows,fr,fps:8,angles:[-6,-3,0,3,6],pose:0,pad,
 protect:(g,x,y)=>g[y][x]!=='.'&&((x>=pad+20&&x<=pad+28&&y>=pad+18&&y<=pad+25)||(x<=pad+15&&y>=pad+22&&y<=pad+32))};
