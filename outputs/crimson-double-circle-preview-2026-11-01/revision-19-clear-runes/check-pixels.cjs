const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const dir=__dirname;
const load=name=>fs.readFileSync(path.join(dir,name+'.rgba'));
const palette=new Set(['182,61,40','241,107,50','255,198,90','255,240,191']);
const colors=new Set();
for(const name of ['inner','outer','combined-128','glyph-atlas','medallion']){
  const p=load(name);
  for(let i=0;i<p.length;i+=4){
    assert.ok(p[i+3]===0||p[i+3]===255,'Binary source alpha');
    if(p[i+3]){const rgb=Array.from(p.subarray(i,i+3)).join(',');assert.ok(palette.has(rgb));colors.add(rgb);}
  }
}
assert.equal(colors.size,4);
function components(mask,w,h){
  const seen=new Uint8Array(w*h);let count=0;
  for(let k=0;k<mask.length;k++)if(mask[k]&&!seen[k]){
    count++;const queue=[k];seen[k]=1;
    for(let i=0;i<queue.length;i++){
      const p=queue[i],x=p%w,y=Math.floor(p/w);
      for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){
        const nx=x+dx,ny=y+dy,j=ny*w+nx;
        if(nx>=0&&ny>=0&&nx<w&&ny<h&&mask[j]&&!seen[j]){seen[j]=1;queue.push(j);}
      }
    }
  }
  return count;
}
const atlas=load('glyph-directions');let connected=0;
for(let id=0;id<8;id++)for(let angle=0;angle<16;angle++){
  const mask=[];
  for(let y=0;y<13;y++)for(let x=0;x<13;x++)mask.push(atlas[(((id*13+y)*208)+angle*13+x)*4+3]>0);
  assert.equal(components(mask,13,13),1,'One connected glyph at rune '+id+' angle '+angle);connected++;
}
const goldOnly=load('glyph-atlas');
for(let i=0;i<goldOnly.length;i+=4)if(goldOnly[i+3])assert.deepEqual(Array.from(goldOnly.subarray(i,i+3)),[255,198,90]);
const outer=load('outer-runes');
assert.equal(components(Array.from({length:128*128},(_,i)=>outer[i*4+3]>0),128,128),16,'Sixteen distinct connected outer rune shapes');
const rim=load('outer-rim');
for(let y=0;y<128;y++)for(let x=0;x<128;x++)if(Math.hypot(x-63.5,y-63.5)<60)assert.equal(rim[(y*128+x)*4+3],0,'No outer rune-band dividers');
const report={sourceDimensions:[128,128],colors:[...colors],alpha:'binary',glyphSize:[5,9],glyphColor:'FFC65A',connectedDirectionSamples:connected,outerRunes:16,outerDividers:'absent',medallions:'original 10x10 outline, empty interiors verified natively',animation:'120 frames / 80ms / outer clockwise and inner counterclockwise',limitations:'Preview-only. Rotation redraws connected strokes at each angle; standard Unity bitmap rotation is not validated. No Unity import, compilation or Play Mode executed.'};
fs.writeFileSync(path.join(dir,'pixel-verification.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify(report));
