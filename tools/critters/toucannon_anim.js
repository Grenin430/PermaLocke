// Toucannon: preserve the perched reference, with a head nod, folded-wing flutter and delayed tail/feet.
const C=require('./critter');
const {pal,rows}=C.load('toucannon');
// White between the legs is enclosed background, not breast plumage.
for(let y=48;y<rows.length;y++)for(let x=0;x<rows[0].length;x++)if(rows[y][x]==='B')rows[y][x]='.';
const N=8,pad=7,t=f=>f/N*2*Math.PI;
const fr=C.frames(rows,{count:N,pad,ink:'FK',parts:[
 {test:(x,y)=>y<=21,move:(f,x,y)=>[Math.round(.8*Math.sin(t(f))*Math.max(0,(22-y)/22)),0]},
 {test:(x,y)=>x>=47&&y>=45,move:(f,x)=>[0,Math.round(Math.sin(t(f)-1.1)*Math.max(0,(x-47)/13))]},
 {test:(x,y)=>y>=48,move:(f,x,y)=>[Math.round(.8*Math.sin(t(f)-.9)*Math.min(1,(y-48)/8)),0]},
 {test:(x,y)=>x>=34&&y>=24&&y<=44,move:(f,x)=>[0,-Math.round((1-Math.cos(t(f)))/2*Math.min(1,(x-34)/13))]},
 {test:()=>true}
],seam:(x,y)=>(y>=20&&y<=24)||(x>=32&&x<=37&&y>=23&&y<=46)||(y>=42&&y<=49)});
console.log('toucannon',JSON.stringify(C.check(fr)));
module.exports={name:'toucannon',pal,rows,fr,fps:8,angles:[-3,0,3],pose:0,pad,layoutHeight:61,
 protect:(g,x,y)=>g[y][x]!=='.'&&((x>=pad+32&&x<=pad+43&&y>=pad+2&&y<=pad+17)||(x>=pad+15&&x<=pad+33&&y>=pad+12&&y<=pad+19))};
