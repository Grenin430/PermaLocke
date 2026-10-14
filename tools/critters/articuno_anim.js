// Articuno: raised feathered wings bend from fixed roots; the long looped tail trails the beat.
const C=require('./critter');const {pal,rows}=C.load('articuno');
for(let y=0;y<rows.length;y++)for(let x=0;x<rows[y].length;x++)if(rows[y][x]==='B'&&(y<30||(y>=58&&y<=69)))rows[y][x]='.';
const N=8,sourcePad=8,t=f=>f/N*2*Math.PI,down=f=>(1-Math.cos(t(f)))/2;
const left=(x,y)=>x<=20&&y<=44;
const bend=(f,x,y)=>[3*down(f)*Math.max(0,(44-y)/44),1.5*down(f)*Math.max(0,(20-x)/20)];
const sourceFrames=C.frames(rows,{count:N,pad:sourcePad,ink:'AFL',parts:[
 {test:left,sample:(f,put,at,mine)=>{for(let y=-4;y<48;y++)for(let x=-4;x<26;x++){let sx=x,sy=y;for(let i=0;i<4;i++){const [dx,dy]=bend(f,sx,sy);sx=x-dx;sy=y-dy;}const X=Math.round(sx),Y=Math.round(sy);if(mine(X,Y))put(x,y,at(X,Y));}}},
 {test:(x,y)=>x>=36&&y<=49,move:(f,x)=>[0,4*down(f)*Math.max(0,(x-36)/35)]},
 {test:(x,y)=>x>=36&&y>=50,move:(f,x)=>[0,1.6*Math.sin(t(f)-1.2-(x-36)*.08)*Math.max(0,(x-36)/28)]},
 {test:(x,y)=>x<=20&&y>=58,move:(f,x)=>[0,1.3*Math.sin(t(f)-1.7)*Math.max(0,(20-x)/20)]},
 {test:(x,y)=>y>=69&&x>=12&&x<=32,move:(f,x,y)=>[.7*Math.sin(t(f)-(x<22?.6:1.2))*Math.max(0,(y-69)/11),0]},
 {test:()=>true}
],seam:(x,y)=>(x>=18&&x<=22&&y<=47)||(x>=34&&x<=38&&y>=30)||(y>=56&&y<=61)||(y>=67&&y<=71)});
const displayScale=.75,pad=6,fr=require('./batch10_display')(sourceFrames,displayScale,'AFL',[[sourcePad+28,sourcePad+31,5,5,'B'],[sourcePad+29,sourcePad+33,1,1,'C']]);
console.log('articuno native',JSON.stringify(C.check(sourceFrames)));console.log('articuno display',JSON.stringify(C.check(fr)));
module.exports={name:'articuno',sealRotatedHoles:true,pal,rows,sourceFrames,fr,fps:7,angles:[-2,0,2],pose:0,pad,displayScale,layoutHeight:61,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+16&&x<=pad+26&&y>=pad+21&&y<=pad+30};
