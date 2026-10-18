// Kyogre: broad pectoral paddles oppose the forked tail's slow stroke; markings stay on each part.
const C=require('./critter');const {pal,rows}=C.load('kyogre');
for(const row of rows)for(let x=0;x<row.length;x++)if(row[x]==='B')row[x]='.';
const N=8,pad=8,t=f=>f/N*2*Math.PI,bob=f=>Math.round(.6*Math.sin(t(f)));
const fr=C.frames(rows,{count:N,pad,ink:'ANL',parts:[
 {test:(x,y)=>x<=30&&y<=33,move:(f,x)=>[0,bob(f)+3*Math.sin(t(f))*(30-x)/30]},
 {test:(x,y)=>x>=62&&y>=29,move:(f,x)=>[0,bob(f)+3.2*Math.sin(t(f)-.3)*(x-62)/31]},
 {test:(x,y)=>x>=50&&y<=23,move:(f,x,y)=>[2*Math.sin(t(f)-1.2)*Math.max(0,(23-y)/23),bob(f)]},
 {test:(x,y)=>x>=33&&x<=47&&y<=26,move:(f,x,y)=>[.8*Math.sin(t(f)-.6)*Math.max(0,(26-y)/14),bob(f)]},
 {test:()=>true,move:f=>[0,bob(f)]}
],seam:(x,y)=>(x>=28&&x<=32&&y<=36)||(x>=60&&x<=64&&y>=27)||(y>=21&&y<=25&&x>=48)||(y>=24&&y<=28&&x>=31&&x<=49)});
console.log('kyogre',JSON.stringify(C.check(fr)));
module.exports={name:'kyogre',sealRotatedHoles:true,pal,rows,fr,fps:6,angles:[16,22,28,34,40],pose:28,pad,layoutHeight:90,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+28&&x<=pad+46&&y>=pad+45&&y<=pad+55};
