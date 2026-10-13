const fs=require("fs");const W=198,H=254,P=4.3,PX=Number(process.argv[2]),PY=Number(process.argv[3]);const b=fs.readFileSync("ref.rgb");
const px=(x,y)=>{x=Math.min(W-1,Math.max(0,Math.round(x)));y=Math.min(H-1,Math.max(0,Math.round(y)));const i=(y*W+x)*3;return [b[i],b[i+1],b[i+2]];};
const C=Math.floor((W-PX)/P),R=Math.floor((H-PY)/P);
function cls([r,g,bl]){
  const mx=Math.max(r,g,bl),mn=Math.min(r,g,bl);
  if(mx<70)return "k";
  if(mx-mn<22){ if(mx>228)return "_"; if(mx>170)return "s"; return "g";}  // white-ish / light grey / grey
  if(r>200&&g>200&&bl<170)return "y";       // yellow / cream
  if(r>200&&g>150&&bl>150)return "p";       // pale pink/peach
  if(r>180&&g>120&&bl<120)return g>170?"y":"l"; // light orange / tan
  if(r>200&&g<130)return "o";               // orange-red
  if(r>150&&g<90)return "r";                // red
  if(r>90&&g<70)return "R";                 // crimson
  if(r>120&&g>90&&bl<90)return "t";         // tan/brown
  return "?";
}
const map=[];for(let y=0;y<R;y++){let row="";for(let x=0;x<C;x++){const s=[px(PX+(x+0.5)*P,PY+(y+0.5)*P),px((x+0.35)*P,(y+0.4)*P),px((x+0.65)*P,(y+0.6)*P)];row+=cls(s[0]);}map.push(row);}
// background: flood from the border through "_" and "s"
const bg=map.map(r=>[...r].map(()=>false));const st=[];for(let x=0;x<C;x++){st.push([x,0],[x,R-1]);}for(let y=0;y<R;y++){st.push([0,y],[C-1,y]);}
while(st.length){const [x,y]=st.pop();if(x<0||y<0||x>=C||y>=R||bg[y][x])continue;if(!"_s".includes(map[y][x]))continue;bg[y][x]=true;st.push([x+1,y],[x-1,y],[x,y+1],[x,y-1]);}
const out=map.map((r,y)=>[...r].map((c,x)=>bg[y][x]?".":(c==="_"?"w":c)).join(""));
console.log(C+"x"+R);out.forEach((r,y)=>console.log(String(y).padStart(2)+" "+r));
fs.writeFileSync("refmap.txt",out.join("\n"));
