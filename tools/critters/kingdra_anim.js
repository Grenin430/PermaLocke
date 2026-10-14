// Kingdra: a dorsal membrane ripples from its root while the curled tail follows a slower wave.
const C=require('./critter');
const {pal,rows}=C.load('kingdra');
// Remove chart background in the crown and between the trunk and dorsal fin. The fin membrane stays.
for(let y=0;y<rows.length;y++)for(let x=0;x<rows[0].length;x++) {
 if((y<=12&&x<=18&&rows[y][x]==='B')||(y>=24&&y<=34&&x>=20&&x<=34&&'BJH'.includes(rows[y][x])))rows[y][x]='.';
}
const N=8,pad=7,t=f=>f/N*2*Math.PI;
const fr=C.frames(rows,{count:N,pad,ink:'DOA',parts:[
 {test:(x,y)=>x>=31&&y>=19&&y<=44,move:(f,x)=>[0,Math.round(2*Math.sin(t(f)-.15*(x-31))*Math.min(1,(x-31)/18))]},
 {test:(x,y)=>y>=49,move:(f,x,y)=>[Math.round(1.6*Math.sin(t(f)-1.2)*Math.min(1,(y-49)/14)),0]},
 {test:(x,y)=>x>=23&&y<=9,move:(f,x,y)=>[Math.round(.9*Math.sin(t(f)-.8)*Math.max(0,(9-y)/9)),0]},
 {test:()=>true}
],seam:(x,y)=>(x>=29&&x<=33&&y>=19&&y<=45)||(y>=47&&y<=51)||(y>=7&&y<=10&&x>=22)});
console.log('kingdra',JSON.stringify(C.check(fr)));
module.exports={name:'kingdra',pal,rows,fr,fps:9,angles:[-6,-3,0,3,6],pose:0,pad,sealRotatedHoles:true,
 protect:(g,x,y)=>g[y][x]!=='.'&&((x>=pad+15&&x<=pad+24&&y>=pad+19&&y<=pad+26)||(x>=pad+5&&x<=pad+14&&y>=pad+29&&y<=pad+36))};
