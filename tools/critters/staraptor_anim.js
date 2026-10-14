// Staraptor: keep the reference's perched pose, with a small head nod, crest, folded wing and tail.
// White cells between the feet are trapped background, not plumage.
const C=require('./critter');
const {pal,rows}=C.load('staraptor');
for(let y=53;y<rows.length;y++)for(let x=0;x<rows[y].length;x++)if(rows[y][x]==='B')rows[y][x]='.';
const N=8,pad=7,t=f=>f/N*2*Math.PI,nod=f=>Math.round(.6*Math.sin(t(f)));
const fr=C.frames(rows,{count:N,pad,ink:'AFPN',parts:[
 {test:(x,y)=>y<=15,move:(f,x,y)=>[nod(f)+.7*Math.sin(t(f)-1.2)*Math.max(0,(15-y)/15),0]},
 {test:(x,y)=>y<=29&&x<=24,move:f=>[nod(f),0]},
 {test:(x,y)=>x>=40&&y<=42,move:(f,x)=>[0,1.2*Math.sin(t(f)-1.1)*(x-40)/14]},
 {test:(x,y)=>x>=26&&x<=43&&y>=31&&y<=44,move:(f,x)=>[0,.8*Math.sin(t(f)+.5)*(x-26)/17]},
 {test:(x,y)=>y>=53,move:(f,x,y)=>[.8*Math.sin(t(f)-(x<24?.8:1.6))*Math.min(1,(y-53)/10),0]},
 {test:()=>true}
],seam:(x,y)=>(y>=13&&y<=17)||(x>=22&&x<=26&&y<=32)||(y>=27&&y<=32&&x<=25)||(x>=38&&x<=42&&y<=44)||(x>=25&&x<=44&&y>=30&&y<=46)||(y>=51&&y<=55)});
console.log('staraptor',JSON.stringify(C.check(fr)));
module.exports={name:'staraptor',pal,rows,fr,fps:8,angles:[-1,0,1],pose:0,pad,layoutHeight:61,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+4&&x<=pad+18&&y>=pad+20&&y<=pad+30};
