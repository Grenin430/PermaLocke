// Aerodactyl: the two membranes bend down from their shoulders; curled tail and feet lag behind.
const C=require('./critter');
const {pal,rows}=C.load('aerodactyl');
const N=8,pad=8,t=f=>f/N*2*Math.PI,down=f=>(1-Math.cos(t(f)))/2;
const fr=C.frames(rows,{count:N,pad,ink:'BKGO',parts:[
 {test:(x,y)=>y<=26&&x>=20&&x<=(y<15?51:49),move:(f,x,y)=>[Math.round(1.3*Math.sin(t(f)-1.2)*Math.max(0,(24-y)/24)),0]},
 {test:(x,y)=>x<=26&&y<=33,move:(f,x)=>[0,Math.round(2.5*down(f)*Math.max(0,(26-x)/26))]},
 {test:(x,y)=>x>=50&&y<=38,move:(f,x)=>[0,Math.round(3.5*down(f)*Math.min(1,(x-50)/28))]},
 {test:(x,y)=>x>=50&&y>=45,move:(f,x,y)=>[Math.round(1.2*Math.sin(t(f)-1.5)*Math.min(1,(y-45)/12)),0]},
 {test:(x,y)=>x>=36&&x<=45&&y>=45,move:(f,x,y)=>[Math.round(Math.sin(t(f)-.7)*Math.min(1,(y-45)/10)),0]},
 {test:()=>true}
],seam:(x,y)=>(x>=23&&x<=29&&y>=26&&y<=35)||(x>=48&&x<=53&&y>=20&&y<=40)||(y>=43&&y<=47&&x>=35)});
console.log('aerodactyl',JSON.stringify(C.check(fr)));
module.exports={name:'aerodactyl',pal,rows,fr,fps:9,angles:[-3,0,3],pose:0,pad,layoutHeight:61,
 protect:(g,x,y)=>g[y][x]!=='.'&&((x>=pad+24&&x<=pad+32&&y>=pad+33&&y<=pad+39)||(x>=pad+20&&x<=pad+34&&y>=pad+39&&y<=pad+58))};
