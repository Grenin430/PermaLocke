// Lugia: hand-like wings flex from their shoulders, while the tail and feet follow later.
const C=require('./critter');const {pal,rows}=C.load('lugia');
const N=8,sourcePad=8,t=f=>f/N*2*Math.PI,down=f=>(1-Math.cos(t(f)))/2;
const left=(x,y)=>x<=17&&y<=49;
const right=(x,y)=>(y<=18&&x>=40)||(y<=26&&x>=55)||(y<=36&&x>=64)||(y<=45&&x>=61)||(y<=54&&x>=54);
const bendL=(f,x,y)=>[2.5*down(f)*Math.max(0,(49-y)/35),0];
const bendR=(f,x,y)=>[-2.5*down(f)*Math.max(0,(54-y)/54),1.5*down(f)*Math.max(0,(x-54)/25)];
const inverse=(bend)=>(f,put,at,mine)=>{for(let y=-4;y<60;y++)for(let x=-4;x<85;x++){let sx=x,sy=y;for(let k=0;k<4;k++){const [dx,dy]=bend(f,sx,sy);sx=x-dx;sy=y-dy;}const X=Math.round(sx),Y=Math.round(sy);if(mine(X,Y))put(x,y,at(X,Y));}};
const sourceFrames=C.frames(rows,{count:N,pad:sourcePad,ink:'BHLO',parts:[
 {test:left,sample:inverse(bendL)},
 {test:right,sample:inverse(bendR)},
 {test:(x,y)=>x>=32&&x<=63&&y>=18&&y<=42&&(y<=34||x>=55),move:(f,x)=>[0,1.3*Math.sin(t(f)-1.1)*Math.max(0,(63-x)/31)]},
 {test:(x,y)=>x>=49&&x<=53&&y>=35&&y<=48,move:(f,x,y)=>[.7*Math.sin(t(f)-.6)*(48-y)/13,0]},
 {test:(x,y)=>x>=45&&y>=71,move:(f,x,y)=>[.8*Math.sin(t(f)-(x<65?.7:1.3))*(y-71)/8,0]},
 {test:()=>true}
],seam:(x,y)=>(x>=15&&x<=20&&y>=38&&y<=53)||(x>=52&&x<=66&&y>=25&&y<=56)||(y>=69&&y<=73)||(y>=45&&y<=49&&x>=47&&x<=55)});
const displayScale=.75,pad=6,fr=require('./batch10_display')(sourceFrames,displayScale,'BHLO',[[sourcePad+21,sourcePad+57,5,6,'EBJ'],[sourcePad+24,sourcePad+66,5,6,'CFJ']]);
console.log('lugia native',JSON.stringify(C.check(sourceFrames)));console.log('lugia display',JSON.stringify(C.check(fr)));
module.exports={name:'lugia',pal,rows,sourceFrames,fr,fps:6,angles:[-2,0,2],pose:0,pad,displayScale,layoutHeight:61,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+13&&x<=pad+23&&y>=pad+40&&y<=pad+55};
