// Remoraid: the split rear fins and the ventral fin follow separate beats.
const C=require('./critter');
const {pal,rows}=C.load('remoraid');
const N=8,pad=6,t=f=>f/N*2*Math.PI,bob=f=>Math.round(.6*Math.sin(t(f)));
const fr=C.frames(rows,{count:N,pad,ink:'CPFN',parts:[
 {test:(x,y)=>x>=23&&y>=19,move:(f,x)=>[0,bob(f)+Math.round(2*Math.sin(t(f)-.8-(x-23)*.1)*Math.min(1,(x-23)/12))]},
 {test:(x,y)=>x>=25&&y<=14,move:(f,x,y)=>[Math.round(1.3*Math.sin(t(f)-1.1)*Math.max(0,(14-y)/14)),bob(f)]},
 {test:(x,y)=>x>=12&&x<=16&&y>=22,move:(f,x,y)=>[Math.round(1.1*Math.sin(t(f)+.7)*Math.max(0,(y-22)/5)),bob(f)]},
 {test:()=>true,move:f=>[0,bob(f)]}
],seam:(x,y)=>(x>=22&&x<=26&&y>=18)||(x>=24&&x<=29&&y>=11&&y<=15)||(x>=11&&x<=17&&y>=21&&y<=24)});
console.log('remoraid',JSON.stringify(C.check(fr)));
module.exports={name:'remoraid',pal,rows,fr,fps:9,angles:[-6,-3,0,3,6],pose:0,pad,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad&&x<=pad+12&&y>=pad+7&&y<=pad+17};
