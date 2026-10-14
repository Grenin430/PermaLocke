// Lanturn: lamps sway on their existing stalks, with a delayed tail beat and small pectoral strokes.
// The small reference needs an explicit 3.66-pixel grid; the automatic 7.32 grid discards details.
const C=require('./critter');
const {pal,rows}=C.load('lanturn');
const N=8,pad=7,t=f=>f/N*2*Math.PI,bob=f=>Math.round(.6*Math.sin(t(f)));
const fr=C.frames(rows,{count:N,pad,ink:'CFOI',parts:[
 {test:(x,y)=>x>=28&&y<=6,move:(f,x,y)=>[Math.round(1.2*Math.sin(t(f)-1.4)*Math.max(0,(7-y)/7)),bob(f)]},
 {test:(x,y)=>y<=13||(x<=11&&y<=23),move:(f,x)=>[0,bob(f)+Math.round(1.5*Math.sin(t(f)-1)*Math.max(0,(27-x)/27))]},
 {test:(x,y)=>x>=51,move:(f,x)=>[0,bob(f)+Math.round(1.8*Math.sin(t(f)-1.5)*Math.min(1,(x-51)/13))]},
 {test:(x,y)=>x>=37&&x<=49&&y>=34&&y<=42,move:(f,x)=>[0,bob(f)+Math.round(1.3*Math.sin(t(f)+.5)*Math.max(0,(x-37)/12))]},
 {test:(x,y)=>x<=24&&y>=38,move:(f,x)=>[0,bob(f)+Math.round(Math.sin(t(f)-.7)*Math.max(0,(24-x)/7))]},
 {test:()=>true,move:f=>[0,bob(f)]}
],seam:(x,y)=>(y>=12&&y<=15&&x<=30)||(x>=49&&x<=53)||(x>=36&&x<=40&&y>=33&&y<=43)||(y>=37&&y<=40&&x<=25)});
console.log('lanturn',JSON.stringify(C.check(fr)));
module.exports={name:'lanturn',pal,rows,fr,fps:7,angles:[-6,-3,0,3,6],pose:0,pad,sealRotatedHoles:true,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+18&&x<=pad+31&&y>=pad+22&&y<=pad+36};
