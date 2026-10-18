// Ho-Oh: broad feathered wings bend from the shoulders; the fan tail follows after the stroke.
const C=require('./critter');const {pal,rows}=C.load('hooh');
// Remove white JPEG/background remnants outside the breast, eye and pale talons.
for(let y=0;y<rows.length;y++)for(let x=0;x<rows[y].length;x++)if(rows[y][x]==='A'&&(y<55||y>84||x>60||(x<16&&y<73)))rows[y][x]='.';
const N=8,sourcePad=10,t=f=>f/N*2*Math.PI,down=f=>(1-Math.cos(t(f)))/2;
const left=(x,y)=>x<=31&&y<=42;
const right=(x,y)=>(x>=49&&y<=55)||(x>=57&&y<=60);
const tail=(x,y)=>(x>=57&&y>=58)||(x>=40&&y>=80);
const bendL=(f,x,y)=>[2.8*down(f)*Math.max(0,(43-y)/43),.8*down(f)*Math.max(0,(24-x)/24)];
const bendR=(f,x,y)=>[-3*down(f)*Math.max(0,(55-y)/55),1.3*down(f)*Math.max(0,(x-49)/34)];
const bendT=(f,x,y)=>[1.1*Math.sin(t(f)-1.1)*Math.max(0,(y-58)/33),.8*Math.sin(t(f)-1.1)*Math.max(0,(x-57)/25)];
const inverse=bend=>(f,put,at,mine)=>{for(let y=-4;y<96;y++)for(let x=-4;x<90;x++){let sx=x,sy=y;for(let k=0;k<4;k++){const [dx,dy]=bend(f,sx,sy);sx=x-dx;sy=y-dy;}const X=Math.round(sx),Y=Math.round(sy);if(mine(X,Y))put(x,y,at(X,Y));}};
const sourceFrames=C.frames(rows,{count:N,pad:sourcePad,ink:'BGO',parts:[
 {test:left,sample:inverse(bendL)}, {test:right,sample:inverse(bendR)}, {test:tail,sample:inverse(bendT)},
 {test:()=>true}
],seam:(x,y)=>(y>=40&&y<=45&&x<=33)||(x>=47&&x<=60&&y>=51&&y<=62)||(x>=55&&x<=59&&y>=58)||(y>=78&&y<=82&&x>=38)});
const displayScale=.7,pad=7,fr=require('./batch10_display')(sourceFrames,displayScale,'BGO',[[sourcePad+25,sourcePad+54,5,7,'BO'],[sourcePad+25,sourcePad+54,5,7,'AN']]);
console.log('hooh native',JSON.stringify(C.check(sourceFrames)));console.log('hooh display',JSON.stringify(C.check(fr)));
module.exports={name:'hooh',pal,rows,sourceFrames,fr,fps:6,angles:[-1,0,1],pose:0,pad,displayScale,layoutHeight:61,
 protect:(g,x,y)=>g[y][x]!=='.'&&x>=pad+15&&x<=pad+23&&y>=pad+35&&y<=pad+45};
