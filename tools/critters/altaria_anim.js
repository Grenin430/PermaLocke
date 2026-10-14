// Altaria: soft cloud wings bend by complete columns; long crest feathers and the tail trail behind.
const C=require('./critter');
const {pal,rows}=C.load('altaria');
// The pale cell between the tail streamers is background; white cloud and cheeks are retained.
rows[57][45]='.';
const N=8,pad=7,t=f=>f/N*2*Math.PI;
const cloud=(f,x)=>-Math.round((1-Math.cos(t(f)))/2*Math.min(1,Math.max(0,Math.abs(x-26)-6)/16));
const fr=C.frames(rows,{count:N,pad,ink:'CFIL',parts:[
 {test:(x,y)=>y<=13,move:(f,x)=>[0,Math.round(1.2*Math.sin(t(f)-.8)*Math.max(0,(x-25)/32))]},
 {test:(x,y)=>x>=36&&y>=54,move:(f,x)=>[0,Math.round(1.2*Math.sin(t(f)-1.4)*Math.min(1,(x-36)/21))]},
 {test:(x,y)=>x<=8&&y>=47,move:(f,x)=>[Math.round(.7*Math.sin(t(f)-1)*Math.max(0,(8-x)/8)),cloud(f,8)]},
 {test:(x,y)=>x>=24&&x<=29&&y>=52,move:(f,x,y)=>[Math.round(.7*Math.sin(t(f)-.7)*Math.min(1,(y-52)/9)),0]},
 {test:(x,y,c)=>y>=26&&'BEKI'.includes(c),move:(f,x)=>[0,cloud(f,x)]},
 {test:()=>true}
],seam:(x,y)=>(y>=25&&x>=18&&x<=34)||(x<=10&&y>=45)||(y>=52&&x>=33)});
console.log('altaria',JSON.stringify(C.check(fr)));
module.exports={name:'altaria',pal,rows,fr,fps:7,angles:[-2,0,2],pose:0,pad,layoutHeight:61,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+15&&x<=pad+32&&y>=pad+17&&y<=pad+25};
