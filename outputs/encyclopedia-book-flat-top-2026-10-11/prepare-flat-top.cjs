const fs = require('fs');
const path = require('path');
const sharp = require('C:/Users/nadom/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const evidence = __dirname;

(async () => {
  const original = await sharp(path.join(evidence, 'book-open-before.png')).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const generated = await sharp(path.join(evidence, 'generated-flat-top-reference.png')).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const {width: gw, height: gh} = generated.info;
  const rows = Array.from({length:gh}, (_,y) => {
    let count=0; for(let x=0;x<gw;x++) if(generated.data[(y*gw+x)*4+3]>=192) count++;
    return count;
  });
  const bodyRows = rows.map((count,y) => count>gw*.4 ? y : -1).filter(y=>y>=0);
  const top=bodyRows[0], bottom=bodyRows.at(-1)+1;
  const bodyColumns=[];
  for(let x=0;x<gw;x++) {
    let count=0; for(let y=top;y<bottom;y++) if(generated.data[(y*gw+x)*4+3]>=192) count++;
    if(count>(bottom-top)*.8) bodyColumns.push(x);
  }
  const left=bodyColumns[0], right=bodyColumns.at(-1)+1;
  const normal = await sharp(generated.data,{raw:generated.info}).extract({left,top,width:right-left,height:bottom-top})
    .resize(661,416,{fit:'fill',kernel:'nearest'}).raw().toBuffer();
  const palette = new Map();
  for(let i=0;i<original.data.length;i+=4) if(original.data[i+3]===255) {
    const key=original.data.subarray(i,i+3).toString('hex');
    if(!palette.has(key)) palette.set(key,[...original.data.subarray(i,i+3)]);
  }
  const colors=[...palette.values()];
  const candidate=Buffer.from(original.data);
  const cache=new Map();
  for(let y=181;y<230;y++) for(let x=85;x<733;x++) {
    const gi=((y-181)*661+(x-78))*4, oi=(y*816+x)*4;
    if(normal[gi+3]<128) continue;
    const key=normal.subarray(gi,gi+3).toString('hex');
    let closest=cache.get(key);
    if(!closest) {
      let distance=Infinity;
      for(const color of colors) {
        const d=color.reduce((sum,v,c)=>sum+(v-normal[gi+c])**2,0);
        if(d<distance) {distance=d;closest=color;}
      }
      cache.set(key,closest);
    }
    candidate[oi]=closest[0];candidate[oi+1]=closest[1];candidate[oi+2]=closest[2];candidate[oi+3]=255;
  }
  await sharp(candidate,{raw:original.info}).png().toFile(path.join(evidence,'book-open-candidate.png'));
  fs.writeFileSync(path.join(evidence,'generated-normalization.json'),JSON.stringify({generatedBody:{left,top,right,bottom},originalPalette:colors,editedBand:{left:85,top:181,right:733,bottom:230},originalBelowBandPreserved:true},null,2));
  process.stdout.write('Generated upper-band candidate normalized to original size and palette.\n');
})().catch(error=>{console.error(error.message);process.exitCode=1;});
