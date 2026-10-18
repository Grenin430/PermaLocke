// Togekiss: broad wings bend from fixed roots, with the tail and feet following the body.
const C=require('./critter');
const {pal,rows}=C.load('togekiss');
// Pale JPEG/grid cells outside the red crest and above the right wing are background.
for(const [x,y] of [[22,0],[21,1],[19,2],[16,5],[15,6],[15,7],[43,8]])if(rows[y][x]==='A')rows[y][x]='.';
const N=8,pad=8,t=f=>f/N*2*Math.PI,bob=f=>Math.round(.6*Math.sin(t(f)-.5));
const fr=C.frames(rows,{count:N,pad,ink:'BMEN',parts:[
 {test:(x,y)=>x<=13&&y<=19,move:(f,x)=>[0,bob(f)-Math.round(1.8*Math.sin(t(f))*Math.max(0,(13-x)/13))]},
 {test:(x,y)=>x>=43&&y<=25,move:(f,x)=>[0,bob(f)-Math.round(2.5*Math.sin(t(f))*Math.min(1,(x-43)/33))]},
 {test:(x,y)=>x>=46&&y>=26&&y<=34,move:(f,x)=>[0,bob(f)+Math.round(Math.sin(t(f)-1.2)*Math.max(0,(x-46)/14))]},
 {test:(x,y)=>y>=36,move:(f,x,y)=>[Math.round(.8*Math.sin(t(f)-1)*Math.max(0,(y-36)/6)),bob(f)]},
 {test:()=>true,move:f=>[0,bob(f)]}
],seam:(x,y)=>(x>=11&&x<=15&&y<=21)||(x>=41&&x<=45&&y<=27)||(y>=24&&y<=28&&x>=44)||(y>=34&&y<=38)});
console.log('togekiss',JSON.stringify(C.check(fr)));
module.exports={name:'togekiss',pal,rows,fr,fps:8,angles:[-6,-3,0,3,6],pose:0,pad,layoutHeight:56,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+12&&x<=pad+23&&y>=pad+11&&y<=pad+22};
