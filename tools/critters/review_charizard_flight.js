// Structural and visual verification of the approved Charizard flight cycle.
const fs=require('fs'),path=require('path'),assert=require('assert/strict'),crypto=require('crypto'),{execFileSync}=require('child_process'),C=require('./critter'),m=require('./charizard_approved_flight');
const OUT='C:/Users/javie/Desktop/imagenes/revision/charizard_vuelo_definitivo',ROOT=path.resolve('../..'),cell=102;
fs.mkdirSync(OUT,{recursive:true});
function components(g){const seen=new Set(),sizes=[];for(let y=0;y<g.length;y++)for(let x=0;x<g[0].length;x++){const id=x+','+y;if(g[y][x]==='.'||seen.has(id))continue;const q=[[x,y]];seen.add(id);let count=0;while(q.length){const [a,b]=q.pop();count++;for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){const xx=a+dx,yy=b+dy,key=xx+','+yy;if(g[yy]?.[xx]&&g[yy][xx]!=='.'&&!seen.has(key)){seen.add(key);q.push([xx,yy]);}}}sizes.push(count);}return sizes;}
const rgba=fs.readFileSync('charizard_sheet.rgba'),colors=Object.fromEntries(Object.entries(m.pal).map(([c,h])=>[h.toUpperCase(),c])),cells=[];
for(let r=0;r<3;r++)for(let f=0;f<16;f++)cells.push(Array.from({length:cell},(_,y)=>Array.from({length:cell},(_,x)=>{
 const i=((r*cell+y)*cell*16+f*cell+x)*4;if(!rgba[i+3])return '.';
 const color=rgba.subarray(i,i+3).toString('hex').toUpperCase();assert.ok(colors[color]);return colors[color];
}).join('')));
const checks={native:C.check(m.nativeFrames),display:C.check(m.fr),sheet:C.check(cells)};
for(const v of Object.values(checks)){assert.equal(v.iso,0);assert.equal(v.holes,0);}
const connected={native:m.nativeFrames.map(components),display:m.fr.map(components),sheet:cells.map(components)};
for(const group of Object.values(connected))assert.ok(group.every(a=>a.length===1));
const protectedFace=[];
m.fullFrames.forEach((g,f)=>{let kept=0,total=0;for(let y=20;y<=44;y++)for(let x=12;x<=54;x++){
 const c=m.body[y][x];if(c==='.')continue;total++;if(g[y+12+m.stats[f].bob][x+10]===c)kept++;
}protectedFace.push({f,total,kept});});
assert.ok(protectedFace.every(p=>p.total===p.kept));
const eyes=cells.map((g,i)=>{let white=0,blue=0,dark=0;const shiftX=(cell-m.fr[0][0].length)/2,shiftY=(cell-m.fr[0].length)/2;
 for(let y=Math.floor(m.face[1]+shiftY)-3;y<m.face[1]+shiftY+m.face[3]+3;y++)for(let x=Math.floor(m.face[0]+shiftX)-3;x<m.face[0]+shiftX+m.face[2]+3;x++){
 const c=g[y]?.[x];if(c==='B')white++;if(c==='C')blue++;if(c==='E')dark++;
 }return {cell:i,white,blue,dark};});
assert.ok(eyes.every(e=>e.white&&e.dark));
const round=v=>{const a=Math.floor(v);return v-a===.5?(a%2?a+1:a):Math.round(v);};
const sky=cells.map(g=>{const ys=g.map((r,y)=>/[^.]/.test(r)?y:null).filter(v=>v!==null);return [Math.min(...ys)+round(3+m.layoutHeight/2-cell/2),Math.max(...ys)+round(65-m.layoutHeight/2-cell/2)];});
assert.ok(sky.every(([a,b])=>a>=0&&b<69));
const transitions=m.nativeFrames.map((g,f)=>g.reduce((n,r,y)=>n+[...r].filter((c,x)=>c!==m.nativeFrames[(f+1)%16][y][x]).length,0));
assert.ok(transitions.at(-1)<=Math.max(...transitions.slice(0,-1)));
const original=require('./charizard_flight_approved.json');assert.deepEqual(m.rows,original.rows);
const protectedList=JSON.parse(fs.readFileSync('C:/Users/javie/Desktop/imagenes/revision/charizard_pose_v3/conservacion.json')).rows.map(p=>p.path).filter(p=>!p.endsWith('/charizard.png')&&!p.endsWith('/CapCritterArt.cs')&&!p.endsWith('/charizard_flight_anim.js'));
protectedList.push('tools/critters/charizard_flight_pose_v3.js','tools/critters/charizard_neck_refinement.js');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex'),canonical=(b,p)=>p.endsWith('.png')?b:Buffer.from(b.toString('utf8').replace(/\r\n/g,'\n'));
const preservation=protectedList.map(p=>({path:p,unchanged:hash(canonical(fs.readFileSync(path.join(ROOT,p)),p))===hash(canonical(execFileSync('git',['show','HEAD:'+p],{cwd:ROOT,maxBuffer:8*1024*1024}),p))}));
assert.ok(preservation.every(p=>p.unchanged));
C.previews('charizard',m.pal,m.nativeFrames,m.fps,3,OUT);
C.previews('charizard_layout',m.pal,m.fr,m.fps,1,OUT);
fs.copyFileSync('charizard_sheet.png',OUT+'/charizard_hoja_definitiva.png');
fs.copyFileSync('charizard_flight_approved.json',OUT+'/charizard_base_aprobada.json');
fs.copyFileSync('C:/Users/javie/Desktop/imagenes/revision/charizard_cuello/charizard_base_nativa.png',OUT+'/charizard_base_aprobada.png');
execFileSync(process.execPath,['rostro.js','charizard',String(cell),'16','5,24,24,24','4',OUT+'/charizard_rostros_48_celdas.png']);
execFileSync(process.execPath,['zoom.js','charizard_flight_anim',Array.from({length:16},(_,i)=>i).join(','),'24,15,30,35','4',OUT+'/charizard_uniones.png']);
const report={frames:16,fps:m.fps,duration:16/m.fps,cell,angles:m.angles,nativeCanvas:[m.nativeFrames[0][0].length,m.nativeFrames[0].length],displayCanvas:[m.fr[0][0].length,m.fr[0].length],displayScale:m.displayScale,baselinePixelChanges:m.baselineChanges,approvedBaseSha256:hash(fs.readFileSync('charizard_flight_approved.json')),checks,connected,protectedFace,eyes,skyRange:[Math.min(...sky.map(v=>v[0])),Math.max(...sky.map(v=>v[1]))],transitions,loopEdge:transitions.at(-1),repairs:m.stats,protectedFiles:preservation.length,preservation,limits:['Escala de visualizacion 60%, vecino mas cercano; fuente aprobada sin reducir','Oclusiones naturales del ala cercana durante la bajada','La prueba holes solo detecta huecos unitarios','Aprobacion de animacion pendiente; base aprobada']};
fs.writeFileSync(OUT+'/auditoria.json',JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({...report,checks,connected:undefined,protectedFace:protectedFace[0],eyes:eyes.length,repairs:undefined,preservation:undefined},null,2));