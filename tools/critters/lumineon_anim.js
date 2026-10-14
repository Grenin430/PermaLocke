// Lumineon: broad dorsal membranes ripple from their roots, with a delayed tail and tiny ventral fins.
const C=require('./critter');
const {pal,rows}=C.load('lumineon');
const N=8,pad=7,t=f=>f/N*2*Math.PI,bob=f=>Math.round(.7*Math.sin(t(f)));
const tail=(x,y)=>x>=35&&y>=30&&y<=40;
const fr=C.frames(rows,{count:N,pad,ink:'COPJ',parts:[
 {test:tail,move:(f,x)=>[0,bob(f)+Math.round(1.5*Math.sin(t(f)-1.3)*Math.max(0,(x-35)/15))]},
 {test:(x,y)=>x>=18&&y<=40,move:(f,x)=>[0,bob(f)+Math.round(2*Math.sin(t(f)-(x-18)*.06)*Math.max(0,(x-18)/38))]},
 {test:(x,y)=>x<=18&&y<=26,move:(f,x,y)=>[Math.round(1.5*Math.sin(t(f)-.5)*Math.max(0,(26-y)/26)),bob(f)]},
 {test:(x,y)=>y>=50,move:(f,x,y)=>[Math.round(Math.sin(t(f)-1.7)*Math.max(0,(y-50)/6)),bob(f)]},
 {test:()=>true,move:f=>[0,bob(f)]}
],seam:(x,y)=>(x>=16&&x<=25&&y>=24&&y<=41)||(x>=30&&x<=38&&y>=36&&y<=42)||(y>=48&&y<=52)});
console.log('lumineon',JSON.stringify(C.check(fr)));
module.exports={sealRotatedHoles:true,name:'lumineon',pal,rows,fr,fps:8,angles:[-6,-3,0,3,6],pose:0,pad,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad&&x<=pad+13&&y>=pad+35&&y<=pad+44};
