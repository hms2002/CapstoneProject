const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const load=n=>fs.readFileSync(path.join(__dirname,n+'.rgba'));
const N=192, palette=new Set(['182,61,40','241,107,50','255,198,90','255,240,191']), colors=new Set();
const bounds={};
for(const name of ['inner','outer','combined-192']){
 const p=load(name);assert.equal(p.length,N*N*4);
 let left=N,top=N,right=-1,bottom=-1;
 for(let i=0;i<p.length;i+=4){
  assert.ok(p[i+3]===0||p[i+3]===255,'Binary alpha only');
  if(p[i+3]){
   const rgb=Array.from(p.subarray(i,i+3)).join(',');assert.ok(palette.has(rgb));colors.add(rgb);
   const x=i/4%N,y=Math.floor(i/4/N);left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);
  }
 }
 bounds[name]={left,top,width:right-left+1,height:bottom-top+1};
}
assert.equal(colors.size,4);
function components(mask,w,h){
 const seen=new Uint8Array(w*h);let count=0;
 for(let k=0;k<mask.length;k++)if(mask[k]&&!seen[k]){
  count++;const q=[k];seen[k]=1;
  for(let i=0;i<q.length;i++){
   const p=q[i],x=p%w,y=Math.floor(p/w);
   for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){
    const nx=x+dx,ny=y+dy,j=ny*w+nx;
    if(nx>=0&&ny>=0&&nx<w&&ny<h&&mask[j]&&!seen[j]){seen[j]=1;q.push(j);}
   }
  }
 }
 return count;
}
const atlas=load('glyph-directions');let connected=0;
for(let id=0;id<8;id++)for(let angle=0;angle<16;angle++){
 const mask=[];
 for(let y=0;y<21;y++)for(let x=0;x<21;x++)mask.push(atlas[(((id*21+y)*336)+angle*21+x)*4+3]>0);
 assert.equal(components(mask,21,21),1,'Connected rune '+id+' heading '+angle);connected++;
}
for(const name of ['outer-runes','inner-runes']){
 const p=load(name);const count=components(Array.from({length:N*N},(_,i)=>p[i*4+3]>0),N,N);
 assert.equal(count,name==='outer-runes'?16:12);
 for(let i=0;i<p.length;i+=4)if(p[i+3])assert.deepEqual(Array.from(p.subarray(i,i+3)),[255,198,90]);
}
const medal=load('medallion');
for(let y=3;y<=11;y++)for(let x=3;x<=11;x++)if(Math.hypot(x-7,y-7)<4.5)assert.equal(medal[(y*15+x)*4+3],0,'Empty medallion interior');
const rim=load('outer-rim');
for(let y=0;y<N;y++)for(let x=0;x<N;x++)if(Math.hypot(x-95.5,y-95.5)<90)assert.equal(rim[(y*N+x)*4+3],0,'No rune-band dividers');
const report={dimensions:[192,192],linearResolutionIncrease:'50%',canvasPixelIncrease:'125%',palette:[...colors],sourceAlpha:'binary',bounds,connectedDirectionSamples:connected,outerRuneCount:16,medallion:'15px empty outline; same relative size',comparison:'128px x3 left, 192px x2 right; both 384px display diameter',animation:'Preview redraws strokes per angle; not a Unity capture. Existing in-game sprites stay 128px.'};
fs.writeFileSync(path.join(__dirname,'pixel-verification.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify(report,null,2));
