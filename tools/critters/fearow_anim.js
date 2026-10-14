// Fearow: the raised wings fold down, with their original feather boundaries and fixed roots.
// The 2.84 px reference is taller than the lab sky: keep the wings partly folded throughout
// the cycle, and offset the canvas anchor by three pixels, without rescaling the drawing.
const C=require('./critter');
const {pal,rows}=C.load('fearow');
for(let y=0;y<12;y++)for(let x=0;x<rows[y].length;x++)if(rows[y][x]==='B')rows[y][x]='.';
const N=8,pad=8,t=f=>f/N*2*Math.PI,down=f=>(1-Math.cos(t(f)))/2;
const back=(x,y)=>y<=35&&x>=21&&x<=44&&(y<15||x>Math.max(20,...rows[y].map((c,i)=>'CEHMO'.includes(c)&&i<36?i:0)));
const fr=C.frames(rows,{count:N,pad,ink:'DJN',parts:[
 {test:back,sample:(f,put,at,mine)=>{const a=6+4*down(f);for(let y=0;y<=36;y++)for(let x=20;x<=45;x++){const sy=Math.round((y-a)/(1-a/36));if(mine(x,sy)&&at(x,sy)!=='.')put(x,y-3,at(x,sy));}}},
 {test:(x,y)=>x>=48&&y<=51,move:(f,x)=>[0,-3+Math.round((6+7*down(f))*Math.min(1,(x-48)/32))]},
 {test:(x,y)=>x>=49&&y>=59,move:(f,x)=>[0,-3+Math.sin(t(f)-1.2)*Math.min(1,(x-49)/18)]},
 {test:(x,y)=>y>=57&&x<=47,move:(f,x,y)=>[Math.sin(t(f)-1)*Math.max(0,(y-57)/14),-3]},
 {test:()=>true,move:()=>[0,-3]}
],seam:(x,y)=>(y>=29&&y<=38&&x>=31&&x<=48)||(x>=46&&x<=51&&y>=27&&y<=52)||(y>=55&&y<=62)});
console.log('fearow',JSON.stringify(C.check(fr)));
module.exports={name:'fearow',sealRotatedHoles:true,pal,rows,fr,fps:8,angles:[-1,0,1],pose:0,pad,layoutHeight:61,
 protect:(g,x,y)=>g[y][x]!=='.'&&((x>=pad+15&&x<=pad+25&&y>=pad+25&&y<=pad+33)||(x<=pad+20&&y>=pad+30&&y<=pad+41))};
