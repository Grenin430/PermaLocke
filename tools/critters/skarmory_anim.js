// Skarmory: rigid wing blades hinge at their roots; the far wing follows a shorter, delayed stroke.
// Its tall reference keeps a nearly level flight path, with no added vertical bob inside the frames.
const C=require('./critter');
const {pal,rows}=C.load('skarmory');
// Neutral white trapped between the forward toes is background, not metal or mouth highlights.
for(let y=58;y<=61;y++)for(let x=19;x<=24;x++)if(rows[y][x]==='E')rows[y][x]='.';
// One pale JPEG fringe cell sits outside the blade contour in the source chart.
rows[31][60]='.';
const N=8,pad=8,t=f=>f/N*2*Math.PI;
const far=(x,y)=>x>=22&&x<=35&&y<=36;
const near=(x,y)=>x>=35&&y<=39&&!(x>=50&&y>=35);
const tail=(x,y)=>x>=45&&y>=35&&y<=46;
const fr=C.frames(rows,{count:N,pad,ink:'CGLO',parts:[
 {test:far,move:(f,x,y)=>[Math.round(1.3*Math.sin(t(f)-.7)*Math.max(0,(36-y)/36)),Math.round(.6*(1+Math.sin(t(f)-.7))*Math.max(0,(36-y)/36))]},
 {test:near,move:(f,x)=>[0,Math.round(3*(1-Math.cos(t(f)))/2*Math.max(0,(x-35)/30))]},
 {test:tail,move:(f,x)=>[0,Math.round(Math.sin(t(f)-1.3)*Math.max(0,(x-45)/20))]},
 {test:(x,y)=>y>=52,move:(f,x,y)=>[Math.round(.7*Math.sin(t(f)-1)*Math.min(1,(y-52)/12)),0]},
 {test:()=>true}
],seam:(x,y)=>(x>=32&&x<=39&&y>=25&&y<=41)||(x>=43&&x<=49&&y>=37&&y<=47)||(y>=49&&y<=54)});
console.log('skarmory',JSON.stringify(C.check(fr)));
module.exports={sealRotatedHoles:true,name:'skarmory',pal,rows,fr,fps:8,angles:[-2,0,2],pose:0,pad,layoutHeight:61,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+6&&x<=pad+16&&y>=pad+26&&y<=pad+39};
