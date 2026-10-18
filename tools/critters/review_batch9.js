// Batch 9 review helpers. Run from tools/critters; references remain read-only.
const fs = require('fs');
const { execFileSync } = require('child_process');
const C = require('./critter');
const FF = 'C:/Program Files (x86)/Soundux/ffmpeg.exe';
const OUT = 'C:/Users/javie/Desktop/imagenes/revision/lote9';
const names = ['bruxish', 'staraptor', 'salamence', 'charizard', 'latios'];
const refName = n => ({bruxish:'bruxish.jpg',staraptor:'Staraptor.jpg',salamence:'Salamence.jpg',charizard:'Charizard.png',latios:'Latios.png'})[n];
fs.mkdirSync('rev', { recursive: true });
function ff(args) { return execFileSync(FF, ['-hide_banner', '-loglevel', 'error', '-y', ...args], { maxBuffer: 64 << 20 }); }
function ppm(name, rows, pal) {
  const h = rows.length, w = rows[0].length, px = [];
  for (const row of rows) for (const ch of row) px.push(...(ch === '.' ? [42,106,144] : [0,2,4].map(i => parseInt(pal[ch].slice(i,i+2),16))));
  fs.writeFileSync(name, Buffer.concat([Buffer.from(`P6 ${w} ${h} 255\n`), Buffer.from(px)]));
}
function compare(n, out) {
  const m = fs.existsSync(`${n}_anim.js`) ? require(`./${n}_anim`) : C.load(n);
  const rows = m.rows || C.load(n).rows;
  ppm(`rev/${n}_base.ppm`, rows, m.pal);
  const g = JSON.parse(fs.readFileSync(`${n}.grid.json`));
  const x = Math.max(0,Math.round(g.x0+g.minX*g.px)), y = Math.max(0,Math.round(g.y0+g.minY*g.py));
  const w = rows[0].length*8, h = rows.length*8;
  ff(['-i', `C:/Users/javie/Desktop/imagenes/${refName(n)}`, '-i', `rev/${n}_base.ppm`, '-filter_complex', `[0:v]format=rgb24,crop=w=min(iw-${x}\\,${Math.round(g.w*g.px)}):h=min(ih-${y}\\,${Math.round(g.h*g.py)}):x=${x}:y=${y},scale=${w}:${h}:flags=neighbor,pad=iw+16:ih+16:8:8:0x101820[a];[1:v]format=rgb24,scale=${w}:${h}:flags=neighbor,pad=iw+16:ih+16:8:8:0x101820[b];[a][b]hstack`, '-frames:v','1',out]);
}
const [cmd,n] = process.argv.slice(2);
if(cmd==='compare') compare(n,`rev/${n}_compare.png`);
if(cmd==='deliver') {
  fs.mkdirSync(OUT,{recursive:true});
  for(const n of names) {
    const m=require(`./${n}_anim`);
    compare(n,`${OUT}/${n}_referencia_y_sprite.png`);
    C.previews(n,m.pal,m.fr,m.fps,6,OUT);
  }
  for(const suffix of ['referencia_y_sprite','fotogramas']) {
    const files=names.map(n=>`${OUT}/${n}_${suffix}.png`);
    const widths=files.map(file=>{try{execFileSync(FF,['-hide_banner','-i',file]);}catch(e){return +e.stderr.toString().match(/, (\d+)x(\d+)/)[1];}});
    const width=Math.max(...widths);
    const filter=files.map((_,i)=>`[${i}:v]format=rgb24,pad=${width}:ih+12:(ow-iw)/2:6:0x101820[p${i}]`).join(';')+';'+files.map((_,i)=>`[p${i}]`).join('')+`vstack=inputs=${files.length}`;
    ff([...files.flatMap(f=>['-i',f]),'-filter_complex',filter,'-frames:v','1',`${OUT}/lote9_${suffix==='fotogramas'?'fotogramas':'referencias_y_sprites'}.png`]);
  }
}
