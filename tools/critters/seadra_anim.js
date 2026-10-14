// Seadra: a quick dorsal-fin beat and a slower bend through the curled tail.
const C=require('./critter');
const {pal,rows}=C.load('seadra');
// White cells behind the head are trapped background; eye whites remain intact.
for(let y=0;y<rows.length;y++)for(let x=24;x<rows[0].length;x++)if(rows[y][x]==='C')rows[y][x]='.';
const N=8,pad=6,t=f=>f/N*2*Math.PI,bob=f=>Math.round(.7*Math.sin(t(f)));
const fr=C.frames(rows,{count:N,pad,ink:'BOGP',parts:[
 {test:(x,y)=>x>=29&&y<=25,move:(f,x)=>[0,bob(f)+Math.round(2*Math.sin(t(f))*Math.min(1,(x-29)/17))]},
 {test:(x,y)=>y>=33,move:(f,x,y)=>[Math.round(1.4*Math.sin(t(f)-1.1)*Math.min(1,(y-33)/12)),bob(f)]},
 {test:()=>true,move:f=>[0,bob(f)]}
],seam:(x,y)=>(x>=27&&x<=32&&y<=26)||(y>=31&&y<=35)});
console.log('seadra',JSON.stringify(C.check(fr)));
module.exports={name:'seadra',pal,rows,fr,fps:10,angles:[-6,-3,0,3,6],pose:0,pad,
 protect:(g,x,y)=>g[y][x]!=='.'&&((x>=pad+11&&x<=pad+18&&y>=pad+12&&y<=pad+18)||(x>=pad&&x<=pad+8&&y>=pad+22&&y<=pad+29))};
