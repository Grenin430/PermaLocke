// Zapdos: angular wing fans flex as whole columns; crest, tail and talons lag behind.
const C=require('./critter');const {pal,rows}=C.load('zapdos');
for(let y=34;y<=44;y++)for(let x=0;x<rows[y].length;x++)if(rows[y][x]==='B'&&((x>=26&&x<=33)||(x>=44&&x<=45&&y>=37&&y<=38)||(x===41&&y>=43)))rows[y][x]='.';
const N=8,pad=7,t=f=>f/N*2*Math.PI,down=f=>(1-Math.cos(t(f)))/2;
const fr=C.frames(rows,{count:N,pad,ink:'FPN',parts:[
 {test:(x,y)=>x<=23&&y<=38,move:(f,x)=>[0,3*down(f)*(23-x)/23]},
 {test:(x,y)=>x>=44&&y<=37,move:(f,x)=>[0,3.5*down(f)*(x-44)/46]},
 {test:(x,y)=>y<=14&&x>=24&&x<=42,move:(f,x,y)=>[.8*Math.sin(t(f)-.6)*(14-y)/14,0]},
 {test:(x,y)=>(x>=54&&y>=41)||(x>=39&&y>=46),move:(f,x,y)=>[Math.sin(t(f)-1.4)*Math.max(0,(y-41)/16),0]},
 {test:(x,y)=>y>=38&&x<=35,move:(f,x,y)=>[.8*Math.sin(t(f)-.7)*Math.max(0,(y-38)/17),0]},
 {test:(x,y)=>y>=37&&x>=44&&x<=53,move:(f,x,y)=>[.8*Math.sin(t(f)-1.2)*Math.max(0,(y-37)/15),0]},
 {test:()=>true}
],seam:(x,y)=>(x>=21&&x<=25)||(x>=42&&x<=46)||(y>=12&&y<=16)||(y>=36&&y<=40)||(y>=44&&y<=48)});
console.log('zapdos',JSON.stringify(C.check(fr)));
module.exports={name:'zapdos',pal,rows,fr,fps:9,angles:[-2,0,2],pose:0,pad,layoutHeight:61,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+24&&x<=pad+38&&y>=pad+16&&y<=pad+33};
