// Audits the generated RGBA sheets, including rotated facial regions and loop transitions.
const fs=require('fs'),C=require('./critter');
const specs={
 remoraid:{cell:54,eyes:[[8,8,4,6,'CP','B']]},
 lanturn:{cell:86,eyes:[[23,24,7,7,'CI','B']]},
 kingdra:{cell:88,eyes:[[18,20,5,5,'DO','BF']]},
 toucannon:{cell:80,eyes:[[36,4,6,6,'F','CN']]},
 altaria:{cell:80,eyes:[[18,18,4,5,'C','B'],[27,18,5,5,'C','B']]}
};
const report=[];
for(const [name,spec] of Object.entries(specs)) {
 const m=require('./'+name+'_anim'),cell=spec.cell,n=m.fr.length,W=cell*n,bytes=fs.readFileSync(name+'_sheet.rgba');
 if(bytes.length!==W*cell*m.angles.length*4)throw new Error(name+': invalid sheet dimensions');
 const colors=Object.fromEntries(Object.entries(m.pal).map(([k,v])=>[v.toLowerCase(),k]));
 const cells=[],eyes=[],border=[];
 for(let r=0;r<m.angles.length;r++)for(let f=0;f<n;f++) {
  const g=Array.from({length:cell},(_,y)=>Array.from({length:cell},(_,x)=>{const i=((r*cell+y)*W+f*cell+x)*4;return bytes[i+3]?colors[bytes.subarray(i,i+3).toString('hex')]:'.';}));
  cells.push(g.map(row=>row.join('')));
  border.push(g[0].some(c=>c!=='.')||g[cell-1].some(c=>c!=='.')||g.some(row=>row[0]!=='.'||row[cell-1]!=='.'));
  const a=m.angles[r]*Math.PI/180,c=Math.cos(a),sn=Math.sin(a),w=m.fr[f][0].length,h=m.fr[f].length;
  for(const [x,y,bw,bh,dark,light] of spec.eyes) {
   const targets=new Set();
   // Include one row either side for the reference body's one-pixel breathing motion.
   for(let yy=y-1;yy<=y+bh;yy++)for(let xx=x;xx<x+bw;xx++) {
    const dx=xx+m.pad+.5-w/2,dy=yy+m.pad+.5-h/2;
    const ox=Math.floor(c*dx-sn*dy+cell/2),oy=Math.floor(sn*dx+c*dy+cell/2);
    targets.add(ox+','+oy);
   }
   const hits=[...targets].map(p=>{const [xx,yy]=p.split(',').map(Number);return g[yy]?.[xx]||'.';});
   eyes.push({r,f,dark:hits.filter(v=>dark.includes(v)).length,light:hits.filter(v=>light.includes(v)).length});
  }
 }
 const transitions=m.fr.map((g,f)=>g.reduce((sum,row,y)=>sum+[...row].filter((v,x)=>v!==m.fr[(f+1)%n][y][x]).length,0));
 const item={name,base:[m.rows[0].length,m.rows.length],canvas:[m.fr[0][0].length,m.fr[0].length],cell,fps:m.fps,frames:n,tilts:m.angles.length,baseCheck:C.check(m.fr),sheetCheck:C.check(cells),clippedCells:border.filter(Boolean).length,eyeRegions:eyes.length,eyeRegionsPassed:eyes.filter(v=>v.dark&&v.light).length,eyeFailures:eyes.filter(v=>!v.dark||!v.light),loopSeamPassed:transitions[n-1]<=Math.max(...transitions.slice(0,n-1))*1.05,transitions};
 report.push(item);
 console.log(JSON.stringify({...item,sheetCheck:{iso:item.sheetCheck.iso,holes:item.sheetCheck.holes}},null,2));
}
fs.writeFileSync('rev/lote6_audit.json',JSON.stringify(report,null,2));
if(report.some(v=>v.baseCheck.iso||v.baseCheck.holes||v.sheetCheck.iso||v.sheetCheck.holes||v.clippedCells||v.eyeRegions!==v.eyeRegionsPassed||!v.loopSeamPassed))process.exitCode=1;
